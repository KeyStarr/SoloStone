using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HistoryFairOriginProof
{
    internal static Game FairGame(FairBotState fair) => (Game)typeof(FairBotState)
        .GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
    private static void PlayedSecrets()
    {
        foreach (int player in new[]{1,2})
        foreach (bool initial in new[]{false,true})
        {
            Game SecretRun(bool swap)
            {
                var game=HistoryFactsProof.Fixture();
                var caster=player==1?game.Player1:game.Player2;
                if(game.CurrentPlayer!=caster) End(game);
                var offers=new[]{HistoryCanonicalProof.Consumer("EX1_289"),Cards.FromId("EX1_294")};
                IPlayable secret;
                if(initial)
                {
                    secret=caster.DeckZone[0];
                    Generic.ChangeEntityBlock(caster,secret,offers[swap?1:0],false);
                    secret=game.IdEntityDic[secret.Id];Generic.Draw(caster,secret);
                }
                else
                {
                    var context=new SabberStoneCore.EffectSystem.EffectContext(game,caster.Id,caster.Hero.Id);
                    context.Discover(offers);
                    int selected=caster.Choice.Choices[swap?1:0];
                    Generic.ChoicePick(caster,game,selected);
                    secret=game.IdEntityDic[selected];
                }
                PlayEntity(game,caster,secret);
                Check(secret.Zone==caster.SecretZone && game.HistoryFacts.Query.Plays().Single().EntityId==secret.Id
                    && game.HistoryFacts.Query.Casts().Count()==2,"real secret play publishes play and cast facts");
                return game;
            }
            var a=SecretRun(false);var b=SecretRun(true);
            var caster=player==1?a.Player1:a.Player2;var viewer=caster.Opponent;
            var before=a.SaveSnapshot();
            var pa=a.CloneExact();pa.ProjectHistoryForFairView(viewer.PlayerId);
            var pb=b.CloneExact();pb.ProjectHistoryForFairView(viewer.PlayerId);
            Check(pa.HistoryFacts.CanonicalText()==pb.HistoryFacts.CanonicalText(),
                "played-secret permutation must have identical projected facts/canonical text before reveal");
            foreach(var projected in new[]{pa,pb,pa.CloneExact(),Game.LoadSnapshot(pa.SaveSnapshot())})
            {
                var facts=projected.HistoryFacts.Query.Events<HistoryCardFact>().ToArray();
                Check(facts.Length==3 && facts.All(f=>!f.DefinitionKnown && !f.OriginKnown && f.EntityId==0
                    && f.SpellSchool==0 && f.Races.Count==0),"private play/cast metadata masked through clone/snapshot");
                foreach(var fact in facts)
                {
                    bool rejected=false;try{_=fact.DbfId;}catch(NotSupportedException){rejected=true;}
                    Check(rejected,"private DbfId query rejects rather than leaking identity");
                    rejected=false;try{_=fact.Definition;}catch(NotSupportedException){rejected=true;}
                    Check(rejected,"private definition query rejects rather than leaking identity");
                }
                Check(!projected.HistoryFacts.HasConsumers && projected.HistoryFacts.Query.Discoveries().All(f=>!f.DefinitionsKnown),
                    "private plays cannot reveal Discover selection or consumer enrollment");
                var definitions=(HistoryDefinition[])typeof(HistoryFacts).GetField("_definitions",BindingFlags.Instance|BindingFlags.NonPublic)
                    .GetValue(projected.HistoryFacts);
                Check(definitions.Length==0,"private definitions removed from associated index");
                Check(projected.HistoryFacts.CanonicalText()==pa.HistoryFacts.CanonicalText(),"clone/snapshot preserve masked canonical text");
            }
            Check(before.SequenceEqual(a.SaveSnapshot()),"secret projection preserves authoritative bytes");
            var privateSetaside=new List<string>();
            foreach(var game in new[]{a.CloneExact(),b.CloneExact()})
            {
                var owner=player==1?game.Player1:game.Player2;
                owner.SetasideZone.Add(owner.SecretZone.Remove(owner.SecretZone.Single()));
                game.ProjectHistoryForFairView(viewer.PlayerId);
                Check(game.HistoryFacts.Query.Plays().All(f=>!f.DefinitionKnown),"unrevealed setaside secret remains private");
                privateSetaside.Add(game.HistoryFacts.CanonicalText());
            }
            Check(privateSetaside[0]==privateSetaside[1],"private setaside permutation remains indistinguishable");
            var own=a.CloneExact();own.ProjectHistoryForFairView(caster.PlayerId);
            Check(own.HistoryFacts.Query.Plays().Single().DbfId==Cards.FromId("EX1_289").AssetId,
                "owner retains played secret identity");
            foreach(var game in new[]{a,b})
            {
                var owner=player==1?game.Player1:game.Player2;
                owner.SecretZone.Single()[GameTag.REVEALED]=1;
                var revealed=game.CloneExact();revealed.ProjectHistoryForFairView(viewer.PlayerId);
                Check(revealed.HistoryFacts.Query.Plays().Single().DbfId==owner.SecretZone.Single().Card.AssetId,
                    "revealed secret identity may appear");
            }
        }
        var triggered=HistoryFactsProof.Fixture();var secret=Play(triggered,triggered.Player1,"EX1_289");End(triggered);
        var attacker=Summon(triggered.Player2,"CS2_231");Attack(triggered,attacker,triggered.Player1.Hero);
        Check(secret.Zone==triggered.Player1.GraveyardZone,"real secret trigger reveals into public graveyard");
        triggered.ProjectHistoryForFairView(triggered.Player2.PlayerId);
        Check(triggered.HistoryFacts.Query.Plays().Single().DbfId==Cards.FromId("EX1_289").AssetId,
            "real reveal exposes played definition");
        Console.WriteLine("PASS history played secrets: normal play, hidden permutations, both viewers, initial/Discover origins, clone/snapshot, indexes, real reveal");
    }
    private static void Check(bool condition,string message)=>Core2026Proof.Check(condition,"history played secrets: "+message);
    private static void FilteredDraws()
    {
        foreach(bool started in new[]{true,false})
        {
            var g=HistoryFactsProof.Fixture();var p=g.Player1;
            var original=p.DeckZone[0]; Generic.ChangeEntityBlock(p,original,Cards.FromId("CS2_029"),false);
            original=g.IdEntityDic[original.Id];
            var foreign=Entity.FromCard(p,Cards.FromId("GAME_005")); Generic.ShuffleIntoDeck(p,p.Hero,foreign);
            var other=g.CloneExact();
            Generic.ChangeEntityBlock(other.Player1,other.IdEntityDic[original.Id],Cards.FromId("GAME_005"),false);
            Generic.ChangeEntityBlock(other.Player1,other.IdEntityDic[foreign.Id],Cards.FromId("CS2_029"),false);
            var fa=FairBotState.Create(g,p.PlayerId,88,DeckStyle.Control);
            var fb=FairBotState.Create(other,p.PlayerId,88,DeckStyle.Control);
            var ga=FairGame(fa);var gb=FairGame(fb);
            Core2026Proof.Check(ga.HistoryFacts.CanonicalText()==gb.HistoryFacts.CanonicalText()
                && fa.Evaluate()==fb.Evaluate() && fa.Actions.Select(x=>x.ToString()).SequenceEqual(fb.Actions.Select(x=>x.ToString())),
                "history origin draw: hidden definitions yield identical fair history/evaluation/actions");
            foreach(var projected in new[]{ga,gb})
            {
                var ctx=new SabberStoneCore.EffectSystem.EffectContext(projected,p.Id,p.Hero.Id);
                var drawn=ctx.DrawWhere(card=>ctx.History.StartedInDeck(card.Id,p.Id)==started && card.Card.Type==CardType.SPELL);
                Core2026Proof.Check(drawn is Unknown && drawn.Zone==projected.Player1.HandZone,
                    "Dragonscale Armaments/Smuggled Shovel: origin-filtered search draw resolves to Unknown");
            }
            Core2026Proof.Check(!fa.CanExpand && !fb.CanExpand && ga.HistoryFacts.CanonicalText()==gb.HistoryFacts.CanonicalText()
                && fa.Evaluate()==fb.Evaluate(),"history origin draw: unknown draw stops expansion with equal future views");
            var context=new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,p.Hero.Id);
            var known=context.DrawWhere(card=>card.Card.Type==CardType.SPELL && context.History.StartedInDeck(card.Id,p.Id)==started);
            Core2026Proof.Check(known.Id==(started?original.Id:foreign.Id),"history origin draw: authoritative matching physical spell drawn");
            var before=g.SaveSnapshot();
            Core2026Proof.Check(context.DrawWhere(card=>card.Card.Type==CardType.SPELL
                && context.History.StartedInDeck(card.Id,p.Id)==started)==null && before.SequenceEqual(g.SaveSnapshot()),
                "history origin draw: missing category neither draws nor fatigues nor consumes RNG");
            while(!p.HandZone.IsFull) Add(p,"CS2_231");
            Core2026Proof.Check(context.DrawWhere(card=>card.Card.Type==CardType.SPELL
                && context.History.StartedInDeck(card.Id,p.Id)!=started)==null
                && g.IdEntityDic[started?foreign.Id:original.Id].Zone==p.GraveyardZone,
                "history origin draw: matching full-hand draw burns and returns no hand card");
        }
        Console.WriteLine("PASS history origin draws: both origin partitions, hidden permutations, unknown expansion stop, absent-category fizzle");
    }
    private static void VisibleZones()
    {
        foreach (bool modernQuest in new[]{false,true})
        foreach (bool initialWeapon in new[]{false,true})
        {
            var g=HistoryFactsProof.Fixture();
            var visible=new List<(int Id,int Owner,bool Initial,string Zone)>();
            var privateEntities=new List<(int Id,int Owner,bool Initial)>();
            foreach (var p in new[]{g.Player1,g.Player2})
            {
                p.AddHeroAndPower(Cards.FromId("HERO_03"));
                Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)),"history visible origin: Dagger Mastery accepted");
                if(initialWeapon)
                {
                    var original=p.DeckZone[0];
                    Generic.ChangeEntityBlock(p,original,Cards.FromId("CS2_082"),false);
                    p.Hero.AddWeapon((Weapon)p.DeckZone.Remove(g.IdEntityDic[original.Id]));
                }
                visible.Add((p.Hero.Weapon.Id,p.Id,initialWeapon,"equipped weapon"));
                visible.Add((p.Hero.Id,p.Id,false,"hero"));
                visible.Add((p.Hero.HeroPower.Id,p.Id,false,"hero power"));
                foreach (bool initial in new[]{false,true})
                foreach (string zone in new[]{"location","graveyard","revealed setaside","quest","revealed secret"})
                {
                    if((zone=="quest" || zone=="revealed secret") && initial!=initialWeapon) continue;
                    string card=zone=="location"?"CORE_REV_990":zone=="quest"?(modernQuest?"TLC_426":"UNG_940"):zone=="revealed secret"?"EX1_289":"CS2_231";
                    IPlayable entity;
                    if(initial)
                    {
                        entity=p.DeckZone[0];
                        Generic.ChangeEntityBlock(p,entity,Cards.FromId(card),false);
                        entity=p.DeckZone.Remove(g.IdEntityDic[entity.Id]);
                    }
                    else entity=Entity.FromCard(p,Cards.FromId(card));
                    if(zone=="location") p.BoardZone.Add((BoardObject)entity);
                    else if(zone=="graveyard") p.GraveyardZone.Add(entity);
                    else if(zone=="revealed setaside") { p.SetasideZone.Add(entity); entity[GameTag.REVEALED]=1; }
                    else { p.SecretZone.Add((Spell)entity); if(zone=="revealed secret") entity[GameTag.REVEALED]=1; }
                    visible.Add((entity.Id,p.Id,initial,zone));
                }
                IPlayable secret;
                if(initialWeapon)
                {
                    secret=p.DeckZone[0];Generic.ChangeEntityBlock(p,secret,Cards.FromId("EX1_287"),false);
                    secret=p.DeckZone.Remove(g.IdEntityDic[secret.Id]);
                }
                else secret=Entity.FromCard(p,Cards.FromId("EX1_287"));
                p.SecretZone.Add((Spell)secret);
                privateEntities.Add((secret.Id,p.Id,initialWeapon));
                var hidden=Entity.FromCard(p,Cards.FromId("CS2_231"));p.SetasideZone.Add(hidden);
                privateEntities.Add((hidden.Id,p.Id,false));
                hidden=p.DeckZone.Remove(p.DeckZone[0]);p.SetasideZone.Add(hidden);
                privateEntities.Add((hidden.Id,p.Id,true));
                End(g);
            }
            var before=g.SaveSnapshot();
            foreach(var viewer in new[]{g.Player1,g.Player2})
            {
                // FairBotState intentionally rejects secrets; exercise its shared projection API directly.
                var projected=g.CloneExact();projected.ProjectHistoryForFairView(viewer.PlayerId);
                foreach(var copy in new[]{projected,projected.CloneExact(),Game.LoadSnapshot(projected.SaveSnapshot())})
                {
                    foreach(var e in visible)
                        Core2026Proof.Check(copy.HistoryFacts.Query.StartedInDeck(e.Id,e.Owner)==e.Initial,
                            "history visible origin: "+e.Zone+" retains physical membership for either viewer");
                    foreach(var e in privateEntities)
                    {
                        bool ownSecret=e.Owner==viewer.Id && copy.IdEntityDic[e.Id].Zone==copy.IdEntityDic[e.Id].Controller.SecretZone;
                        if(ownSecret) Core2026Proof.Check(copy.HistoryFacts.Query.StartedInDeck(e.Id,e.Owner)==e.Initial,"history visible origin: own secret known");
                        else
                        {
                            bool rejected=false;try{copy.HistoryFacts.Query.StartedInDeck(e.Id,e.Owner);}catch(NotSupportedException){rejected=true;}
                            Core2026Proof.Check(rejected,"history visible origin: opponent secret/private setaside unavailable");
                        }
                    }
                }
            }
            Core2026Proof.Check(before.SequenceEqual(g.SaveSnapshot()),"history visible origin: projection leaves authoritative bytes unchanged");
        }
        Console.WriteLine("PASS history visible origins: Dagger Mastery, initial weapons, direct locations, heroes/powers, secrets/legacy+modern quests, revealed setaside, graveyard, both viewers, clone/snapshot, privacy");
    }
    internal static void Run()
    {
        PlayedSecrets();
        VisibleZones();
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var hand = p.DeckZone[0]; Generic.Draw(p, hand);
        var original = (Minion)p.DeckZone[0]; Generic.Draw(p, original); PlayEntity(g,p,original); Kill(g,original);
        var generated = (Minion)Play(g,p,"CS2_231");
        Generic.ShuffleIntoDeck(p,p.Hero,p.BoardZone.Remove(generated));
        var enemy = p.Opponent;
        var opponentOriginal = enemy.DeckZone[0]; Generic.Draw(enemy,opponentOriginal);
        End(g); PlayEntity(g,enemy,opponentOriginal);
        var opponentBoard=enemy.HandZone.First(card=>card is Minion);PlayEntity(g,enemy,opponentBoard);End(g);
        enemy.SetasideZone.Add(enemy.BoardZone.Remove((Minion)opponentOriginal));
        var fair = FairBotState.Create(g,p.PlayerId,47,DeckStyle.Control);
        var copy = FairGame(fair); var q = copy.HistoryFacts.Query;
        Core2026Proof.Check(q.StartedInDeck(hand.Id,p.Id), "Story of the Waygate: own hand origin visible");
        Core2026Proof.Check(q.StartedInDeck(original.Id,p.Id) && q.StartedInDeck(opponentOriginal.Id,enemy.Id)
            && q.StartedInDeck(opponentBoard.Id,enemy.Id),
            "history fair origin: public graveyard, opposing board and past play origins visible");
        Core2026Proof.Check(q.Plays().Count(f => f.ControllerId == p.Id && !f.StartedInDeck) == 1,
            "Techysaurus: own past play origin retained after shuffle, without current entity link");
        Core2026Proof.Check(q.Plays().Single(f => f.PlayId == g.HistoryFacts.Query.Plays().Single(x=>x.EntityId==generated.Id).PlayId).EntityId == 0,
            "history fair origin: own past play cannot reveal hidden deck identity");
        // Search copies carry fair origin beliefs (fix/ai-search-history): with no own-deck ledger every genuinely
        // hidden actor (masked deck slots, including the shuffled-in generated card, and the opposing hand) gets the
        // constant started-in-deck convention, so nothing about the hidden truth leaks. Independence from hidden
        // state is twin-tested in tests/golden/HistorySearchTests.cs.
        foreach (var hidden in copy.Player1.DeckZone.Concat(copy.Player2.DeckZone).Concat(copy.Player2.HandZone))
            Core2026Proof.Check(q.KnownOrigin(hidden.Id,hidden.Controller.Id) == true && q.StartedInDeck(hidden.Id,hidden.Controller.Id),
                "history fair origin: genuinely hidden origin is the constant belief, not the truth");
        PlayEntity(copy,copy.Player1,copy.IdEntityDic[hand.Id]);
        Core2026Proof.Check(copy.HistoryFacts.Query.Plays().Last().StartedInDeck,
            "history fair origin: future own play records known origin");
        var sibling=copy.CloneExact();
        Core2026Proof.Check(sibling.HistoryFacts.Query.StartedInDeck(hand.Id,p.Id),"history fair origin: clone retains projection policy");
        Core2026Proof.Check(g.HistoryFacts.Query.StartedInDeck(generated.Id,p.Id)==false,
            "history fair origin: projection leaves authoritative membership untouched");
        FilteredDraws();
        Console.WriteLine("PASS history fair origins: own hand/past plays, public board/graveyard, hidden beliefs, future plays, clone");
    }
}
