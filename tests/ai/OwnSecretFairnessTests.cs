using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// NPC World v1 / WP1 (S6): the golden fair view keeps the bot's OWN Secrets live (it placed them and knows them);
// opponent Secrets stay removed and only counted; legacy views still reject. In the simulated reply the human must not
// read the bot's Secret identity: policy can sample beliefs, but scoring must preserve the owner-known Secret.
// Fairness: twins differing only in the human's hidden zones (hand, deck order, Secret identity, setaside/created
// cards) give identical fair views, reply views and golden decisions while the bot's own Secret is in play.
internal static class OwnSecretFairnessTests
{
    private const string IceBarrier = "CORE_EX1_289";   // when your hero is attacked, gain 8 Armor
    private const string Counterspell = "CORE_EX1_287"; // when your opponent casts a spell, Counter it
    private const string HumanSecret = "CORE_LOOT_101"; // Explosive Runes (human side, played on the human's turn)
    private const string OtherHumanSecret = "JAIL_315"; // Mystic Misdirection (twin)
    private static readonly string[] SampleSpells = { "CS2_024", "CS2_029", "CS2_025", "CS2_023", "CS2_026", "EX1_277", "CS2_027", "CS2_022" };

    private static FairOptions Golden() => new FairOptions { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true };

    // Human plays its Secret on its own (earlier) turn, then the bot's turn starts and the bot arms its own Secret.
    private static Game Build(int seat, string ownSecret)
    {
        var game = DriverTests.Fixture(3 - seat); var human = game.CurrentPlayer; var bot = human.Opponent;
        var secret = Entity.FromCard(human, Cards.FromId(HumanSecret), zone: human.HandZone);
        DriverTests.Check(game.Process(PlayCardTask.Spell(human, secret)) && human.SecretZone.Any(c => c.Id == secret.Id), "human secret did not arm");
        foreach (string id in new[] { "CS2_029", "CS2_182", "CS2_024" }) Entity.FromCard(human, Cards.FromId(id), zone: human.HandZone);
        var attacker = (Minion)Entity.FromCard(human, Cards.FromId("CS2_182"), zone: human.BoardZone);
        attacker.IsExhausted = false;
        Entity.FromCard(human, Cards.FromId("CS2_029"), zone: human.SetasideZone); // a created/stored card only the human knows
        DriverTests.Check(game.Process(EndTurnTask.Any(human)) && game.CurrentPlayer == bot, "bot turn");
        foreach (var card in bot.HandZone.ToArray()) { bot.HandZone.Remove(card); bot.SetasideZone.Add(card); }
        var own = Entity.FromCard(bot, Cards.FromId(ownSecret), zone: bot.HandZone);
        DriverTests.Check(game.Process(PlayCardTask.Spell(bot, own)) && bot.SecretZone.Any(c => c.Id == own.Id && c.ActivatedTrigger != null), "own secret did not arm");
        foreach (string id in new[] { "CS2_024", "CS2_182", "CS2_029" }) Entity.FromCard(bot, Cards.FromId(id), zone: bot.HandZone);
        attacker.IsExhausted = false;
        return game;
    }

    private static Game Twin(Game game, int seat)
    {
        var variant = DriverTests.HiddenVariant(game, seat);
        var human = (variant.Player1.PlayerId == seat ? variant.Player1 : variant.Player2).Opponent;
        var old = human.SecretZone.Single(); old.ActivatedTrigger?.Remove(); human.SecretZone.Remove(old);
        var swapped = Entity.FromCard(human, Cards.FromId(OtherHumanSecret), zone: human.SecretZone, id: old.Id);
        swapped.Card.Power?.Trigger?.Activate(swapped);
        foreach (var created in human.SetasideZone.Where(c => c.Card.Id == "CS2_029").ToArray())
            human.SetasideZone.ChangeEntity(created, Entity.FromCard(human, Cards.FromId("CS2_032"), id: created.Id));
        return variant;
    }

