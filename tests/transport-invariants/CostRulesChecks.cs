using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
namespace TransportInvariants;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Minion = SabberStoneCore.Model.Entities.Minion;
using Game = SabberStoneCore.Model.Game;
using Card = SabberStoneCore.Model.Card;
using Zone = SabberStoneCore.Enums.Zone;

internal static class CostRulesChecks
{
    private static void Check(bool value, string label) => SelfChecks.Check(value, "cost: " + label);
    internal static void Run()
    {
        Unpinned_MixedCostProjection();
        AlternateCostTradeRedraw();
        AlternateCostRetainedHealthTransform();
        SabotagePresentation();
        MillenniumProjection();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "warrior", 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var p = game.Player1;
        game.PowerHistory.Last.Clear(); p.BaseMana = 10; p.UsedMana = 0;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        session.Flush();
        int Sent(IPlayable entity, GameTag tag) => session.Client.Entities[entity.Id].Get(tag);
        void Flush(string label)
        {
            session.Flush();
            var violations = Invariants.Compare(game, session.Client).ToArray();
            Check(violations.Length == 0, label + ": " + string.Join(";", violations.Select(v => v.Entity + ":" + v.Tag + "=" + v.Expected + "/" + v.Shown)));
            game.PowerHistory.Last.Clear();
        }
        Flush("initial hand");
        // Capture reveal and removal in separate immutable batches, then replay both after mutation.
        long historicalOverride = CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        game.PowerHistory.Last.Clear();
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(card));
        var reveal = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        CostRules.RemoveApplication(card, historicalOverride);
        var removal = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        session.Invoke("SendHistory", reveal);
        Check(Sent(card, GameTag.CARD_ALTERNATE_COST) == 1, "historical reveal keeps original Health");
        session.Invoke("SendHistory", removal);
        Check(Sent(card, GameTag.CARD_ALTERNATE_COST) == 0, "historical removal explicitly restores Mana");
        Flush("historical override removal");
        CostRules.SetCostThisTurn(card, 0);
        CostRules.AddPaymentOverride(card, CostPaymentKind.Health, game.Turn);
        game.PowerHistory.Last.Clear();
        CostRules.ExpireTurn(game, game.Turn);
        var expiry = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        long later = CostRules.AddPaymentOverride(card, CostPaymentKind.Armor);
        long laterCost = CostRules.SetCostThisTurn(card, 1);
        session.Invoke("SendHistory", expiry);
        Check(Sent(card, GameTag.COST) == 4 && Sent(card, GameTag.CARD_ALTERNATE_COST) == 0,
            "historical expiry stores immutable COST and explicit Mana despite later mutations");
        CostRules.RemoveApplication(card, later); CostRules.RemoveApplication(card, laterCost);
        Flush("historical expiry");
        CostRules.AddNextHeroPowerRule(p, setCost: 0, kind: CostPaymentKind.Health);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(Generic.PayPhase(p, p.Hero.HeroPower), "historical HP consumption");
        var consumed = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        long laterHp = CostRules.AddPaymentOverride(p.Hero.HeroPower, CostPaymentKind.Armor);
        session.Invoke("SendHistory", consumed);
        Check(Sent(p.Hero.HeroPower, GameTag.COST) == 2 && Sent(p.Hero.HeroPower, GameTag.CARD_ALTERNATE_COST) == 0,
            "historical consumption stores final COST and Mana");
        CostRules.RemoveApplication(p.Hero.HeroPower, laterHp);
        Flush("historical rule consumption");
        var tax = new Effect(GameTag.COST, EffectOperator.ADD, 2);
        tax.ApplyAuraTo(card); Flush("getter-only aura cost");
        Check(Sent(card, GameTag.COST) == 6, "aura without setter reconciled");
        tax.RemoveAuraFrom(card); Flush("aura removal");
        foreach (var kind in new[] { CostPaymentKind.Health, CostPaymentKind.Armor })
        {
            long handle = CostRules.AddPaymentOverride(card, kind);
            Effects.ReduceCost(1).ApplyTo(card); Flush("discounted alternate amount");
            Check(Sent(card, GameTag.CARD_ALTERNATE_COST) == (int)kind && Sent(card, GameTag.COST) == card.Cost,
                "resource selector and amount agree");
            CostRules.RemoveApplication(card, handle); Flush("explicit alternate reset");
            Check(Sent(card, GameTag.CARD_ALTERNATE_COST) == 0, "alternate transition to Mana sends zero");
            var last = session.Packets.Last(packet => packet.Id == 19); // Native serialized history is also replayed by NativeSession.
            Check(last.Base64.Length > 0, "transition travelled serialized history");
        }
        CostRules.SetCostThisTurn(card, 0); Flush("temporary SET zero");
        card[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = 8;
        CostRules.AddNextCardRule(p, CostPaymentKind.Health);
        Check(Generic.PayPhase(p, card), "commit zero"); Flush("zero receipt");
        Check(Sent(card, GameTag.TAG_LAST_KNOWN_COST_IN_HAND) == 0, "zero paid amount overwrites previous receipt");
        session.Process(EndTurnTask.Any(p)); Flush("temporary expiry");
        Check(Sent(card, GameTag.COST) == 2, "temporary SET expiry restores surviving reductions");
        session.Process(EndTurnTask.Any(game.Player2)); Flush("human turn");

        foreach (var entry in new[] { ("TIME_612", CostPaymentKind.Health), ("BE_042", CostPaymentKind.Armor) })
        {
            Card definition = Definition(entry.Item1);
            var intrinsic = Generic.DrawCard(p, definition);
            Flush("intrinsic initial reveal");
            Check(Sent(intrinsic, GameTag.CARD_ALTERNATE_COST) == (int)entry.Item2, "intrinsic initial alternate icon");
            p.SetasideZone.Add(p.HandZone.Remove(intrinsic)); Flush("fixture removal");
        }
        var deckCard = (Playable)Entity.FromCard(p, Cards.FromId("CS2_120"));
        Effects.SetCost(1).ApplyTo(deckCard); CostRules.AddPaymentOverride(deckCard, CostPaymentKind.Health);
        p.DeckZone.Add(deckCard);
        var copy = Generic.Copy(p, p.Hero, deckCard, Zone.HAND); Flush("copy reveal");
        Check(Sent(copy, GameTag.COST) == 1 && Sent(copy, GameTag.CARD_ALTERNATE_COST) == 1, "copy reveal carries structured state");
        Generic.Draw(p, deckCard); Flush("deck-to-hand reveal");
        Check(Sent(deckCard, GameTag.COST) == 1 && Sent(deckCard, GameTag.CARD_ALTERNATE_COST) == 1, "deck reveal retains cost/resource");
        while (p.HandZone.Count > 3) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Last()));
        var source = (Minion)Entity.FromCard(p, Cards.FromId("CS2_231"));
        Generic.SummonBlock(game, source, -1, p.Hero);
        new ConditionalHeroPowerCostAura(3, 0).Activate(source); Flush("conditional HP");
        Check(Sent(p.Hero.HeroPower, GameTag.COST) == 0, "conditional HP displayed zero");
        var fourth = Generic.DrawCard(p, Cards.FromId("CS2_182")); Flush("conditional HP hand four");
        Check(Sent(p.Hero.HeroPower, GameTag.COST) == 2, "conditional HP deactivates");
        CostRules.AddNextHeroPowerRule(p, setCost: 0, expiryTurn: game.Turn); Flush("next HP rule");
        Check(Sent(p.Hero.HeroPower, GameTag.COST) == 0, "one-use HP displayed zero");
        CostRules.ExpireTurn(game, game.Turn); Flush("next HP expiry");
        Check(Sent(p.Hero.HeroPower, GameTag.COST) == 2, "one-use HP expiry reconciled");
        foreach (GameTag tag in new[] { GameTag.COST, GameTag.CARD_ALTERNATE_COST })
        {
            var tags = session.Client.Entities[card.Id].Tags; int correct = Sent(card, tag);
            tags[(int)tag] = correct + 17;
            Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == card.Id && v.Tag == tag.ToString()),
                "strict checker detects independently corrupted " + tag);
            tags[(int)tag] = correct;
        }
        var hidden = (Playable)game.Player2.HandZone.First();
        Effects.SetCost(0).ApplyTo(hidden); CostRules.AddPaymentOverride(hidden, CostPaymentKind.Armor);
        var hiddenDeck = (Playable)game.Player2.DeckZone.First();
        CostRules.AddPaymentOverride(hiddenDeck, CostPaymentKind.Health); Flush("privacy");
        Check(session.Client.Entities[hidden.Id].CardId == "" && Sent(hidden, GameTag.CARD_ALTERNATE_COST) == 0
            && Sent(hidden, GameTag.COST) == 0 && session.Client.Entities[hiddenDeck.Id].CardId == ""
            && Sent(hiddenDeck, GameTag.CARD_ALTERNATE_COST) == 0, "opponent hand/deck cost bindings remain private");
        Console.WriteLine("PASS cost transport: historical removal/expiry/consumption; serialized getter COST/2837; Health/Armor reset; initial reveal; discounted/zero payment; SET/HP expiry; copy/deck; privacy; separate corruptions");
    }
    private static void Unpinned_MixedCostProjection()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("TLC_436"));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
        CostRules.AddNextCardRule(p, CostPaymentKind.Armor);
        long health = CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        var healthHistory = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        long armor = CostRules.AddPaymentOverride(card, CostPaymentKind.Armor);
        var armorHistory = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        session.Invoke("SendHistory", healthHistory);
        Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 1,
            "Unpinned historical Health wins despite later Armor mutation");
        session.Invoke("SendHistory", armorHistory);
        Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 2,
            "Unpinned later Armor wins on wire");
        session.Flush();
        Check(!Invariants.Compare(game, session.Client).Any(), "Unpinned mixed payment strict projection");
        CostRules.RemoveApplication(card, armor); session.Flush();
        Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 1,
            "Unpinned removing newest restores older Health");
        CostRules.RemoveApplication(card, health); session.Flush();
        Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 2,
            "Unpinned removing Health restores Armor rule above printed Corpses");
        Console.WriteLine("PASS Unpinned_MixedCostProjection: printed Corpses; Armor/Health/Armor; historical wire; removal");
    }

    private static void AlternateCostTradeRedraw()
    {
        foreach (bool expire in new[] { true, false })
        {
            var game = OfflinePoolTestRecipes.NewGame(); var p = game.Player1;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var card = (Playable)Generic.DrawCard(p, Cards.FromId("TLC_255"));
            CostRules.SetCostThisTurn(card, 1);
            CostRules.AddPaymentOverride(card, CostPaymentKind.Health, game.Turn);
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush(); session.PublishDecision(p);
            Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 1, "Trade fixture initially Health");
            session.Process(TradeCardTask.Any(p, card));
            Check(card.Zone == p.DeckZone && session.Client.Entities[card.Id].CardId == "", "Trade hides source in deck");
            game.PowerHistory.Last.Clear();
            if (expire) { session.Process(EndTurnTask.Any(p)); session.Process(EndTurnTask.Any(p.Opponent)); }
            // Hidden expiry must not publish either private cost tag.
            game.PowerHistory.Last.Clear();
            int packets = session.Packets.Count;
            CostRules.SetCostThisTurn(card, 0); session.Flush();
            var hiddenPackets = session.Packets.Skip(packets).Where(x => x.Id == 19).ToArray();
            foreach (var wire in hiddenPackets)
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
                Check(!history.List.Any(e => e.HasTagChange && e.TagChange.Entity == card.Id
                    && (e.TagChange.Tag == 48 || e.TagChange.Tag == 2837)), "hidden deck cost tags stay private");
            }
            game.PowerHistory.Last.Clear();
            Generic.Draw(p, card);
            // Historical replay must clear stale Health before final getter reconciliation too.
            var reveal = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
            session.Invoke("SendHistory", reveal);
            var sent = session.Client.Entities[card.Id];
            Check(sent.Get(GameTag.CARD_ALTERNATE_COST) == (expire ? 0 : 1) && sent.Get(GameTag.COST) == card.Cost,
                "AlternateCostTradeRedraw " + (expire ? "expired Mana reset" : "active Health retained"));
            Check(!Invariants.Compare(game, session.Client).Any(), "Trade redraw strict invariants");
        }
        Console.WriteLine("PASS AlternateCostTradeRedraw: Trade/HIDE; later-turn Mana zero; active Health; immutable reveal; hidden deck privacy");
    }

    private static void AlternateCostRetainedHealthTransform()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        long handle = CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(p, card, Cards.FromId("CS2_120"), false);
        Check(CostRules.PaymentKind(card) == CostPaymentKind.Health, "retained transform keeps engine Health override");
        var transform = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        CostRules.RemoveApplication(card, handle);
        var removal = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        session.Invoke("SendHistory", transform);
        Check(session.Client.Entities[card.Id].CardId == "CS2_120"
            && session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 1,
            "AlternateCostRetainedHealthTransform historical CHANGE_ENTITY retains Health despite later Mana mutation");
        session.Invoke("SendHistory", removal);
        Check(session.Client.Entities[card.Id].Get(GameTag.CARD_ALTERNATE_COST) == 0,
            "retained transform subsequent removal restores Mana");
        Console.WriteLine("PASS AlternateCostRetainedHealthTransform: historical retained-Health transform; later Mana removal");
    }

    private static void SabotagePresentation()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var p = game.Player1;
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        foreach (var owner in new[] { p, p.Opponent })
        {
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var left = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var source = Entity.FromCard(owner.Opponent, Cards.FromId("CATA_186"), zone: owner.Opponent.SetasideZone);
            source.Power.PowerTask.Process(game, owner.Opponent, source, null, new SabberStoneCore.Model.TaskStack());
            var token = owner.HandZone.Single(c => c.Card.Id == "CATA_186t");
            var right = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            session.Flush(); game.PowerHistory.Last.Clear();
            var actors = left.AppliedEnchantments.Concat(right.AppliedEnchantments).Where(e => e.Card.Id == "CATA_186te").ToArray();
            if (owner == p)
            {
                Check(session.Client.Entities[left.Id].Get(GameTag.COST) == 5 && session.Client.Entities[right.Id].Get(GameTag.COST) == 5,
                    "Sabotage visible neighbor COST includes exactly one tax");
                Check(actors.All(e => session.Client.Entities[e.Id].CardId == "CATA_186te"
                    && session.Client.Entities[e.Id].Get(GameTag.ATTACHED) == e.Target.Id), "Sabotage visible presentation actors");
                owner.HandZone.Swap(token, left); session.Flush(); game.PowerHistory.Last.Clear();
                Check(session.Client.Entities[right.Id].Get(GameTag.COST) == 4, "Sabotage reorder clears visible tax");
                owner.SetasideZone.Add(owner.HandZone.Remove(token)); session.Flush(); game.PowerHistory.Last.Clear();
                Check(session.Client.Entities[left.Id].Get(GameTag.COST) == 4, "Sabotage removal clears visible tax");
            }
            else
            {
                Check(actors.All(e => session.Client.Entities[e.Id].CardId == ""
                    && session.Client.Entities[e.Id].Get(GameTag.ATTACHED) == 0), "Sabotage opponent presentation stays private");
                Check(new[] { left, token, right }.All(e => session.Client.Entities[e.Id].CardId == ""
                    && session.Client.Entities[e.Id].Get(GameTag.COST) == 0), "Sabotage opponent hand COST/identity private: " + string.Join(";", new[] { left, token, right }.Select(e => e.Id + ":" + session.Client.Entities[e.Id].CardId + ":" + session.Client.Entities[e.Id].Get(GameTag.COST))));
            }
            Check(!Invariants.Compare(game, session.Client).Any(), "Sabotage strict projection");
        }
        Console.WriteLine("PASS SabotagePresentation: visible neighbor COST/actors; reorder/removal; opponent privacy");
    }

    private static void MillenniumProjection()
    {
        var game = OfflinePoolTestRecipes.NewGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var source = Entity.FromCard(owner, Cards.FromId("TIME_615"), zone: owner.SetasideZone);
            source.Power.PowerTask.Process(game, owner, source, null, new TaskStack());
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(owner.HandZone.Count == 10, "Millennium fills hand through real module");
            foreach (var held in owner.HandZone)
            {
                var shown = session.Client.Entities[held.Id];
                var actor = held.AppliedEnchantments.Single(e => e.Card.Id == "TIME_615e");
                if (owner == game.Player1)
                    Check(shown.CardId == held.Card.Id && shown.Get(GameTag.COST) == held.Cost && shown.Get(GameTag.CARD_ALTERNATE_COST) == 1
                        && session.Client.Entities[actor.Id].Get(GameTag.TAG_ONE_TURN_EFFECT) == 1, "Millennium visible Health amount/this-turn actor");
                else
                    Check(shown.CardId == "" && shown.Get(GameTag.COST) == 0 && shown.Get(GameTag.CARD_ALTERNATE_COST) == 0
                        && session.Client.Entities[actor.Id].CardId == "" && session.Client.Entities[actor.Id].Get(GameTag.ATTACHED) == 0,
                        "Millennium opponent cards and payment actors private");
            }
            CostRules.ExpireTurn(game, game.Turn); session.Flush(); game.PowerHistory.Last.Clear();
            if (owner == game.Player1)
                Check(owner.HandZone.All(c => session.Client.Entities[c.Id].Get(GameTag.CARD_ALTERNATE_COST) == 0
                    && session.Client.Entities[c.Id].Get(GameTag.COST) == c.Cost), "Millennium expiry restores Mana and correct COST");
            Check(!Invariants.Compare(game, session.Client).Any(), "Millennium strict projection");
        }
        Console.WriteLine("PASS MillenniumProjection: fill; Health amount/338 actor; Mana expiry; opponent privacy");
    }

    private static Card Definition(string id)
    {
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
        using var json = System.Text.Json.JsonDocument.Parse(gzip);
        var row = json.RootElement.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("cardId").GetString() == id);
        var xml = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("CardDefs", new System.Xml.Linq.XElement("Entity",
            new System.Xml.Linq.XAttribute("CardID", id), new System.Xml.Linq.XAttribute("ID", row.GetProperty("dbfId").GetInt32()),
            row.GetProperty("tags").EnumerateObject().Select(t => new System.Xml.Linq.XElement("Tag",
                new System.Xml.Linq.XAttribute("enumID", t.Name), new System.Xml.Linq.XAttribute("value", t.Value.GetInt32()))))));
        return SabberStoneCore.Loader.CardLoader.LoadDefinitions(xml, overlay: true).Single();
    }

}
