using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class BarkshieldSentinelProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_470", (g, p, id) => {
        var held = (Minion)Add(p, id); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.Health == 2, "printed Taunt and health");
        UsePower(g, p); Core2026Proof.Check(source.Health == 4 && held.Health == 2, "friendly power buffs board only");
        Core2026Proof.Check(source.AppliedEnchantments.Single().Card.Id == "EDR_470e", "exact health enchantment");
        var clone = g.Clone(); End(clone); End(clone); UsePower(clone, clone.Player1);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).Health == 6 && source.Health == 4, "clone trigger independent");
        End(g); UsePower(g, p.Opponent); Core2026Proof.Check(source.Health == 4, "opponent power ignored; buff persists at turn end");
        End(g); source.Silence(); UsePower(g, p); Core2026Proof.Check(source.Health == 2, "Silence removes buff and trigger");
        Kill(g, source); var fresh = Summon(p, id); Kill(g, fresh);
        End(g); End(g); UsePower(g, p); Core2026Proof.Check(fresh.Health == 2, "unsilenced dead source no longer triggers");
    });
}