    private static FairBotState Reply(FairBotState view, string[] hand)
    {
        var ended = view.Apply(view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
        return ended.Determinize(hand.Take(ended.Facts.Opponent.Hand.Count).Concat(Enumerable.Repeat(hand[0],
            Math.Max(0, ended.Facts.Opponent.Hand.Count - hand.Length))).ToArray(), 5).ForReply();
    }

    private static Controller BotIn(FairBotState reply, int seat)
    {
        var g = FairViewTests.Copy(reply); return g.Player1.PlayerId == seat ? g.Player1 : g.Player2;
    }

    internal static void Run()
    {
        Sabotage();
        AuditReproTests.Audit20261031_C4_ReplyPreservesKnownSecretOutcome();
        foreach (var id in new[] { IceBarrier, Counterspell, HumanSecret, OtherHumanSecret })
            DriverTests.Check(Cards.FromId(id).IsSecret, "secret fixture " + id);
        foreach (int seat in new[] { 1, 2 })
        {
            var game = Build(seat, IceBarrier); var bot = game.CurrentPlayer;
            string before = Authority(game);

            bool rejected = false;
            try { FairBotState.Create(game, seat, 71, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true }); }
            catch (NotSupportedException) { rejected = true; }
            DriverTests.Check(rejected, "legacy fair view must still reject own secrets");

            var view = FairBotState.Create(game, seat, 71, DeckStyle.Tempo, Golden());
            var copy = FairViewTests.Copy(view); var copyBot = copy.Player1.PlayerId == seat ? copy.Player1 : copy.Player2;
            DriverTests.Check(copyBot.SecretZone.Count(c => c.Card.Id == IceBarrier && c.ActivatedTrigger != null) == 1, "own secret missing or unarmed in the golden fair view");
            DriverTests.Check(!copyBot.Opponent.SecretZone.Any() && !copy.IdEntityDic.Values.Any(c => c.Card.Id == HumanSecret), "opponent secret identity survived");
            DriverTests.Check(view.Facts.OpponentSecretCount == 1, "opponent secret count");
            DriverTests.Check(before == Authority(game), "fair view changed authoritative bytes");

            // Owner knowledge controls actual outcomes; policy beliefs must not replace that defense.
            string[] hand = { "CS2_024", "CS2_029", "CS2_025", "CS2_023" };
            var reply = Reply(view, hand);
            var replyBot = BotIn(reply, seat);
            DriverTests.Check(replyBot.SecretZone.Count(c => c.IsSecret && c.ActivatedTrigger != null && c.Card.Class == CardClass.MAGE) == 1,
                "reply view lost the bot's actual armed secret");
            var counterGame = Build(seat, Counterspell);
            var counterView = FairBotState.Create(counterGame, seat, 71, DeckStyle.Tempo, Golden());
            DriverTests.Check(BotIn(Reply(counterView, hand), seat).SecretZone.Single().Card.Id == Counterspell
                && replyBot.SecretZone.Single().Card.Id == IceBarrier,
                "reply scoring replaced the owner-known Secret");

            // The actual Counterspell fires on a sampled hand play; it must never borrow Ice Barrier armor.
            bool countered = false;
            for (int k = 0; k < 64 && !countered; k++)
            {
                var sample = new[] { SampleSpells[k % SampleSpells.Length], SampleSpells[(k / SampleSpells.Length) % SampleSpells.Length], "CS2_182" };
                var r = Reply(counterView, sample);
                if (!BotIn(r, seat).SecretZone.Any(c => c.Card.Id == Counterspell)) continue;
                var cast = r.Actions.FirstOrDefault(a => a.Type == PlayerTaskType.PLAY_CARD
                    && FairViewTests.Copy(r).IdEntityDic[a.Source].Card.Type == CardType.SPELL);
                if (cast == null) continue;
                var after = r.Apply(cast);
                countered = !BotIn(after, seat).SecretZone.Any(c => c.Card.Id == Counterspell);
            }
            DriverTests.Check(countered, "actual Counterspell did not fire on a sampled spell");

            // Hidden-zone twins (hand, deck order, Secret identity, setaside): same views, replies and decisions.
            var variant = Twin(game, seat);
            var variantView = FairBotState.Create(variant, seat, 71, DeckStyle.Tempo, Golden());
            DriverTests.Check(FairViewTests.Fingerprint(view) == FairViewTests.Fingerprint(variantView), "hidden zones leak into the own-secret fair view");
            DriverTests.Check(FairViewTests.Fingerprint(reply) == FairViewTests.Fingerprint(Reply(variantView, hand)), "hidden zones leak into the reply view");
            var options = new DriverOptions { FairViewFactory = (g, id, s, style) => FairBotState.Create(g, id, s, style, Golden()) };
            var search = DriverTests.Decide(game, seat, options);
            DriverTests.Check(search.Trace.Kind == "search", "own secret forced a fallback: " + search.Trace.FallbackReason);
            DriverTests.Check(DriverTests.Trace(search.Trace) == DriverTests.Trace(DriverTests.Decide(variant, seat, options).Trace), "hidden zones changed the own-secret search decision");
            string a1 = GoldenTrace(game, seat), b1 = GoldenTrace(variant, seat);
            DriverTests.Check(a1.StartsWith("search|", StringComparison.Ordinal), "golden pilot fell back with an own secret: " + a1);
            DriverTests.Check(a1 == b1, "hidden zones changed the golden decision with an own secret");
            DriverTests.Check(before == Authority(game), "golden decision changed authoritative bytes");
        }
        Console.WriteLine("PASS own-secret fairness: own Secrets kept and armed, reply outcomes retain actual owner-known Secrets (fire on sampled plays), opponent Secrets removed, hidden-zone twins (hand/deck/Secret/setaside) give identical views, replies and decisions, legacy still rejects");
    }

