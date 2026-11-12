using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VioletPunisherProof
{
    internal static void Run() => Unpinned_VioletPunisher();
    private static void Unpinned_VioletPunisher() => StandardSetsProof.Case("JAIL_101", (g, p, id) => {
        var victim = Summon(p.Opponent, "EDR_272");
        Generic.AddEnchantmentBlock(g, Cards.FromId("UNG_952e"), victim, victim);
        int attack = victim.AttackDamage, health = victim.BaseHealth;
        var m = (Minion)Play(g, p, id, victim);
        Core2026Proof.Check(m.AttackDamage == 7 && m.BaseHealth == 6 && BonusEffectsProof.Count(m) == 3, "three distinct active effects give +3/+3");
        Core2026Proof.Check(BonusEffectsProof.Count(victim) == 0 && victim.AttackDamage == attack && victim.BaseHealth == health && victim.HasDeathrattle, "theft preserves combined stats and Deathrattle");
        var empty = Summon(p.Opponent, "CS2_182");
        var plain = (Minion)Play(g, p, id, empty);
        Core2026Proof.Check(plain.AttackDamage == 4 && plain.BaseHealth == 3 && BonusEffectsProof.Count(plain) == 0, "empty victim grants nothing");
        var source = Add(p, id);
        Core2026Proof.Check(p.Options().Where(x => x.Source?.Id == source.Id && x.PlayerTaskType == PlayerTaskType.PLAY_CARD).All(x => x.Target is Minion target && target.Controller == p.Opponent), "only enemy minion targets");
        m.Silence(); Core2026Proof.Check(m.AttackDamage == 4 && m.BaseHealth == 3 && BonusEffectsProof.Count(m) == 0, "Silence clears stolen grants");
    });
}
