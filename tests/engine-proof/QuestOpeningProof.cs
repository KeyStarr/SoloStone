using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class QuestOpeningProof
{
    private static Game Opening(int first, bool shuffle, params string[] quests)
    {
        // Game reverses an unshuffled deck. Put quests below ordinary draws, in zone enumeration order.
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30 - quests.Length)
            .Concat(quests.Reverse().Select(Cards.FromId)).ToList();
        var g = new Game(new GameConfig { RandomSeed = 37, StartPlayer = first, Shuffle = shuffle,
            SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.PALADIN, Player2HeroClass = CardClass.PALADIN,
            Player1Deck = deck.ToList(), Player2Deck = deck.ToList() });
        g.StartGame();
        return g;
    }

    private static void Mulligan(Game g, bool keep)
    {
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            var choices = p.Choice.Choices.Where(id => keep || !g.IdEntityDic[id].Card.IsQuest).ToList();
            Core2026Proof.Check(g.Process(ChooseTask.Mulligan(p, choices)), "quest mulligan accepted");
        }
        g.MainBegin();
    }

    private static void OpeningAndMulligan(int first, bool keep)
    {
        var g = Opening(first, false, "TLC_426");
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            Core2026Proof.Check(p.HandZone.Count == (p == g.FirstPlayer ? 3 : 5), "quest occupies normal opening slot, coin separate");
            Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "TLC_426") == 1 && !p.DeckZone.Any(c => c.Card.IsQuest),
                "modern quest reserved for both player positions");
            Core2026Proof.Check(p.SecretZone.ModernQuests.Count == 0, "opening quest not installed");
        }
        var bytes = g.SaveSnapshot();
        var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(bytes);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && bytes.SequenceEqual(restored.SaveSnapshot()),
            "opening clone and snapshot equal");
        Mulligan(clone, keep);
        Core2026Proof.Check(g.Player1.Choice != null && g.Player2.Choice != null, "clone mulligan independent");
        Mulligan(restored, keep); Mulligan(g, keep);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
            && Canonical.History(g) == Canonical.History(restored)
            && g.Random.CaptureState().Equals(restored.Random.CaptureState()), "opening resume state/history/RNG equal");
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            Core2026Proof.Check(p.HandZone.Any(c => c.Card.IsQuest) == keep, "quest keep/replace honored without reinsertion");
            Core2026Proof.Check(p.DeckZone.Any(c => c.Card.IsQuest) != keep, "replaced quest returns to deck");
        }
        if (keep)
        {
            foreach (var branch in new[] { g, clone, restored })
            {
                var p = branch.CurrentPlayer;
                PlayEntity(branch, p, p.HandZone.Single(c => c.Card.IsQuest));
                Summon(p, "EX1_506");
                Core2026Proof.Check(p.SecretZone.ModernQuests.Single().QuestProgress == 1, "kept quest activates normally after resume");
            }
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored), "resumed opening quest progress equal");
        }
    }

    private static void Unpinned_MultiQuestOpening()
    {
        foreach (int first in new[] { 1, 2 })
        foreach (var order in new[] { new[] { "TLC_426", "UNG_942" }, new[] { "UNG_942", "TLC_426" } })
        {
            // The second identity has a temporary modern definition; neither quest is played here.
            var g = Opening(first, false, order);
            foreach (var p in new[] { g.Player1, g.Player2 })
                Core2026Proof.Check(p.HandZone.Count(c => c.Card.IsQuest) == 1 && p.HandZone.Any(c => c.Card.Id == order[0])
                    && p.DeckZone.Any(c => c.Card.Id == order[1]), "Unpinned_MultiQuestOpening: only first quest reserved");
        }
    }

    private static void LegacyEquivalence()
    {
        QuestDefinition.TryGet(Cards.FromId("TLC_426").AssetId, out var definition);
        foreach (int first in new[] { 1, 2 })
        foreach (bool shuffle in new[] { false, true })
        {
            var modernRegistry = Opening(first, shuffle);
            Game legacyRegistry;
            QuestDefinition.Unregister(definition.DbfId);
            try { legacyRegistry = Opening(first, shuffle); }
            finally { QuestDefinition.Register(definition); }
            Core2026Proof.Check(Canonical.State(modernRegistry) == Canonical.State(legacyRegistry)
                && Canonical.History(modernRegistry) == Canonical.History(legacyRegistry)
                && modernRegistry.Random.CaptureState().Equals(legacyRegistry.Random.CaptureState()),
                "no-modern-quest deck preserves legacy opening draw/history/RNG");
            foreach (var p in new[] { modernRegistry.Player1, modernRegistry.Player2 })
                Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "CS2_182") == (p == modernRegistry.FirstPlayer ? 3 : 4),
                    "no quest reserves no extra card");
        }
    }

    internal static void Run()
    {
        foreach (int first in new[] { 1, 2 }) foreach (bool keep in new[] { true, false }) OpeningAndMulligan(first, keep);
        QuestLifecycleProof.WithSyntheticQuest(Unpinned_MultiQuestOpening);
        LegacyEquivalence();
        Console.WriteLine("PASS quest opening: both positions, keep/replace, Unpinned_MultiQuestOpening, legacy parity, clone/snapshot");
    }
}
