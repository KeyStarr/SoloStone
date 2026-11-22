using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Retail publishes all of one activation's summons in its own block, with no SHOW_ENTITY for a minion created straight into
// play, so the client spawns them together. Summons queued by EnqueueTask / EnqueueNumberTask used to get one sibling POWER
// block each, and each created minion a SHOW_ENTITY; either made the client play one spawn at a time.
// A trigger of a spawn (Knife Juggler) stays nested between the spawns, as in retail.
// Retail (hsreplay-test-data hslog-tests): 34104_subspell.power.log:11268-11359 (Swarm of Locusts: six FULL_ENTITY ZONE=PLAY
// in its POWER block, no SHOW_ENTITY); :28817-28883 (two Splitting Saplings die together: each Deathrattle is its own
// TRIGGER block holding both of its summons); puzzlelab.power.log:6883-6916 (Reincarnate summons for the opponent: one
// FULL_ENTITY CONTROLLER=2 ZONE=PLAY in player 1's POWER block, no ZONE change, no SHOW_ENTITY).
internal static class QueuedSummonHistoryProof
{
    [GroupProof("queued-summon-history")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var (name, run) in new (string, Action)[] {
            ("EX1_538", Hounds), ("CORE_GVG_061", Muster), ("FP1_002", Creeper),
            ("EX1_248", FeralSpirit), ("EX1_573", Cenarius), ("ICC_832", Malfurion), ("EX1_116", Leeroy),
            ("UNG_076", Eggnapper), ("FP1_019", PoisonSeeds), ("LOOT_080", EmeraldSpellstone), ("LOOT_064t1", SapphireSpellstone),
            ("FP1_002x2", TwoCreepers), ("FP1_031", Rivendare), ("FP1_002+EX1_096", CreeperAndHoarder),
            ("chained-continuations", ChainedContinuations), ("ICC_054", SpreadingPlague), ("ICC_041", Defile),
            ("dormant-copy", DormantCopy),
            ("created-in-play-boundaries", CreatedInPlayBoundaries), ("refused-fold-order", RefusedFoldOrder),
            ("summon-slot", SummonSlot), ("untouchable-slot", UntouchableSlot),
            // Negative cases: these keep their own shape.
            ("LOOT_093", RecruitFromDeck), ("summon-from-hand", SummonFromHand), ("UNG_854", DiscoverThenSummon),
            ("own-hit", ChildWithOwnHit), ("TRL_232", OverkillSummon) })
        {
            try { run(); Console.WriteLine("QUEUED-SUMMON-HISTORY " + name + " PASS"); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); Console.WriteLine("QUEUED-SUMMON-HISTORY " + name + " FAIL " + error.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static Game NewGame(string deck = "CS2_182")
    {
        var game = new Game(new GameConfig { RandomSeed = 42, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId(deck), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        return game;
    }

    private static void CreatedInPlayBoundaries()
    {
        foreach (bool inPlay in new[] { false, true })
        foreach (bool productionOrder in new[] { false, true })
        foreach (string boundary in new[] { "block-before", "subspell-before", "flush-before", "block-after", "subspell-after", "flush-after" })
        {
            var g = NewGame(); var p = g.Player1; var h = g.PowerHistory;
            var source = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
            var from = (h.Full.Count, h.Last.Count);
            void Edge()
            {
                if (boundary.StartsWith("block")) h.Add(new PowerHistoryBlockStart { BlockType = BlockType.POWER, Source = source.Id });
                else if (boundary.StartsWith("subspell")) h.Add(new PowerHistorySubSpellStart { Source = source.Id });
                else h.Last.Clear();
            }
            if (boundary.EndsWith("before")) Edge();
            var minion = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"),
                inPlay ? new EntityData { [GameTag.ZONE] = (int)Zone.PLAY } : null);
            // SummonPhase records the insertion cursor after creation.
            if (productionOrder) from = (h.Full.Count, h.Last.Count);
            if (boundary.EndsWith("after")) Edge();
            minion.Damage = 1;
            p.BoardZone.Add(minion, 1);
            var full = h.Full.OfType<PowerHistoryFullEntity>().Single(e => e.Entity.Id == minion.Id);
            var before = h.Full.ToArray();
            bool published = h.PublishCreatedInPlay(minion, from);
            string name = $"{boundary} inPlay={inPlay} productionOrder={productionOrder}";
            if (boundary.EndsWith("before"))
            {
                // Retail 186852_hearthstone_access_eoe.power.log:3333-3338: state belongs in FULL; an edge/flush
                // before creation only prevents relocation when the insertion cursor still includes that boundary.
                Core2026Proof.Check(published && full.Entity.Tags[GameTag.ZONE_POSITION] == 2
                    && full.Entity.Tags[GameTag.DAMAGE] == 1 && full.HealthIsMaximum
                    && full.Entity.Tags[GameTag.HEALTH] == minion.Health + minion.Damage, name + ": fold state and maximum Health");
                int shift = h.Full.FindIndex(e => e is PowerHistoryTagChange t
                    && t.EntityId == right.Id && t.Tag == GameTag.ZONE_POSITION);
                Core2026Proof.Check(shift >= 0 && (shift < h.Full.IndexOf(full)) == productionOrder,
                    name + ": relocate shifts only within insertion boundary");
                Core2026Proof.Check(!h.Full.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == minion.Id),
                    name + ": own state folded");
            }
            else
            {
                Core2026Proof.Check(published == (inPlay && !boundary.StartsWith("flush"))
                    && h.Full.SequenceEqual(before) && !full.HealthIsMaximum
                    && (!full.Entity.Tags.TryGetValue(GameTag.ZONE_POSITION, out int position) || position == 0),
                    name + ": touched/flushed creation stays unchanged");
            }
        }
    }

    private static void RefusedFoldOrder()
    {
        var g = NewGame(); var p = g.Player1; var h = g.PowerHistory;
        Summon(p, "CS2_182"); Summon(p, "CS2_182");
        var minion = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"));
        minion.Damage = 1;
        // An own row absent from Full before the insertion cursor must refuse the fold before moving neighbours.
        h.Full.Remove(h.Last.OfType<PowerHistoryTagChange>().Single(t => t.EntityId == minion.Id && t.Tag == GameTag.DAMAGE));
        var from = (h.Full.Count, h.Last.Count);
        p.BoardZone.Add(minion, 1);
        var full = h.Full.ToArray(); var last = h.Last.ToArray();
        Core2026Proof.Check(!h.PublishCreatedInPlay(minion, from) && h.Full.SequenceEqual(full) && h.Last.SequenceEqual(last),
            "refused fold leaves both history lists in their original order");
    }

    private static void SummonSlot()
    {
        var g = NewGame(); var p = g.Player1;
        var source = Summon(p, "CS2_182");
        var right = new[] { Summon(p, "CS2_182"), Summon(p, "CS2_182"), Summon(p, "CS2_182") };
        int cursor = g.PowerHistory.Full.Count;
        new SummonTask("CS2_231", SummonSide.RIGHT).Process(g, p, source, null);
        var minion = p.BoardZone.Single(m => m.Card.Id == "CS2_231");
        var rows = g.PowerHistory.Full.Skip(cursor).ToList();
        var full = rows.OfType<PowerHistoryFullEntity>().Single(f => f.Entity.Id == minion.Id);
        var shifts = rows.OfType<PowerHistoryTagChange>().Where(t => t.Tag == GameTag.ZONE_POSITION
            && right.Any(m => m.Id == t.EntityId)).ToList();
        Core2026Proof.Check(shifts.Select(t => t.EntityId).SequenceEqual(right.Reverse().Select(m => m.Id))
            && shifts.All(t => rows.IndexOf(t) < rows.IndexOf(full)
                && t.Value == right.Single(m => m.Id == t.EntityId).ZonePosition + 1),
            "every right neighbour shifts before FULL, highest position first");
        Core2026Proof.Check(minion.ZonePosition == 1 && full.Entity.Tags[GameTag.ZONE] == (int)Zone.PLAY
            && full.Entity.Tags[GameTag.ZONE_POSITION] == minion.ZonePosition + 1,
            "normal SummonTask FULL carries PLAY and final nonzero mid-row slot");
        Core2026Proof.Check(!rows.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == minion.Id && t.Tag == GameTag.ZONE_POSITION)
            && !rows.OfType<PowerHistoryShowEntity>().Any(), "no own position TAG_CHANGE or SHOW_ENTITY");
    }

    private static void UntouchableSlot()
    {
        foreach (bool second in new[] { false, true })
        {
            var g = NewGame(); var p = second ? g.Player2 : g.Player1;
            var source = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
            int cursor = g.PowerHistory.Full.Count;
            new SummonTask("UNG_829t2", SummonSide.RIGHT).Process(g, p, source, null);
            var portal = p.BoardZone.Single(m => m.Card.Id == "UNG_829t2");
            var rows = g.PowerHistory.Full.Skip(cursor).ToList();
            var full = rows.OfType<PowerHistoryFullEntity>().Single(f => f.Entity.Id == portal.Id);
            int shift = rows.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == right.Id && t.Tag == GameTag.ZONE_POSITION);
            // Same retail insertion shape as 186852_hearthstone_access_eoe.power.log:3402-3407, including permanents.
            Core2026Proof.Check(portal.Untouchable && full.Entity.Tags[GameTag.ZONE_POSITION] == 2
                && !rows.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == portal.Id && t.Tag == GameTag.ZONE_POSITION)
                && shift >= 0 && shift < rows.IndexOf(full)
                && !rows.OfType<PowerHistoryShowEntity>().Any(e => e.Entity.Id == portal.Id),
                "Untouchable summon uses created-in-play slot publication on both seats");
        }
    }

    private static void Hounds()
    {
        var g = NewGame(); var p = g.Player1;
        var juggler = Summon(p, "NEW1_019");
        for (int i = 0; i < 3; i++) Summon(p.Opponent, "CS2_182").BaseHealth = 100;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "EX1_538");
        var shape = Shape(g, cursor);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_538t") == 3, "three Hounds summoned");
        OneBlock(shape, spell.Id, BlockType.POWER, "EX1_538t", 3);
        var power = shape.Blocks.Single(b => b.Source == spell.Id && b.BlockType == BlockType.POWER);
        var throws = shape.Blocks.Where(b => b.Source == juggler.Id && b.BlockType == BlockType.TRIGGER).ToList();
        Core2026Proof.Check(throws.Count == 3 && throws.All(b => ReferenceEquals(shape.Parent[b], power)), "each Knife Juggler throw nests inside the spell's block");
        PredictedPlayClean(shape, spell.Id);
    }

    private static void Muster()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "CORE_GVG_061");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 3 && p.Hero.Weapon != null, "three Recruits and a weapon");
        var shape = Shape(g, cursor);
        OneBlock(shape, spell.Id, BlockType.POWER, "CS2_101t", 3);
        PredictedPlayClean(shape, spell.Id);
    }

    private static void Creeper()
    {
        var g = NewGame(); var p = g.Player1;
        var creeper = Summon(p, "FP1_002");
        int cursor = g.PowerHistory.Full.Count;
        Kill(g, creeper);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "two Spectral Spiders summoned");
        OneBlock(Shape(g, cursor), creeper.Id, BlockType.TRIGGER, "FP1_002t", 2);
    }

    private static void FeralSpirit()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "EX1_248");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_tk11") == 2, "two Spirit Wolves summoned");
        var shape = Shape(g, cursor);
        OneBlock(shape, spell.Id, BlockType.POWER, "EX1_tk11", 2);
        PredictedPlayClean(shape, spell.Id);
    }

    // Choose One: the summon option runs the chosen option's task; its summons still share one block.
    private static void Cenarius()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var cenarius = PlayChoice(g, p, "EX1_573", 2);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_573t") == 2, "two Treants summoned");
        var shape = Shape(g, cursor);
        Together(shape, "EX1_573t", 2, 1);
        PredictedPlayClean(shape, cenarius.Id);
    }

    private static void Malfurion()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var hero = PlayChoice(g, p, "ICC_832", 1);
        var summoned = p.BoardZone.Select(m => m.Card.Id).Distinct().ToList();
        Core2026Proof.Check(summoned.Count == 1 && p.BoardZone.Count == 2, "two of one Malfurion token summoned: " + string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
        var shape = Shape(g, cursor);
        Together(shape, summoned[0], 2, 1);
        PredictedPlayClean(shape, hero.Id);
    }

    // Summoned for the opponent from a card, not created in play: the FULL_ENTITY must still say PLAY (puzzlelab.power.log:6913).
    private static void Leeroy()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var leeroy = Play(g, p, "EX1_116");
        Core2026Proof.Check(p.Opponent.BoardZone.Count(m => m.Card.Id == "EX1_116t") == 2, "two Whelps for the opponent");
        var shape = Shape(g, cursor);
        OneBlock(shape, leeroy.Id, BlockType.POWER, "EX1_116t", 2);
        PredictedPlayClean(shape, leeroy.Id);
    }

    private static void Eggnapper()
    {
        var g = NewGame(); var p = g.Player1;
        var eggnapper = Summon(p, "UNG_076");
        int cursor = g.PowerHistory.Full.Count;
        Kill(g, eggnapper);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "UNG_076t1") == 2, "two Raptors summoned");
        OneBlock(Shape(g, cursor), eggnapper.Id, BlockType.TRIGGER, "UNG_076t1", 2);
    }

    // Both sides' Treants come from one activation: friendly ones by SummonTask, the opponent's by SummonOpTask.
    private static void PoisonSeeds()
    {
        var g = NewGame(); var p = g.Player1;
        Summon(p, "CS2_182");
        for (int i = 0; i < 2; i++) Summon(p.Opponent, "CS2_182");
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "FP1_019");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_019t") == 1 && p.Opponent.BoardZone.Count(m => m.Card.Id == "FP1_019t") == 2,
            "a Treant for each destroyed minion, on its side");
        var shape = Shape(g, cursor);
        OneBlock(shape, spell.Id, BlockType.POWER, "FP1_019t", 3);
        PredictedPlayClean(shape, spell.Id);
    }

    private static void EmeraldSpellstone()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "LOOT_080");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "LOOT_077t") == 2, "two Wolves summoned");
        var shape = Shape(g, cursor);
        OneBlock(shape, spell.Id, BlockType.POWER, "LOOT_077t", 2);
        PredictedPlayClean(shape, spell.Id);
    }

    // Copies of a friendly minion (SummonCopyTask), queued twice by a targeted spell.
    private static void SapphireSpellstone()
    {
        var g = NewGame(); var p = g.Player1;
        var target = Summon(p, "CS2_182");
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "LOOT_064t1", target);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 3, "two copies summoned");
        var shape = Shape(g, cursor);
        OneBlock(shape, spell.Id, BlockType.POWER, "CS2_182", 2);
        PredictedPlayClean(shape, spell.Id);
    }

    private static void DormantCopy()
    {
        var g = NewGame(); var p = g.Player1;
        var source = (Minion)Play(g, p, "EDR_469");
        int cursor = g.PowerHistory.Full.Count;
        g.PowerHistory.Last.Clear();
        var copy = (Minion)SabberStoneCore.Actions.Generic.Copy(p, p.Hero, source, Zone.PLAY);
        var helper = copy.AppliedEnchantments.Single(e => e.Card.Id == "EDR_469e");
        foreach (var history in new[] { g.PowerHistory.Full.Skip(cursor).ToList(), g.PowerHistory.Last })
        {
            int parent = history.FindIndex(e => e is PowerHistoryFullEntity full && full.Entity.Id == copy.Id
                && full.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && zone == (int)Zone.PLAY
                && (!full.Entity.Tags.TryGetValue(GameTag.DORMANT, out int sleep) || sleep == 0));
            int enter = history.FindIndex(e => e is PowerHistoryTagChange tag && tag.EntityId == copy.Id
                && tag.Tag == GameTag.DORMANT && tag.Value == 1);
            int child = history.FindIndex(e => e is PowerHistoryFullEntity full && full.Entity.Id == helper.Id
                && full.Entity.Tags.TryGetValue(GameTag.ATTACHED, out int attached) && attached == copy.Id);
            Core2026Proof.Check(copy.IsDormant && parent >= 0 && enter > parent && child > enter,
                "Dormant copy keeps FULL without entered DORMANT, then DORMANT change, then attached helper FULL");
            // Its other Dormant entry tags stay changes after the FULL too: UNTOUCHABLE as in retail
            // (49534_cached_tag_for_dormant_change.power.log:3016), DORMANT_VISUAL as before #261 (retail sends none).
            foreach (var entry in new[] { GameTag.DORMANT_VISUAL, GameTag.UNTOUCHABLE })
            {
                var full = (PowerHistoryFullEntity)history[parent];
                int change = history.FindIndex(e => e is PowerHistoryTagChange tag && tag.EntityId == copy.Id && tag.Tag == entry && tag.Value == 1);
                Core2026Proof.Check((!full.Entity.Tags.TryGetValue(entry, out int folded) || folded == 0) && change > parent,
                    "Dormant copy's " + entry + " entry stays a change after its FULL");
            }
        }
    }

    // Two Deathrattles of one death phase: each its own TRIGGER block with its own summons (34104_subspell.power.log:28817-28883).
    private static void TwoCreepers()
    {
        var g = NewGame(); var p = g.Player1;
        var first = Summon(p, "FP1_002"); var second = Summon(p, "FP1_002");
        int cursor = g.PowerHistory.Full.Count;
        first.ToBeDestroyed = true; second.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 4, "four Spectral Spiders summoned");
        var shape = Shape(g, cursor);
        foreach (var creeper in new[] { first, second }) PerActivation(shape, creeper.Id, "FP1_002t", 1, 2);
    }

    // Rivendare repeats one Deathrattle: each repeat is its own activation with its own summons.
    private static void Rivendare()
    {
        var g = NewGame(); var p = g.Player1;
        Summon(p, "FP1_031");
        var creeper = Summon(p, "FP1_002");
        int cursor = g.PowerHistory.Full.Count;
        Kill(g, creeper);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 4, "four Spectral Spiders summoned");
        PerActivation(Shape(g, cursor), creeper.Id, "FP1_002t", 2, 2);
    }

    // A sibling Deathrattle with rows of its own (Loot Hoarder's draw) runs before the Creeper's queued summons; they cannot
    // rejoin the Creeper's block past it, but still share one block instead of one each.
    private static void CreeperAndHoarder()
    {
        var g = NewGame(); var p = g.Player1;
        var creeper = Summon(p, "FP1_002"); var hoarder = Summon(p, "EX1_096");
        int cursor = g.PowerHistory.Full.Count;
        creeper.ToBeDestroyed = true; hoarder.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "two Spectral Spiders summoned");
        Together(Shape(g, cursor), "FP1_002t", 2, 1);
    }

    private static void ChainedContinuations()
    {
        var g = NewGame(); var p = g.Player1;
        var source = Summon(p, "CS2_182");
        ISimpleTask chain = new SummonTask("CS2_101t");
        for (int i = 0; i < 3; i++)
            chain = ComplexTask.Create(new SummonTask("CS2_101t"), new EnqueueTask(1, chain));
        int cursor = g.PowerHistory.Full.Count;
        g.TaskQueue.StartEvent();
        g.TaskQueue.Enqueue(chain, p, source, null);
        g.ProcessTasks();
        g.TaskQueue.EndEvent();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 4, "four chained Recruits summoned");
        OneBlock(Shape(g, cursor), source.Id, BlockType.POWER, "CS2_101t", 4);
    }

    private static void SpreadingPlague()
    {
        var g = NewGame(); var p = g.Player1;
        for (int i = 0; i < 4; i++) Summon(p.Opponent, "CS2_182");
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "ICC_054");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "ICC_832t4") == 4, "four Scourges summoned");
        var shape = Shape(g, cursor);
        var waves = shape.Blocks.Where(b => b.Source == spell.Id && b.BlockType == BlockType.POWER).ToList();
        Core2026Proof.Check(waves.Count == 4 && waves.All(b => shape.Creations.Count(c => c.Card == "ICC_832t4"
            && ReferenceEquals(c.Block, b)) == 1), "one Scourge in each of four cast blocks" + shape.Dump);
        CreatedInPlay(shape, shape.Creations.Where(c => c.Card == "ICC_832t4").ToList());
        PredictedPlayClean(shape, spell.Id);
    }

    private static void Defile()
    {
        var g = NewGame(); var p = g.Player1;
        var victims = Enumerable.Range(1, 3).Select(health => {
            var minion = Summon(p.Opponent, "CS2_182"); minion.BaseHealth = health; return minion;
        }).ToList();
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "ICC_041");
        Core2026Proof.Check(victims.All(m => m.Zone == p.Opponent.GraveyardZone), "Defile kills the 1/2/3 Health ladder");
        var shape = Shape(g, cursor);
        var waves = shape.Blocks.Where(b => b.Source == spell.Id && b.BlockType == BlockType.POWER).ToList();
        Core2026Proof.Check(waves.Count == 4, "four Defile waves, including the final empty wave" + shape.Dump);
        var history = g.PowerHistory.Full.Skip(cursor).ToList();
        for (int i = 0; i < victims.Count; i++)
        {
            var deaths = shape.Blocks.Single(b => b.BlockType == BlockType.DEATHS && shape.OwnRows[b].Any(e =>
                e is PowerHistoryTagChange tag && tag.EntityId == victims[i].Id && tag.Tag == GameTag.ZONE && tag.Value == (int)Zone.GRAVEYARD));
            // Defile's next wave starts by processing the previous wave's deaths, before dealing its own damage.
            Core2026Proof.Check(ReferenceEquals(shape.Parent[deaths], waves[i + 1])
                && history.IndexOf(waves[i]) < history.IndexOf(deaths)
                && shape.OwnRows[waves[i + 1]].Where(e => e is PowerHistoryMetaData meta && meta.Type == MetaDataType.DAMAGE)
                    .All(hit => history.IndexOf(deaths) < history.IndexOf(hit)),
                "death phase precedes the next Defile hit" + shape.Dump);
        }
        PredictedPlayClean(shape, spell.Id);

        // Repeat casts have no continuation parent; inspect the real queued recast so removing the Repeat guard fails.
        g = NewGame(); p = g.Player1;
        Summon(p.Opponent, "CS2_182").BaseHealth = 1;
        var defile = (Spell)Add(p, "ICC_041");
        using var cast = g.Facts.Cast(defile, HistoryCastKind.Player);
        g.TaskQueue.StartEvent();
        defile.ActivateTask();
        g.TaskQueue.Process();
        var queue = (System.Collections.IEnumerable)typeof(TaskQueue).GetProperty("CurrentQueue",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(g.TaskQueue);
        var recast = queue.Cast<object>().Single();
        var activation = (QueuedSpellActivation)recast.GetType().GetField("HistoryCast").GetValue(recast);
        Core2026Proof.Check(activation?.Kind == HistoryCastKind.Repeat
            && recast.GetType().GetField("HistoryParent").GetValue(recast) == null, "Defile's queued Repeat has no continuation history parent");
        g.ProcessTasks();
        g.TaskQueue.EndEvent();
        cast.Complete();
    }

    // A summon of a card that already exists in a hidden zone is a reveal: it keeps its SHOW_ENTITY.
    private static void RecruitFromDeck()
    {
        var g = NewGame("CS2_168"); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "LOOT_093");
        var recruits = p.BoardZone.Where(m => m.Card.Id == "CS2_168").Select(m => m.Id).ToList();
        Core2026Proof.Check(recruits.Count == 3, "three minions recruited from the deck");
        var shape = Shape(g, cursor);
        Core2026Proof.Check(recruits.All(shape.Shown.Contains) && !shape.Creations.Any(c => recruits.Contains(c.Id)),
            "each recruited minion is revealed by SHOW_ENTITY, not created" + shape.Dump);
        PredictedPlayClean(shape, spell.Id);
    }

    // A minion created in no zone that went somewhere else first (here its hand) is not created in PLAY when summoned later.
    private static void SummonFromHand()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var minion = (Minion)Add(p, "CS2_182");
        SabberStoneCore.Actions.Generic.RemoveFromZone(p, minion);
        SabberStoneCore.Actions.Generic.SummonBlock(g, minion, -1, null);
        var history = g.PowerHistory.Full.Skip(cursor).ToList();
        var created = history.OfType<PowerHistoryFullEntity>().SingleOrDefault(f => f.Entity.Id == minion.Id);
        Core2026Proof.Check(created != null && (!created.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) || zone != (int)Zone.PLAY)
            && history.OfType<PowerHistoryShowEntity>().Any(show => show.Entity.Id == minion.Id), "a summon from hand keeps its SHOW_ENTITY");
    }

    // A Discover offer is created in SETASIDE; the summoned pick keeps its FULL_ENTITY there and its move to PLAY.
    private static void DiscoverThenSummon()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        Play(g, p, "UNG_854");
        Core2026Proof.Check(p.Choice != null, "Discover offered");
        int pick = p.Choice.Choices[0];
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, pick)), "pick accepted");
        Core2026Proof.Check(p.BoardZone.Any(m => m.Id == pick), "the pick is summoned");
        var history = g.PowerHistory.Full.Skip(cursor).ToList();
        var created = history.OfType<PowerHistoryFullEntity>().Single(f => f.Entity.Id == pick);
        Core2026Proof.Check(created.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && zone == (int)Zone.SETASIDE
            && history.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == pick && t.Tag == GameTag.ZONE && t.Value == (int)Zone.PLAY),
            "the Discover pick is not rewritten as created in PLAY");
    }

    // A queued activation with hits of its own joins the parent, as in retail (hslog-tests/toki.power.log:4239).
    // Deaths after the first hit are inside this activation, not the start of another repeat wave.
    private static void ChildWithOwnHit()
    {
        var g = NewGame(); var p = g.Player1;
        var source = Summon(p, "CS2_182");
        var victim = Summon(p.Opponent, "CS2_182");
        int cursor = g.PowerHistory.Full.Count;
        g.TaskQueue.StartEvent();
        g.TaskQueue.Enqueue(new SabberStoneCore.Tasks.SimpleTasks.EnqueueTask(2, SabberStoneCore.Tasks.ComplexTask.Create(
            new SabberStoneCore.Tasks.SimpleTasks.DamageTask(1, SabberStoneCore.Tasks.SimpleTasks.EntityType.OP_HERO),
            new CustomTask((game, controller, s, t, stack) =>
            {
                if (victim.Zone == p.Opponent.BoardZone) victim.ToBeDestroyed = true;
                game.DeathProcessingAndAuraUpdate();
            }),
            new SabberStoneCore.Tasks.SimpleTasks.SummonTask("CS2_101t"))), p, source, null);
        g.ProcessTasks();
        g.TaskQueue.EndEvent();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 2 && p.Opponent.Hero.Damage == 2, "two hits, two Recruits");
        var shape = Shape(g, cursor);
        Together(shape, "CS2_101t", 2, 1);
        var power = shape.Blocks.Single(b => b.Source == source.Id && b.BlockType == BlockType.POWER);
        var deaths = shape.Blocks.Single(b => b.BlockType == BlockType.DEATHS);
        Core2026Proof.Check(ReferenceEquals(shape.Parent[deaths], power) && victim.Zone == p.Opponent.GraveyardZone,
            "later death phase stays inside the joined continuation" + shape.Dump);
        Core2026Proof.Check(shape.OwnRows[power].OfType<PowerHistoryMetaData>().Count(m => m.Type == MetaDataType.DAMAGE) == 2,
            "both hits share the parent block with both Recruits" + shape.Dump);
    }

    // An Overkill summon runs in its own block inside the ATTACK block, never in the ATTACK block's own rows.
    private static void OverkillSummon()
    {
        var g = NewGame(); var p = g.Player1;
        var direhorn = Summon(p, "TRL_232");
        var victim = Summon(p.Opponent, "CS2_168");
        int cursor = g.PowerHistory.Full.Count;
        Attack(g, direhorn, victim);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TRL_232t") == 1, "Overkill summoned the Direhorn Hatchling");
        var shape = Shape(g, cursor);
        var spawn = shape.Creations.Single(c => c.Card == "TRL_232t");
        var attack = shape.Blocks.Single(b => b.BlockType == BlockType.ATTACK);
        Core2026Proof.Check(spawn.Block.BlockType != BlockType.ATTACK && spawn.Block.Source == direhorn.Id
            && ReferenceEquals(shape.Parent[spawn.Block], attack), "summoned in its own block nested in the ATTACK block" + shape.Dump);
        CreatedInPlay(shape, new List<(int, string, PowerHistoryBlockStart, int)> { spawn });
    }

    private static IPlayable PlayChoice(Game game, Controller player, string id, int choice)
    {
        var source = Add(player, id);
        player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
        var task = player.Options().OfType<PlayCardTask>().FirstOrDefault(t => t.Source?.Id == source.Id && t.ChooseOne == choice);
        Core2026Proof.Check(task != null, "legal choice missing: " + id + " " + choice);
        Core2026Proof.Check(game.Process(task), "legal choice rejected: " + id + " " + choice);
        return source;
    }

    private static void OneBlock(HistoryShape shape, int source, BlockType type, string summoned, int count)
    {
        // Other blocks of the source may follow (Feral Spirit's Overload is its own POWER block); none may hold a spawn.
        var spawns = shape.Creations.Where(c => c.Card == summoned).ToList();
        var own = shape.Blocks.Where(b => b.Source == source && b.BlockType != BlockType.PLAY && spawns.Any(c => ReferenceEquals(c.Block, b))).ToList();
        Core2026Proof.Check(own.Count == 1 && own[0].BlockType == type, $"one {type} block from the source holds the spawns, found " + string.Join(",", own.Select(b => b.BlockType)) + shape.Dump);
        Core2026Proof.Check(spawns.Count == count && spawns.All(c => ReferenceEquals(c.Block, own[0])), $"all {count} {summoned} created directly in that block" + shape.Dump);
        CreatedInPlay(shape, spawns);
    }

    // The summons of one activation share one block that is not the PLAY block, however the source is named.
    private static void Together(HistoryShape shape, string summoned, int count, int blocks)
    {
        var spawns = shape.Creations.Where(c => c.Card == summoned).ToList();
        var groups = spawns.GroupBy(c => c.Block).ToList();
        Core2026Proof.Check(spawns.Count == count * blocks && groups.Count == blocks && groups.All(group => group.Count() == count
            && group.Key.BlockType != BlockType.PLAY), $"{blocks} block(s) of {count} {summoned}" + shape.Dump);
        CreatedInPlay(shape, spawns);
    }

    private static void PerActivation(HistoryShape shape, int source, string summoned, int activations, int count)
    {
        var own = shape.Blocks.Where(b => b.Source == source).ToList();
        Core2026Proof.Check(own.Count == activations && own.All(b => b.BlockType == BlockType.TRIGGER),
            $"{activations} TRIGGER block(s) from {source}, found " + string.Join(",", own.Select(b => b.BlockType)) + shape.Dump);
        var spawns = shape.Creations.Where(c => c.Card == summoned && own.Contains(c.Block)).ToList();
        Core2026Proof.Check(own.All(b => spawns.Count(c => ReferenceEquals(c.Block, b)) == count), $"{count} {summoned} directly in each activation" + shape.Dump);
        CreatedInPlay(shape, spawns);
    }

    private static void CreatedInPlay(HistoryShape shape, List<(int Id, string Card, PowerHistoryBlockStart Block, int Zone)> spawns)
    {
        // A SHOW_ENTITY of a minion its FULL_ENTITY already put in play reads to the client as a reveal the spawn waits for.
        Core2026Proof.Check(!shape.Shown.Overlaps(spawns.Select(c => c.Id)), "no SHOW_ENTITY for a created summon" + shape.Dump);
        Core2026Proof.Check(spawns.All(c => c.Zone == (int)Zone.PLAY) && !shape.ZoneMoved.Overlaps(spawns.Select(c => c.Id)),
            "each created summon's FULL_ENTITY says PLAY, with no ZONE change after it" + shape.Dump);
    }

    // Admission rule R7 (docs/TESTING.md, Client drop prediction), headless: the client ignores every card zone change in
    // the lists of a predicted PLAY block's own rows, so no summoned minion may be created, revealed or moved there.
    private static void PredictedPlayClean(HistoryShape shape, int played)
    {
        var play = shape.Blocks.Single(b => b.Source == played && b.BlockType == BlockType.PLAY);
        var stray = shape.OwnRows[play].Where(entry => entry is PowerHistoryFullEntity full && shape.Summoned.Contains(full.Entity.Id)
            || entry is PowerHistoryShowEntity show && shape.Summoned.Contains(show.Entity.Id)
            || entry is PowerHistoryTagChange tag && (tag.Tag == GameTag.ZONE || tag.Tag == GameTag.CONTROLLER)
                && shape.Summoned.Contains(tag.EntityId)).ToList();
        Core2026Proof.Check(stray.Count == 0, "R7: summons stay nested, out of the PLAY block's own lists: " + string.Join(",", stray.Select(e => e.GetType().Name)) + shape.Dump);
    }

    private sealed class HistoryShape
    {
        internal readonly List<PowerHistoryBlockStart> Blocks = new();
        internal readonly Dictionary<PowerHistoryBlockStart, PowerHistoryBlockStart> Parent = new();
        internal readonly Dictionary<PowerHistoryBlockStart, List<IPowerHistoryEntry>> OwnRows = new();
        internal readonly List<(int Id, string Card, PowerHistoryBlockStart Block, int Zone)> Creations = new();
        internal readonly HashSet<int> Shown = new();
        internal readonly HashSet<int> ZoneMoved = new();
        // Minions that entered PLAY in this run (created there or moved there), other than the played card itself.
        internal readonly HashSet<int> Summoned = new();
        internal string Dump = "";
    }

    private static HistoryShape Shape(Game game, int cursor)
    {
        var shape = new HistoryShape();
        var open = new Stack<PowerHistoryBlockStart>();
        var dump = new System.Text.StringBuilder();
        var created = new HashSet<int>();
        var played = new HashSet<int>();
        foreach (var entry in game.PowerHistory.Full.Skip(cursor))
        {
            string pad = new string(' ', 2 * open.Count);
            if (entry is PowerHistoryBlockStart start)
            {
                dump.Append($"\n  {pad}{start.BlockType} src={start.Source}");
                if (start.BlockType == BlockType.PLAY) played.Add(start.Source);
                shape.Blocks.Add(start);
                shape.Parent[start] = open.Count > 0 ? open.Peek() : null;
                shape.OwnRows[start] = new List<IPowerHistoryEntry>();
                open.Push(start);
                continue;
            }
            if (entry is PowerHistoryBlockEnd) { open.Pop(); continue; }
            if (open.Count > 0) shape.OwnRows[open.Peek()].Add(entry);
            if (entry is PowerHistoryFullEntity full && open.Count > 0)
            {
                var entity = game.IdEntityDic[full.Entity.Id];
                int zone = full.Entity.Tags.TryGetValue(GameTag.ZONE, out int z) ? z : 0;
                shape.Creations.Add((full.Entity.Id, entity.Card.Id, open.Peek(), zone));
                created.Add(full.Entity.Id);
                if (entity is Minion) dump.Append($"\n  {pad}FULL {full.Entity.Id} {entity.Card.Id} zone={(Zone)zone}");
            }
            else if (entry is PowerHistoryShowEntity show) { shape.Shown.Add(show.Entity.Id); dump.Append($"\n  {pad}SHOW {show.Entity.Id}"); }
            else if (entry is PowerHistoryMetaData meta) dump.Append($"\n  {pad}META {meta.Type} {string.Join(",", meta.Info ?? new List<int>())}");
            else if (entry is PowerHistoryTagChange tag && tag.Tag == GameTag.ZONE)
            {
                if (created.Contains(tag.EntityId)) shape.ZoneMoved.Add(tag.EntityId);
                if (tag.Value == (int)Zone.PLAY && game.IdEntityDic[tag.EntityId] is Minion) shape.Summoned.Add(tag.EntityId);
                dump.Append($"\n  {pad}ZONE {tag.EntityId}={(Zone)tag.Value}");
            }
        }
        foreach (var creation in shape.Creations)
            if (creation.Zone == (int)Zone.PLAY && game.IdEntityDic[creation.Id] is Minion) shape.Summoned.Add(creation.Id);
        shape.Summoned.ExceptWith(played);
        Core2026Proof.Check(open.Count == 0, "every block closed");
        shape.Dump = dump.ToString();
        return shape;
    }
}
