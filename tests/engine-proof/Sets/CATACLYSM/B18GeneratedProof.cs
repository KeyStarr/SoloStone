using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B18GeneratedProof
{
    [GroupProof("b18-t-generated-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_504", (g,p,id) => B18AyaProof.Run());
        StandardSetsProof.Case("CATA_470", (g,p,id) => Unpinned_NefarianCreationDragonCheckTime());
    }

    internal static void Answer(Game game, int selected)
    {
        var d = game.GetPendingDecisions().Single();
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { selected }), out var error), "generated choice: "+error);
    }

    private static void Unpinned_NefarianCreationDragonCheckTime()
    {
        foreach (bool held in new[] { false, true })
        foreach (bool full in new[] { false, true }) {
            var g = ImbueProof.Make(CardClass.DEATHKNIGHT, 17); var p = g.Player1;
            if (held) Add(p,"EX1_043");
            var source = Add(p,"CATA_470"); var oracle = g.CloneExact(history:true);
            var expected = AssembleRules.Take(oracle,VictorNefarius.Candidates(oracle,oracle.Player1,true).ToList(),3);
            PlayEntity(g,p,source);
            Core2026Proof.Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).SequenceEqual(expected.Select(c=>c.Id)),
                "uniform seeded Dragon offers preserve draw order");
            Core2026Proof.Check(p.Choice.Choices.Distinct().Count()==p.Choice.Choices.Count,"distinct Dragon offers");
            // Change hand and source while paused: the per-invocation boolean must stay frozen.
            foreach(var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
            if (!held) Add(p,"EX1_043");
            Kill(g,(Minion)source);
            while (!p.BoardZone.IsFull) Summon(p,"CS2_231");
            foreach (var firstState in new[] {g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=firstState.Player1;
                var dragon=firstState.IdEntityDic[owner.Choice.Choices[0]].Card;
                Answer(firstState,owner.Choice.Choices[0]);
                if (owner.Choice==null) {
                    Core2026Proof.Check(VictorNefarius.Candidates(firstState,owner,false,10-dragon.Cost).Length==0
                        && owner.HandZone.All(c=>c.Card.Id!="CATA_470t1"),"empty remaining bucket yields nothing");
                    continue;
                }
                Core2026Proof.Check(owner.Choice.Choices.Select(id=>firstState.IdEntityDic[id].Card.Id).Distinct().Count()==owner.Choice.Choices.Count
                    && owner.Choice.Choices.All(id=>firstState.IdEntityDic[id].Card.Cost<=10-dragon.Cost),"distinct Undead within remaining budget");
                foreach (var next in new[] {firstState.CloneExact(history:true),Game.LoadSnapshot(firstState.SaveSnapshot())}) {
                    owner=next.Player1;
                    var undead=next.IdEntityDic[owner.Choice.Choices[0]].Card;
                    if(full) while(!owner.HandZone.IsFull) Add(owner,"CS2_231");
                    Answer(next,owner.Choice.Choices[0]);
                    Core2026Proof.Check(owner.Choice==null,"no orphan generated decision");
                    var result=owner.HandZone.SingleOrDefault(c=>c.Card.Id=="CATA_470t1");
                    if(full) { Core2026Proof.Check(result==null && owner.HandZone.Count==10,"full hand burns Creation"); continue; }
                    Core2026Proof.Check(result!=null && result.Card.Cost==dragon.Cost+undead.Cost
                        && result.Cost==Math.Max(0,result.Card.Cost-(held?3:0)),"snapshot-only discount; full board/source departure do not abort");
                    var copy=Generic.Copy(owner,owner.Hero,result,Zone.HAND);
                    Core2026Proof.Check(copy.Cost==result.Cost && copy.Card.ATK==result.Card.ATK,"Creation copy retains definition and discount");
                }
            }
        }
    }
}
