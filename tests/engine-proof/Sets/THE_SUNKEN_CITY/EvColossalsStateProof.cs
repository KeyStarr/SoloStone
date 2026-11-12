using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static partial class EvColossalsProof
{
    private static void ExportedTokens()
    {
        // The engine's own cards stand in for the export's rows when this machine has none.
        var export=ClientExport.TryCards();int count=0;
        var ids=export!=null ? export.Keys.Where(Wanted) : Cards.All.Select(c=>c.Id).Where(Wanted);
        bool Wanted(string id)=>Ids.Any(body=>id==body || id.StartsWith(body+"t",StringComparison.Ordinal) || id.StartsWith(body+"e",StringComparison.Ordinal));
        foreach(string id in ids) {
            var card=Cards.FromId(id);count++;
            Core2026Proof.Check(card!=null && card.Implemented && card.ExcludeFromLegacyRandomPools,"registered full body/appendage definition "+id);
            if(export==null)continue;
            var row=export[id];
            Core2026Proof.Check(card.AssetId==row.GetProperty("dbfId").GetInt32() && card.Cost==row.GetProperty("cost").GetInt32()
                && card.ATK==row.GetProperty("atk").GetInt32() && card.Health==row.GetProperty("health").GetInt32()
                && card.Text==row.GetProperty("text").GetString(),"pinned full body/appendage definition "+id);
            foreach(var tag in row.GetProperty("tags").EnumerateObject())Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name))==tag.Value.GetInt32(),"pinned token tag "+id+":"+tag.Name);
        }
        if(export==null)ClientExport.Skip("Colossal bodies and appendages against the client export");
        Core2026Proof.Check(count==50,"thirteen bodies, 28 minion appendages, weapon and eight enchantments");
    }
    private static Game Fresh(int seed=41, OfflinePoolManifest pools=null)
    {
        var g=new Game(new GameConfig { RandomSeed=seed,StartPlayer=1,Shuffle=false,SkipMulligan=true,History=true,Logging=false,ReplayPools=pools,
            Player1HeroClass=CardClass.WARRIOR,Player2HeroClass=CardClass.ROGUE,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        g.StartGame();EmptyHand(g.Player1);EmptyHand(g.Player2);return g;
    }
    private static void Unpinned_Lord()
    {
        // Test candidate membership independently of coordinator-owned installed admission.
        var catalog=OfflinePoolFixtures.Catalog();var rows=catalog.Root.Element("Members");
        foreach(string id in Ids) {
            rows.Elements("Card").Where(c=>(string)c.Attribute("cardId")==id).Remove();
            rows.Add(ClientExport.PoolMember(id,"EvColossalsProof.Run",spellSchool:"NONE"));
        }
        var pools=OfflinePoolFixtures.Manifest(catalog);
        var seen=new HashSet<string>();
        for(int seed=1;seed<=128 && seen.Count<Ids.Length;seed++) {
            var g=Fresh(seed,pools);var p=g.Player1;
            var candidates=OfflineCardPools.Candidates(g,p.HeroClass,EventCardPools.PastColossal(),126698);
            Core2026Proof.Check(candidates.Count==13,"local Past Colossal admission must contain all thirteen; actual="+candidates.Count);
            var expected=candidates[g.Random.Clone().Next(candidates.Count)];
            if(seed==1) Console.WriteLine("ev-colossals Lord bucket="+candidates.Count+" ordered="+string.Join(",",candidates.Select(c=>c.Id)));
            var lord=Play(g,p,"CATA_EVENT_000");var generated=p.HandZone.Single();
            Core2026Proof.Check(generated.Card.Id==expected.Id && generated.Cost==expected.Cost && generated[GameTag.DISPLAYED_CREATOR]==lord.Id,"Lord seeded ordered pick at normal cost seed="+seed);
            seen.Add(generated.Card.Id);
        }
        Core2026Proof.Check(seen.SetEquals(Ids),"seeded Lord reaches each of thirteen bodies");
        var poolGame=Fresh();
        Console.WriteLine("ev-colossals crew buckets "+string.Join(" ",new[]{(CardClass)1,CardClass.DRUID,CardClass.HUNTER,CardClass.MAGE,CardClass.PALADIN,CardClass.PRIEST,CardClass.ROGUE,CardClass.SHAMAN,CardClass.WARLOCK,CardClass.WARRIOR,(CardClass)14}.Select(cls=>cls+"="+OfflineCardPools.Candidates(poolGame,cls,NellieTheGreatThresher.Pirates(),74894).Count)));
        var full=Fresh(pools:pools);FillHand(full.Player1);var source=Summon(full.Player1,"CATA_EVENT_000");
        var before=full.Random.DrawCount;Resolve(full,source);
        Core2026Proof.Check(full.Player1.HandZone.Count==10 && full.Random.DrawCount==before+1,"Lord full-hand burns generated body but consumes seeded pick");
    }
    private static void ForcedDredgeOptions()
    {
        var game=Fresh();var leviathan=(Minion)Play(game,game.Player1,"TSC_030");End(game);
        new AttackTask(EntityType.TARGET,EntityType.SOURCE).Process(game,game.Player2,game.Player2.Hero,leviathan);
        Core2026Proof.Check(game.Player1.Choice!=null && game.Resolution.HasSuspendedEffect,
            "forced Leviathan attack opens its owner's off-turn Dredge");
        foreach(var next in new[]{game.CloneExact(history:true),Game.LoadSnapshotWithPools(game.SaveSnapshot(),game.OfflinePools),game}) {
            while(next.Player1.Choice!=null) {
                var legal=HsSim.Simulation.Legal(next);
                Core2026Proof.Check(legal.Count>0 && legal.All(t=>t is ChooseTask && t.Controller==next.Player1),
                    "simulator offers only the off-turn Dredge owner's answers");
                Core2026Proof.Check(HsSim.Simulation.Process(next,legal[0]),"off-turn Dredge answer accepted");
            }
            Core2026Proof.Check(next.CurrentPlayer==next.Player2 && !next.Resolution.HasSuspendedEffect,
                "forced Dredge completes without stealing the active turn");
        }
    }
    private static void Privacy(Game g,Controller owner,IEnumerable<int> privateIds)
    {
        var identities=privateIds.Select(id=>g.IdEntityDic[id].Card.Id).ToHashSet();
        foreach(int viewer in new[]{owner.PlayerId,owner.Opponent.PlayerId}) {
            var bot=FairBotState.Create(g,viewer,97,DeckStyle.Value);
            var fair=(Game)typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bot);
            if(viewer!=owner.PlayerId) Core2026Proof.Check(!fair.IdEntityDic.Values.Any(c=>c.Controller.PlayerId==owner.PlayerId
                && (c.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE) && identities.Contains(c.Card.Id)),
                "opponent bot cannot read private crew/offer identity after canonical actor renumbering");
            Core2026Proof.Check(g.SaveSnapshot().Length>0,"fair projection preserves authoritative snapshot");
        }
    }
    private static void Unpinned_StatePrivacyAndRewind()
    {
        ForcedDredgeOptions();
        foreach(bool second in new[]{false,true}) {
            var g=Fresh();if(second)End(g);var p=g.CurrentPlayer;EmptyHand(p);
            var body=(Minion)Play(g,p,"TSC_660");var ship=ColossalRules.AppendagesOf(body).Single();
            while(p.Choice!=null) {
                var d=g.GetPendingDecisions().Single();
                Privacy(g,p,p.Choice.Choices);
                Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{p.Choice.Choices[0]}),out var error),"crew answer: "+error);
            }
            var crew=PayloadRules.Entries(ship,NellieTheGreatThresher.Crew);Core2026Proof.Check(crew.Length==3,"Ship has three private crew");
            Privacy(g,p,crew.Select(c=>c.Id));
            // Copy uses existing saved payload links; each Ship owns an independent crew.
            var copy=(Minion)Generic.Copy(p,p.Hero,ship,Zone.PLAY,preserveBoardMinionState:true);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(PayloadRules.Entries(copy,NellieTheGreatThresher.Crew).Length==3,"board copy retains independent crew");
            Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[copy.Id]));
            Core2026Proof.Check(p.HandZone.Count==3 && p.HandZone.All(c=>c.Cost==1),"copied Ship grants same three discounted Pirates");
            var silenceCopy=(Minion)Generic.Copy(p,p.Hero,ship,Zone.PLAY,preserveBoardMinionState:true);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
            silenceCopy.Silence();g.DeathProcessingAndAuraUpdate();Core2026Proof.Check(PayloadRules.Entries(silenceCopy,NellieTheGreatThresher.Crew).Length==0,"Silence clears private crew");
            var receiver=p.Opponent;EmptyHand(receiver);
            new ControlTask(EntityType.TARGET,true).Process(g,p,p.Hero,ship);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
            Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[ship.Id]));
            Core2026Proof.Check(receiver.HandZone.Count==3 && receiver.HandZone.All(c=>c.Cost==1),"stolen Ship delivers private crew to current controller");
        }
        var conceded=Fresh();var pending=(Minion)Play(conceded,conceded.Player1,"TSC_660");
        Core2026Proof.Check(conceded.GetPendingDecisions().Count>0 && conceded.Process(ConcedeTask.Any(conceded.Player1)) && conceded.State==State.COMPLETE,"Concede during crew choices never stalls");
        var rewindGame=Fresh();var owner=rewindGame.Player1;
        var victim=Summon(owner.Opponent,"CS2_182");var giant=(Minion)Play(rewindGame,owner,"TSC_962");
        var stalk=Summon(owner,"TSC_219t");Generic.AddEnchantmentBlock(rewindGame,Cards.FromId("TSC_219e"),giant,stalk);
        var count=PayloadRules.Entries(giant,Gigafin.Food).Length;
        var nellie=(Minion)Play(rewindGame,owner,"TSC_660");while(owner.Choice!=null)Answer(rewindGame);
        var rewindShip=ColossalRules.AppendagesOf(nellie).Single();
        var leviathan=(Minion)Play(rewindGame,owner,"TSC_030");var target=Summon(owner.Opponent,"CS2_200");Attack(rewindGame,leviathan,target);
        int top=rewindGame.IdEntityDic[owner.Choice.Choices[2]][DiscoverRealRules.BoundEntity];Answer(rewindGame,2);
        var rewind=Add(owner,"GAME_005");rewind[GameTag.REWIND]=1;PlayEntity(rewindGame,owner,rewind);
        Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(rewindGame),"Rewind opens with food and grown Stalk");
        RewindTestHelpers.AnswerRewind(rewindGame);RewindTestHelpers.KeepIfOffered(rewindGame);
        Core2026Proof.Check(PayloadRules.Entries(rewindGame.IdEntityDic[giant.Id],Gigafin.Food).Length==count && rewindGame.IdEntityDic[stalk.Id][GameTag.TAG_SCRIPT_DATA_NUM_1]==3
            && PayloadRules.Entries(rewindGame.IdEntityDic[rewindShip.Id],NellieTheGreatThresher.Crew).Length==3
            && rewindGame.Player1.DeckZone.Last().Id==top,"Rewind preserves food, crew, Stalk growth and Dredged actual top entity");
    }
}
