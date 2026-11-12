using System;
using System.Linq;

internal static class TierRewardPlanTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static LocalTierGrant Grant(LocalTierGrantKind kind, string reference, int count = 1) =>
        new LocalTierGrant { Kind = kind, Ref = reference, Count = count };
    private static LocalTierReward Reward(string id, string opponent, params LocalTierGrant[] grants) =>
        new LocalTierReward { MilestoneId = id, OpponentName = opponent, Grants = grants.ToList() };

    internal static void Run()
    {
        string catchUpText = LocalRewardPresentation.TierRewardText(new[] {
            Reward("floor:bronze-5", null, Grant(LocalTierGrantKind.Pack, "STANDARD", 2)) });
        Check(catchUpText.Contains("Bronze 5 reached") && !catchUpText.Contains("You beat"), "catch-up fallback names no opponent");
        // A world NPC the world could not name (failed boot) is never shown by its raw id.
        var unnamed = Reward("floor:silver-10", null, Grant(LocalTierGrantKind.Pack, "STANDARD", 1));
        unnamed.OpponentNpcId = "npc.n.marrow-mind";
        string unnamedText = LocalRewardPresentation.TierRewardText(new[] { unnamed });
        Check(unnamedText.Contains("You beat your opponent to reach Silver") && !unnamedText.Contains("npc.n."),
            "unnamed opponent falls back to a neutral label: " + unnamedText);
        var reward = Reward("floor:gold-10", "Firebat",
            Grant(LocalTierGrantKind.Pack, "STANDARD", 3),
            Grant(LocalTierGrantKind.Token, "LEGENDARY", 2),
            Grant(LocalTierGrantKind.DeckKit, "kit:ladder-gold"),
            Grant(LocalTierGrantKind.CardBack, "910002"),
            Grant(LocalTierGrantKind.Trophy, "trophy:legend"),
            Grant(LocalTierGrantKind.LegendDeck, "Snapshot"),
            Grant(LocalTierGrantKind.Recipe, "recipe-1"),
            Grant(LocalTierGrantKind.Challengers, "npc-1"),
            Grant(LocalTierGrantKind.Challengers, "npc-2"));
        var plans = LocalTierRewardPlan.Build(new[] { reward }, id => id == "recipe-1" ? "Fire deck" : null,
            id => false, id => id == "npc-1" ? "Jaina" : null);
        Check(plans.Count == 1, "one plan per milestone");
        var plan = plans[0];
        Check(plan.Header == "You beat Firebat to reach Gold", "header uses saved opponent and milestone");
        Check(plan.Tiles.Count == 2, "caption grants must not invent native reward tiles");
        Check(plan.Tiles[0].Kind == LocalTierRewardTileKind.Pack && plan.Tiles[0].Id == 713 && plan.Tiles[0].Count == 3,
            "pack maps to Standard booster 713 with saved count");
        Check(plan.Tiles[1].Kind == LocalTierRewardTileKind.RandomCard && plan.Tiles[1].Rarity == "LEGENDARY"
            && plan.Tiles[1].Count == 2, "token maps to random-card display with rarity and count");
        Check(plan.Captions.SequenceEqual(new[] {
            "Craft any Legendary (x2)", "Deck kit: claim a ready-made deck in the Shop", "New card back: Gold",
            "Trophy: Legend", "Legend deck saved", "Recipe: Fire deck", "New challengers: Jaina, npc-2"
        }), "every non-native grant and token explanation stays visible in receipt order");
        Check(reward.Grants.Count == 9 && reward.Grants[3].Ref == "910002", "planning cannot mutate saved grants");

        var realBack = LocalTierRewardPlan.Build(new[] { Reward("floor:silver-10", null,
            Grant(LocalTierGrantKind.CardBack, "42")) }, null, id => id == 42)[0];
        Check(realBack.Header == "Silver reached", "catch-up header must not invent an opponent");
        Check(realBack.Tiles.Count == 1 && realBack.Tiles[0].Kind == LocalTierRewardTileKind.CardBack
            && realBack.Tiles[0].Id == 42 && realBack.Captions.Count == 0, "only DBF-backed card backs get native tiles");

        var ordered = LocalTierRewardPlan.Build(new[] {
            Reward("floor:diamond-5", "Ragnaros", Grant(LocalTierGrantKind.Token, "EPIC")),
            Reward("legend:1", "Champion", Grant(LocalTierGrantKind.Trophy, "trophy:legend-1"))
        }, null, id => false);
        Check(ordered.Select(p => p.MilestoneId).SequenceEqual(new[] { "floor:diamond-5", "legend:1" }), "milestones retain saved order");
        Check(ordered[0].Header == "You beat Ragnaros to reach Diamond 5\nFrom here, nobody goes easy on you.", "Diamond 5 wall line");
        Check(ordered[0].Captions.Single() == "Craft any Epic", "single token caption");
        Check(ordered[1].Header == "You dethroned Champion to reach Legend #1"
            && ordered[1].Captions.Single() == "Trophy: Legend #1", "Legend #1 names dethroned NPC");
        Check(LocalTierRewardPlan.Build(Array.Empty<LocalTierReward>(), null, id => false).Count == 0, "empty rewards skip presentation");
        foreach (var back in LocalShop.LadderExclusiveCardBacks)
        {
            var placeholder = LocalTierRewardPlan.Build(new[] { Reward("legend:top-100", null,
                Grant(LocalTierGrantKind.CardBack, back.Value.ToString())) }, null, id => false)[0];
            Check(placeholder.Tiles.Count == 0 && placeholder.Captions.Single() == "New card back: " + back.Key,
                "reserved " + back.Key + " back must stay a caption");
        }
        var unresolved = LocalTierRewardPlan.Build(new[] { Reward("legend", null,
            Grant(LocalTierGrantKind.Recipe, "missing"), Grant(LocalTierGrantKind.LegendDeck, "Legacy")) }, id => null, id => false)[0];
        Check(unresolved.Captions.SequenceEqual(new[] { "Recipe: missing", "Legend deck: legacy claim" }), "missing names and legacy decks remain honest");
    }
}
