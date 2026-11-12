using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class IKnowAGuyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WON_350", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_01")); CostSliceProof.Definition("CFM_940e");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c[GameTag.TAUNT] > 0 && c.Id != id), "Taunt minion filter");
        Play(g, p, id); var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
            && offers.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id)), "three distinct admitted offers");
        int chosen = offers[1]; var definition = g.IdEntityDic[chosen].Card;
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        var minion = (Minion)p.HandZone.Single();
        Core2026Proof.Check(minion.Id == chosen && minion.AttackDamage == definition.ATK + 1 && minion.Health == definition.Health + 2
            && minion.AppliedEnchantments.Single().Card.Id == "CFM_940e", "exact chosen hand entity gains +1/+2");
        End(g); End(g); Core2026Proof.Check(minion.AttackDamage == definition.ATK + 1, "buff persists across turns");
        Generic.RemoveFromZone(p, minion); Generic.SummonBlock(g, minion, -1, null); minion.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(minion.AttackDamage == definition.ATK && minion.Health == definition.Health && !minion.HasTaunt, "Silence removes hand buff and Taunt");
        EmptyHand(p); FillHand(p, 9); Play(g, p, id); chosen = p.Choice.Choices[0]; FillHand(p);
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[chosen].Zone != p.HandZone && p.Choice == null, "full-hand selection cannot overflow");
        EmptyHand(p);
        Pool(g, id, 0, () => { long draws = g.Random.DrawCount; Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && draws == g.Random.DrawCount, "empty Taunt pool no buff or RNG"); });
    });
}
