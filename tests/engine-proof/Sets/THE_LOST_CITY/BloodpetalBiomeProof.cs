using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BloodpetalBiomeProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_449", (g, p, id) => {
        p.Hero.Card = Cards.HeroCard(CardClass.WARLOCK);
        var biome = LocationProofs.Play(p, id);
        Core2026Proof.Check(biome.Card.Type == CardType.LOCATION && biome.Card.Cost == 1 && biome.Durability == 2, "1-Cost two-charge location");
        Core2026Proof.Check(!LocationRules.Activate(p.Opponent, biome), "opponent cannot activate owner's location");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6, "location occupies seventh physical slot");
        CostRules.AddNextTemporaryRule(p, 2);
        PoolPilotProof.Resume(g, next => LocationProofs.Use((BoardObject)next.IdEntityDic[biome.Id]));
        var filter = LostCityPoolProof.Filter(id);
        Core2026Proof.Check(filter.ClassMode == PoolClassMode.ControllerAndNeutral && filter.Type == CardType.MINION
            && filter.MinCost == 1 && filter.MaxCost == 1, "Unpinned_TemporaryPools B1 printed class-plus-Neutral 1-Cost minions");
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, filter, biome.Card.AssetId).Count >= 3,
            "B1 at least three admitted Warlock/Neutral candidates");
        var offers = LostCityPoolProof.Offers(g, p, biome, c => c.Type == CardType.MINION && c.Cost == 1);
        Core2026Proof.Check(biome.Durability == 2 && !LocationRules.CanActivate(p, biome, null), "suspended activation retains charge and blocks reuse until choice completes");
        foreach (int pick in offers)
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            PlayerDecisionProof.Answer(branch, pick);
            var selected = branch.IdEntityDic[pick];
            Core2026Proof.Check(((BoardObject)branch.IdEntityDic[biome.Id]).Durability == 1, "choice completion spends one charge");
            Core2026Proof.Check(branch.PowerHistory.Last.OfType<PowerHistoryShowEntity>().Last(e => e.Entity.Id == pick)
                .Entity.Tags[TemporaryRules.Temporary] == 1, "hand-entry SHOW already carries Temporary mark");
            Core2026Proof.Check(selected.Zone == owner.HandZone && selected.Cost == 0 && TemporaryRules.IsTemporary(selected)
                && selected.AppliedEnchantments.Single().Card.AssetId == 115623, "Unpinned_TemporaryProjection marked before grant; discounted COST visible in hand");
            Core2026Proof.Check(branch.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == pick && t.Tag == GameTag.COST && t.Value == 0),
                "Unpinned_SpelunkerNext post-admission COST published for pre-marked chosen card");
            Batch9PoolProof.Generated(branch, branch.IdEntityDic[biome.Id], new[] { selected }, 1);
            TemporaryRulesProof.End(branch);
            Core2026Proof.Check(selected.Zone == owner.GraveyardZone && owner.DiscardedEntities.Count(i => i == pick) == 1 && !TemporaryRules.IsTemporary(selected),
                "Unpinned_TemporaryExpiry Unpinned_TemporaryResidence owner end really discards chosen card once");
        }
        var full = g.CloneExact(history: true); PoolPilotProof.FillHand(full.Player1);
        int discarded = full.Player1.DiscardedEntities.Count; int chosen = offers[0];
        PlayerDecisionProof.Answer(full, chosen);
        Core2026Proof.Check(full.IdEntityDic[chosen].Zone == full.Player1.GraveyardZone && !TemporaryRules.IsTemporary(full.IdEntityDic[chosen])
            && full.Player1.DiscardedEntities.Count == discarded, "full-hand chosen card burns without discard event or stale Temporary helper");
        PoolPilotProof.Resume(g, next => PlayerDecisionProof.Answer(next, chosen));
        TemporaryRulesProof.End(g); TemporaryRulesProof.End(g);
        Core2026Proof.Check(!LocationRules.CanActivate(p, biome, null), "one owner start clears cooldown without reopening");
        TemporaryRulesProof.End(g); TemporaryRulesProof.End(g);
        PoolPilotProof.Resume(g, next => LocationProofs.Use((BoardObject)next.IdEntityDic[biome.Id]));
        Core2026Proof.Check(biome.Zone == p.BoardZone && biome.Durability == 1 && p.Choice != null, "final non-summoning activation remains until choice resolves");
        chosen = p.Choice.Choices[0];
        PoolPilotProof.Resume(g, next => PlayerDecisionProof.Answer(next, chosen));
        var last = g.IdEntityDic[chosen];
        Core2026Proof.Check(last.Zone == p.HandZone && TemporaryRules.IsTemporary(last) && biome.Zone == p.GraveyardZone, "final choice grants card then retires generator");
        TemporaryRulesProof.End(g); TemporaryRulesProof.End(g); TemporaryRulesProof.End(g);
        Core2026Proof.Check(last.Zone == p.GraveyardZone && p.DiscardedEntities.Count(i => i == chosen) == 1,
            "Unpinned_TemporaryEndOrder expiry never repeats after generator retirement");
    });
}
