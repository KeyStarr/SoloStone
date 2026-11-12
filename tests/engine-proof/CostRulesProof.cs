using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CostRulesProof
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("CostRules: " + message);
    }
    internal static void Run()
    {
        CostReviewProof.Run();
        foreach (bool history in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var card = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
            Effects.ReduceCost(1).ApplyTo(card);
            Effects.SetCost(0).ApplyTo(card, true);
            Check(card.Cost == 0, "temporary SET applies");
            Check(game.Process(EndTurnTask.Any(game.Player1)), "end turn");
            Check(card.Cost == card.Card.Cost - 1, "temporary SET expiry retains older reduction");
        }
        Equivalence();
        foreach (bool history in new[] { false, true }) { IdentityExpiry(history); Enchantments(history); AuraConditions(history); }
        Console.WriteLine("PASS cost ledger: 59049 mixed histories; identity/cache/expiry; raw COST; conditional HP/hand adjacency");
    }
    private static void Equivalence()
    {
        // Compare against pre-ledger scalar arithmetic, including signed intermediate values.
        var game = HeroDivineShieldProof.NewGame(false);
        var card = (Playable)Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        var operations = new[] { EffectOperator.ADD, EffectOperator.SUB, EffectOperator.SET };
        var values = new[] { -2, 0, 7 };
        for (int sequence = 0; sequence < 59049; sequence++)
        {
            card.ResetCost(); int old = card.Card.Cost, code = sequence;
            for (int step = 0; step < 5; step++)
            {
                int digit = code % 9; code /= 9;
                var op = operations[digit / 3]; int value = values[digit % 3];
                switch (op) { case EffectOperator.ADD: old += value; break;
                    case EffectOperator.SUB: old -= value; break; case EffectOperator.SET: old = value; break; }
                // Warm the cache between permanent scalar applications, as at HEAD.
                CostRules.AddModifier(card, op, value);
                Check(card.Cost == Math.Max(0, old), "permanent scalar equivalence sequence=" + sequence);
            }
        }
        card.ResetCost(); card.Cost = 5;
        Effects.ReduceCost(7).ApplyTo(card); Effects.AddCost(3).ApplyTo(card);
        Check(card.Cost == 1, "signed reduction survives final floor");
    }

    private static void IdentityExpiry(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var card = (Playable)Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        long first = CostRules.AddModifier(card, EffectOperator.SUB, 1, game.Turn);
        long second = CostRules.AddModifier(card, EffectOperator.SUB, 1, game.Turn);
        Check(first != second && card.Cost == 2, "identical applications have distinct handles");
        CostRules.RemoveApplication(card, first); Check(card.Cost == 3, "remove first independently");
        CostRules.RemoveApplication(card, first); Check(card.Cost == 3, "repeated remove harmless");
        CostRules.SetCostThisTurn(card, 0); card.Cost = 2;
        // A9: temporary SET remains above a later permanent SET; chronological cross-layer ordering is deferred.
        Check(card.Cost == 0, "A9 temporary SET overlays later permanent SET");
        game.Process(EndTurnTask.Any(game.Player1));
        Check(card.Cost == 2, "later SET survives older temporary SET expiry");
        CostRules.RemoveApplication(card, second); Check(card.Cost == 2, "SET dominates remaining earlier modifier");
        var aura = new Effect(GameTag.COST, EffectOperator.ADD, 3);
        aura.ApplyAuraTo(card); Check(card.Cost == 5, "raw aura COST routes to existing aura store");
        aura.RemoveAuraFrom(card); Check(card.Cost == 2, "warm aura removal recalculates");
        new Effect(GameTag.COST, EffectOperator.SET, 0).ApplyTo(card, true);
        Check(game.OneTurnEffects.All(e => e.effect.Tag != GameTag.COST), "single cleanup owner");
        game.Process(EndTurnTask.Any(game.Player2)); Check(card.Cost == 2, "raw SET expiry no double undo");
        var giant = (Playable)Generic.DrawCard(game.CurrentPlayer, Cards.FromId("EX1_105"));
        giant.Cost = 5; game.AuraUpdate();
        Check(giant.Cost == Math.Max(0, 5 - game.CurrentPlayer.HandZone.Count + 1), "own adaptive text last");
    }

    private static void AuraConditions(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var source = HeroDivineShieldProof.Summon(game, "CS2_231");
        new ConditionalHeroPowerCostAura(3, 0).Activate(source);
        var cards = Enumerable.Range(0, 3).Select(_ => Generic.DrawCard(p, Cards.FromId("CS2_182"))).ToArray();
        Check(p.Hero.HeroPower.Cost == 0, "Fletcher hand count 3");
        var fourth = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Check(p.Hero.HeroPower.Cost == 2, "Fletcher hand count 4");
        p.SetasideZone.Add(p.HandZone.Remove(fourth));
        Check(p.Hero.HeroPower.Cost == 0, "Fletcher returns to hand count 3");
        var old = p.Hero.HeroPower;
        p.Hero.HeroPower = (HeroPower)Entity.FromCard(p, Cards.FromId("CS1h_001"));
        Check(p.Hero.HeroPower.Cost == 0 && old.Cost == old.Card.Cost, "HP replacement rebinds conditional aura");
        source.Silence(); game.AuraUpdate(); Check(p.Hero.HeroPower.Cost == 2, "silence removes HP contribution");
        new HandAdjacencyCostAura(1).Activate(cards[1]);
        Check(cards[0].Cost == 5 && cards[1].Cost == 4 && cards[2].Cost == 5, "adjacency center");
        p.HandZone.Swap(cards[1], cards[0]);
        Check(cards[0].Cost == 5 && cards[1].Cost == 4 && cards[2].Cost == 4, "adjacency reorder at edge");
        p.SetasideZone.Add(p.HandZone.Remove(cards[1]));
        Check(cards[0].Cost == 4 && cards[2].Cost == 4, "adjacency source removal");
    }

    private static void Enchantments(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var target = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var first = Enchantment.GetInstance(p, p.Hero, target, Cards.FromId("CS2_087e"));
        var second = Enchantment.GetInstance(p, p.Hero, target, Cards.FromId("CS2_087e"));
        CostRules.AddModifier(target, EffectOperator.SUB, 1, game.Turn, enchantmentId: first.Id);
        CostRules.AddModifier(target, EffectOperator.SUB, 1, game.Turn, enchantmentId: second.Id);
        first.Remove(); Check(target.Cost == 3, "same-value enchantment removal is bound to owner");
        new Enchant(GameTag.COST, EffectOperator.SET, 0) { IsOneTurnEffect = true }.ActivateTo(target);
        target.Cost = 2;
        game.Process(EndTurnTask.Any(p)); Check(target.Cost == 2, "raw enchantment temporary SET and later permanent SET");
        second.Remove(); Check(target.Cost == 2, "expired enchantment removal does not alter permanent SET");
        var transformed = Generic.ChangeEntityBlock(game.Player1, target, Cards.FromId("CS2_120"), true);
        Check(transformed.Cost == 2 && ((Playable)transformed)._costOverlay.Modifiers.Count == 0, "normal transform clears temporary overlay");
    }

}
