using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_TamePetProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_300", (g, p, id) => {
        int deck = p.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1
            && CompanionRules.Roster(p).All(x => Cards.FromId(x).Cost == 4), "upgrades to four and draws once");
        Play(g, p, id);
        Core2026Proof.Check(CompanionRules.Roster(p).All(x => Cards.FromId(x).Cost == 5), "successive Pet reaches five");
        End(g); End(g);
        Core2026Proof.Check(CompanionRules.Roster(p).All(x => Cards.FromId(x).Cost == 5), "turn expiry retains player roster");
    });
}
