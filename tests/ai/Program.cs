using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// --audit: the dated one-off audit reproductions (tests/run.py runs them as the `ai-audit` suite); the rest are name filters.
if (args.Length > 0 && args[0] == "--audit") { Environment.ExitCode = AuditReproTests.Run(args.Skip(1).ToArray()); return; }

if (args.Contains("--fairness-closure")) { FairnessClosureTests.Run(); return; }
if (args.Length > 0) throw new ArgumentException("unknown arguments: " + string.Join(" ", args) + " (use no argument, --audit or --fairness-closure)");

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static Game Fixture(CardClass own = CardClass.MAGE)
{
    var game = new Game(new GameConfig {
        Player1HeroClass = own, Player2HeroClass = CardClass.PRIEST,
        Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
        Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_200"), 30).ToList(),
        StartPlayer = 1, Shuffle = false, SkipMulligan = true, RandomSeed = 7, History = false, Logging = false });
    game.StartGame();
    return game;
}
static Game PaladinFixture()
{
    var game = Fixture(CardClass.PALADIN);
    foreach (var card in game.Player1.HandZone.ToArray()) game.Player1.HandZone.Remove(card);
    game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
    return game;
}
static List<PlayerTask> CardOrEnd(Game game, IPlayable card) => game.Player1.Options().Where(task =>
    task.PlayerTaskType == PlayerTaskType.END_TURN || task.Source?.Id == card.Id).ToList();
static PlayerTask ChooseCard(Game game, IPlayable card) => LocalBot.Choose(CardOrEnd(game, card));

var live = Fixture();
int count = live.Player1.DeckCards.Count;
var clone = live.CloneExact();
clone.Player1.DeckCards.Clear();
Check(live.Player1.DeckCards.Count == count, "Engine clone shares live DeckCards list");
Console.WriteLine("PASS engine clone deck-list isolation");

var game = Fixture();
foreach (var card in game.Player1.HandZone.ToArray()) game.Player1.HandZone.Remove(card);
game.Player1.BaseMana = 2; game.Player1.UsedMana = 0;
var ogre = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone:game.Player1.BoardZone);
ogre.IsExhausted = false;
var taunt = Entity.FromCard(game.Player2, Cards.FromId("CS1_042"), zone:game.Player2.BoardZone);
var frostbolt = Entity.FromCard(game.Player1, Cards.FromId("CS2_024"), zone:game.Player1.HandZone);
game.Player2.Hero.Damage = 24;
var decision = BotSearch.Choose(FairViewTests.Create(game, 1, 71, DeckStyle.Tempo), new BotBudget(4,10,600,5000));
var action = decision.Action.Resolve(game.Player1.Options());
Check(action.Source?.Id == frostbolt.Id && action.Target?.Id == taunt.Id,
    "Two-action lethal missed: remove taunt with Frostbolt before Ogre attacks");

game.Process(action);
Check(game.State != State.COMPLETE, "Planner executed more than root action");
var next = BotSearch.Choose(FairViewTests.Create(game, 1, 72, DeckStyle.Tempo), new BotBudget(4,10,600,5000));
game.Process(next.Action.Resolve(game.Player1.Options()));
Check(game.Player1.PlayState == PlayState.WON, "Replanned Ogre attack should finish lethal");
Console.WriteLine($"PASS two-action lethal, {decision.Nodes} nodes, depth {decision.Depth}, {decision.Milliseconds}ms");

string Fingerprint(Game g) => string.Join("|", g.IdEntityDic.Values.OrderBy(e => e.Id).Select(e =>
    $"{e.Id}:{e.Card.Id}:{e[GameTag.ZONE]}:{e[GameTag.HEALTH]}:{e[GameTag.DAMAGE]}:{e[GameTag.ATK]}"))
    + string.Join(",", g.Player1.DeckZone.Select(c => c.Id))
    + string.Join(",", g.Player1.DeckCards.Concat(g.Player2.DeckCards).Select(c => c.Id))
    + $"/{g.Turn}/{g.Player1.RemainingMana}/{g.Player2.RemainingMana}/{g.NextCloneIndex}"
    + $"/{g.Player1.ControllerAuraEffects.Hash()}/{g.Player2.ControllerAuraEffects.Hash()}";
string RandomOutcome(Game g) => string.Join("|", new Character[] { g.Player1.Hero, g.Player2.Hero }
    .Concat(g.Player1.BoardZone).Concat(g.Player2.BoardZone).OrderBy(e => e.Id).Select(e =>
        $"{e.Id}:{e.Card.Id}:{e.Health}:{e.Damage}:{e.AttackDamage}"))
    + $"/{g.Player1.Hero.Weapon?.Durability}/{g.Player2.Hero.Weapon?.Durability}";
