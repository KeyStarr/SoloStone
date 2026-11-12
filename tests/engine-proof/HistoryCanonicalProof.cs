using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class HistoryCanonicalProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history canonical: "+message);
    // Test-only declaration helper. Catalogue/production modules are never registered as consumers.
    internal static Card Consumer(string id="GAME_005") { var card=Cards.FromId(id).Clone();card.ConsumesHistory=true;return card; }
    internal static void Run()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var bytes=g.SaveSnapshot();
        _=g.HistoryFacts.Query.Plays().ToArray();_=g.HistoryFacts.Query.StartedInDeck(p.DeckZone[0].Id,p.Id);
        Check(!g.HistoryFacts.HasConsumers && bytes.SequenceEqual(g.SaveSnapshot()),"queries never enroll or mutate");
        var old=Canonical.State(g);
        var consumer=Entity.FromCard(p,Consumer());
        Check(g.HistoryFacts.HasConsumers && Canonical.State(g).Contains("|history-facts-v1:"),"late-created declared consumer enables canonical append");
        Check(g.HistoryFacts.Query.Events<TurnStartedFact>().Count()==1 && g.HistoryFacts.Query.HandEntries().Count()>=7,
            "late enrollment retains prior facts");
        p.GraveyardZone.Add(consumer);
        Check(g.HistoryFacts.HasConsumers,"consumer removal does not unenroll game");
        var a=HistoryFactsProof.Fixture(false);var b=HistoryFactsProof.Fixture(false);
        Entity.FromCard(a.Player1,Consumer());Entity.FromCard(b.Player1,Consumer());
        var ea=Entity.FromCard(a.Player1,Cards.FromId("CS2_231"));var eb=Entity.FromCard(b.Player1,Cards.FromId("CS2_231"));
        a.Player1.HandZone.Add(ea);a.Player1.SetasideZone.Add(a.Player1.HandZone.Remove(ea));b.Player1.SetasideZone.Add(eb);
        Check(Canonical.State(a).Split("|history-facts-v1:")[0]==Canonical.State(b).Split("|history-facts-v1:")[0],"identical board/tag state fixture");
        Check(Canonical.Sha(Canonical.State(a))!=Canonical.Sha(Canonical.State(b)),"different past changes canonical hash");
        var initial=new Game(new SabberStoneCore.Config.GameConfig { RandomSeed=73,StartPlayer=1,Shuffle=false,SkipMulligan=true,
            Player1Deck=new[]{Consumer("CS2_182")}.Concat(Enumerable.Repeat(Cards.FromId("CS2_182"),29)).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        Check(initial.HistoryFacts.HasConsumers,"consumer enrolled during initial deck construction");
        initial.StartGame();
        Check(initial.HistoryFacts.HasConsumers,"initial deck consumer remains sticky");
        var changed=HistoryFactsProof.Fixture();var original=changed.Player1.DeckZone[0];
        Generic.ChangeEntityBlock(changed.Player1,original,Consumer("CS2_182"),false);
        Check(changed.HistoryFacts.HasConsumers,"in-place consumer definition transformation enrolls game");
        var enchant=Cards.FromId("CS2_188o").Clone();enchant.ConsumesHistory=true;
        Generic.AddEnchantmentBlock(changed,enchant,changed.Player1.Hero,changed.Player1.Hero);
        Check(changed.HistoryFacts.HasConsumers,"declared enchantment consumer enrolled");
        var offers=HistoryFactsProof.Fixture();
        var candidate=Consumer();
        Generic.CreateChoiceCards(offers.Player1,offers.Player1.Hero,null,ChoiceType.GENERAL,ChoiceAction.HAND,new[]{candidate},null);
        Check(!offers.HistoryFacts.HasConsumers,"unselected offer does not enroll consumer");
        Generic.ChoicePick(offers.Player1,offers,offers.Player1.Choice.Choices[0]);
        Check(offers.HistoryFacts.HasConsumers,"selected offer enrolls before continuation");
        FairView();
        Console.WriteLine("PASS history canonical: no-consumer baseline, query purity, late/sticky enrollment, past-sensitive hash, fair projection");
    }
    private static void FairView()
    {
        // Both runs expose the same board/counts, but select opposite private offers and
        // enroll different hidden physical deck entities before the opening draw.
        Game PrivateRun(bool swap)
        {
            var g=HistoryFactsProof.Fixture(beforeStart:game=>{
                var p=game.Player2;
                var original=p.DeckZone[p.DeckZone.Count-1];
                var generated=Entity.FromCard(p,original.Card);
                if(swap) {p.DeckZone.Remove(original);p.SetasideZone.Add(original);p.DeckZone.Add(generated);}
                else {p.DeckZone.Remove(original);p.DeckZone.Add(original);p.SetasideZone.Add(generated);}
            });
            var publicCard=(Minion)Play(g,g.Player1,"CS2_231");
            Generic.ShuffleIntoDeck(g.Player1,g.Player1.Hero,g.Player1.BoardZone.Remove(publicCard));
            Add(g.Player1,"CS2_231");
            var opponent=g.Player2;
            var context=new SabberStoneCore.EffectSystem.EffectContext(g,opponent.Id,opponent.Hero.Id);
            context.Discover(new[]{Cards.FromId("CS2_029"),Cards.FromId("CS2_231")});
            Generic.ChoicePick(opponent,g,opponent.Choice.Choices[swap?1:0]);
            Add(opponent,swap?"CS2_029":"CS2_231");
            return g;
        }
        var a=PrivateRun(false);var b=PrivateRun(true);
        Check(a.HistoryFacts.Query.Discoveries().Single().SelectedDefinitions.Single().DbfId
            !=b.HistoryFacts.Query.Discoveries().Single().SelectedDefinitions.Single().DbfId,
            "fixture has genuinely different private Discover definitions before projection");
        Check(a.HistoryFacts.CanonicalText()!=b.HistoryFacts.CanonicalText(),"authoritative private histories differ");
        var initialField=typeof(HistoryFacts).GetField("_initial",BindingFlags.Instance|BindingFlags.NonPublic);
        var ia=(Dictionary<int,int>)initialField.GetValue(a.HistoryFacts);
        var ib=(Dictionary<int,int>)initialField.GetValue(b.HistoryFacts);
        Check(!ia.Keys.Order().SequenceEqual(ib.Keys.Order()),"fixture private membership differs before projection");
        var authoritative=a.HistoryFacts.CanonicalText();int count=a.HistoryFacts.Count;
        var fa=FairBotState.Create(a,a.Player1.PlayerId,42,DeckStyle.Control);
        var fb=FairBotState.Create(b,b.Player1.PlayerId,42,DeckStyle.Control);
        var field=typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic);
        var ga=(Game)field.GetValue(fa);var gb=(Game)field.GetValue(fb);
        // Search copies carry fair origin beliefs (fix/ai-search-history): the real private membership is replaced by
        // the constant started-in-own-deck convention for every hidden actor, never copied from the authoritative store.
        var hiddenActors=ga.Player1.DeckZone.Concat(ga.Player2.DeckZone).Concat(ga.Player2.HandZone).ToArray();
        var projectedInitial=(Dictionary<int,int>)initialField.GetValue(ga.HistoryFacts);
        Check(hiddenActors.All(e=>projectedInitial.TryGetValue(e.Id,out int owner) && owner==e.Controller.Id)
            && ((HistoryDefinition[])typeof(HistoryFacts).GetField("_definitions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ga.HistoryFacts))
                .All(def=>ga.HistoryFacts.Query.Events<HistoryCardFact>().Any(f=>ReferenceEquals(f.Definition,def))),
            "projected private membership is the constant belief; unused definition index erased");
        Check(ga.HistoryFacts.CanonicalText()==gb.HistoryFacts.CanonicalText(),"hidden permutations equal projected facts");
        Check(ga.HistoryFacts.Query.Plays().All(f=>f.EntityId==0) && ga.HistoryFacts.Query.Plays().Single().DbfId==Cards.FromId("CS2_231").AssetId,
            "public definition retained, hidden current entity cannot be recovered through past play link");
        Check(fa.Evaluate()==fb.Evaluate() && fa.Actions.Select(x=>x.ToString()).SequenceEqual(fb.Actions.Select(x=>x.ToString())),"hidden permutations equal evaluation/actions");
        Check(count==ga.HistoryFacts.Count && count==a.HistoryFacts.Count && authoritative==a.HistoryFacts.CanonicalText(),"masking records nothing and never edits authoritative chunks");
        var action=fa.Actions.First(x=>x.Type==SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.PLAY_CARD);
        var future=fa.Apply(action);var futureGame=(Game)field.GetValue(future);
        Check(!futureGame.HistoryFacts.Query.Plays().Last().StartedInDeck,"projected future visible generated play retains known origin");
        // Erased origins resolve to the fair constant belief (started in deck), never an invented false.
        var hiddenOpponent=ga.Player2.DeckZone[0];
        Check(ga.HistoryFacts.Query.KnownOrigin(hiddenOpponent.Id,hiddenOpponent.Controller.Id)==true
            && ga.HistoryFacts.Query.StartedInDeck(hiddenOpponent.Id,hiddenOpponent.Controller.Id),
            "hidden private queries return the fair constant belief rather than an invented false");
    }
}
