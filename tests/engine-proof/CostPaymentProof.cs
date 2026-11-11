using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class CostPaymentProof
{
    private static void Check(bool condition, string message) => CostRulesProof.Check(condition, "payment: " + message);
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            foreach (CostPaymentKind kind in Enum.GetValues<CostPaymentKind>().Where(kind => kind != CostPaymentKind.Corpses))
            foreach (int balance in new[] { 2, 3, 4 })
            {
                var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
                var card = Generic.DrawCard(p, Cards.FromId("CS2_182")); card.Cost = 3;
                CostRules.AddPaymentOverride((Playable)card, kind);
                p.BaseMana = kind == CostPaymentKind.Mana ? balance : 10; p.UsedMana = 0;
                p.Hero.Health = kind == CostPaymentKind.Health ? balance : 30;
                p.Hero.Armor = kind == CostPaymentKind.Armor ? balance : 10;
                p.Hero.IsImmune = true; p.Hero.HasDivineShield = true;
                int damaged = 0; game.TriggerManager.DealDamageTrigger += _ => damaged++;
                p.Hero.TakeDamageTrigger += _ => damaged++;
                int health = p.Hero.Health, armor = p.Hero.Armor, mana = p.RemainingMana;
                bool legal = kind == CostPaymentKind.Health ? balance > 3 : balance >= 3;
                Check(card.IsPlayableByPlayer == legal, "option affordability " + kind + balance);
                Check(Generic.PayPhase(p, card) == legal, "commit affordability " + kind + balance);
                Check(p.Hero.Health == health - (legal && kind == CostPaymentKind.Health ? 3 : 0)
                    && p.Hero.Armor == armor - (legal && kind == CostPaymentKind.Armor ? 3 : 0)
                    && p.RemainingMana == mana - (legal && kind == CostPaymentKind.Mana ? 3 : 0)
                    && damaged == 0 && p.Hero.HasDivineShield, "atomic resource payment bypasses damage/prevention");
            }
            Zero(history); Rules(history); Intrinsic(history); HeroPowerFailure(history); PlayBoundaries(history); LegacyHideStatOptions(history);
        }
        Console.WriteLine("PASS cost payment: Mana/Health/Armor eligibility; prevention; zero; one-use; intrinsic; failed HP; legacy HideStat options");
    }

    private static void Zero(bool history)
    {
        foreach (CostPaymentKind kind in Enum.GetValues<CostPaymentKind>())
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            var card = Generic.DrawCard(p, Cards.FromId("CS2_182")); card.Cost = 0;
            card[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = 9;
            CostRules.AddNextCardRule(p, kind);
            var quote = CostRules.Quote(p, card);
            Check(Generic.PayPhase(p, card) && card[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 0, "zero records committed amount");
            Check(!CostRules.Quote(p, card).Rules.Any() && quote.Rules.Length == 1, "zero consumes matching one-use");
        }
    }

    private static void Rules(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = Generic.DrawCard(p, Cards.FromId("CS2_168")); card.Cost = 3; // Murloc Raider
        var other = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, CardType.MINION, Race.MURLOC, 3);
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, CardType.MINION, Race.MURLOC, 3);
        Check(CostRules.Quote(p, other).Kind == CostPaymentKind.Mana, "nonmatching card ignored");
        p.Hero.Health = 3;
        Check(!Generic.PayPhase(p, card) && CostRules.Quote(p, card).Rules.Length == 2, "failed payment retains all rules");
        p.Hero.Health = 4;
        Check(Generic.PayPhase(p, card) && p.Hero.Health == 1 && CostRules.Quote(p, card).Rules.Length == 0,
            "identical replacements charge once and consume together");
        var stale = CostRules.Quote(p, other); other.Cost = other.Cost + 1;
        int mana = p.RemainingMana;
        Check(!CostRules.CommitPayment(p, other, stale) && p.RemainingMana == mana, "stale quote rejected atomically");
        p.TemporaryMana = 2; other.Cost = 3;
        Check(Generic.PayPhase(p, other) && p.TemporaryMana == 0 && p.UsedMana == 1, "temporary mana spent first");
    }

    // R4: https://hearthstone.blizzard.com/en-us/news/23934449 (26.0: payment is not damage).
    // R5/R6: https://hearthstone.wiki.gg/wiki/Change_Cost_to_Health (strict Health; inclusive Armor).
    private static void Intrinsic(bool history)
    {
        foreach (var pair in new[] { ("TIME_612", CostPaymentKind.Health), ("BE_042", CostPaymentKind.Armor) })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            // With the export the definition is rebuilt from the client's row; without it the engine's own card stands in.
            var definition = ClientExport.Present ? Definition(pair.Item1) : Cards.FromId(pair.Item1);
            // Export-verified, unregistered definitions; no card-module admission.
            Check(definition[GameTag.CARD_ALTERNATE_COST] == (int)pair.Item2, "pinned intrinsic tag");
            var card = Generic.DrawCard(p, definition);
            Effects.ReduceCost(1).ApplyTo(card);
            Check(CostRules.Quote(p, card).Kind == pair.Item2 && CostRules.Quote(p, card).Amount == card.Cost,
                "intrinsic resource uses discounted amount");
        }
    }

    private static void HeroPowerFailure(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        p.BaseMana = 0; int hp = p.Hero.Health, uses = p.HeroPowerActivationsThisTurn;
        Check(!Generic.HeroPower(p, p.Opponent.Hero, skipPrePhase: true) && p.HeroPowerActivationsThisTurn == uses
            && !p.Hero.HeroPower.IsExhausted && p.Hero.Health == hp, "failed HP payment stops all effects");
        CostRules.AddNextHeroPowerRule(p, setCost: 0);
        Check(p.Hero.HeroPower.Cost == 0 && Generic.HeroPower(p, p.Opponent.Hero)
            && CostRules.Quote(p, p.Hero.HeroPower).Rules.Length == 0 && p.Hero.HeroPower.Cost == 2,
            "HP zero consumed at activation commitment");
    }
    private static void PlayBoundaries(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var secret = (Spell)Entity.FromCard(p.Opponent, Cards.FromId("EX1_287")); p.Opponent.SecretZone.Add(secret); secret.Power.Trigger.Activate(secret);
        var spell = Generic.DrawCard(p, Cards.FromId("CS2_029")); spell.Cost = 3;
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, cardType: CardType.SPELL);
        p.BaseMana = 0; int targetHealth = p.Opponent.Hero.Health;
        Check(game.Process(PlayCardTask.Any(p, spell, p.Opponent.Hero)) && p.Hero.Health == 27
            && p.Opponent.Hero.Health == targetHealth && p.Opponent.SecretZone.Count == 0
            && game.CostState.Rules.Count == 0, "countered spell pays and consumes before cast");

        game = HeroDivineShieldProof.NewGame(history); p = game.Player1;
        CostRules.AddNextCardRule(p, CostPaymentKind.Health);
        var trade = Generic.DrawCard(p, Cards.FromId("TLC_255"));
        Check(game.Process(TradeCardTask.Any(p, trade)) && game.CostState.Rules.Count == 1, "Trade does not consume");
        var prepare = Generic.DrawCard(p, Cards.FromId("CS2_182")); PrepareRules.Grant(prepare);
        Check(game.Process(PrepareCardTask.Any(p, prepare)) && game.CostState.Rules.Count == 1, "Prepare does not consume");
        var summon = (Minion)Entity.FromCard(p, Cards.FromId("CS2_231"));
        Generic.SummonBlock(game, summon, -1, p.Hero);
        var cast = (Spell)Entity.FromCard(p, Cards.FromId("CS2_029"));
        Generic.CastSpell(p, game, cast, p.Opponent.Hero, 0);
        Check(game.CostState.Rules.Count == 1 && p.Hero.Health == 30, "generated summon/indirect cast never pay or consume");

        game = HeroDivineShieldProof.NewGame(history); p = game.Player1;
        var hp = p.Hero.HeroPower; var definition = hp.Card.Clone();
        definition.Power = new Power { PowerTask = new PaymentObservedTask() }; hp.Card = definition;
        CostRules.AddNextHeroPowerRule(p, setCost: 0);
        Check(Generic.HeroPower(p, p.Opponent.Hero) && hp[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "one-use HP consumed before nested effect activation");

        game = HeroDivineShieldProof.NewGame(history); p = game.Player1;
        var legacy = Generic.DrawCard(p, Cards.FromId("CS2_029")); legacy.Cost = 3;
        new Effect(GameTag.CARD_COSTS_HEALTH, EffectOperator.SET, 1).ApplyAuraTo(legacy);
        p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 2;
        Check(Generic.PayPhase(p, legacy) && p.Hero.Health == 27 && p.RemainingMana == 10,
            "overlapping legacy Health sources resolve once");
    }

    private static void LegacyHideStatOptions(bool history)
    {
        foreach (bool cardAura in new[] { false, true })
        foreach (int cost in new[] { 0, 3 })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            var scroll = Generic.DrawCard(p, Cards.FromId("LOOT_104")); scroll.Cost = cost;
            bool Offered() => p.Options().Any(t => t is PlayCardTask && t.Source == scroll);
            Check(scroll.Card.HideStat && !Offered(), "HideStat suppresses affordable Mana option");
            p.BaseMana = 0;
            if (cardAura) new Effect(GameTag.CARD_COSTS_HEALTH, EffectOperator.SET, 1).ApplyAuraTo(scroll);
            else p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1;
            foreach (int health in new[] { 1, 3, 4 })
            {
                p.Hero.Health = health;
                Check(Offered() == (health > cost),
                    $"legacy HideStat Health option: cardAura={cardAura}, cost={cost}, health={health}, history={history}");
            }
        }
    }

    private sealed class PaymentObservedTask : SimpleTask
    {
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            Check(!CostRules.Quote(controller, controller.Hero.HeroPower).Rules.Any(), "next HP removed before effects");
            source[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            return TaskState.COMPLETE;
        }
    }

    private static Card Definition(string id)
    {
        var row = ClientExport.RequireCards()[id];
        var xml = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("CardDefs", new System.Xml.Linq.XElement("Entity",
            new System.Xml.Linq.XAttribute("CardID", id), new System.Xml.Linq.XAttribute("ID", row.GetProperty("dbfId").GetInt32()),
            row.GetProperty("tags").EnumerateObject().Select(t => new System.Xml.Linq.XElement("Tag",
                new System.Xml.Linq.XAttribute("enumID", t.Name), new System.Xml.Linq.XAttribute("value", t.Value.GetInt32()))))));
        return SabberStoneCore.Loader.CardLoader.LoadDefinitions(xml, overlay: true).Single();
    }

}
