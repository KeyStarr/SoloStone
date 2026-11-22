using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Config;
using SabberStoneCore.Kettle;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // #195: Halazzi, the Lynx played HalazziTheLynx_SpawnToHand once per Lynx. Its Battlecry queues one AddCardTo per free hand
    // slot (EnqueueNumberTask), and each queued repeat ran in a sibling POWER block sourced by Halazzi. The client's
    // PowerProcessor makes a PowerSpellController for every POWER task list and activates the card's play effect in each
    // (PowerSpellController.AddPowerSourceAndTargets / ActivateCardEffects). Retail sends one POWER block per effect index
    // (HearthSim hsreplay-test-data: no PLAY with two same-index POWER blocks of its card outside a doubled Battlecry; Unleash
    // the Hounds, Burgle and Animal Companion create several cards inside one). Every card whose power queues repeats of itself
    // the same way shares the fix (TaskQueue continuation), so the sweep below covers a spell and a summon too. Both seats.
    private static void PlayEffectOnceHistory()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (var (id, held, enemies) in new[] {
            ("CORE_TRL_900", 3, 0), ("CORE_TRL_900", 0, 0), ("CORE_TRL_900", 9, 0), ("TRL_900", 3, 0),
            ("EX1_538", 3, 3), ("CORE_GVG_061", 3, 0) })
        {
            string name = $"{id}-held{held}-{(bot ? "opponent" : "owner")}";
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game;
            var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
            foreach (var minion in owner.Opponent.BoardZone.ToArray()) owner.Opponent.SetasideZone.Add(minion.Zone.Remove(minion));
            for (int i = 0; i < enemies; i++) EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.BoardZone);
            for (int i = 0; i < held; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            owner.BaseMana = 10; owner.UsedMana = 0;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count, hand = owner.HandZone.Count, board = owner.BoardZone.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            DumpDrawHistory(name, capture, cursor);
            var rows = History(capture, cursor);
            Check(rows.Count(e => e.HasPowerStart) == rows.Count(e => e.HasPowerEnd), name + ": every block closes");
            // The card's own POWER blocks directly inside its PLAY block, and what each creates.
            var powers = new List<(int Index, int Created)>();
            int depth = 0, play = -1;
            foreach (var row in rows)
            {
                if (row.HasPowerStart)
                {
                    if (depth == 0 && (int)row.PowerStart.Type == (int)BlockType.PLAY && row.PowerStart.Source == source.Id) play = 0;
                    else if (play == 0 && depth == 1 && (int)row.PowerStart.Type == (int)BlockType.POWER && row.PowerStart.Source == source.Id)
                        powers.Add((row.PowerStart.EffectIndex, 0));
                    depth++;
                    continue;
                }
                if (row.HasPowerEnd) { if (--depth == 0) play = -1; continue; }
                if (row.HasFullEntity && play == 0 && depth >= 2 && powers.Count > 0)
                    powers[^1] = (powers[^1].Index, powers[^1].Created + 1);
            }
            if (powers.Count != 1 || powers.Select(p => p.Index).Distinct().Count() != powers.Count)
                failures.Add($"{name}: {powers.Count} POWER blocks of the card in its PLAY ({string.Join(",", powers.Select(p => p.Index + ":" + p.Created))}); "
                    + "the client plays the card's play effect once per block, retail sends one");
            int created = id.EndsWith("TRL_900") ? Math.Min(10 - held, 10) : id == "EX1_538" ? 3 : 0;
            if (id.EndsWith("TRL_900"))
            {
                Check(owner.HandZone.Count(c => c.Card.Id == "TRL_348t") == created && owner.HandZone.Count == hand - 1 + created,
                    name + ": Halazzi fills the hand");
                if (powers.Count == 1 && powers[0].Created != created)
                    failures.Add($"{name}: the POWER block creates {powers[0].Created} Lynxes, expected {created}");
            }
            if (id == "EX1_538") Check(owner.BoardZone.Count == board + 3, name + ": one Hound per enemy minion");
            capture.SerializeAll();
            cases++;
        }
        failures.AddRange(ContinuationHistory());
        failures.AddRange(RepeatSweep());
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS play effect once: {cases} original cases, 18 continuation cases, {RepeatSweepCards.Length}-card repeat sweep, Brann + Halazzi");
    }

    // Every card whose power queued its repeats into same-source sibling POWER blocks (a sweep of all 2339 cards playable from
    // hand, 2026-11-19: 62 cards before the summon join, 39 after it). Count every same-source POWER/TRIGGER at any depth.
    private static readonly string[] RepeatSweepCards = {
        "BE_183", "BOT_084", "BOT_254", "BOT_536", "BOT_544", "BOT_600", "BOT_601", "BRM_001", "BRM_030", "CFM_621t23", "CFM_621t30", "CFM_621t31",
        "CFM_811", "CORE_GVG_061", "CORE_TRL_900", "DAL_010", "DAL_173", "DAL_177", "DAL_177ts", "EX1_050", "EX1_116", "EX1_161",
        "EX1_248", "EX1_384", "EX1_538", "EX1_562", "EX1_573b", "FP1_019", "GIL_147", "GIL_548", "GIL_553", "GIL_825", "GVG_042", "GVG_045", "GVG_061", "GVG_090",
        "ICC_314t2", "ICC_314t3", "ICC_832a", "ICC_832b", "KAR_026", "LOOT_064t1", "LOOT_064t2", "LOOT_103t1", "LOOT_103t2",
        "LOOT_347", "LOOT_526", "OG_090", "OG_114", "OG_116", "OG_195a", "OG_239", "OG_273", "OG_280", "TIME_027", "TRL_253", "TRL_362", "TRL_900", "UNG_002", "UNG_926" };
    // LOOT_054 waits for a player's choice; UNG_047 has a separate decision-boundary fixture below.

    private static List<string> RepeatSweep()
    {
        var failures = new List<string>();
        foreach (string id in RepeatSweepCards)
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            var owner = game.Player1;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
            owner.BaseMana = 10; owner.UsedMana = 0;
            EngineEntity.FromCard(owner, Cards.FromId("CS2_172"), zone: owner.BoardZone);
            EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.BoardZone);
            for (int i = 0; i < 3; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            var option = owner.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
            if (option == null) { failures.Add(id + ": sweep fixture cannot play it"); continue; }
            int from = game.PowerHistory.Full.Count;
            game.Process(option);
            // Include nested activations and continuations outside PLAY, not just its immediate POWER children. Only POWER: the
            // card's own triggers (Lifesteal, a zone transform, C'Thun's copy) are separate TRIGGER blocks sharing the index.
            var blocks = game.PowerHistory.Full.Skip(from).OfType<PowerHistoryBlockStart>()
                .Where(s => s.Source == source.Id && s.BlockType == BlockType.POWER)
                .ToArray();
            if (id == "GIL_825")
            {
                if (blocks.Length != 2) failures.Add(id + $": expected two separate POWER waves, got {blocks.Length}");
                continue;
            }
            var keys = blocks.Select(s => s.Index + "/" + s.EffectIndex).ToArray();
            if (keys.Length != keys.Distinct().Count())
                failures.Add($"{id}: {keys.Length} same-source POWER blocks at any depth share an effect index ("
                    + string.Join(",", blocks.Select(s => $"{s.BlockType}:{s.Index}/{s.EffectIndex}"
                        + (s.TriggerKeyword.HasValue ? $":{(GameTag)s.TriggerKeyword.Value}" : ""))) + ")");
        }
        failures.AddRange(BrannHalazzi());
        return failures;
    }

    private static List<(int Start, int End, int Depth, BlockType Type)> ContinuationBlocks(
        List<PowerHistoryData> rows, int source, string name)
    {
        var stack = new Stack<int>();
        var blocks = new List<(int Start, int End, int Depth, BlockType Type)>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].HasPowerStart) stack.Push(i);
            else if (rows[i].HasPowerEnd)
            {
                Check(stack.Count > 0, name + ": no unmatched BLOCK_END");
                int start = stack.Pop();
                var block = rows[start].PowerStart;
                var type = (BlockType)(int)block.Type;
                if (block.Source == source && (type == BlockType.POWER || type == BlockType.TRIGGER))
                    blocks.Add((start, i, stack.Count, type));
            }
        }
        Check(stack.Count == 0, name + ": every wire block closes");
        return blocks.OrderBy(b => b.Start).ToList();
    }

    private static List<string> ContinuationHistory()
    {
        var failures = new List<string>();
        foreach (bool bot in new[] { false, true })
        foreach (var (id, enrage) in new[] { ("ICC_041", false), ("GIL_825", false), ("GIL_825", true), ("ICC_054", false),
            ("CFM_806", false), ("ICC_201", false), ("UNG_047", false), ("FP1_002", false), ("TRL_347", false) })
        {
            string name = $"{id}{(enrage ? "-enrage" : "")}-continuation-{(bot ? "opponent" : "owner")}";
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game;
            var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).Concat(owner.BoardZone)
                .Concat(owner.Opponent.BoardZone).ToArray()) old.Controller.SetasideZone.Add(old.Zone.Remove(old));
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: id == "FP1_002" ? owner.BoardZone : owner.HandZone);
            var victims = new List<IPlayable>();
            if (id is "ICC_041" or "GIL_825" or "ICC_054" or "TRL_347")
                for (int i = 0; i < (id == "TRL_347" ? 1 : id == "GIL_825" ? 2 : 3); i++)
                {
                    var victim = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId(id == "GIL_825" && i == 0 ? "EX1_096" : "CS2_182"), zone: owner.Opponent.BoardZone);
                    if (id == "ICC_041") victim.Damage = victim.Health - (i + 1);
                    if (id == "GIL_825") victim.BaseHealth = (i + 1) * 2;
                    if (id == "TRL_347") victim.Damage = victim.Health - 1;
                    victims.Add(victim);
                }
            // Survives wave one damaged: AuraUpdate writes its Enrage before the next wave's DEATHS block.
            var berserker = enrage ? (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("EX1_393"), zone: owner.Opponent.BoardZone) : null;
            if (id == "UNG_047")
                victims.Add(EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone));
            var drawn = new List<IPlayable>();
            if (id is "CFM_806" or "ICC_201")
                // DeckZone draws from the end; retain draw order in the assertions below.
                foreach (string card in Enumerable.Repeat(id == "CFM_806" ? "EX1_043" : "FP1_002", 3)
                    .Concat(new[] { "CS2_182", "CS2_029" }).Reverse())
                    drawn.Insert(0, EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.DeckZone));
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (id == "FP1_002")
            {
                ((Minion)source).ToBeDestroyed = true;
                game.DeathProcessingAndAuraUpdate();
            }
            else InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == source.Id && (id is not ("UNG_047" or "TRL_347") || t.Target?.Id == victims[0].Id)));
            FlushFieldHistory(match);
            var phases = new List<int> { capture.HistorySnapshots(cursor).Sum(h => h.List.Count) };
            var picks = new List<int>();
            if (id == "UNG_047")
            {
                for (int i = 0; i < 2; i++)
                {
                    Check(owner.Choice?.Choices.Count == 3, name + $": Adapt decision {i + 1} offers three choices");
                    int pick = owner.Choice.Choices.First();
                    picks.Add(pick);
                    InvokeProcess(match, ChooseTask.Pick(owner, pick));
                    FlushFieldHistory(match);
                    phases.Add(capture.HistorySnapshots(cursor).Sum(h => h.List.Count));
                }
                Check(owner.Choice == null && victims[0].Zone == owner.GraveyardZone, name + ": consumed target and finished both Adapts");
            }
            var rows = capture.HistorySnapshots(cursor).SelectMany(h => h.List).ToList();
            DumpDrawHistory(name, capture, cursor);
            var blocks = ContinuationBlocks(rows, source.Id, name);
            string shape = string.Join(",", blocks.Select(b => $"{b.Type}@{b.Depth}[{b.Start}:{b.End}]"));
            Console.WriteLine($"SHAPE {name}: {shape}; phases={string.Join(",", phases)}");
            if (id is "ICC_041" or "GIL_825")
            {
                int waves = victims.Count + 1;
                if (blocks.Count != waves || blocks.Any(b => b.Type != BlockType.POWER))
                    failures.Add(name + $": expected {waves} separate POWER waves (including the final empty wave), got " + shape);
                Check(victims.All(v => v.Zone == owner.Opponent.GraveyardZone), name + ": all health steps die");
                if (enrage) Check(berserker.Zone == owner.Opponent.GraveyardZone, name + ": Amani dies in wave two");
                if (blocks.Count == waves)
                    for (int i = 0; i < waves; i++)
                        Check(rows.Skip(blocks[i].Start + 1).Take(blocks[i].End - blocks[i].Start - 1)
                            .Where(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE)
                            .SelectMany(e => e.MetaData.Info).SequenceEqual(victims.Skip(i).Select(v => v.Id)
                                .Concat(enrage && i < 2 ? new[] { berserker.Id } : Array.Empty<int>())),
                            name + $": wave {i + 1} hits only remaining minions");
                // Each recast starts by draining the preceding wave's deaths, before its own damage.
                for (int i = 0; blocks.Count == waves && i < victims.Count; i++)
                {
                    int death = rows.FindIndex(e => IsZone(e, victims[i].Id, EngineZone.GRAVEYARD));
                    int nextHit = rows.FindIndex(blocks[i + 1].Start, blocks[i + 1].End - blocks[i + 1].Start,
                        e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE);
                    Check(blocks[i].Depth == blocks[i + 1].Depth && blocks[i].End < blocks[i + 1].Start && death > blocks[i + 1].Start
                        && death < (nextHit < 0 ? blocks[i + 1].End : nextHit), name + $": death phase {i + 1} precedes next damage wave");
                    if (!enrage)
                        Check(rows[blocks[i + 1].Start + 1].HasPowerStart
                            && (int)rows[blocks[i + 1].Start + 1].PowerStart.Type == (int)BlockType.DEATHS,
                            name + ": recast opens with DEATHS");
                    else if (i == 0)
                    {
                        int deaths = rows.FindIndex(blocks[1].Start + 1, blocks[1].End - blocks[1].Start - 1,
                            e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DEATHS);
                        int enraged = rows.FindIndex(blocks[1].Start + 1, blocks[1].End - blocks[1].Start - 1,
                            e => e.HasTagChange && e.TagChange.Entity == berserker.Id && e.TagChange.Tag == (int)GameTag.ATK && e.TagChange.Value == 5);
                        Check(enraged > blocks[1].Start && enraged < deaths, name + ": surviving Amani gains Enrage before DEATHS");
                    }
                    if (id == "GIL_825" && i == 0)
                    {
                        int trigger = rows.FindIndex(blocks[i + 1].Start, blocks[i + 1].End - blocks[i + 1].Start,
                            e => e.HasPowerStart && e.PowerStart.Source == victims[0].Id
                                && (int)e.PowerStart.Type == (int)BlockType.TRIGGER && e.PowerStart.TriggerKeyword == (int)GameTag.DEATHRATTLE);
                        Check(trigger > death && trigger < nextHit, name + ": Deathrattle precedes next damage wave");
                    }
                }
            }
            if (id == "ICC_054")
            {
                if (blocks.Count != 3 || blocks.Any(b => b.Type != BlockType.POWER))
                    failures.Add(name + ": expected three separate POWER casts, got " + shape);
                Check(owner.BoardZone.Count == 3 && owner.BoardZone.All(m => m.Card.Id == "ICC_832t4"), name + ": three Scarabs catch up");
                if (blocks.Count == 3)
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        var block = blocks[i];
                        Check(rows.Skip(block.Start + 1).Take(block.End - block.Start - 1)
                            .Count(e => e.HasFullEntity && SnapshotName(e) == "ICC_832t4") == 1, name + ": one Scarab per cast");
                        Check(i == 0 || blocks[i - 1].End < block.Start, name + ": recast is a sibling, not nested");
                    }
            }
            if (id is "ICC_201")
            {
                // Roll the Bones casts itself again (ComplexTask.RecursiveTask, HistoryCastKind.Repeat), like Defile and Spreading
                // Plague: each cast is its own sibling POWER block.
                Check(drawn.Take(4).All(c => c.Zone == owner.HandZone) && drawn[4].Zone == owner.DeckZone,
                    name + ": draws three qualifying minions then stops at non-qualifier");
                if (blocks.Count != 4 || blocks.Any(b => b.Type != BlockType.POWER) || blocks.Zip(blocks.Skip(1), (a, b) => a.End < b.Start).Any(x => !x))
                    failures.Add(name + ": expected four sibling POWER casts, got " + shape);
            }
            if (id is "CFM_806")
            {
                // Wrathion repeats a draw-until instruction inside one Battlecry: one effect.
                Check(drawn.Take(4).All(c => c.Zone == owner.HandZone) && drawn[4].Zone == owner.DeckZone,
                    name + ": draws three qualifying minions then stops at non-qualifier");
                if (blocks.Count != 1 || blocks[0].Type != BlockType.POWER)
                    failures.Add(name + ": expected all four draws in one POWER, got " + shape);
                else
                    Check(rows.Skip(blocks[0].Start + 1).Take(blocks[0].End - blocks[0].Start - 1)
                        .Where(e => e.HasShowEntity && drawn.Any(c => c.Id == e.ShowEntity.Entity))
                        .Select(e => e.ShowEntity.Entity).SequenceEqual(drawn.Take(4).Select(c => c.Id)), name + ": all four draw reveals inside one effect");
            }
            if (id == "UNG_047")
            {
                // Adapt flushes history for each decision. The Pterrordax POWER creates both sets of offers; each answer
                // then has two top-level POWERs of its chosen Adapt token (apply effect, move token to graveyard).
                Check(blocks.Count == 1 && blocks[0].Type == BlockType.POWER && blocks[0].End < phases[0],
                    name + ": source Battlecry closes before the first answer");
                Check(rows.Skip(blocks[0].Start + 1).Take(blocks[0].End - blocks[0].Start - 1)
                    .Count(e => e.HasFullEntity) == 6, name + ": both sets of three Adapt offers inside the Battlecry");
                for (int i = 0; i < picks.Count; i++)
                {
                    var answer = rows.Skip(phases[i]).Take(phases[i + 1] - phases[i]).ToList();
                    var powers = ContinuationBlocks(answer, picks[i], name);
                    Check(powers.Count == 2 && powers.All(b => b.Type == BlockType.POWER && b.Depth == 0)
                        && powers[0].End < powers[1].Start && answer[powers[0].Start].PowerStart.Target == source.Id,
                        name + $": Adapt answer {i + 1} has separate effect and cleanup POWERs");
                    Check(answer.Skip(powers[0].Start + 1).Take(powers[0].End - powers[0].Start - 1)
                        .Any(e => e.HasShowEntity && SnapshotTag(e, GameTag.ATTACHED) == source.Id)
                        && answer.Skip(powers[1].Start + 1).Take(powers[1].End - powers[1].Start - 1)
                            .Any(e => IsZone(e, picks[i], EngineZone.GRAVEYARD)), name + ": answer applies its Adapt and retires the token");
                }
            }
            if (id == "FP1_002")
            {
                Check(blocks.Count == 1 && blocks[0].Type == BlockType.TRIGGER, name + ": repeats stay in one TRIGGER");
                int death = rows.FindIndex(e => IsZone(e, source.Id, EngineZone.GRAVEYARD));
                Check(rows[blocks[0].Start].PowerStart.TriggerKeyword == (int)GameTag.DEATHRATTLE && death >= 0 && death < blocks[0].Start,
                    name + ": Deathrattle TRIGGER follows the source's death");
                Check(owner.BoardZone.Count == 2 && owner.BoardZone.All(m => m.Card.Id == "FP1_002t"), name + ": both Spectral Spiders summoned");
                Check(rows.Skip(blocks[0].Start + 1).Take(blocks[0].End - blocks[0].Start - 1)
                    .Count(e => e.HasFullEntity && SnapshotName(e) == "FP1_002t") == 2, name + ": both summons inside the TRIGGER");
            }
            if (id == "TRL_347")
            {
                // TakeDamage drains Overkill while the damage POWER is still open; joining cannot erase that live parent.
                Check(blocks.Count == 2 && blocks.All(b => b.Type == BlockType.POWER)
                    && blocks[1].Depth == blocks[0].Depth + 1 && blocks[0].Start < blocks[1].Start && blocks[1].End < blocks[0].End,
                    name + ": Overkill continuation stays nested inside the open damage POWER");
                Check(owner.BoardZone.Count == 1 && owner.BoardZone[0].Card.Id == "TRL_347t"
                    && victims[0].Zone == owner.Opponent.GraveyardZone, name + ": overkill summons one Devilsaur");
                Check(rows.Skip(blocks[1].Start + 1).Take(blocks[1].End - blocks[1].Start - 1)
                    .Count(e => e.HasFullEntity && SnapshotName(e) == "TRL_347t") == 1, name + ": Devilsaur inside nested continuation");
            }
            capture.SerializeAll();
        }
        return failures;
    }

    // Brann doubles the Battlecry: two POWER blocks, as retail sends for a doubled Battlecry (two same-index blocks in
    // hslog-tests/39832_blocktype_invalid.power.log:6248). The first holds every Lynx that reaches the hand.
    private static List<string> BrannHalazzi()
    {
        var failures = new List<string>();
        var game = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        var owner = game.Player1;
        foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
        owner.BaseMana = 10; owner.UsedMana = 0;
        EngineEntity.FromCard(owner, Cards.FromId("LOE_077"), zone: owner.BoardZone);
        for (int i = 0; i < 3; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
        var source = EngineEntity.FromCard(owner, Cards.FromId("CORE_TRL_900"), zone: owner.HandZone);
        int from = game.PowerHistory.Full.Count;
        game.Process(owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        var powers = new List<int>();
        int depth = 0; bool inPlay = false;
        foreach (var e in game.PowerHistory.Full.Skip(from))
        {
            if (e is PowerHistoryBlockStart s)
            {
                if (depth == 0 && s.BlockType == BlockType.PLAY && s.Source == source.Id) inPlay = true;
                else if (inPlay && depth == 1 && s.BlockType == BlockType.POWER && s.Source == source.Id) powers.Add(0);
                depth++;
            }
            else if (e is PowerHistoryBlockEnd && --depth == 0) inPlay = false;
            else if (inPlay && depth >= 2 && powers.Count > 0 && e is PowerHistoryFullEntity full && full.Entity.Name == "TRL_348t") powers[^1]++;
        }
        // The second Battlecry counts the free slots before the first one's Lynxes arrive, so its Lynxes are made and burnt
        // (engine order, unchanged here); they stay in its own block.
        if (owner.HandZone.Count(c => c.Card.Id == "TRL_348t") != 7 || powers.Count != 2 || powers[0] != 7)
            failures.Add($"Brann + Halazzi: POWER blocks hold {string.Join(",", powers)} Lynxes, expected two blocks, the first with 7");
        return failures;
    }
}
