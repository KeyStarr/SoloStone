using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BootlegAlchemistProof
{
    private static OfflinePoolFilter Filter(int cost) => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: cost + 5, maxCost: cost + 5);

    private static void Result(Game g, int selectedId, int cost, int position)
    {
        var result = g.IdEntityDic[selectedId]; var p = result.Controller;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(cost), 126111);
        Core2026Proof.Check(result.Card.Type == CardType.SPELL && result.Card.Cost == cost + 5 &&
            candidates.Any(c => c.Id == result.Card.Id && c.AssetId == result.Card.AssetId) && result.Card.Id != "JAIL_313",
            "transformed identity belongs to exact dynamic any-class spell pool, excluding source");
        // PinnedRule: Tier A, GnomeSayin (2021-12-01), self cost effects override one-shot cost setters.
        // https://us.forums.blizzard.com/en/hearthstone/t/patch-220-card-effect-priority-update/76118
        int visibleCost = Math.Max(0, cost - (result.Card.Id == "JAIL_514" ? p.HandZone.Count : 0));
        Core2026Proof.Check(result.Id == selectedId && result.Zone == p.HandZone && result.ZonePosition == position &&
            p.HandZone[position].Id == selectedId && result.Cost == visibleCost && result[GameTag.COST] == visibleCost,
            "same hand entity/controller/position retains selected current Cost before intrinsic discounts");
        Core2026Proof.Check(result.AppliedEnchantments.Count(e => e.Card.Id == "JAIL_313e") == 1 &&
            result.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_313e")[GameTag.TAG_SCRIPT_DATA_NUM_1] == cost,
            "exact Bootleg cost enchantment applied once");
    }

    // Shatter parents cost 4 and this pool starts at 5, so the catalog cost is forced: the SET Cost path must land
    // before the split on both capacity branches if a parent ever qualifies.
    private static void ShatterParentKeepsCost(Game g, Controller p, string id)
    {
        var installed = g.OfflinePools;
        var xml = OfflinePoolFixtures.FixtureCatalog(new[] { "CATA_489" }); var rows = xml.Root.Element("Members");
        var parentRow = rows.Elements("Card").Single(r => (string)r.Attribute("cardId") == "CATA_489");
        parentRow.SetAttributeValue("cost", 5);
        rows.ReplaceNodes(parentRow);
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
        foreach (int count in new[] { 9, 10 })
        {
            var selected = Add(p, "CS2_182"); selected.Cost = 0;
            Play(g, p, id);
            PoolPilotProof.FillHand(p, count);
            PlayerDecisionProof.Answer(g, selected.Id);
            var left = p.HandZone.Single(c => c.Card.Id == "CATA_489t");
            Core2026Proof.Check(left.Cost == 0 && p.HandZone[0].Id == left.Id, "left half inherits the kept SET Cost");
            if (count == 9) Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Single(c => c.Card.Id == "CATA_489t2").Cost == 0, "two spaces: both halves keep Cost");
            else Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Any(c => c.Card.Id == "CATA_489t2") && !p.HandZone.Any(c => c.Card.Id == "CATA_489t2"), "one space: right half burns");
            PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        }
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, installed);
    }

    internal static void Run() => StandardSetsProof.Case("JAIL_313", (g, p, id) => {
        // Tier A: pinned 37.0.0.254477 client text; pool policy §4 permits dynamic shortages.
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single();
        Core2026Proof.Check(declaration.Filter == OfflinePoolFilter.Random(type: CardType.SPELL) &&
            declaration.CostBinding == SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber &&
            declaration.OrdinaryCost == 6, "declared any-class dynamic spell pool, ordinary one-cost hand card");
        var enchantment = Cards.FromId("JAIL_313e");
        Core2026Proof.Check(enchantment.AssetId == 131362 && enchantment.Implemented &&
            enchantment.Type == CardType.ENCHANTMENT && enchantment.Text == "Transformed and kept Cost.", "exact pinned Bootleg enchantment");
        var source = Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "empty hand completes without choice or generation");
        ClearBoard(g, p);
        var opponent = Add(p.Opponent, "CS2_029");
        foreach (string targetId in new[] { "CS2_182", "CS2_029", "CS2_106", "JAIL_877" })
        {
            var left = Add(p, "CS2_171"); var selected = Add(p, targetId); var right = Add(p, "CS2_171");
            selected.Cost = 1;
            source = Play(g, p, id);
            Core2026Proof.Check(p.Choice.ChoiceType == ChoiceType.TARGET && p.Choice.SourceId == source.Id &&
                p.Choice.Choices.SequenceEqual(new[] { left.Id, selected.Id, right.Id }) &&
                p.Options().Count == 3 && p.Options().All(t => t is ChooseTask), "offers exact friendly hand and bot choices");
            long draws = g.Random.DrawCount;
            var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
            foreach (var branch in new[] { g, clone, restored })
            {
                PlayerDecisionProof.Answer(branch, selected.Id);
                Result(branch, selected.Id, 1, 1);
                Core2026Proof.Check(branch.Random.DrawCount == draws + 1 && branch.Player1.Choice == null &&
                    !branch.Resolution.HasSuspendedEffect, "one RNG pick, completed continuation");
            }
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored),
                "pending transformation clone/snapshot future deterministic");
            Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { left.Id, selected.Id, right.Id }) &&
                left.Card.Id == "CS2_171" && right.Card.Id == "CS2_171" && opponent.Card.Id == "CS2_029",
                "unselected cards and opponent unchanged");
            PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        }
        // Different current costs, including natural two-member and unavailable pools.
        foreach (int cost in new[] { 0, 2, 3, 5, 99 })
        {
            var selected = Add(p, "CS2_182"); selected.Cost = cost;
            long draws = g.Random.DrawCount;
            Play(g, p, id);
            PlayerDecisionProof.Answer(g, selected.Id);
            if (cost == 99)
                Core2026Proof.Check(selected.Card.Id == "CS2_182" && selected.Cost == cost && g.Random.DrawCount == draws,
                    "empty dynamic pool leaves chosen card unchanged without RNG");
            else Result(g, selected.Id, cost, 0);
            PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        }
        foreach (int count in new[] { 0, 1 }) VioletHoldPoolProof.WithPool(g, id, count, () => {
            var selected = Add(p, "CS2_182"); selected.Cost = 0;
            long before = g.Random.DrawCount;
            Play(g, p, id); PlayerDecisionProof.Answer(g, selected.Id);
            if (count == 0) Core2026Proof.Check(selected.Card.Id == "CS2_182", "empty manifest preserves hand card");
            else Result(g, selected.Id, 0, 0);
            Core2026Proof.Check(g.Random.DrawCount == before + count, "empty/singleton exact RNG consumption");
            PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        }, 5);
        ShatterParentKeepsCost(g, p, id);
        // One remaining slot and a physical full board after playing the Battlecry.
        LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        PoolPilotProof.FillHand(p, 9);
        var chosen = p.HandZone[4]; chosen.Cost = 0;
        source = Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.IsFull && p.Choice.Choices.Count == 9, "mixed full board still permits hand decision");
        Add(p, "CS2_171");
        PlayerDecisionProof.Answer(g, chosen.Id);
        Result(g, chosen.Id, 0, 4);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.BoardZone.SlotCount == 7, "transformation neither adds nor burns a hand card nor summons");
        PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        p.BoardZone.Remove(p.BoardZone.GetLocations().Single());

        var first = Add(p, "CS2_182"); first.Cost = 0;
        var second = Add(p, "CS2_182"); second.Cost = 1;
        Summon(p, "LOE_077"); source = Play(g, p, id);
        PlayerDecisionProof.Answer(g, first.Id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { first.Id, second.Id }), "Brann rebuilds current hand including transformed spell");
        PlayerDecisionProof.Answer(g, second.Id);
        Result(g, first.Id, 0, 0); Result(g, second.Id, 1, 1);
        Core2026Proof.Check(p.Choice == null, "separate Brann choices finish once");
        PoolPilotProof.EmptyHand(p); ClearBoard(g, p);

        chosen = Add(p, "CS2_182"); chosen.Cost = 0;
        source = Play(g, p, id); Kill(g, (Minion)source);
        PlayerDecisionProof.Answer(g, chosen.Id); Result(g, chosen.Id, 0, 0);
        Core2026Proof.Check(source.Zone == p.GraveyardZone, "committed transformation survives source leaving play");
    });
}
