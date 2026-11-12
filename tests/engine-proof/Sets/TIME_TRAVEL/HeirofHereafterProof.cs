using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HeirofHereafterProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_871", (g, p, id) => {
        var none = (Minion)Play(g, p, id); Core2026Proof.Check(none.AttackDamage == 2 && none.Health == 6 && none.HasTaunt, "no damaged minions means no buff");
        var friendly = Summon(p, "CS2_182"); friendly.Damage = 1;
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 2;
        Summon(p.Opponent, "CS2_182"); p.Hero.Damage = 1; p.Opponent.Hero.Damage = 1;
        var both = (Minion)Play(g, p, id);
        Core2026Proof.Check(both.AttackDamage == 6 && both.Health == 10 && both.AppliedEnchantments.Count(e => e.Card.Id == "TIME_871e") == 2, "counts damaged minions on both boards; excludes healthy minions and damaged heroes");
        friendly.Damage = 0; var one = (Minion)Play(g, p, id);
        Core2026Proof.Check(one.AttackDamage == 4 && one.Health == 8, "healed minion no longer counts");
        Play(g, p, "BE_228", both); Core2026Proof.Check(both.AttackDamage == 2 && both.Health == 6, "silence removes all gains");
    });
}
