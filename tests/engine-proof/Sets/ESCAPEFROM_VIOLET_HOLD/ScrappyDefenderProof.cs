using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScrappyDefenderProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_311", (g, p, id) => {
        while (p.DeckZone.Count > 25) { var card = p.DeckZone[0]; p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 7, "25 cards gives +5 Attack and Taunt");
        var clone = g.Clone(); Generic.Draw(clone.Player1); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 2 && source.AttackDamage == 7, "clone threshold independent");
        Generic.Draw(p); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(source.AttackDamage == 2, "24 cards removes Attack");
        var refill = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(refill); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 7, "deck refill restores Attack");
        End(g); Generic.Draw(p.Opponent); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 7, "opponent deck changes do not count; effect persists past turn"); End(g);
        Core2026Proof.Check(source.AttackDamage == 2, "natural friendly draw crosses threshold");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182"))); g.DeathProcessingAndAuraUpdate();
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(source.AttackDamage == 2 && !source.HasTaunt, "silence removes adaptive Attack and Taunt");
        Kill(g, source);
        var fresh = (Minion)Play(g, p, id);
        Core2026Proof.Check(!fresh.IsSilenced && fresh.AttackDamage == 7 && g.Auras.Any(a => a.Owner.Id == fresh.Id), "fresh Defender aura active before removal");
        Kill(g, fresh); Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        var held = Add(p, id);
        Core2026Proof.Check(((Minion)held).AttackDamage == 2 && !g.Auras.Any(a => a.Owner.Id == fresh.Id)
            && fresh.OngoingEffect == null, "unsilenced source removal detaches aura; hand keeps printed Attack");
    });
}
