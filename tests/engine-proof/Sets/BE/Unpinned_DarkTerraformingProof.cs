using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class Unpinned_DarkTerraformingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_EVENT_102", (g,p,id) => {
        foreach (bool friendly in new[] { false, true })
        foreach (int center in new[] { 0, 3, 6 })
        foreach (int bonus in new[] { 0, 2 })
        {
            var next=g.CloneExact(history:true); var owner=next.Player1;
            var side=friendly?owner:owner.Opponent;
            var bodies=Enumerable.Range(0,7).Select(_=>Summon(side,"EX1_561")).ToArray();
            foreach(var body in bodies) body.Health=30;
            owner[GameTag.SPELLPOWER]=bonus;
            var spell=Add(owner,id);
            var tasks=owner.Options().Where(t=>t.Source?.Id==spell.Id).ToArray();
            Core2026Proof.Check(tasks.Length>0 && tasks.All(t=>t.Target is Minion),"Unpinned_Terraform minion-only targets");
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner,spell,bodies[center])),"Terraform cast");
            Core2026Proof.Check(bodies.Select(b=>b.Damage).SequenceEqual(Enumerable.Range(0,7).Select(i=>Math.Max(0,5+bonus-Math.Abs(i-center)))),
                "Unpinned_Terraform both directions, edge, falloff and Spell Damage extends range");
            Core2026Proof.Check(owner.Hero.Damage==0 && owner.Opponent.Hero.Damage==0,"Terraform never hits heroes");
        }
        var board=Enumerable.Range(0,7).Select(_=>Summon(p.Opponent,"EX1_561")).ToArray();
        foreach(var b in board) b.Health=30;
        board[2].HasDivineShield=true; board[4].IsImmune=true;
        board[3].Health=1;
        Play(g,p,id,board[3]);
        Core2026Proof.Check(board[3].Zone==p.Opponent.GraveyardZone && board[2].Damage==0 && !board[2].HasDivineShield
            && board[4].Damage==0 && board[1].Damage==3 && board[5].Damage==3 && board[0].Damage==2 && board[6].Damage==2,
            "Unpinned_Terraform lethal center and prevention never stop or collapse either wave");
        StandardSetsProof.ParkBoard(p.Opponent);
        var left=Summon(p.Opponent,"EX1_561");
        var location=LocationProofs.Location(p.Opponent);
        var target=Summon(p.Opponent,"EX1_561");
        var dormant=Summon(p.Opponent,"TIME_046");
        var right=Summon(p.Opponent,"EX1_561");
        Play(g,p,id,target);
        Core2026Proof.Check(left.Damage==3 && right.Damage==3 && dormant.Damage==0 && location[GameTag.DAMAGE]==0,
            "Unpinned_Terraform continuing path skips locations/Dormant but counts physical positions");
    });
}