    // Stickybomb Saboteur (CATA_186): its Sabotage (CATA_186t) holds a HandAdjacencyCostAura whose CATA_186te
    // presentation follows the hand neighbours. Masking a neighbour (HandZone.ChangeEntity -> RefreshHandAuras) must not
    // recreate that enchantment on the hidden card, and twins differing only in hidden zones must project identically.
    private static void Sabotage()
    {
        foreach (int seat in new[] { 1, 2 })
            foreach (bool botPlays in new[] { true, false })
            {
                var game = DriverTests.Fixture(botPlays ? seat : 3 - seat);
                var actor = game.CurrentPlayer; var other = actor.Opponent;
                var bot = botPlays ? actor : other; var human = bot.Opponent;
                foreach (string id in new[] { "CS2_182", "CS2_029", "CS2_024", "CS2_200" }) Entity.FromCard(human, Cards.FromId(id), zone: human.HandZone);
                foreach (string id in new[] { "CS2_182", "CS2_024" }) Entity.FromCard(bot, Cards.FromId(id), zone: bot.HandZone);
                var saboteur = Entity.FromCard(actor, Cards.FromId("CATA_186"), zone: actor.HandZone);
                DriverTests.Check(game.Process(PlayCardTask.Minion(actor, saboteur)), "Saboteur play rejected");
                // Move the Sabotage between hidden cards so both neighbours are private to the bot.
                var sabotage = other.HandZone.Single(c => c.Card.Id == "CATA_186t");
                if (!botPlays) DriverTests.Check(game.Process(EndTurnTask.Any(actor)), "end turn");
                else
                {
                    other.HandZone.Remove(sabotage);
                    other.HandZone.Add(sabotage, 2);
                }
                DriverTests.Check(game.CurrentPlayer == bot, "bot to act");
                var twin = SabotageTwin(game, bot.PlayerId);
                var view = FairBotState.Create(game, bot.PlayerId, 71, DeckStyle.Tempo, Golden());
                var twinView = FairBotState.Create(twin, bot.PlayerId, 71, DeckStyle.Tempo, Golden());
                foreach (var v in new[] { view, twinView })
                {
                    var copy = FairViewTests.Copy(v);
                    var leaks = copy.IdEntityDic.Values.OfType<Enchantment>().Where(e => e.Target is Unknown
                        || e.Target is IPlayable p && copy.IdEntityDic.TryGetValue(p.Id, out var live) && !ReferenceEquals(live, p)).Select(e => e.Card.Id + "->" + e.Target.Id).ToArray();
                    DriverTests.Check(leaks.Length == 0, "enchantment on a hidden card survived masking: " + string.Join(",", leaks));
                }
                DriverTests.Check(FairViewTests.Fingerprint(view) == FairViewTests.Fingerprint(twinView),
                    "Sabotage adjacency leaks hidden cards (seat " + seat + ", bot plays " + botPlays + ")");
                var options = new DriverOptions { FairViewFactory = (g, id, sd, style) => FairBotState.Create(g, id, sd, style, Golden()) };
                DriverTests.Check(DriverTests.Trace(DriverTests.Decide(game, bot.PlayerId, options).Trace)
                    == DriverTests.Trace(DriverTests.Decide(twin, bot.PlayerId, options).Trace), "Sabotage twin changed the decision");
            }
        Console.WriteLine("PASS Sabotage twin: hand-adjacency presentations never attach to masked cards; twins project and decide identically");
    }

    // Hidden-zone twin keeping public cards (the Sabotage) in place: other human hand cards and both decks change.
    private static Game SabotageTwin(Game game, int botId)
    {
        var copy = game.CloneExact();
        var bot = copy.Player1.PlayerId == botId ? copy.Player1 : copy.Player2; var human = bot.Opponent;
        foreach (var old in human.HandZone.Where(c => c.Card.Id != "CATA_186t").ToArray())
            human.HandZone.ChangeEntity(old, Entity.FromCard(human, Cards.FromId(old.Card.Id == "CS2_029" ? "CS2_032" : "CS2_029"), id: old.Id));
        foreach (var old in human.DeckZone.ToArray())
            human.DeckZone.ChangeEntity(old, Entity.FromCard(human, Cards.FromId("EX1_414"), id: old.Id));
        copy.SetRandomSeed(424242); bot.DeckZone.Shuffle(); human.DeckZone.Shuffle();
        return copy;
    }

    // Engine snapshots refuse this hand-built fixture (non-quiescent legacy work); compare every actor's state instead.
    private static string Authority(Game game) => string.Join("|", game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e =>
        e.Id + ":" + e.Card.Id + ":" + e.Zone?.Type + ":" + e.ZonePosition + ":" + string.Join(",", e.NativeTags.OrderBy(t => (int)t.Key).Select(t => (int)t.Key + "=" + t.Value))));

    private static string GoldenTrace(Game game, int seat)
    {
        var pilot = new GoldenPilot(NpcCatalog.All[0], DeckPlans.DragonWarrior);
        int index = 0;
        var result = BotDriver.Decide(game, seat, DeckStyle.Tempo, view => pilot.DecidePilot(view,
            new GoldenContext(game.Turn, 1, 997 * game.Turn + 31, pilot.Profile)), ref index, pilot.Options, tasks: game.CurrentPlayer.Options());
        return DriverTests.Trace(result.Trace);
    }
}
