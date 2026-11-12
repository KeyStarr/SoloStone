using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1HistoryExtProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history extension: "+message);
    [GroupProof("b14-f1-history-ext")]
    internal static void Run()
    {
        B14F1HistoryDefinitionProof.Run();
        foreach(bool history in new[]{false,true})
        {
            var game=HistoryFactsProof.Fixture(history);var player=game.Player1;var query=game.HistoryFacts.Query;
            Generic.DiscardBlock(player,Add(player,"CS2_182"));
            Check(query.Discards().Count()==1,"one successful discard");
            player.GraveyardZone.Add(player.HandZone.Remove(Add(player,"CS2_231")));
            Check(query.Discards().Count()==1,"non-discard removal excluded");
            var card=Add(player,"CS2_231");Add(player,"CS2_182");
            PlayEntity(game,player,card);
            var play=query.LatestPlay(card.Id);
            Check(play.HandPosition==0 && play.HandCount==2 && query.CompletedPlays().Single().PlayId==play.PlayId,
                "pre-payment hand position/count and completed-play link");
            Check(!query.CompletedPlays(query.ThisTurn.ExcludingPlay(play.PlayId)).Any(),"excluded play also excludes its completion");
            var cancelled=Add(player,"GAME_005");
            TriggerManager.TriggerHandler counter=e=>((Spell)e).IsCountered=true;
            game.TriggerManager.CastSpellTrigger+=counter;PlayEntity(game,player,cancelled);game.TriggerManager.CastSpellTrigger-=counter;
            Check(query.CompletedPlays().Any(f=>f.EntityId==cancelled.Id),"countered card still finishes its committed play");
            var source=Summon(player,"CS2_231");source[GameTag.REBORN]=1;Kill(game,source);
            Check(query.Reborns().Count()==1 && query.Reborns().Single().DbfId==Cards.FromId("CS2_231").AssetId,
                "successful Reborn records dead source immediately");
            var returned=player.BoardZone.Last(m=>m[GameTag.HAS_BEEN_REBORN]>0);Play(game,player,"CAP_801",returned);Kill(game,returned);
            Check(query.Reborns().Count()==2,"same lineage contributes a second occurrence");
            var reborn=player.BoardZone.Last(m=>m[GameTag.HAS_BEEN_REBORN]>0);reborn.Silence();Kill(game,reborn);
            Check(query.Reborns().Count()==2,"Silence does not erase prior history or create success");
            var bounced=(Minion)card;Generic.ReturnToHandBlock(player,bounced);
            Check(query.LatestHandEntry(card.Id).FromZone==Zone.PLAY,"battlefield bounce origin survives Reset");
            var minion=Summon(player,"CS2_182");minion.TakeDamage(player.Hero,2);minion.TakeHeal(player.Hero,5);
            Check(HealingRules.MinionHealingThisTurn(player)==2 && HealingRules.RestoredHealthThisTurn(player),"actual capped minion healing");
            minion.TakeHeal(player.Hero,5);
            Check(HealingRules.MinionHealingThisTurn(player)==2,"full-Health attempts excluded");
            var enemy=Summon(player.Opponent,"CS2_182");enemy.TakeDamage(player.Hero,1);enemy.TakeHeal(player.Hero,1);
            Check(HealingRules.MinionHealingThisTurn(player)==3 && HealingRules.MinionHealingThisTurn(player.Opponent)==0,
                "healer gets credit regardless of recipient controller");
            string text=game.HistoryFacts.CanonicalText();
            foreach(var copy in new[]{game.CloneExact(),Game.LoadSnapshot(game.SaveSnapshot())})
            {
                Check(copy.HistoryFacts.CanonicalText()==text && HealingRules.MinionHealingThisTurn(copy.Player1)==3,"clone/snapshot retain facts and tags");
                Generic.DiscardBlock(copy.Player1,Add(copy.Player1,"CS2_231"));
                Check(game.HistoryFacts.CanonicalText()==text,"fork independent tail");
            }
            var old=Summon(player,"RLK_503");Kill(game,old);End(game);
            var fresh=Summon(player,"RLK_503");Kill(game,fresh);End(game);
            Check(query.Deaths(query.SincePreviousTurnEnded(player.Id)).Any(f=>f.EntityId==fresh.Id)
                && !query.Deaths(query.SincePreviousTurnEnded(player.Id)).Any(f=>f.EntityId==old.Id),"deaths since previous own turn ended");
            Check(!HealingRules.RestoredHealthThisTurn(player) && HealingRules.MinionHealingThisTurn(player)==0,"healing expires at global turn boundary");
            var hidden=Add(player.Opponent,"CS2_029");Generic.ShuffleIntoDeck(player.Opponent,player.Opponent.Hero,player.Opponent.HandZone.Remove(hidden));
            var fair=game.CloneExact();fair.ProjectHistoryForFairView(player.PlayerId);
            Check(fair.HistoryFacts.Query.HandEntries().Where(f=>f.ControllerId==player.Opponent.Id).All(f=>f.EntityId==0),"opponent hand links hidden");
            Check(Game.LoadSnapshot(fair.SaveSnapshot()).HistoryFacts.CanonicalText()==fair.HistoryFacts.CanonicalText(),"projected extension facts validate");
        }
        RebornControl();Pools();Rewind();
        Console.WriteLine("PASS B14 F1 history: discard/completion/Reborn/hand origin/healing/death windows/clone/snapshot/privacy");
    }
    private static void RebornControl()
    {
        var game=HistoryFactsProof.Fixture();var player=game.Player1;var source=Summon(player,"CS2_120");source[GameTag.REBORN]=1;
        TriggerManager.TriggerHandler steal=e=>{
            if(e is Minion m && m[GameTag.HAS_BEEN_REBORN]>0)
                new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET)
                    .Process(game,player.Opponent,player.Opponent.Hero,m);
        };
        game.TriggerManager.AfterSummonTrigger+=steal;Kill(game,source);game.TriggerManager.AfterSummonTrigger-=steal;
        var facts=game.HistoryFacts.Query.Reborns().ToArray();
        Check(facts.Length==1 && facts[0].ControllerId==player.Id,"control transfer during summon reaction cannot add a second Reborn occurrence");
    }
    private static void Pools()
    {
        var manifest=OfflinePoolPolicy.Load().BuildManifest();
        var ids=new[]{"BE_997","CORE_RLK_116","EDR_781"};
        var declarations=ids.Select(id=>SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id));
        foreach(var declaration in declarations)
        foreach(var pool in declaration.Pools)
        {
            var counts=pool.Classes.Select(name=>OfflineCardPools.Candidates(manifest,PoolRequirement.HeroClass(name),pool.Filter,declaration.DbfId).Count).ToArray();
            Check(counts.All(n=>n>=3),declaration.CardId+" admitted candidates per playable class");
            Console.WriteLine("B14 F1 pool "+declaration.CardId+": "+string.Join(",",counts));
        }
    }
    private static void Rewind()
    {
        var game=PinnedPoolFixture.Use(PlayerDecisionProof.Fixture(1));var player=game.Player1;
        Generic.DiscardBlock(player,Add(player,"CS2_231"));
        var target=Summon(player,"CS2_182");target.TakeDamage(player.Hero,2);target.TakeHeal(player.Hero,1);
        Play(game,player,"TIME_001");
        foreach(var next in new[]{game.CloneExact(),PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot()),game})
        {
            Generic.DiscardBlock(next.Player1,Add(next.Player1,"CS2_231"));
            ((Minion)next.IdEntityDic[target.Id]).TakeHeal(next.Player1.Hero,1);
            var transaction=next.Rewind;var decision=next.GetPendingDecisions().Single();
            Check(next.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id,decision.ControllerId,
                new[]{transaction.RewindEntityId}),out var error),"Rewind answer: "+error);
            Check(next.HistoryFacts.Query.Discards().Count()==1 && HealingRules.MinionHealingThisTurn(next.Player1)==1,
                "Rewind restores facts/counters then replays once, including cloned/loaded offer");
        }
    }

}
