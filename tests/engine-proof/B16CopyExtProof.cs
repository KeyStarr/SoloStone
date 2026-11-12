using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
internal static class B16CopyExtProof
{
    [GroupProof("b16-copy-ext")]
    internal static void Run()
    {
        var g = B16CopyExtRegressionProof.NewGame("EDR_491", "JAIL_434"); var p = g.Player1;
        var source = Add(p.Opponent, "CS2_182");
        var copy = Generic.Copy(p, p.Hero, source, Zone.HAND);
        Core2026Proof.Check(copy[(GameTag)32308] == p.Opponent.PlayerId, "copy records immediate opposing controller");
        var ownCopy = Generic.Copy(p, p.Hero, copy, Zone.HAND);
        Core2026Proof.Check(ownCopy[(GameTag)32308] == p.PlayerId, "copy of own copy replaces ancestry");
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) })
            Core2026Proof.Check(branch.IdEntityDic[copy.Id][(GameTag)32308] == p.Opponent.PlayerId, "lineage clone/snapshot");
        Play(g,p,"AT_098");
        Core2026Proof.Check(PayloadRules.CopiedFromOpponent(p.Hero.HeroPower),"direct Hero Power copy records source controller");
        Play(g,p,"BE_852");
        Kill(g,Summon(p,"EX1_029"));var captured=Play(g,p,"EDR_491");
        var mind=Add(p,"JAIL_432");var unshackle=Add(p,"JAIL_433");
        var qualifier=Generic.Copy(p,p.Hero,Add(p.Opponent,"CS2_231"),Zone.HAND);PlayEntity(g,p,qualifier);
        int witness=p.PlayHistory.Count-1;var beetle=Summon(p,"EDR_818t");
        var rewind = Add(p,"TIME_001");
        Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p,rewind)) && RewindTestHelpers.IsRewindOffered(g),"Rewind offered with private lineage");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            branch.IdEntityDic[copy.Id][PayloadRules.CopySourceController]=p.PlayerId;
            branch.IdEntityDic[mind.Id][PayloadRules.HeldCopyPlayed]=0;
            branch.IdEntityDic[unshackle.Id][PayloadRules.HeldCopyPlayed]=0;
            ((SabberStoneCore.Model.Entities.Minion)branch.IdEntityDic[captured.Id]).Silence();
            branch.Player1.AppliedEnchantments.Single(a=>a.Card.Id=="BE_852e").Remove();
            RewindTestHelpers.AnswerRewind(branch);
            Core2026Proof.Check(PayloadRules.CopiedFromOpponent(branch.IdEntityDic[copy.Id]),"Rewind restores copy provenance");
            Core2026Proof.Check(PayloadRules.HeldOpponentCopy(branch.IdEntityDic[mind.Id]) && branch.IdEntityDic[unshackle.Id].Cost==1,"Rewind restores holding windows");
            Core2026Proof.Check(branch.IdEntityDic[captured.Id].HasDeathrattle && branch.Player1.AppliedEnchantments.Any(a=>a.Card.Id=="BE_852e"),"Rewind restores captured grants and controller effect");
            Core2026Proof.Check(CopyPlayHistory.SourceControllerAt(branch.Player1,witness)==branch.Player2.PlayerId
                && branch.IdEntityDic[beetle.Id].Zone==branch.Player1.BoardZone,"Rewind restores play witness and Beetle membership");
            RewindTestHelpers.KeepIfOffered(branch);
        }
    }
}
