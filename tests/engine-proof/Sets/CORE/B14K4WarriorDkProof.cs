using System;
using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14K4WarriorDkProof
{
    [GroupProof("b14-k4-warrior-dk-cards", ProofKind.Cards)]
    internal static void Run()
    {
        B14K4BEProof.Run(); B14K4CataclysmProof.Run();
        DeathKnightC1Proof.Case("CORE_CATA_007", Unpinned_Consumption);
        B14K4EmeraldDreamProof.Run(); B14K4VioletHoldProof.Run();
        B14K4TimeTravelProof.Run(); B14K4LostCityProof.Run();
    }
    internal static bool Answer(Game game, int id)
    {
        var decision=game.GetPendingDecisions().Single();
        return game.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{id}),out _);
    }
    // D3 approved 2026-10-22, docs/roadmap/2026-10-22-parity-roadmap.md §5;
    // data/rules-evidence/deathknight/consumption-timing.yaml (C/D, unpinned): both hits, deaths/Deathrattles, draws.
    private static void Unpinned_Consumption(Game g, Controller p, string id)
    {
        int deck=p.DeckZone.Count; Play(g,p,id);
        Core2026Proof.Check(p.DeckZone.Count==deck,"no enemies no draw");
        var a=Summon(p.Opponent,"CS2_182"); var b=Summon(p.Opponent,"CS2_182");
        a.Damage=2; b.Damage=2;
        Play(g,p,id); Core2026Proof.Check(a.Zone==p.Opponent.GraveyardZone && b.Zone==p.Opponent.GraveyardZone && p.DeckZone.Count==deck-2,"two direct deaths draw two");
        ParkBoard(p.Opponent); var shield=Summon(p.Opponent,"EX1_008"); shield.HasDivineShield=true;
        deck=p.DeckZone.Count; Play(g,p,id);
        Core2026Proof.Check(shield.Zone==p.Opponent.BoardZone && !shield.HasDivineShield && p.DeckZone.Count==deck,"prevented hit and survivor do not draw");
        Kill(g,shield); var tick=Summon(p.Opponent,"END_017t");
        // Tick and Tock empties the hand; D3 requires its Deathrattle before Consumption draws.
        tick.BaseHealth=3; deck=p.DeckZone.Count;
        Play(g,p,id); Core2026Proof.Check(p.HandZone.Count==1 && p.DeckZone.Count==deck-1,"Deathrattle hand clear precedes death-dependent draw");
        foreach(bool reverse in new[]{false,true})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed); var owner=branch.Player1;
            var order=reverse ? new[]{"CS2_200","CS2_182","CS2_182"} : new[]{"CS2_182","CS2_182","CS2_200"};
            var victims=order.Select(x=>Summon(owner.Opponent,x)).ToArray();
            foreach(var m in victims) m.BaseHealth=10;
            branch.Random.SetSeed(g.Seed);
            var expected=new EffectContext(branch.CloneExact(),owner.Id,owner.Hero.Id).SampleDistinct(victims.Select(m=>m.Id).ToArray(),2);
            Play(branch,owner,id);
            Core2026Proof.Check(victims.All(m=>m.Damage==(expected.Contains(m.Id)?3:0)),"seeded distinct random hits follow reordered board, including nonfirst targets");
        }
    }
}
