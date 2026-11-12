using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4EscapefromVioletHoldProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CAP_104", Scenario);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g,p,id); p.Opponent.Hero.BaseHealth=100;
        p.Opponent.Hero.TakeDamage(body,2);
        Core2026Proof.Check(p.Opponent.Hero.Damage==3,"own-turn Pirate damage includes self");
        End(g); p.Opponent.Hero.TakeDamage(body,2);
        Core2026Proof.Check(p.Opponent.Hero.Damage==5,"opponent turn has no Pirate bonus");
        End(g); body.Silence(); p.Opponent.Hero.TakeDamage(body,2);
        Core2026Proof.Check(p.Opponent.Hero.Damage==7,"Silence removes Pirate provider");
    }
}
