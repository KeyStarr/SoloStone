using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1TimeTravelProof
{
    internal static void Run()=>StandardSetsProof.Case("TIME_600",PreciseShot);
    private static void PreciseShot(Game g, Controller p, string id)
    {
        Add(p,"CS2_231");var shot=Add(p,id);Add(p,"CS2_231");int before=p.Opponent.Hero.Health;
        PlayEntity(g,p,shot,p.Opponent.Hero);Core2026Proof.Check(p.Opponent.Hero.Health==before-5,"odd exact center uses pre-removal hand");
        var even=Add(p,id);before=p.Opponent.Hero.Health;PlayEntity(g,p,even,p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health==before-3,"not exact center uses base three");
        var automatic=(Spell)Entity.FromCard(p,Cards.FromId(id));before=p.Opponent.Hero.Health;
        Generic.CastSpell(p,g,automatic,p.Opponent.Hero,0);
        Core2026Proof.Check(p.Opponent.Hero.Health==before-3,"effect cast has no pre-play hand position");
    }
}
