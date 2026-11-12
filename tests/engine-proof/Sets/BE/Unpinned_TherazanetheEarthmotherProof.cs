using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_TherazanetheEarthmotherProof
{
    internal static void Run() => StandardSetsProof.Case("BE_337", (g,p,id) => {

        p.Opponent.Hero[GameTag.HEALTH]=1000;
        var source=(Minion)Play(g,p,id); source.Silence(); Kill(g,source);
        Play(g,p,"GAME_005");
        Play(g,p,"CORE_CS2_029",p.Opponent.Hero); Play(g,p,"CORE_CS2_029",p.Opponent.Hero);
        Play(g,p,"CORE_CS2_024",p.Opponent.Hero); Play(g,p,"CORE_CS2_024",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==27,"Unpinned_TherazaneSchools: one charge per school; source-independent; schoolless preserves charges");
    });
}
