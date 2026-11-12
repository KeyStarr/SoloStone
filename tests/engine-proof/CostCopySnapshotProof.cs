using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System.Runtime.Serialization;

internal static class CostCopySnapshotProof
{
    private static void Check(bool value, string label) => CostRulesProof.Check(value, "copy/snapshot: " + label);
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            Copy(history); Clone(history); OfflinePoolTestRecipes.WithDeclarations(() => Discover(history)); Deck(history); AuraClone(history);
        }
        Console.WriteLine("PASS cost copy/snapshot: independent handles/clone; recipient auras; deck changes/draw; suspended Discover/expiry/schema");
    }

    private static void Copy(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Effects.ReduceCost(1).ApplyTo(card);
        new Effect(GameTag.COST, EffectOperator.ADD, 7).ApplyAuraTo(card);
        long temp = CostRules.SetCostThisTurn(card, 0);
        long payment = CostRules.AddPaymentOverride(card, CostPaymentKind.Health, game.Turn);
        var copy = (Playable)Generic.Copy(p.Opponent, p.Hero, card, Zone.HAND);
        Check(card.Cost == 7 && copy.Cost == 0 && CostRules.PaymentKind(copy) == CostPaymentKind.Health,
            "copy ledger/payment, excludes source aura");
        Check(card._costOverlay.Modifiers.Select(m => m.Handle).Intersect(copy._costOverlay.Modifiers.Select(m => m.Handle)).Count() == 0,
            "copy allocates independent application identities");
        new Effect(GameTag.COST, EffectOperator.ADD, 2).ApplyAuraTo(copy);
        CostRules.RemoveApplication(card, temp); CostRules.RemoveApplication(card, payment);
        Check(copy.Cost == 2 && CostRules.PaymentKind(copy) == CostPaymentKind.Health && card.Cost == 10, "copy independent");
        game.Process(EndTurnTask.Any(p));
        Check(copy.Cost == 5 && CostRules.PaymentKind(copy) == CostPaymentKind.Mana, "copy retains absolute expiry turn");
        var fresh = Generic.Copy(p, p.Hero, card, Zone.DECK, initialize: c => c.Cost = 2);
        Check(fresh.Cost == 2, "fresh backward-zone copy SET 2");
    }

    private static void Clone(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        long reduction = CostRules.AddModifier(card, EffectOperator.SUB, 1, game.Turn);
        CostRules.SetCostThisTurn(card, 0); CostRules.AddPaymentOverride(card, CostPaymentKind.Health, game.Turn);
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, maxEffectiveCost: 3);
        foreach (var resumed in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var target = (Playable)resumed.IdEntityDic[card.Id];
            Check(target.Cost == 0 && CostRules.Quote(resumed.Player1, target).Rules.Length == 1, "restored semantic state");
            CostRules.RemoveApplication(target, reduction);
            Check(card._costOverlay.Modifiers.Count == 2, "independent modifier list");
            Check(Generic.PayPhase(resumed.Player1, target) && CostRules.Quote(p, card).Rules.Length == 1, "independent rule consumption");
            resumed.Process(EndTurnTask.Any(resumed.Player1)); Check(target.Cost == 4 && card.Cost == 0, "independent expiry");
            long next = CostRules.AddModifier(target, EffectOperator.ADD, 1, resumed.Turn);
            Check(next > game.CostState.Counter && game.CostState.Counter == 4, "counter restored and independent");
        }
    }

    private static void Discover(bool history) => OfflinePoolTestRecipes.WithSource(OfflinePoolTestRecipes.Small,
        definition => DiscoverState(history, definition));

    private static void DiscoverState(bool history, Card definition)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var held = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_029"));
        Effects.ReduceCost(1).ApplyTo(held); CostRules.SetCostThisTurn(held, 0);
        CostRules.AddPaymentOverride(held, CostPaymentKind.Health, game.Turn);
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, maxEffectiveCost: 3);
        var discover = Generic.DrawCard(p, definition); discover.Cost = 0;
        // Nonmatching next-spell rule survives the fixture minion's suspended Discover.
        game.CostState.Rules.Clear(); CostRules.AddNextCardRule(p, CostPaymentKind.Health, cardType: CardType.SPELL, maxEffectiveCost: 3);
        Check(game.Process(PlayCardTask.Any(p, discover)) && p.Choice != null, "suspended Discover");
        byte[] bytes = game.SaveSnapshot();
        var loaded = Game.LoadSnapshot(bytes); var clone = game.CloneExact(history);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "semantic schema byte round trip");
        foreach (var resumed in new[] { game, loaded, clone })
        {
            var actor = resumed.Player1;
            var pending = resumed.GetPendingDecisions().Single();
            Check(resumed.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId,
                new[] { pending.Options[0].OptionId }), out var error), "answer restored Discover: " + error);
            var target = resumed.IdEntityDic[held.Id];
            Check(target.Cost == 0 && CostRules.PaymentKind(target) == CostPaymentKind.Health
                && CostRules.Quote(actor, target).Rules.Length == 1, "cost state survives continuation");
            Check(Generic.PayPhase(actor, target) && target[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 0, "answer then zero commit");
            resumed.Process(EndTurnTask.Any(actor));
            Check(target.Cost == target.Card.Cost - 1 && CostRules.PaymentKind(target) == CostPaymentKind.Mana, "same restored expiry");
        }
        Check(Canonical.State(game) == Canonical.State(loaded) && Canonical.State(game) == Canonical.State(clone)
            && game.Random.CaptureState().Equals(loaded.Random.CaptureState()), "identical outcome and RNG");
        bytes["SabberStone/decision-snapshot".Length + 1] ^= 0x40;
        bool rejected = false; try { Game.LoadSnapshot(bytes); } catch (SerializationException) { rejected = true; }
        Check(rejected, "incompatible snapshot rejected");
    }

    private static void Deck(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        foreach (var item in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(item));
        var deck = new[] { "CS2_182", "CS2_231", "CS2_120" }.Select(id => Entity.FromCard(p, Cards.FromId(id))).ToArray();
        foreach (var item in deck) p.DeckZone.Add(item);
        var order = p.DeckZone.Select(c => c.Id).ToArray();
        // Krona's primitive use: mutate existing bottom min(5, Count), never shuffle or create replacements.
        foreach (var item in p.DeckZone.Reverse().Take(5)) Effects.SetCost(1).ApplyTo(item);
        foreach (var item in p.HandZone.Concat(p.DeckZone).ToArray())
            CostRules.AddModifier((Playable)item, item is Spell ? EffectOperator.ADD : EffectOperator.SUB, 1);
        var later = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(later);
        Check(order.SequenceEqual(p.DeckZone.Take(3).Select(c => c.Id)) && later.Cost == 4, "short bottom order/existing entities only");
        var source = p.DeckZone.First();
        var copied = Generic.Copy(p.Opponent, p.Hero, source, Zone.HAND);
        Check(copied.Cost == 0, "deck-to-hand copy retains permanent changes");
        var drawn = Generic.Draw(p, source);
        Check(drawn != null && drawn.Cost == 0, "deck modifiers survive draw");
    }

    private static void AuraClone(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var source = HeroDivineShieldProof.Summon(game, "CS2_231");
        new ConditionalHeroPowerCostAura(10, 0).Activate(source);
        var held = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182")); new HandAdjacencyCostAura(1).Activate(held);
        foreach (var resumed in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            ((Minion)resumed.IdEntityDic[source.Id]).Silence();
            resumed.Player1.SetasideZone.Add(resumed.Player1.HandZone.Remove(resumed.IdEntityDic[held.Id])); resumed.AuraUpdate();
            Check(resumed.Player1.Hero.HeroPower.Cost == 2 && p.Hero.HeroPower.Cost == 0 && held.OngoingEffect != null,
                "narrow aura state independently cloned/restored");
        }
    }
}