Game EmptyHand()
{
    var g = Fixture();
    foreach (var c in g.Player1.HandZone.ToArray()) g.Player1.HandZone.Remove(c);
    g.Player1.BaseMana = 10; g.Player1.UsedMana = 0;
    return g;
}
var thorough = new BotBudget(4,10,600,5000);
var hold = EmptyHand();
hold.Player1.BaseMana = 4;
var fireball = Entity.FromCard(hold.Player1,Cards.FromId("CS2_029"),zone:hold.Player1.HandZone);
Entity.FromCard(hold.Player2,Cards.FromId("CS2_231"),zone:hold.Player2.BoardZone);
var conserved = BotSearch.Choose(FairViewTests.Create(hold,1,71,DeckStyle.Tempo),thorough);
Check(conserved.Action.Source != fireball.Id, "Wasteful Fireball over hero-power removal");
Console.WriteLine("PASS removal conservation");

var visible = EmptyHand();
Entity.FromCard(visible.Player1,Cards.FromId("CS2_023"),zone:visible.Player1.HandZone);
Entity.FromCard(visible.Player1,Cards.FromId("CS2_029"),zone:visible.Player1.HandZone);
var changed = visible.CloneExact();
foreach (var old in changed.Player2.HandZone.ToArray())
    changed.Player2.HandZone.ChangeEntity(old,Entity.FromCard(changed.Player2,Cards.FromId("CS2_029"),id:old.Id));
foreach (var old in changed.Player2.DeckZone.ToArray())
    changed.Player2.DeckZone.ChangeEntity(old,Entity.FromCard(changed.Player2,Cards.FromId("EX1_414"),id:old.Id));
changed.SetRandomSeed(789123); changed.Player1.DeckZone.Shuffle();
var fairA = FairViewTests.Create(visible,1,71,DeckStyle.Tempo);
var fairB = FairViewTests.Create(changed,1,71,DeckStyle.Tempo);
Check(fairA.Describe() == fairB.Describe(), "Observation leaks hidden identity/order");
var planA = BotSearch.Choose(fairA,thorough); var planB = BotSearch.Choose(fairB,thorough);
Check(planA.Action.Equals(planB.Action) && planA.Value == planB.Value, "Hidden cards/RNG alter plan");
Console.WriteLine("PASS hidden hand/decks/RNG noninterference");

string before = Fingerprint(visible);
string legalBefore = string.Join("|",visible.Player1.Options().Select(BotAction.From));
foreach (var key in fairA.Actions) Check(key.Equals(BotAction.From(key.Resolve(visible.Player1.Options()))), "Action round trip failed");
BotSearch.Choose(FairViewTests.Create(visible,1,71,DeckStyle.Tempo),thorough);
Check(before == Fingerprint(visible), "Planning mutates live entities, decks or counters");
Check(legalBefore == string.Join("|",visible.Player1.Options().Select(BotAction.From)), "Planning mutates actual tasks");
Console.WriteLine("PASS live-state and action mapping isolation");

var draw = fairA.Actions.Single(a => a.Source == visible.Player1.HandZone.First(c => c.Card.Id == "CS2_023").Id);
var afterDraw = fairA.Apply(draw);
Check(!afterDraw.CanExpand && afterDraw.Describe().Contains("UNKNOWN"), "Search reads simulated future draw");
var capped = BotSearch.Choose(fairA,new BotBudget(10,10,1,5000));
Check(capped.Nodes <= 1 && fairA.Actions.Any(a => a.Equals(capped.Action)), "Node cap/legality broken");
Check(BotBudget.ForStars(150).Nodes > BotBudget.ForStars(0).Nodes && BotBudget.ForStars(150).Depth > BotBudget.ForStars(0).Depth, "Rank does not change search");
Console.WriteLine("PASS draw boundary and search budgets");

var priest = Fixture(CardClass.PRIEST);
priest.Player1.BaseMana = 10;
Entity.FromCard(priest.Player1,Cards.FromId("EX1_591"),zone:priest.Player1.BoardZone);
priest.Player1.Hero.Damage = 5;
var heals = priest.Player1.Options().Where(t => t.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_POWER).ToList();
Check(LocalBot.Score(heals.Single(t => t.Target == priest.Player2.Hero)) > LocalBot.Score(heals.Single(t => t.Target == priest.Player1.Hero)), "Auchenai fallback heals itself for damage");
Console.WriteLine("PASS healing inversion");

var opponents = new List<string>();
for (int n = 0; n < 24; n++)
{
    var nextOpponent = LocalDecks.NextOpponent(opponents);
    Check(!opponents.TakeLast(2).Contains(nextOpponent.Id), "Recent opponent repeated");
    opponents.Add(nextOpponent.Id);
}
Check(opponents.Distinct().Count() == 8 && opponents.GroupBy(x => x).All(g => g.Count() == 3), "Opponent rotation is unbalanced");
Console.WriteLine("PASS eight-archetype rotation");

