using EngineUnknown = SabberStoneCore.Model.Entities.Unknown;
using EngineGame = SabberStoneCore.Model.Game;
using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static QuestNativeProof;
using SabberStoneCore.Actions;

// Fair search over own-deck offers (Dark Gift deck Discover, Tracking) and effects that read hidden cards.
internal static partial class FairViewHiddenOfferProof
{



    // Tracking's choice continuation is not snapshot-serializable; compare entity identities instead.
    private static string Fingerprint(EngineGame g) => string.Join(";", g.IdEntityDic.Select(e => e).OrderBy(e => e.Key).Select(e =>
        $"{e.Key}:{e.Value.GetType().Name}:{e.Value.Card.Id}:{e.Value.Zone?.Type}")) + "|" + string.Join(",", g.Player1.Choice?.Choices ?? new System.Collections.Generic.List<int>());


    internal static void Run()
    {
        // Exercise the live adapter itself, with clocks disabled like the rest of this runner.
        {
            var game = Fixture();
            Check(game.Process(EndTurnTask.Any(game.Player1)), "driver parity bot turn");
            var bot = game.Player2;
            foreach (var card in bot.HandZone.ToArray()) { bot.HandZone.Remove(card); bot.SetasideZone.Add(card); }
            bot.BaseMana = 10; bot.UsedMana = 0; game.Player1.Hero.Damage = 24;
            Add(bot, "CS2_029");
            var match = new NativeMatch((_, __) => { }, "mage", "mage", 23);
            typeof(NativeMatch).GetProperty("Game").SetValue(match, game);
            int index = 0;
            var expected = BotDriver.Decide(game, 2, LocalDecks.Get("mage").Style,
                state => PilotDecision.From(BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock())), ref index,
                tasks: bot.Options());
            var actual = (PlayerTask)typeof(NativeMatch).GetMethod("ChooseBotTask", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(match, new object[] { bot.Options() });
            Check(expected.Trace.Kind == "search" && BotAction.From(actual).Equals(BotAction.From(expected.Task))
                && match.BotFallbacks == 0 && match.BotNodes > 0, "NativeMatch.ChooseBotTask differs from shared driver");
            Check(game.Process(actual) && bot.PlayState == PlayState.WON, "live adapter task must execute on authority");
        }
        foreach (bool discard in new[] { false, true })
        {
            string previous = null;
            foreach (string hidden in new[] { "CS2_029", "CS2_120" })
            {
                var g = Fixture(); var p = g.Player1;
                int target = 0;
                if (discard)
                {
                    Summon(g, p, "CS2_200");
                    Check(g.Process(EndTurnTask.Any(p)), "Platysaur setup opponent turn");
                    target = Play(g, p.Opponent, "TLC_603").Id;
                    Generic.ChangeEntityBlock(p.Opponent, p.Opponent.HandZone.Last(), Cards.FromId(hidden), false);
                    Check(g.Process(EndTurnTask.Any(p.Opponent)), "Platysaur setup owner turn");
                    p.BoardZone.Single().AttackDamage = 50;
                }
                else { Summon(g, p, "JAIL_205"); Add(p.Opponent, hidden); }
                var before = g.SaveSnapshot();
                var state = FairBotState.Create(g, p.PlayerId, 83, DeckStyle.Value);
                var action = state.Actions.First(a => discard
                    ? a.Type == PlayerTaskType.MINION_ATTACK && a.Target == target
                    : a.Type == PlayerTaskType.END_TURN);
                bool rejected = false;
                try { state.Apply(action); }
                catch (NotSupportedException error) when (error.Message.StartsWith("Private historical ", StringComparison.Ordinal)) { rejected = true; }
                Check(rejected, "private-history branch rejects unavailable hand identity or discard definition");
                var fingerprint = Fingerprint(SearchGame(state));
                var decision = BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock());
                string observed = decision.Action + "|" + decision.Value + "|" + state.Describe();
                Check(previous == null || previous == observed, "hidden permutations yield identical bounded search");
                previous = observed;
                Check(decision.Nodes > 0 && decision.Action.Resolve(p.Options()) != null
                    && fingerprint == Fingerprint(SearchGame(state)) && before.SequenceEqual(g.SaveSnapshot()),
                    "private-history branches stop without mutating the fair parent or authoritative game");
            }
        }
        // Root: the chooser's own DECK offers stay real in its fair view, so the selector accepts each pick.
        foreach (var id in new[] { "EDR_856", "CORE_DS1_184" })
        {
            var g = Fixture(); var p = g.Player1;
            Play(g, p, id);
            var offers = p.Choice?.Choices?.ToArray();
            Check(offers != null && offers.Length == 3 && offers.All(o => g.IdEntityDic[o].Zone == p.DeckZone), id + " opens own-deck offers");
            var before = Fingerprint(g);
            var state = FairBotState.Create(g, p.PlayerId, 77, DeckStyle.Value);
            var view = SearchGame(state);
            Check(offers.All(o => view.IdEntityDic[o] is Minion m && m.Card.Id == g.IdEntityDic[o].Card.Id)
                && view.Player1.DeckZone.Where(c => !offers.Contains(c.Id)).All(c => c is EngineUnknown)
                && view.Player2.DeckZone.All(c => c is EngineUnknown), id + " fair view keeps only the offers visible");
            Check(state.Actions.Length == 3, id + " fair view offers each pick");
            foreach (var action in state.Actions)
            {
                int chosen = int.Parse(action.ToString().Split(':').Last());
                var child = SearchGame(state.Apply(action));
                var real = g.CloneExact(); var pending = real.GetPendingDecisions().Single();
                Check(real.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { chosen }), out _), id + " real pick accepted");
                string Seen(EngineGame game) => game.IdEntityDic[chosen].Zone?.Type + ":" + game.IdEntityDic[chosen].Card.Id + "|"
                    + string.Join(",", game.Player1.HandZone.Select(c => c.Id + ":" + c.Card.Id));
                Check(child.Player1.Choice == null && Seen(child) == Seen(real), id + " simulated pick matches the real pick");
            }
            var decision = BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock());
            Check(decision.Nodes > 0 && decision.Action.Resolve(p.Options()) != null, id + " search resolves a real pick");
            var rival = SearchGame(FairBotState.Create(g, p.Opponent.PlayerId, 77, DeckStyle.Value));
            AssertHiddenActors(g, rival, p.PlayerId, offers);
            Check(Fingerprint(g) == before, id + " fair views leave the real game unchanged");
        }
        // Q'onzu keeps the chooser's selected spell offboard during its keep/topdeck choice (showcase seed 8450).
        // The chooser still knows that pick; masking it left the suspended recipe holding an Unknown without a zone.
        {
            var g = Fixture(); var p = g.Player1;
            Play(g, p, "EDR_517");
            var spells = p.Choice?.Choices?.ToArray();
            Check(spells != null && spells.Length == 3, "Q'onzu opens its spell Discover");
            int kept = spells[0];
            var first = g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new DecisionAnswer(first.Id, first.ControllerId, new[] { kept }), out _), "Q'onzu spell pick accepted");
            Check(g.IdEntityDic[kept].Zone == p.SetasideZone
                && p.Choice.Choices.Select(n => g.IdEntityDic[n].Card.Id).SequenceEqual(new[] { "EDR_517A", "EDR_517B" }),
                "Q'onzu holds its pick offboard until keep/topdeck is answered");
            var before = g.SaveSnapshot();
            var state = FairBotState.Create(g, p.PlayerId, 81, DeckStyle.Value);
            var view = SearchGame(state);
            Check(view.IdEntityDic[kept].Card.Id == g.IdEntityDic[kept].Card.Id && view.IdEntityDic[kept].Zone == view.Player1.SetasideZone,
                "chooser's fair view keeps its own pending Q'onzu pick");
            Check(state.Actions.Length == 2, "fair view offers keep and topdeck");
            foreach (var action in state.Actions)
            {
                int option = int.Parse(action.ToString().Split(':').Last());
                var child = SearchGame(state.Apply(action));
                var real = g.CloneExact(); var pending = real.GetPendingDecisions().Single();
                Check(real.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { option }), out _), "real Q'onzu option accepted");
                string Seen(EngineGame game) => game.IdEntityDic[kept].Zone?.Type + ":" + game.IdEntityDic[kept].Card.Id + ":"
                    + game.IdEntityDic[kept].Controller.PlayerId + "|" + string.Join(",", game.Player1.HandZone.Select(c => c.Id + ":" + c.Card.Id));
                Check(child.Player1.Choice == null && !child.Resolution.HasSuspendedEffect && Seen(child) == Seen(real),
                    "simulated Q'onzu option matches the real option");
            }
            Check(BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock()).Action.Resolve(p.Options()) != null,
                "search resolves a real Q'onzu option");
            var rival = SearchGame(FairBotState.Create(g, p.Opponent.PlayerId, 81, DeckStyle.Value));
            AssertHiddenActors(g, rival, p.PlayerId, spells);
            Check(before.SequenceEqual(g.SaveSnapshot()), "Q'onzu fair views leave the real game unchanged");
        }

        // In-search: simulated effects that draw, buff, copy or pick hidden cards degrade to hidden cards.
        {
            var g = Fixture(); var p = g.Player1; p.UsedMana = 0;
            var vanguard = Add(p, "TIME_003"); var mimicry = Add(p, "EDR_522"); var tracking = Add(p, "CORE_DS1_184");
            var state = FairBotState.Create(g, p.PlayerId, 79, DeckStyle.Value);
            int Hidden(FairBotState s) => SearchGame(s).Player1.HandZone.Count(c => c is EngineUnknown);
            FairBotState PlayCard(IPlayable card) =>
                state.Apply(state.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == card.Id));
            Check(Hidden(PlayCard(vanguard)) == 1, "Portal Vanguard draws then buffs a hidden card");
            Check(Hidden(PlayCard(mimicry)) == 2, "Mimicry copies two hidden opponent draws");
            var tracked = PlayCard(tracking);
            var picks = tracked.Actions;
            Check(picks.Length == 1 && picks.All(a => a.Type == PlayerTaskType.CHOOSE), "Tracking offers hidden deck picks");
            Check(Hidden(tracked.Apply(picks[0])) == 1, "Tracking pick of a hidden card draws a hidden card");
            Check(BotSearch.Choose(state, BotBudget.ForStars(150).WithoutClock()).Nodes > 0, "search expands hidden-card effects");
        }
        // A hidden in-search Discover still completes for history consumers, without revealing its definition.
        {
            var g = Fixture(); var p = g.Player1;
            var scuffle = Add(p, "TLC_365"); var tracking = Add(p, "CORE_DS1_184");
            Check(scuffle.Card.AssetId == 118266 && scuffle.Cost == 3, "Storage Scuffle starts at printed cost");
            var before = g.SaveSnapshot();
            var state = FairBotState.Create(g, p.PlayerId, 79, DeckStyle.Value);
            var tracked = state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == tracking.Id));
            var pendingView = SearchGame(tracked);
            Check(pendingView.Player1.Choice.Choices.All(id => pendingView.IdEntityDic[id] is EngineUnknown)
                && !pendingView.HistoryFacts.Query.Discoveries().Any()
                && pendingView.IdEntityDic[scuffle.Id].Cost == 3, "hidden Tracking offers do not complete Discover before a pick");
            var child = SearchGame(tracked.Apply(tracked.Actions.Single()));

            var real = g.CloneExact();
            Check(real.Process(real.Player1.Options().Single(t => t.Source?.Id == tracking.Id)), "real Tracking play accepted");
            var pending = real.GetPendingDecisions().Single();
            int chosen = real.Player1.Choice.Choices[0];
            Check(real.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { chosen }), out _), "real Tracking pick accepted");
            var realFact = real.HistoryFacts.Query.Discoveries().Single();
            Check(realFact.DefinitionsKnown && realFact.SelectedDefinitions.Single().DbfId == real.IdEntityDic[chosen].Card.AssetId
                && real.IdEntityDic[scuffle.Id].Cost == 0, "real Tracking retains selected definition and discounts Storage Scuffle");

            Check(child.HistoryFacts.Query.Discoveries().Count() == 1, "hidden Tracking pick records one Discover completion");
            foreach (var copy in new[] { child, child.CloneExact() })
            {
                var fact = copy.HistoryFacts.Query.Discoveries().Single();
                Check(fact.OperationId == realFact.OperationId && fact.ControllerId == p.Id && !fact.DefinitionsKnown,
                    "hidden Discover preserves operation and controller with a redacted definition");
                bool rejected = false;
                try { _ = fact.SelectedDefinitions; } catch (NotSupportedException) { rejected = true; }
                Check(rejected, "hidden Discover definition access cannot expose card identity");
                Check(copy.IdEntityDic[scuffle.Id].Cost == 0 && copy.IdEntityDic[scuffle.Id].Cost == real.IdEntityDic[scuffle.Id].Cost,
                    "hidden Discover discounts Storage Scuffle like the real game, including clone");
            }
            Check(before.SequenceEqual(g.SaveSnapshot()), "hidden Discover search preserves authoritative bytes");
        }
        foreach (bool generated in new[] { false, true })
        {
            var g = Fixture(); var p = g.Player1;
            IPlayable foreign = null;
            if (generated)
            {
                foreign = SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_029"));
                Generic.ShuffleIntoDeck(p, p.Hero, foreign);
            }
            var warden = Add(p, "EDR_256");
            var before = g.SaveSnapshot();
            var state = FairBotState.Create(g, p.PlayerId, 81, DeckStyle.Value);
            var child = state.Apply(state.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == warden.Id));
            Check(SearchGame(child).Player1.HandZone.Single() is EngineUnknown && !child.CanExpand,
                "Dreamwarden masked-origin draw stays unknown and stops search");
            Check(before.SequenceEqual(g.SaveSnapshot()), "Dreamwarden search preserves authoritative bytes");
            Check(g.Process(p.Options().First(t => t.Source?.Id == warden.Id)), "real Dreamwarden accepted");
            Check(generated ? foreign.Zone == p.HandZone && p.HandZone.Count == 1
                : p.HandZone.Count == 0, "real Dreamwarden still draws only a generated physical card");
            Check(((Minion)g.IdEntityDic[warden.Id]).AttackDamage == warden.Card.ATK + (generated ? 2 : 0),
                "real Dreamwarden buff still requires a matching origin");
        }
        Console.WriteLine("PASS fair view hidden offers: Dark Gift/Tracking deck picks searchable and opponent-masked; hidden draw/buff/copy/pick simulated; Dreamwarden masked origins; redacted Discover completion discounts Storage Scuffle");
    }
}
