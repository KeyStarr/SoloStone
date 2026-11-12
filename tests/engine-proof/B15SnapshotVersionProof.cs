using System.Reflection;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B15SnapshotVersionProof
{
    [GroupProof("b15-snapshot-version")]
    internal static void Run()
    {
        B14F1HistoryDefinitionProof.Run();
        var knight = Historical("CORE_ETC_523");
        var game = HistoryFactsProof.Fixture();
        var held = Entity.FromCard(game.Player1, knight);
        Core2026Proof.Check(!knight.HealthCostCondition(held), "preserved batch-14 Health predicate initially false");
        game.Player1.Hero.TakeDamage(game.Player1.Hero, 2);
        game.Player1.Hero.TakeHeal(game.Player1.Hero, 1);
        Core2026Proof.Check(knight.HealthCostCondition(held), "preserved batch-14 predicate remains executable");
        Core2026Proof.Check(Historical("TIME_103").HasBoardHistoryDisplays, "preserved batch-14 board displays survive");

        var peddler = Historical("CORE_WON_096");
        game = HistoryFactsProof.Fixture();
        held = Entity.FromCard(game.Player1, peddler);
        game.Player1.HandZone.Add(held);
        PlayEntity(game, game.Player1, held);
        Core2026Proof.Check(game.Player1.Choice?.Choices.Count == 3 && game.Player1.Choice.Choices.All(id => game.IdEntityDic[id].Card.Cost == 1),
            "preserved pool filter still discovers three one-Cost cards");
        Core2026Proof.Check(Filter(Historical("CORE_WON_096", "pool-filter")) == new OfflinePoolFilter(maxCost: 1),
            "preserved inline pool filter retains old criteria and defaults all five new fields");
        DestroyDeck(Historical("LOOT_415t6"), damage: 0);
        DestroyDeck(RoundTrip(Cards.FromId("LOOT_415t6")), damage: 3);

        foreach (string id in new[] { "DINO_407", "CORE_RLK_567", "CATA_614", "TIME_876e", "RLK_567e2" })
        {
            var original = Cards.FromId(id);
            var restored = RoundTrip(original);
            Core2026Proof.Check((restored.Power.HandEntryTask != null) == (original.Power.HandEntryTask != null)
                && restored.Power.RetainInHeldForm == original.Power.RetainInHeldForm, "held-form definition flags survive: " + id);
        }
        var mirrex = RoundTrip(Cards.FromId("DINO_407"));
        game = HistoryFactsProof.Fixture();
        held = Entity.FromCard(game.Player1, mirrex);
        game.Player1.HandZone.Add(held);
        End(game); Play(game, game.Player2, "CS2_200");
        Core2026Proof.Check(held.Card.Id == "CS2_200", "decoded hand-entry callback installs working recurrence");

        foreach (var filter in new[] {
            new OfflinePoolFilter(exactAttack: 0), new OfflinePoolFilter(attackParity: 0),
            new OfflinePoolFilter(costParity: 1), new OfflinePoolFilter(cardIds: new[] { "CS2_182", "CS2_200" }),
            OfflinePoolFilter.Union(new OfflinePoolFilter(exactAttack: 2), new OfflinePoolFilter(costParity: 0)) })
        {
            var card = Cards.FromId("CS2_182").Clone();
            card.Power = new Power { PowerTask = new DiscoverWithDarkGiftThenContinueTask(filter) };
            var restored = Filter(RoundTrip(card));
            Core2026Proof.Check(restored == filter, "nested pool-filter extension survives, including zero and union branches");
        }
        Console.WriteLine("PASS B15 definition versions: preserved batch-13/14 bytes, executable old recipes, held-form/filter/destroy extensions");
    }

    private static OfflinePoolFilter Filter(Card card) => (OfflinePoolFilter)typeof(DiscoverWithDarkGiftThenContinueTask)
        .GetField("_filter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(card.Power.PowerTask);

    private static Card Historical(string id, string fixture = null)
    {
        using var reader = new StreamReader(typeof(B15SnapshotVersionProof).Assembly
            .GetManifestResourceStream("DefinitionFixtures/batch14-" + (fixture ?? id) + ".b64"));
        string bytes = reader.ReadToEnd();
        var card = SnapshotCodec.DecodeDefinition(bytes);
        Core2026Proof.Check(card.Id == id && !ReferenceEquals(card, Cards.FromId(id)), "preserved detached identity: " + id);
        Core2026Proof.Check(card.Power?.HandEntryTask == null && card.Power?.RetainInHeldForm != true, "old held-form fields default: " + id);
        Core2026Proof.Check(SnapshotCodec.EncodeDefinition(card) == bytes, "preserved batch-14 bytes re-encode identically: " + id);
        return card;
    }

    private static Card RoundTrip(Card card)
    {
        string bytes = SnapshotCodec.EncodeDefinition(card, detached: true);
        Core2026Proof.Check(Convert.FromBase64String(bytes).Take(3).SequenceEqual(new byte[] { 255, 255, 4 }), "new fields require definition layout v4");
        var restored = SnapshotCodec.DecodeDefinition(bytes);
        Core2026Proof.Check(SnapshotCodec.EncodeDefinition(restored) == bytes, "v4 definition round trip");
        return restored;
    }

    private static void DestroyDeck(Card card, int damage)
    {
        var game = B15DrawReplacementProof.NewGame();
        B15DrawReplacementProof.EmptyDeck(game.Player2);
        Entity.FromCard(game.Player2, Cards.FromId("JAIL_398"), zone: game.Player2.DeckZone);
        var target = Summon(game.Player1, "CS2_182");
        var source = Entity.FromCard(game.Player1, card);
        game.Player1.HandZone.Add(source);
        PlayEntity(game, game.Player1, source);
        Core2026Proof.Check(game.Player2.DeckZone.IsEmpty && target.Damage == damage,
            "historical destroy defaults false; v4 destroy preserves offboard Deathrattle behavior");
    }
}
