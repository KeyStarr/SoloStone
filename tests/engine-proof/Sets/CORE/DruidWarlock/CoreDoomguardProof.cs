using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreDoomguardProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_310", (g, p, id) => {
        foreach (var card in new[]{"CS2_182","CS2_029","EX1_008","CS2_200"}) Add(p,card);
        var candidates=p.HandZone.Select(c=>c.Id).ToHashSet(); var opposite=Add(p.Opponent,"CS2_182");
        PoolPilotProof.Resume(g, next => {
            var owner=next.Player1; var draws=next.Random.DrawCount; var source=(Minion)Play(next,owner,id);
            Core2026Proof.Check(next.Random.DrawCount>draws, "four-card discard advances game RNG");
            Core2026Proof.Check(source.HasCharge && source.AttackDamage==5 && source.Health==7, "printed 5/7 Charge");
            Core2026Proof.Check(owner.HandZone.Count==2 && owner.HandZone.All(c=>candidates.Contains(c.Id)) && owner.GraveyardZone.Count(c=>candidates.Contains(c.Id))==2, "discard exactly two random original hand cards");
            Core2026Proof.Check(owner.Opponent.HandZone.Single().Id==opposite.Id, "opponent hand untouched");
            Core2026Proof.Check(owner.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.MINION_ATTACK && t.Source?.Id==source.Id && t.Target?.Id==owner.Opponent.Hero.Id), "Charge allows immediate hero attack");
            Attack(next,source,owner.Opponent.Hero); Core2026Proof.Check(owner.Opponent.Hero.Damage==5,"Charge attack deals five");
        });
        PoolPilotProof.EmptyHand(p); var empty=(Minion)Play(g,p,id); Core2026Proof.Check(p.HandZone.Count==0 && empty.HasCharge,"empty hand still permits battlecry");
        var only=Add(p,"CS2_182"); Play(g,p,id); Core2026Proof.Check(only.Zone.Type==Zone.GRAVEYARD && p.HandZone.Count==0,"one card is discarded once");
        var kept=Add(p,"CS2_029"); var summoned=Summon(p,id); Core2026Proof.Check(p.HandZone.Single().Id==kept.Id,"summoning skips discard battlecry");
        summoned.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(!summoned.HasCharge,"Silence removes Charge");
    }, legacyPoolMember: true);
}
