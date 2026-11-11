using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_NirioftheCraterProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_836", (g,p,id) => {

        p.Opponent.Hero[GameTag.HEALTH]=1000;
        var niri=(Minion)Play(g,p,id);
        var minion=Add(p,"CS2_231"); minion.Cost=1;
        p.UsedMana=0; g.Process(PlayCardTask.Any(p,minion));
        Core2026Proof.Check(((Minion)minion).AttackDamage==2 && ((Minion)minion).Health==2,"Niri doubles one-cost played minion stats");
        var spell=Add(p,"CS2_029"); spell.Cost=1; p.UsedMana=0;
        g.Process(PlayCardTask.Any(p,spell,p.Opponent.Hero));
        Core2026Proof.Check(p.Opponent.Hero.Damage==12,"Unpinned_NiriCost: higher printed cost reduced to one repeats");
        var free=Add(p,"CS2_087"); free.Cost=0; p.UsedMana=0;
        g.Process(PlayCardTask.Any(p,free,minion));
        Core2026Proof.Check(((Minion)minion).AttackDamage==5,"Unpinned_NiriCost: printed one reduced to zero does not repeat");
        var sacrifice=Summon(p,"CS2_231"); Play(g,p,"EDR_531",sacrifice);
        Core2026Proof.Check(p.Hero.Armor==8,"Pinned_NiriSacrifice: second body cannot sacrifice departed target for more Armor");
        niri.Silence(); Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==18,"Niri Silence removes repeat eligibility");
    });
}
