using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SI7SlayerProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_000", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasStealth, "printed Stealth");
        var ordinary = Summon(p, "CS2_182"); Attack(g, ordinary, p.Opponent.Hero);
        Core2026Proof.Check(ordinary.AttackDamage == 4 && ordinary.Health == 5, "non-Stealthed attacker gets no buff");
        var hidden = Summon(p, "EX1_008"); hidden.HasStealth = true;
        var clone = g.Clone(); var cloneHidden = (Minion)clone.IdEntityDic[hidden.Id];
        Attack(clone, cloneHidden, clone.Player2.Hero);
        Core2026Proof.Check(cloneHidden.AttackDamage == 3 && hidden.AttackDamage == 1 && hidden.HasStealth, "clone attack buffs only clone");
        Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(hidden.AttackDamage == 3 && hidden.Health == 3 && !hidden.HasStealth && p.Opponent.Hero.Damage == 7
            && hidden.AppliedEnchantments.Any(e => e.Card.Id == "CAP_000e"), "Stealth checked before removal; buff before damage");
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 4, "source qualifies for own trigger");
        End(g); var enemy = Summon(p.Opponent, "EX1_008"); enemy.HasStealth = true; Attack(g, enemy, p.Hero);
        Core2026Proof.Check(enemy.AttackDamage == 1 && enemy.Health == 1, "enemy Stealth does not trigger"); End(g);
        Core2026Proof.Check(hidden.AttackDamage == 3 && source.AttackDamage == 3, "buff persists past end of turn");
        hidden.Silence(); Core2026Proof.Check(hidden.AttackDamage == 1 && hidden.Health == 1, "buff removed by silence");
        source.Silence(); hidden.HasStealth = true; Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(hidden.AttackDamage == 1, "silenced source has no trigger");
        Kill(g, source);
        var fresh = (Minion)Play(g, p, id);
        Core2026Proof.Check(!fresh.IsSilenced && fresh.ActivatedTrigger != null, "fresh Slayer trigger active before removal");
        Kill(g, fresh); End(g); End(g); hidden.HasStealth = true; Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(hidden.AttackDamage == 1, "removed source has no trigger");
    });
}
