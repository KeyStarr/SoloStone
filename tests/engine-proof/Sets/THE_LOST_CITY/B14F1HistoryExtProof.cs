using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1LostCityProof
{
    internal static void Run()=>StandardSetsProof.Case("DINO_137",SkittishSaucier);
    private static void SkittishSaucier(Game g, Controller p, string id)
    {
        var left=Add(p,"CS2_029");var saucier=Add(p,id);var right=Add(p,"CS2_023");var far=Add(p,"CS2_024");
        PlayEntity(g,p,saucier);
        Core2026Proof.Check(left.Cost==3 && right.Cost==2 && far.Cost==2,"pre-removal neighbors alone reduced");
    }
}
