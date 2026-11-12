using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_MigratingElekkProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_303", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var roster = CompanionRules.Roster(p);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3 && source.HasTaunt
            && roster.All(x => Cards.FromId(x).Cost == 4), "2/3 Taunt upgrades companions");
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(CompanionRules.Roster(p).SequenceEqual(roster), "source Silence and departure do not erase roster");
        Summon(p, id);
        Core2026Proof.Check(CompanionRules.Roster(p).SequenceEqual(roster), "summoned Elekk has no Battlecry");
    });
}
