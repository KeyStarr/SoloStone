using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static partial class B18TMiscProof
{
    private static void Unpinned_RewindState()
    {
        var g=DiscoverRealProof.New();var p=g.Player1;var target=Add(p,"CS2_200");
        var host=(Minion)Play(g,p,"END_018");var osk=Add(p,"TLC_452");string form=osk.Card.Id;
        var definition=Card.CreateAssembledCard(Cards.FromId("CATA_133"),Cards.FromId("CATA_133"),Cards.FromId("CATA_999"));
        var power=BasicHeroPowerRules.InstallPassive(p,host,definition);
        var rewind=Add(p,"TIME_001");PlayEntity(g,p,rewind);
        Assert(RewindTestHelpers.IsRewindOffered(g),"Rewind offered with journal/passive/held form");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            next.IdEntityDic[target.Id].Cost=4;
            TitanographerOsk.Roll(next.IdEntityDic[osk.Id]);
            new ReplaceHeroPower(Cards.FromId(BasicHeroPowerRules.PowerId(next.Player1.HeroClass))).Process(next,next.Player1,next.IdEntityDic[host.Id],null);
            RewindTestHelpers.AnswerRewind(next);
            Assert(next.IdEntityDic[target.Id].Cost==int.MaxValue && next.IdEntityDic[osk.Id].Card.Id==form,"Rewind restores journal and initialized hand identity");
            Assert(next.Player1.Hero.HeroPower.Id==power.Id && next.Player1.Hero.CantBeTargetedBySpells,"Rewind restores passive and keyword aura");
            RewindTestHelpers.KeepIfOffered(next);
            Kill(next,(Minion)next.IdEntityDic[host.Id]);Assert(next.IdEntityDic[target.Id].Cost==6,"restored source link still removes its application");
        }
    }
}
