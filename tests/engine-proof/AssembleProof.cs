using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class AssembleProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("b15-assemble")]
    internal static void Run()
    {
        Catalog();
        Unpinned_SummonPayloadPosition();
        Unpinned_QueuedPayloadIdentity();
        Unpinned_IndependentHalves();
        Unpinned_TriggerLifecycle();
        Unpinned_ThawHandResidence();
        Pinned_FilterStages();
        SeededOrderedSampling();
        Unpinned_CapacityAndCombo();
        Unpinned_ComboRepetition();
        Unpinned_SourceLeaves();
        Console.WriteLine("PASS Assemble catalog, independent halves, lifecycle, hand residence, filters, seeded ordering, capacity");
    }

    internal static void Catalog()
    {
        // The catalogue is compared with the client export's rows in the client tier only (ClientExport.Oracle).
        var rows = ClientExport.Oracle ? ClientExport.RequireCards().Values.Where(c =>
            c.GetProperty("tags").TryGetProperty("4533", out var tag) && tag.GetInt32() != 0
            && c.GetProperty("text").GetString().Count(ch => ch == '&') == 1).ToArray() : null;
        if (rows != null)
        {
            Check(rows.Length == 321 && rows.Length == AssembleCatalog.Entries.Count, "exact curated export catalog, including alias identities");
            foreach (var row in rows)
            {
                var entry = AssembleCatalog.Entries[row.GetProperty("cardId").GetString()];
                Check(entry.DbfId == row.GetProperty("dbfId").GetInt32() && entry.Text == row.GetProperty("text").GetString(),
                    "catalog identity and split text match pinned export");
            }
        }
        else ClientExport.Skip("Assemble catalogue against the client export");
        var supported = Cards.All.Where(AssembleCatalog.Supports).ToArray();
        Check(supported.Length >= 50, "audited executable subset installed");
        foreach (var top in supported)
        foreach (var bottom in supported)
        {
            var composed = Card.CreateAssembledCard(bottom, top, bottom);
            Check(composed.Id == bottom.Id && composed.AssetId == bottom.AssetId && composed.Cost == bottom.Cost
                && composed.ATK == bottom.ATK && composed.Health == bottom.Health && composed.Class == bottom.Class
                && composed.Text == AssembleCatalog.Half(top, true) + AssembleCatalog.Half(bottom, false),
                "all audited halves compose without changing body metadata");
            Check(composed[(GameTag)32] == top[(GameTag)32] && composed[(GameTag)4577] == top[(GameTag)4577],
                "native trigger presentation follows the chosen upper half");
        }
        Console.WriteLine($"assemble catalog: pinned={rows?.Length.ToString() ?? "skipped"}; executable={supported.Length}");
    }

    private static void Unpinned_SummonPayloadPosition()
    {
        foreach (string top in new[] { "CORE_DMF_067", "CORE_LOOT_413", "TIME_428" })
        foreach (string lower in new[] { "CORE_EX1_110", "TLC_225", "TLC_237", "EDR_492", "JAIL_440", "CATA_476", "CORE_AV_337" })
        {
            var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.WARRIOR); var p = g.Player1;
            var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
            var definition = Card.CreateAssembledCard(Cards.FromId("CORE_LOOT_413"), Cards.FromId(top), Cards.FromId(lower));
            var source = (Minion)Generic.DrawCard(p, definition);
            var right = Summon(p, "CS2_182"); p.BaseMana = 10; p.UsedMana = 0;
            Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 2)), "positioned composite play accepted");
            if (top == "CORE_LOOT_413") Kill(g, source);
            if (top == "TIME_428") End(g);
            var tokens = p.BoardZone.Where(m => m.Id != left.Id && m.Id != right.Id && m.Id != source.Id).ToArray();
            Check(tokens.Length > 0, "composed summon payload executes");
            var prefix = new[] { location.Id, left.Id }.Concat(source.Zone == p.BoardZone ? new[] { source.Id } : Array.Empty<int>());
            Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(prefix.Concat(tokens.Select(m => m.Id)).Append(right.Id)),
                "Unpinned_Assemble summon payload uses selected event's physical source slot: " + top + "/" + lower);
            if (lower == "CORE_EX1_110") Check(tokens.All(m => m.AttackDamage == 5 && m.Health == 5), "pinned Core Baine snapshot retained");
            // Direct death fixtures settle their summons at the next public action boundary.
            End(g);
            Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)), "adapted summon payload snapshot");
        }
    }

    private static void Unpinned_QueuedPayloadIdentity()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.WARRIOR); var p = g.Player1;
        var definition = Card.CreateAssembledCard(Cards.FromId("CORE_LOOT_413"), Cards.FromId("CORE_DMF_067"), Cards.FromId("CORE_EX1_110"));
        var source = (Minion)Generic.DrawCard(p, definition); PlayEntity(g, p, source);
        var queued = source.Power.PowerTask;
        Generic.ChangeEntityBlock(p, source, Cards.FromId("CS2_182"), true);
        int count = p.BoardZone.Count;
        queued.Process(g, p, source, null, new TaskStack());
        Check(p.BoardZone.Count == count + 1 && p.BoardZone.Last().Card.Id == "EX1_110t", "queued payload retains its recipe after source transformation");
    }

    private static Card ArmorBody(string top = "CORE_DMF_067") => Card.CreateAssembledCard(
        Cards.FromId("CORE_LOOT_413"), Cards.FromId(top), Cards.FromId("CORE_LOOT_413"));

    private static void Unpinned_IndependentHalves()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.WARRIOR); var p = g.Player1;
        var definition = ArmorBody();
        int armor = p.Hero.Armor;
        var source = Generic.DrawCard(p, definition);
        PlayEntity(g, p, source);
        Check(p.Hero.Armor == armor + 3 && source.HasDeathrattle, "new Battlecry and Deathrattle execute selected armor payload");
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools);
        foreach (var next in new[] { g, clone, loaded })
        {
            var player = next.Player1; var body = (Minion)next.IdEntityDic[source.Id];
            var handCopy = Generic.Copy(player, player.Hero, body, Zone.HAND);
            Check(handCopy.Card.Text == definition.Text && handCopy.Card[AssembleRules.FirstHalf] == definition[AssembleRules.FirstHalf],
                "backward copy keeps composed definition");
            body.Silence(); next.DeathProcessingAndAuraUpdate();
            Check(!body.HasDeathrattle, "Silence suppresses composed text");
            Check(Generic.ReturnToHandBlock(player, body) && body.Card.Text == definition.Text, "bounce keeps composed identity");
            PlayEntity(next, player, body);
            Check(player.Hero.Armor == armor + 6 && body.HasDeathrattle, "bounce restores composed intrinsic text");
            Kill(next, body);
            Check(player.Hero.Armor == armor + 9, "selected Deathrattle runs after replay");
            var revived = Generic.Copy(player, player.Hero, body, Zone.PLAY);
            Check(revived.Card.Text == definition.Text && revived.HasDeathrattle, "graveyard resummon keeps composition");
            Kill(next, (Minion)revived);
            Check(player.Hero.Armor == armor + 12, "resummoned selected payload executes");
        }
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
            "stateful composed payload exact clone/snapshot and future identity");
    }

    private static void Unpinned_TriggerLifecycle()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.WARRIOR); var p = g.Player1;
        var definition = ArmorBody("TIME_428");
        var source = (Minion)Generic.DrawCard(p, definition); PlayEntity(g, p, source);
        int armor = p.Hero.Armor;
        End(g); Check(p.Hero.Armor == armor + 3, "owner end triggers lower payload");
        End(g); Check(p.Hero.Armor == armor + 3, "opponent end cannot trigger");
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g); Check(p.Hero.Armor == armor + 3, "Silence removes selected trigger");
        End(g); Generic.ReturnToHandBlock(p, source); PlayEntity(g, p, source);
        Generic.ReturnToHandBlock(p, source);
        End(g); Check(p.Hero.Armor == armor + 3, "leave-play cancels trigger");
    }

    private static void Unpinned_ThawHandResidence()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.MAGE); var p = g.Player1;
        Play(g, p, "BE_257"); AnswerAll(g, p);
        var held = p.HandZone.Single(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]);
        int cost = held.Cost;
        var copy = Generic.Copy(p, p.Hero, held, Zone.HAND);
        var foreign = Generic.Copy(p.Opponent, p.Hero, held, Zone.HAND);
        var saved = Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools);
        foreach (var next in new[] { g, g.CloneExact(history: true), saved })
        {
            var owner = next.Player1; var original = next.IdEntityDic[held.Id]; var localCopy = next.IdEntityDic[copy.Id];
            var opponentCopy = next.IdEntityDic[foreign.Id];
            End(next);
            Check(original.Cost == cost - 1 && localCopy.Cost == cost - 1 && opponentCopy.Cost == cost,
                "independent recurrence uses current controller owner end");
            End(next); Check(original.Cost == cost - 1 && opponentCopy.Cost == cost - 1, "opponent recurrence independent");
            owner.DeckZone.Add(owner.HandZone.Remove(original));
            Generic.Draw(owner, original);
            PlayEntity(next, owner, localCopy);
            Generic.ReturnToHandBlock(owner, (Minion)localCopy);
            Check(original.AppliedEnchantments.All(e => e.Card.Id != "BE_257e")
                && localCopy.AppliedEnchantments.All(e => e.Card.Id != "BE_257e"), "shuffle/play permanently end recurrence");
            int drawnCost = original.Cost, bouncedCost = localCopy.Cost;
            End(next); Check(original.Cost == drawnCost && localCopy.Cost == bouncedCost, "draw/bounce do not restart recurrence");
        }
        // Effective zero must not bank reductions behind another modifier.
        var zeroGame = KnownMetaProofFixture.BlackEmpireGame(CardClass.MAGE); var zeroPlayer = zeroGame.Player1;
        var zero = Generic.DrawCard(zeroPlayer, ArmorBody());
        Generic.AddEnchantmentBlock(zeroGame, Cards.FromId("BE_257e"), zero, zero);
        CostRules.AddModifier((Playable)zero, EffectOperator.SUB, 20);
        End(zeroGame);
        Check(zero.AppliedEnchantments.All(e => e.Card.Id != "BE_257e2"), "zero effective Cost banks no negative discounts");
    }

    internal static void AnswerAll(Game game, Controller player)
    {
        while (player.Choice != null) PlayerDecisionProof.Answer(game, player.Choice.Choices[0]);
    }

    private static void Pinned_FilterStages()
    {
        var body = Cards.FromId("CORE_LOOT_413"); var donor = Cards.FromId("CORE_EX1_007");
        Check(AssembleRules.DonorMatches(body, donor, "BE_851", CardClass.DRUID), "Unpinned_StewardOnlyBaseBeast: non-Beast donor admitted");
        Check(AssembleRules.DonorMatches(body, donor, "BE_553", CardClass.HUNTER), "Unpinned_TinkerOnlyBaseMech: non-Mech donor admitted");
        Check(AssembleRules.DonorMatches(body, donor, "BE_571", CardClass.DEATHKNIGHT), "Pinned_Fleshshape: no Deathrattle predicate on donors");
        Check(AssembleRules.DonorMatches(body, donor, "BE_740", CardClass.WARRIOR), "Pinned_Chisel: no Taunt predicate on donors");
        Check(AssembleRules.DonorMatches(body, donor, "BE_229", CardClass.PALADIN), "Pinned_Sandcrafter: no owner-end predicate on donors");
        var one = Cards.FromId("BE_207");
        Check(AssembleRules.DonorMatches(one, body, "BE_262", (CardClass)14)
            && AssembleRules.DonorMatches(one, donor, "BE_262", (CardClass)14)
            && !AssembleRules.DonorMatches(one, Cards.FromId("TIME_100"), "BE_262", (CardClass)14),
            "Pinned_AttentiveTenderBaseOneDonorsOneThroughThree");
        var four = Cards.FromId("TIME_100");
        Check(AssembleRules.DonorMatches(four, Cards.FromId("CS3_024"), "BE_044", CardClass.MAGE)
            && AssembleRules.DonorMatches(four, Cards.FromId("CORE_EX1_110"), "BE_044", CardClass.MAGE)
            && !AssembleRules.DonorMatches(four, Cards.FromId("CORE_EX1_559"), "BE_044", CardClass.MAGE),
            "Pinned_FetcherPrimaryFourDonorsFourToSix");
        Check(AssembleRules.PrimaryMatches(Cards.FromId("CORE_EX1_559"), "BE_257", CardClass.ROGUE),
            "explicit Mage primary remains Mage when acquired by another hero class");
        Check(AssembleRules.DonorMatches(Cards.FromId("CORE_EX1_559"), Cards.FromId("CORE_UNG_848"), "BE_257", CardClass.MAGE),
            "Pinned_ThawMageBaseAllowsNeutralDonor");
        Check(!AssembleRules.PrimaryMatches(body, "BE_702", CardClass.ROGUE)
            && AssembleRules.PrimaryMatches(Cards.FromId("CORE_EX1_319"), "BE_702", CardClass.ROGUE),
            "Unpinned_Shadowcrafter excludes Neutral and current class from body");
    }

    private static void Unpinned_SourceLeaves()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.MAGE); var p = g.Player1;
        var source = (Minion)Play(g, p, "BE_044");
        Kill(g, source);
        AnswerAll(g, p);
        Check(p.Choice == null && p.HandZone.Count(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]) == 1,
            "started composition finishes after source leaves play");
        var stable = g.SaveSnapshot();
        g.RestoreRewindBaseline(stable);
        Check(p != g.Player1 && g.Player1.HandZone.Any(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]),
            "in-place Rewind restores composed definition");
    }

    private static void SeededOrderedSampling()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.MAGE); var reference = g.CloneExact(history: true);
        var cards = new[] { "CORE_LOOT_413", "CORE_EX1_007", "CORE_EX1_110", "TIME_100", "EDR_492" }.Select(Cards.FromId).ToList();
        var expected = new List<string>(); var candidates = cards.ToList();
        for (int i = 0; i < 2; i++) { int pick = reference.Random.Next(candidates.Count); expected.Add(candidates[pick].Id); candidates.RemoveAt(pick); }
        var actual = AssembleRules.Take(g, cards, 2).Select(c => c.Id).ToArray();
        Check(actual.SequenceEqual(expected) && g.Random.CaptureState().Equals(reference.Random.CaptureState()),
            "seeded, order-sensitive donor selection and exact RNG consumption");
        Check(AssembleRules.Take(g, new List<Card>(), 2).Length == 0, "empty bucket yields nothing");
    }

    private static void Unpinned_CapacityAndCombo()
    {
        var g = KnownMetaProofFixture.BlackEmpireGame(CardClass.HUNTER); var p = g.Player1;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "BE_553"); AnswerAll(g, p);
        Check(p.HandZone.Any(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]), "full board does not constrain hand generation");
        FillHand(p); var source = p.BoardZone[0]; int before = p.HandZone.Count;
        Cards.FromId("BE_553").Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Check(p.HandZone.Count == before && p.GraveyardZone.Any(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]),
            "full hand burns composed result and Overdrive without replacing held cards");
        var combo = KnownMetaProofFixture.BlackEmpireGame(CardClass.ROGUE); var rogue = combo.Player1;
        Play(combo, rogue, "BE_702");
        Check(rogue.Choice == null && !rogue.HandZone.Any(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]),
            "inactive Combo generates nothing");
    }

    private static void Unpinned_ComboRepetition()
    {
        foreach (var (support, coin, combos, battlecries) in new[] {
            ("shark", true, 2, 2), ("shark", false, 0, 2),
            ("none", true, 1, 1), ("brann", true, 1, 2),
            ("opposing-shark", true, 1, 1), ("silenced-shark", true, 1, 1),
            ("shark-and-brann", true, 2, 2)
        })
        foreach (string id in new[] { "CORE_EX1_131", "BE_702", "BE_044" })
        {
            var game = KnownMetaProofFixture.BlackEmpireGame(CardClass.ROGUE); var p = game.Player1;
            if (support.Contains("shark"))
            {
                var shark = Summon(support == "opposing-shark" ? p.Opponent : p, "TRL_092");
                if (support == "silenced-shark") { shark.Silence(); game.DeathProcessingAndAuraUpdate(); }
            }
            if (support.Contains("brann")) Summon(p, "LOE_077");
            if (coin) Play(game, p, "GAME_005");
            int plays = p.PlayHistory.Count, minions = p.NumMinionsPlayedThisTurn;
            var source = Play(game, p, id);
            int answers = 0, expected = id == "BE_044" ? battlecries : combos;
            while (p.Choice != null)
            {
                Check(answers++ < 4, "Combo/Battlecry repetition must terminate");
                var clone = game.CloneExact(history: true);
                var bytes = game.SaveSnapshot(); var loaded = Game.LoadSnapshotWithPools(bytes, game.OfflinePools);
                Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "repeated decision snapshot bytes stable");
                foreach (var next in new[] { game, clone, loaded })
                    PlayerDecisionProof.Answer(next, next.Player1.Choice.Choices[0]);
                Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded)
                    && Canonical.History(game) == Canonical.History(loaded)
                    && game.Random.CaptureState().Equals(clone.Random.CaptureState())
                    && game.Random.CaptureState().Equals(loaded.Random.CaptureState()),
                    "repetition resumes identically at each donor/half boundary");
            }
            int results = id == "CORE_EX1_131" ? p.BoardZone.Count(m => m.Card.Id == "EX1_131t")
                : p.HandZone.Count(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]);
            Check(results == expected && answers == (id == "CORE_EX1_131" ? 0 : expected * 2),
                $"Unpinned_ComboRepetition {id}/{support}/coin={coin}: expected {expected}, got {results}, answers={answers}");
            Check(p.PlayHistory.Count == plays + 1 && p.NumMinionsPlayedThisTurn == minions + 1
                && p.UsedMana == source.Cost && !game.Resolution.HasSuspendedEffect,
                "repeated effect pays, records and plays source once");
        }
        var decisions = Cards.All.Where(c => c.Power?.PowerTask is SabberStoneCore.Tasks.SimpleTasks.DecisionBattlecryTask).ToArray();
        Console.WriteLine($"decision repetition audit: recipes={decisions.Length}; Combo={string.Join(",", decisions.Where(c => c.Combo).Select(c => c.Id))}");
    }
}