var combo = Fixture(CardClass.PRIEST);
foreach(var c in combo.Player1.HandZone.ToArray()) combo.Player1.HandZone.Remove(c);
combo.Player1.BaseMana=10;combo.Player1.UsedMana=0;combo.Player2.Hero.Damage=10;
var sentinel=(Minion)Entity.FromCard(combo.Player1,Cards.FromId("CS2_179"),zone:combo.Player1.BoardZone);sentinel.IsExhausted=false;
Entity.FromCard(combo.Player1,Cards.FromId("CS2_236"),zone:combo.Player1.HandZone);
Entity.FromCard(combo.Player1,Cards.FromId("CS2_236"),zone:combo.Player1.HandZone);
Entity.FromCard(combo.Player1,Cards.FromId("CS1_129"),zone:combo.Player1.HandZone);
var bronzeBudget=BotBudget.ForStars(0);var legendBudget=BotBudget.ForStars(150);
var shallow=BotSearch.Choose(FairViewTests.Create(combo,1,71,DeckStyle.Combo),new BotBudget(bronzeBudget.Depth,bronzeBudget.Branch,bronzeBudget.Nodes,5000));
var deep=BotSearch.Choose(FairViewTests.Create(combo,1,71,DeckStyle.Combo),new BotBudget(legendBudget.Depth,legendBudget.Branch,legendBudget.Nodes,5000));
Check(shallow.Value<999000 && deep.Value>999000, "Legend cannot see four-action Inner Fire lethal beyond Bronze horizon");
Console.WriteLine($"PASS rank tactical horizon: Bronze value={shallow.Value} depth={shallow.Depth}, Legend value={deep.Value} depth={deep.Depth} nodes={deep.Nodes}");

var crowded = new Game(new GameConfig { Player1HeroClass=CardClass.MAGE,Player2HeroClass=CardClass.PRIEST, Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(), Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_200"),30).ToList(), StartPlayer=1,Shuffle=false,SkipMulligan=true,RandomSeed=7,History=false,Logging=false });
crowded.StartGame(); foreach(var c in crowded.Player1.HandZone.ToArray()) crowded.Player1.HandZone.Remove(c);
crowded.Player1.BaseMana=7;crowded.Player1.UsedMana=0;
var crowdedOgre=(Minion)Entity.FromCard(crowded.Player1,Cards.FromId("CS2_200"),zone:crowded.Player1.BoardZone); crowdedOgre.IsExhausted=false;
for(int i=0;i<4;i++) { var m=(Minion)Entity.FromCard(crowded.Player1,Cards.FromId(i < 2 ? "CS2_182" : "CS2_033"),zone:crowded.Player1.BoardZone);m.IsExhausted=true; }
var crowdedTaunt=Entity.FromCard(crowded.Player2,Cards.FromId("CS1_042"),zone:crowded.Player2.BoardZone);
var crowdedBolt=Entity.FromCard(crowded.Player1,Cards.FromId("CS2_024"),zone:crowded.Player1.HandZone);
var tank=Entity.FromCard(crowded.Player1,Cards.FromId("CS2_200"),zone:crowded.Player1.HandZone);
crowded.Player2.Hero.Damage=24;

var crowdedPlan=BotSearch.Choose(FairViewTests.Create(crowded,1,71,DeckStyle.Tempo),new BotBudget(2,6,24,5000));
Check(crowdedPlan.Action.Source==crowdedBolt.Id && crowdedPlan.Action.Target==crowdedTaunt.Id, "Placement duplicates hide taunt-removal lethal");
Console.WriteLine("PASS crowded board branch diversity");

var random = EmptyHand();
var brawl = Entity.FromCard(random.Player1,Cards.FromId("EX1_407"),zone:random.Player1.HandZone);
for(int i=0;i<3;i++)
{
    Entity.FromCard(random.Player1,Cards.FromId("CS2_182"),zone:random.Player1.BoardZone);
    Entity.FromCard(random.Player2,Cards.FromId("CS2_200"),zone:random.Player2.BoardZone);
}
var unplanned = random.CloneExact();
BotSearch.Choose(FairViewTests.Create(random,1,123456,DeckStyle.Control),thorough);
random.Process(random.Player1.Options().Single(t=>t.Source?.Id==brawl.Id));
unplanned.Process(unplanned.Player1.Options().Single(t=>t.Source?.Id==brawl.Id));
string Survivors(Game g)=>string.Join(",",g.Player1.BoardZone.Concat(g.Player2.BoardZone).Select(c=>c.Id).OrderBy(i=>i));
Check(Survivors(random)==Survivors(unplanned), "Planning changes actual next random outcome");
Console.WriteLine("PASS real RNG outcome preserved across planning");
for(int n=0;n<200;n++)
{
    var copied=visible.CloneExact();
    Check(copied.Player1.HandZone.Select(c=>c.Card.Id).SequenceEqual(visible.Player1.HandZone.Select(c=>c.Card.Id)), "Clone loses entity data");
    if(n%20==0)GC.Collect();
}
Console.WriteLine("PASS repeated clone/collection stress");

var secrets=Fixture();
Entity.FromCard(secrets.Player2,Cards.FromId("EX1_295"),zone:secrets.Player2.SecretZone);
bool rejectedSecret=false;
try { FairViewTests.Create(secrets,1,71,DeckStyle.Tempo); } catch(NotSupportedException) { rejectedSecret=true; }
Check(rejectedSecret,"Unsupported secret entered search with actual hidden identity");
Console.WriteLine("PASS unsupported-secret search rejection");

var reinforce = PaladinFixture();
var reinforceChoice = LocalBot.Choose(reinforce.Player1.Options().Where(task =>
    task.PlayerTaskType == PlayerTaskType.HERO_POWER || task.PlayerTaskType == PlayerTaskType.END_TURN).ToList());
Check(reinforceChoice.PlayerTaskType == PlayerTaskType.HERO_POWER, "Reinforce loses to END_TURN on an open board");
Check(LocalBot.Score(reinforceChoice) > 0, "Reinforce has no positive open-board value");
for (int n = 0; n < 7; n++) Entity.FromCard(reinforce.Player1, Cards.FromId("CS2_231"), zone:reinforce.Player1.BoardZone);
Check(!reinforce.Player1.Options().Any(task => task.PlayerTaskType == PlayerTaskType.HERO_POWER),
    "Reinforce remains legal on a full board");
Console.WriteLine("PASS Reinforce open/full-board policy");

var holy = PaladinFixture();
holy.Player1.Hero.Damage = 6; holy.Player2.Hero.Damage = 10;
var holyLight = Entity.FromCard(holy.Player1, Cards.FromId("CS2_089"), zone:holy.Player1.HandZone);
Check(ChooseCard(holy, holyLight).Target == holy.Player1.Hero, "Holy Light heals enemy instead of damaged friendly hero");
holy.Player1.Hero.Damage = 0;
Check(ChooseCard(holy, holyLight).PlayerTaskType == PlayerTaskType.END_TURN,
    "Holy Light heals enemy when no friendly healing is useful");

var invertedHoly = PaladinFixture();
invertedHoly.Player1.Hero.Damage = 5; invertedHoly.Player2.Hero.Damage = 5;
Entity.FromCard(invertedHoly.Player1, Cards.FromId("EX1_591"), zone:invertedHoly.Player1.BoardZone);
var invertedLight = Entity.FromCard(invertedHoly.Player1, Cards.FromId("CS2_089"), zone:invertedHoly.Player1.HandZone);
Check(ChooseCard(invertedHoly, invertedLight).Target == invertedHoly.Player2.Hero,
    "Auchenai Holy Light damages friendly character");

var lay = PaladinFixture();
lay.Player1.Hero.Damage = 10; lay.Player2.Hero.Damage = 10;
var layOnHands = Entity.FromCard(lay.Player1, Cards.FromId("EX1_354"), zone:lay.Player1.HandZone);
Check(ChooseCard(lay, layOnHands).Target == lay.Player1.Hero, "Lay on Hands heals enemy instead of damaged friendly hero");
Check(LocalBot.Choose(CardOrEnd(lay, layOnHands).Where(task => task.PlayerTaskType == PlayerTaskType.END_TURN
    || task.Target?.Controller == lay.Player2).ToList()).PlayerTaskType == PlayerTaskType.END_TURN,
    "Lay on Hands harmful enemy target beats END_TURN");
lay.Player1.Hero.Damage = 0;
var fullHealthLay = ChooseCard(lay, layOnHands);
Check(fullHealthLay.Source == layOnHands && fullHealthLay.Target?.Controller == lay.Player1,
    "Lay on Hands refuses friendly full-health draw");

var hammerGame = PaladinFixture();
Entity.FromCard(hammerGame.Player1, Cards.FromId("CS2_182"), zone:hammerGame.Player1.BoardZone);
Entity.FromCard(hammerGame.Player2, Cards.FromId("CS2_182"), zone:hammerGame.Player2.BoardZone);
var hammer = Entity.FromCard(hammerGame.Player1, Cards.FromId("CS2_094"), zone:hammerGame.Player1.HandZone);
var hammerChoice = ChooseCard(hammerGame, hammer);
Check(hammerChoice.Target?.Controller == hammerGame.Player2, "Hammer of Wrath fails enemy damage target");
Check(LocalBot.Choose(CardOrEnd(hammerGame, hammer).Where(task => task.PlayerTaskType == PlayerTaskType.END_TURN
    || task.Target?.Controller == hammerGame.Player1).ToList()).PlayerTaskType == PlayerTaskType.END_TURN,
    "Hammer of Wrath harmful friendly target beats END_TURN");
Console.WriteLine("PASS Paladin heal inversion and Hammer direction");

foreach (string id in new[] { "CS2_087", "CS2_092", "EX1_355" })
{
    var buffGame = PaladinFixture();
    var friend = Entity.FromCard(buffGame.Player1, Cards.FromId("CS2_182"), zone:buffGame.Player1.BoardZone);
    Entity.FromCard(buffGame.Player2, Cards.FromId("CS2_182"), zone:buffGame.Player2.BoardZone);
    var buff = Entity.FromCard(buffGame.Player1, Cards.FromId(id), zone:buffGame.Player1.HandZone);
    Check(ChooseCard(buffGame, buff).Target == friend, id + " buffs enemy instead of friendly minion");
    Check(LocalBot.Choose(CardOrEnd(buffGame, buff).Where(task => task.PlayerTaskType == PlayerTaskType.END_TURN
        || task.Target?.Controller == buffGame.Player2).ToList()).PlayerTaskType == PlayerTaskType.END_TURN,
        id + " harmful enemy buff beats END_TURN");
}

foreach (string id in new[] { "EX1_371", "EX1_362" })
{
    var shieldGame = PaladinFixture();
    var fresh = (Minion)Entity.FromCard(shieldGame.Player1, Cards.FromId("CS2_182"), zone:shieldGame.Player1.BoardZone);
    var shielded = (Minion)Entity.FromCard(shieldGame.Player1, Cards.FromId("CS2_182"), zone:shieldGame.Player1.BoardZone);
    shielded.HasDivineShield = true;
    Entity.FromCard(shieldGame.Player2, Cards.FromId("CS2_182"), zone:shieldGame.Player2.BoardZone);
    var shield = Entity.FromCard(shieldGame.Player1, Cards.FromId(id), zone:shieldGame.Player1.HandZone);
    Check(ChooseCard(shieldGame, shield).Target == fresh, id + " wastes Divine Shield or targets enemy");
    Check(LocalBot.Choose(CardOrEnd(shieldGame, shield).Where(task => task.PlayerTaskType == PlayerTaskType.END_TURN
        || task.Target != fresh).ToList()).PlayerTaskType == PlayerTaskType.END_TURN,
        id + " harmful or wasteful shield beats END_TURN");
}

foreach (string id in new[] { "EX1_360", "EX1_382" })
{
    var reduceGame = PaladinFixture();
    Entity.FromCard(reduceGame.Player1, Cards.FromId("CS2_200"), zone:reduceGame.Player1.BoardZone);
    Entity.FromCard(reduceGame.Player2, Cards.FromId("CS2_231"), zone:reduceGame.Player2.BoardZone);
    var threat = Entity.FromCard(reduceGame.Player2, Cards.FromId("CS2_200"), zone:reduceGame.Player2.BoardZone);
    var reducer = Entity.FromCard(reduceGame.Player1, Cards.FromId(id), zone:reduceGame.Player1.HandZone);
    Check(ChooseCard(reduceGame, reducer).Target == threat, id + " reduces friendly or low enemy attack");
    Check(LocalBot.Choose(CardOrEnd(reduceGame, reducer).Where(task => task.PlayerTaskType == PlayerTaskType.END_TURN
        || task.Target != threat).ToList()).PlayerTaskType == PlayerTaskType.END_TURN,
        id + " harmful or wasteful attack reduction beats END_TURN");
}

foreach (string id in new[] { "EX1_362", "EX1_382" })
{
    var bodyGame = PaladinFixture();
    var body = Entity.FromCard(bodyGame.Player1, Cards.FromId(id), zone:bodyGame.Player1.HandZone);
    var choice = ChooseCard(bodyGame, body);
    Check(choice.Source == body && choice.Target == null, id + " refuses legal body play without battlecry target");
}
Console.WriteLine("PASS Paladin friendly buff/shield, enemy reduction, optional bodies");

var equalityUseful = PaladinFixture();
Entity.FromCard(equalityUseful.Player1, Cards.FromId("CS2_231"), zone:equalityUseful.Player1.BoardZone);
Entity.FromCard(equalityUseful.Player2, Cards.FromId("CS2_200"), zone:equalityUseful.Player2.BoardZone);
Entity.FromCard(equalityUseful.Player2, Cards.FromId("CS2_200"), zone:equalityUseful.Player2.BoardZone);
var equality = Entity.FromCard(equalityUseful.Player1, Cards.FromId("EX1_619"), zone:equalityUseful.Player1.HandZone);
Check(ChooseCard(equalityUseful, equality).Source == equality, "Equality refuses useful enemy-health reset");

var equalityWaste = PaladinFixture();
Entity.FromCard(equalityWaste.Player1, Cards.FromId("CS2_200"), zone:equalityWaste.Player1.BoardZone);
Entity.FromCard(equalityWaste.Player1, Cards.FromId("CS2_200"), zone:equalityWaste.Player1.BoardZone);
Entity.FromCard(equalityWaste.Player2, Cards.FromId("CS2_231"), zone:equalityWaste.Player2.BoardZone);
var wasteEquality = Entity.FromCard(equalityWaste.Player1, Cards.FromId("EX1_619"), zone:equalityWaste.Player1.HandZone);
Check(ChooseCard(equalityWaste, wasteEquality).PlayerTaskType == PlayerTaskType.END_TURN,
    "Equality destroys more friendly health than enemy health");

var consecrationUseful = PaladinFixture();
Entity.FromCard(consecrationUseful.Player2, Cards.FromId("CS2_182"), zone:consecrationUseful.Player2.BoardZone);
Entity.FromCard(consecrationUseful.Player2, Cards.FromId("CS2_182"), zone:consecrationUseful.Player2.BoardZone);
var consecration = Entity.FromCard(consecrationUseful.Player1, Cards.FromId("CS2_093"), zone:consecrationUseful.Player1.HandZone);
Check(ChooseCard(consecrationUseful, consecration).Source == consecration, "Consecration refuses useful clear");

var consecrationWaste = PaladinFixture();
var wasteConsecration = Entity.FromCard(consecrationWaste.Player1, Cards.FromId("CS2_093"), zone:consecrationWaste.Player1.HandZone);
Check(ChooseCard(consecrationWaste, wasteConsecration).PlayerTaskType == PlayerTaskType.END_TURN,
    "Consecration wastes on empty nonlethal board");
consecrationWaste.Player2.Hero.Damage = 28;
Check(ChooseCard(consecrationWaste, wasteConsecration).Source == wasteConsecration,
    "Consecration misses empty-board lethal");
Console.WriteLine("PASS useful/wasteful Equality and Consecration");

var swordGame = PaladinFixture();
var sword = Entity.FromCard(swordGame.Player1, Cards.FromId("EX1_366"), zone:swordGame.Player1.HandZone);
swordGame.Process(swordGame.Player1.Options().Single(task => task.Source?.Id == sword.Id));
swordGame.Player1.UsedMana = 0;
var blessing = Entity.FromCard(swordGame.Player1, Cards.FromId("CS2_087"), zone:swordGame.Player1.HandZone);
var bomber = Entity.FromCard(swordGame.Player1, Cards.FromId("EX1_082"), zone:swordGame.Player1.HandZone);
var swordTwin = swordGame.CloneExact();
string swordBefore = Fingerprint(swordGame);
int swordEntities = swordGame.IdEntityDic.Count;
int swordDurability = swordGame.Player1.Hero.Weapon.Durability;
var swordState = FairViewTests.Create(swordGame, 1, 404, DeckStyle.Value);
var afterRecruit = swordState.Apply(swordState.Actions.Single(action => action.Type == PlayerTaskType.HERO_POWER));
var afterBuff = afterRecruit.Apply(afterRecruit.Actions.Single(action => action.Source == blessing.Id));
Check(afterBuff.Describe().Contains(":CS2_101t:5:2"), "Sword clone did not trigger then accept targeted Recruit buff");
BotSearch.Choose(swordState, new BotBudget(3, 10, 100, 5000));
Check(Fingerprint(swordGame) == swordBefore && swordGame.IdEntityDic.Count == swordEntities
    && swordGame.Player1.Hero.Weapon.Durability == swordDurability,
    "Sword search mutates real weapon, entities, board, hand, health, aura, or counters");
swordGame.Process(swordGame.Player1.Options().First(task => task.Source?.Id == bomber.Id));
swordTwin.Process(swordTwin.Player1.Options().First(task => task.Source?.Id == bomber.Id));
Check(RandomOutcome(swordGame) == RandomOutcome(swordTwin), "Sword search changes real next random outcome");
Console.WriteLine("PASS Sword/Recruit/buff live-state and RNG preservation");

var suspended = Fixture(CardClass.DRUID);
suspended.Player1.BaseMana = 10;
var cenarius = SabberStoneCore.Actions.Generic.DrawCard(suspended.Player1, Cards.FromId("EDR_209"));
Check(suspended.Process(PlayCardTask.Any(suspended.Player1, cenarius)), "Cenarius play rejected");
var suspendedBytes = suspended.SaveSnapshot();
var fair = FairViewTests.Create(suspended, 1, 417, DeckStyle.Control);
var sameFair = FairViewTests.Create(suspended, 1, 417, DeckStyle.Control);
for (int prompt = 0; prompt < 3; prompt++)
{
    Check(fair.Actions.Length == 2 && fair.Actions.All(a => a.Type == PlayerTaskType.CHOOSE), "suspended fair offers missing");
    var pick = BotSearch.Choose(fair, new BotBudget(3, 2, 20, 5000));
    var repeatPick = BotSearch.Choose(sameFair, new BotBudget(3, 2, 20, 5000));
    Check(pick.Action.Equals(repeatPick.Action), "simulation seed changes suspended decision");
    fair = fair.Apply(pick.Action);
    sameFair = sameFair.Apply(repeatPick.Action);
    Check(fair.Describe() == sameFair.Describe(), "exact branch resume diverged");
    Check(suspendedBytes.SequenceEqual(suspended.SaveSnapshot()), "search mutated authoritative suspended game");
}
Check(fair.Actions.All(a => a.Type != PlayerTaskType.CHOOSE), "fair search left final decision pending");
Console.WriteLine("PASS suspended Cenarius search: three TryAnswer resumes, reproducibility, authoritative isolation");

var locations = EmptyHand();
var depths = (BoardObject)Entity.FromCard(locations.Player1, Cards.FromId("CORE_REV_990"), zone:locations.Player1.BoardZone);
var fragile = Entity.FromCard(locations.Player2, Cards.FromId("CS2_231"), zone:locations.Player2.BoardZone);
var friendlyAttacker = (Minion)Entity.FromCard(locations.Player1, Cards.FromId("CS2_182"), zone:locations.Player1.BoardZone);
friendlyAttacker.IsExhausted = false;
var uses = locations.Player1.Options().OfType<ActivateLocationTask>().ToArray();
Check(uses.Length == 2 && uses.All(t => LocalBot.Score(t) != -1000), "location activation fell through heuristic");
Check(LocalBot.Choose(uses.Cast<PlayerTask>().Append(EndTurnTask.Any(locations.Player1)).ToList()).ActionTarget == fragile,
    "Depths should remove enemy one-health minion");
Check(LocalBot.Score(uses.Single(t => t.ActionTarget == friendlyAttacker)) > -10000, "Depths should buff surviving friendly attacker");
friendlyAttacker.Damage = friendlyAttacker.Health - 1;
Check(LocalBot.Choose(new System.Collections.Generic.List<PlayerTask> { uses.Single(t => t.ActionTarget == friendlyAttacker), EndTurnTask.Any(locations.Player1) })
    .PlayerTaskType == PlayerTaskType.END_TURN, "Depths should skip lethal friendly damage");
friendlyAttacker.Damage = 0;
foreach (var use in uses)
    Check(BotAction.From(use).Resolve(locations.Player1.Options()).ActionTarget == use.ActionTarget, "activation action target round trip");
locations.Player1.UsedMana = locations.Player1.BaseMana;
locations.Player2.Hero.Damage = 26;
((Minion)fragile).HasTaunt = true;
locations.DeathProcessingAndAuraUpdate();
var locationTwin = locations.CloneExact();
var locationBefore = locations.SaveSnapshot();
var locationView = FairViewTests.Create(locations, 1, 938, DeckStyle.Value);
Check(locationView.Describe().Contains("CORE_REV_990"), "fair description omits locations");
var activationPlan = BotSearch.Choose(locationView, new BotBudget(2, 10, 100, 5000));
Check(activationPlan.Action.Type == PlayerTaskType.ACTIVATE_LOCATION, "search never selects useful activation");
var branch = locationView.Apply(BotAction.From(uses.Single(t => t.ActionTarget == fragile)));
Check(branch.Describe() != locationView.Describe() && locationView.Actions.Any(a => a.Type == PlayerTaskType.ACTIVATE_LOCATION)
    && !branch.Actions.Any(a => a.Type == PlayerTaskType.ACTIVATE_LOCATION), "branch cooldown/state not isolated");
Check(locationBefore.SequenceEqual(locations.SaveSnapshot()) && depths.Durability == 3 && depths.Location.UsesCompleted == 0,
    "location search changes authoritative state/RNG");
locations.Player1.UsedMana = 0; locationTwin.Player1.UsedMana = 0;
var bomberLocation = Entity.FromCard(locations.Player1, Cards.FromId("EX1_082"), zone:locations.Player1.HandZone);
var twinBomber = Entity.FromCard(locationTwin.Player1, Cards.FromId("EX1_082"), zone:locationTwin.Player1.HandZone);
locations.Process(locations.Player1.Options().First(t => t.Source?.Id == bomberLocation.Id));
locationTwin.Process(locationTwin.Player1.Options().First(t => t.Source?.Id == twinBomber.Id));
Check(RandomOutcome(locations) == RandomOutcome(locationTwin), "location search consumes live RNG");
var networkGame = EmptyHand();
var network = (BoardObject)Entity.FromCard(networkGame.Player1, Cards.FromId("JAIL_877"), zone:networkGame.Player1.BoardZone);
var networkUse = networkGame.Player1.Options().OfType<ActivateLocationTask>().Single();
Check(LocalBot.Score(networkUse) > 0 && BotAction.From(networkUse).Resolve(networkGame.Player1.Options()).ActionTarget == null,
    "Network untargeted action/score");
var cooldownGame = networkGame.CloneExact();
Check(cooldownGame.Process(new ActivateLocationTask(cooldownGame.Player1,
    (BoardObject)cooldownGame.IdEntityDic[network.Id])), "bot cooldown fixture activation");
for (int ownerTurn = 1; ownerTurn <= 2; ownerTurn++)
{
    Check(cooldownGame.Process(EndTurnTask.Any(cooldownGame.Player1)) &&
        cooldownGame.Process(EndTurnTask.Any(cooldownGame.Player2)), "bot cooldown fixture turn cycle");
    var cooldownView = FairViewTests.Create(cooldownGame, 1, 943, DeckStyle.Value);
    Check(cooldownView.Actions.Any(a => a.Type == PlayerTaskType.ACTIVATE_LOCATION && a.Source == network.Id) == (ownerTurn == 2),
        "bot must skip next owner turn and offer activation on following owner turn");
}
var fullClone = networkGame.CloneExact();
var clonedNetwork = fullClone.Player1.BoardZone.GetLocations().Single();
double fullLocationValue = FairViewTests.Create(fullClone, 1, 942, DeckStyle.Value).Evaluate();
clonedNetwork.Location.Stage = 2; clonedNetwork.Location.ConditionLocked = true; clonedNetwork[GameTag.DAMAGE] = clonedNetwork[GameTag.HEALTH] - 1;
Check(FairViewTests.Create(fullClone, 1, 942, DeckStyle.Value).Evaluate() == fullLocationValue - 3 && clonedNetwork[GameTag.HEALTH] == 2,
    "bot values remaining HEALTH minus DAMAGE, not printed durability");
Check(network.Location.Stage == 0 && !network.Location.ConditionLocked && network.Durability == 2, "clone shares LocationState");
for (int n = 0; n < 6; n++) Entity.FromCard(networkGame.Player1, Cards.FromId("CS2_182"), zone:networkGame.Player1.BoardZone);
Check(!networkGame.Player1.Options().OfType<ActivateLocationTask>().Any(), "non-final Network use on full board");
network[GameTag.DAMAGE] = network[GameTag.HEALTH] - 1;
Check(LocalBot.Score(networkGame.Player1.Options().OfType<ActivateLocationTask>().Single()) > 0, "final Network use should free slot");
Console.WriteLine("PASS locations AI: useful/pointless targets, action round trip, search, cooldown/state clone, live RNG isolation");

var placementGame = EmptyHand();
Entity.FromCard(placementGame.Player1, Cards.FromId("CS2_182"), zone:placementGame.Player1.BoardZone);
var locationHand = (BoardObject)Entity.FromCard(placementGame.Player1, Cards.FromId("JAIL_877"), zone:placementGame.Player1.HandZone);
var locationPlays = placementGame.Player1.Options().Where(t => t.Source == locationHand).ToArray();
Check(locationPlays.Length == 2 && locationPlays.All(t => LocalBot.Score(t) > -1000), "location hand placement/score");
var placementState = FairViewTests.Create(placementGame, 1, 942, DeckStyle.Value);
var branches = (IEnumerable<BotAction>)typeof(BotSearch).GetMethod("Branches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    .Invoke(null, new object[] { placementState, 64 });
Check(branches.Where(a => a.Source == locationHand.Id).Select(a => a.Position).Distinct().Count() == 2,
    "search collapsed location placements");
Check(placementState.Actions.Where(a => a.Source == locationHand.Id).Select(a => placementState.Apply(a).Describe()).Distinct().Count() == 2,
    "location placement branches lose physical state");
Console.WriteLine("PASS location hand play and bounded placement alternatives");

var renovatorGame = EmptyHand();
renovatorGame.Player1.BaseMana = 10;
var renovator = Entity.FromCard(renovatorGame.Player1, Cards.FromId("CORE_REV_023"), zone:renovatorGame.Player1.HandZone);
var weakLocation = (BoardObject)Entity.FromCard(renovatorGame.Player2, Cards.FromId("CORE_REV_990"), zone:renovatorGame.Player2.BoardZone);
weakLocation[GameTag.DAMAGE] = weakLocation[GameTag.HEALTH] - 1;
var strongLocation = (BoardObject)Entity.FromCard(renovatorGame.Player2, Cards.FromId("TIME_044"), zone:renovatorGame.Player2.BoardZone);
var renovatorPlays = renovatorGame.Player1.Options().OfType<PlayCardTask>().Where(t => t.Source == renovator).Cast<PlayerTask>().ToList();
Check(renovatorPlays.Count == 2 && renovatorPlays.All(t => t.ActionTarget is BoardObject), "Renovator must offer location targets");
Check(LocalBot.Score(renovatorPlays.Single(t => t.ActionTarget == strongLocation)) > LocalBot.Score(renovatorPlays.Single(t => t.ActionTarget == weakLocation)), "Renovator should value remaining location charges");
Check(LocalBot.Choose(renovatorPlays).ActionTarget == strongLocation, "Renovator should remove higher-value location");
var renovatorBytes = renovatorGame.SaveSnapshot();
var renovatorView = FairViewTests.Create(renovatorGame, 1, 943, DeckStyle.Value);
var renovatorAction = renovatorView.Actions.First(a => a.Source == renovator.Id && a.Target == strongLocation.Id);
Check(renovatorView.Apply(renovatorAction).Actions.All(a => a.Source != renovator.Id), "fair state must apply Renovator location target");
Check(renovatorBytes.SequenceEqual(renovatorGame.SaveSnapshot()), "Renovator fair branch changed authority");
Console.WriteLine("PASS Renovator AI location value, action target, fair clone isolation");

Check(decision.Stop == SearchStop.Terminal, "Lethal search must report Terminal");
Check(capped.Stop == SearchStop.Nodes, "Node-limited search must report Nodes");
DriverTests.Run();
FairViewTests.Run();
OwnSecretFairnessTests.Run();

FairnessClosureTests.Run();
