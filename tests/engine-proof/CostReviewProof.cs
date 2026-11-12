using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CostReviewProof
{
    private static void Check(bool value, string label) => CostRulesProof.Check(value, label);
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (bool history in new[] { false, true })
        foreach (var proof in new Action<bool>[] { WarmSetAura, ScalarCostWrite, RemovedTransform, RetainedTransform, PortalCopy, PortalExpiry,
            SameIdAdjacency, OngoingApplications, EffectivePredicate, RetireActors,
            RetainedTransformOverlay, NextRuleZones, TransformAdjacencySource, RetainedPlayAffordability })
        {
            try { proof(history); }
            catch (Exception e) { failures.Add(proof.Method.Name + "/history=" + history + ": " + e.Message); }
        }
        foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
        Check(failures.Count == 0, "review regressions=" + failures.Count);
        Console.WriteLine("PASS cost review: WarmSetAura; ScalarCostWrite; RemovedTransform; RetainedTransform; PortalCopy; PortalExpiry; SameIdAdjacency; OngoingApplications; EffectivePredicate; RetireActors; RetainedTransformOverlay; NextRuleZones; TransformAdjacencySource; RetainedPlayAffordability");
    }

    private static void ScalarCostWrite(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var card = (Playable)Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        Effects.ReduceCost(1).ApplyTo(card);
        Check(card.Cost == 3, "warm permanent-only discount");
        card.Cost = 8;
        foreach (var candidate in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            Check(candidate.IdEntityDic[card.Id].Cost == 8, "scalar write replaces warmed permanent-only cost");
    }

    private static void RemovedTransform(bool history)
    {
        foreach (string identity in new[] { "CS2_120", "CS2_029" })
        foreach (bool set in new[] { false, true })
        foreach (bool adjacent in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var source = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            var card = (Playable)Entity.FromCard(p, Cards.FromId("CS2_182"),
                zone: adjacent ? p.HandZone : p.DeckZone);
            if (set) Effects.SetCost(1).ApplyTo(card);
            else Effects.ReduceCost(2).ApplyTo(card);
            if (adjacent) new HandAdjacencyCostAura(1).Activate(source);
            Check(card.Cost == (set ? 1 : 2) + (adjacent ? 1 : 0), "warm discounted transform input");
            var transformed = Generic.ChangeEntityBlock(p, card, Cards.FromId(identity), true);
            game.AuraUpdate();
            int printed = identity == "CS2_120" ? 2 : 4;
            foreach (var candidate in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var replacement = candidate.IdEntityDic[card.Id];
                Check(replacement.Card.Id == identity && replacement.Cost == printed + (adjacent ? 1 : 0),
                    "remove-enchantments transform restores printed cost and keeps external tax");
                if (!adjacent) continue;
                candidate.Player1.SetasideZone.Add(candidate.Player1.HandZone.Remove(candidate.IdEntityDic[source.Id]));
                Check(replacement.Cost == printed, "transform tax detaches exactly once");
            }
        }
    }

    private static void RetainedTransformOverlay(bool history)
    {
        foreach (string identity in new[] { "CS2_120", "CS2_024" })
        foreach (bool applyBefore in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var source = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            var target = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
            new HandAdjacencyCostAura(1).Activate(source);
            Check(target.Cost == 5, "warm neighbour tax before retained transform");
            if (applyBefore) CostRules.AddModifier(target, EffectOperator.SUB, 1, game.Turn);
            target = (Playable)Generic.ChangeEntityBlock(p, target, Cards.FromId(identity), false);
            if (!applyBefore) CostRules.AddModifier(target, EffectOperator.SUB, 1, game.Turn);
            game.AuraUpdate();
            Check(target.Cost == 2, "retained printed 2 minus temporary 1 plus adjacency 1");
            foreach (var restored in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var copy = (Playable)restored.IdEntityDic[target.Id];
                Check(copy.Cost == 2, "retained pre-aura input survives clone/snapshot");
                var owner = restored.Player1;
                owner.SetasideZone.Add(owner.HandZone.Remove(restored.IdEntityDic[source.Id]));
                Check(copy.Cost == 1 && target.Cost == 2, "restored adjacency removal independent of overlay");
                CostRules.ExpireTurn(restored, restored.Turn);
                Check(copy.Cost == 2, "expiry preserves legacy transform cache after aura removal");
            }
            Effects.ReduceCost(1).ApplyTo(target);
            Check(target.Cost == 1, "permanent reduction updates retained pre-aura input");
            CostRules.ExpireTurn(game, game.Turn);
            Check(target.Cost == 2, "expiry preserves legacy retained-transform scalar/cache");
        }
    }

    private static void NextRuleZones(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var deck = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(deck);
        var grave = Entity.FromCard(p, Cards.FromId("CS2_029")); p.GraveyardZone.Add(grave);
        var hand = Generic.DrawCard(p, Cards.FromId("CS2_029"));
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, cardType: CardType.SPELL, setCost: 1);
        Check(hand.Cost == 1 && CostRules.PaymentKind(hand) == CostPaymentKind.Health,
            "next spell applies to hand candidate");
        Check(deck.Cost == 4 && grave.Cost == 4 && CostRules.PaymentKind(deck) == CostPaymentKind.Mana
            && CostRules.Quote(p, grave).Rules.Length == 0, "next spell excludes deck/grave numeric and resource costs");
        var blackhorn = Generic.DrawCard(p, Cards.FromId("CATA_720")); blackhorn.Cost = 0;
        Check(game.Process(PlayCardTask.Any(p, blackhorn)) && p.DeckZone.Contains(deck)
            && deck.Cost == 4 && game.CostState.Rules.Count == 1, "Blackhorn keeps expensive deck spell under next-spell SET");
        var board = HeroDivineShieldProof.Summon(game, "CS2_182");
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, cardType: CardType.MINION, setCost: 1);
        Check(board.Cost == 4 && blackhorn.Cost == 7 && CostRules.Quote(p, board).Rules.Length == 0
            && CostRules.PaymentKind(board) == CostPaymentKind.Mana, "next minion excludes board");
        var active = p.Hero.HeroPower;
        var inactive = (HeroPower)Entity.FromCard(p, active.Card); p.SetasideZone.Add(inactive);
        CostRules.AddNextHeroPowerRule(p, setCost: 0, kind: CostPaymentKind.Health);
        Check(active.Cost == 0 && CostRules.Quote(p, active).Rules.Length == 1
            && inactive.Cost == 2 && CostRules.Quote(p, inactive).Rules.Length == 0,
            "next HP matches only active power");
        p.Hero.HeroPower = inactive;
        Check(active.Cost == 2 && CostRules.PaymentKind(active) == CostPaymentKind.Mana
            && inactive.Cost == 0, "next HP follows replacement identity");
    }

    private static void TransformAdjacencySource(bool history)
    {
        foreach (bool remove in new[] { false, true })
        foreach (string identity in new[] { "CS2_182", "CS2_029" })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var left = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            var source = Generic.DrawCard(p, Cards.FromId(identity));
            var right = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            // Primitive fixture only; no card-module registration.
            var sabotage = Cards.FromId("CS2_008").Clone();
            sabotage.Power = new Power { Aura = new HandAdjacencyCostAura(1) };
            source = Generic.ChangeEntityBlock(p, source, sabotage, remove); game.AuraUpdate();
            Check(source.OngoingEffect is HandAdjacencyCostAura && left.Cost == 5 && right.Cost == 5,
                "transform into adjacency source activates both neighbours");
            p.SetasideZone.Add(p.HandZone.Remove(source)); game.AuraUpdate();
            Check(source.OngoingEffect == null && left.Cost == 4 && right.Cost == 4,
                "transformed source removal ends contributions exactly once");
        }
    }

    // Caller audit: player options are hand-only; indirect casts/summons and decision resumes bypass PlayCardBlock.
    private static void RetainedPlayAffordability(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var option = p.Options().OfType<PlayCardTask>().First(t => t.Source == card);
        CostRules.AddNextCardRule(p, CostPaymentKind.Health, cardType: CardType.MINION);
        p.Hero.Health = 4;
        int mana = p.RemainingMana, plays = p.NumCardsPlayedThisTurn;
        Check(!game.Process(option) && card.Zone == p.HandZone && p.Hero.Health == 4
            && p.RemainingMana == mana && p.NumCardsPlayedThisTurn == plays && game.CostState.Rules.Count == 1,
            "retained option rechecks affordability before payment or play effects");
    }

    // Differential expectations from 3020e77: cache arithmetic, not replay of SET aura.
    private static void WarmSetAura(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var card = (Playable)Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        Effects.SetCost(1).ApplyAuraTo(card);
        Check(card.Cost == 1, "warm Aviana-style SET aura");
        Effects.ReduceCost(1).ApplyTo(card);
        Check(card.Cost == 0, "HEAD cached SET 1 then ReduceCost 1 is zero");
        card.Cost = 8;
        Check(card.Cost == 0, "HEAD setter preserves warmed cache");
    }

    private static void RetainedTransform(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Entity.FromCard(p, Cards.FromId("CS2_168")); p.DeckZone.Add(card);
        Check(card.Cost == 1, "Liam selection warms deck scalar");
        Generic.ChangeEntityBlock(p, card, Cards.FromId("CS2_182"), false);
        Check(card.Cost == 1, "HEAD Liam retained transform keeps cached deck cost");
        Effects.AddCost(1).ApplyAuraTo(card);
        Check(card.Cost == 2, "warm retained transform manager");
        Effects.ReduceCost(1).ApplyTo(card);
        Check(card.Cost == 1, "permanent reduction records against existing manager");
        Generic.ChangeEntityBlock(p, card, Cards.FromId("CS2_120"), false);
        Check(card.Cost == 2, "HEAD retained transform rebases recorded permanent enchantment then aura");
        var temporary = (Playable)Entity.FromCard(p, Cards.FromId("CS2_168")); p.DeckZone.Add(temporary);
        Check(temporary.Cost == 1, "warm scalar before overlay-only retained transform");
        CostRules.SetCostThisTurn(temporary, 0);
        Generic.ChangeEntityBlock(p, temporary, Cards.FromId("CS2_182"), false);
        Check(temporary.Cost == 0, "temporary survives retained transform");
        CostRules.ExpireTurn(game, game.Turn);
        Check(temporary.Cost == 1, "overlay does not create a legacy cost manager or rebase cached scalar");
    }

    private static void PortalCopy(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        HeroDivineShieldProof.Summon(game, "EX1_315"); game.AuraUpdate();
        Check(card.Cost == 2, "Portal source discount");
        var copy = Generic.Copy(p.Opponent, p.Hero, card, Zone.HAND);
        // Source-bound Portal reductions do not follow a hand copy to the other controller.
        Check(copy.Cost == 4, "Portal hand copy recalculates recipient auras");
        Check(((Playable)copy)._costManager == null, "Portal must not create transferable overlay SET");
    }

    private static void PortalExpiry(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        CostRules.SetCostThisTurn(card, 0);
        HeroDivineShieldProof.Summon(game, "EX1_315"); game.AuraUpdate();
        Check(card.Cost == 0, "temporary SET overlays Portal scalar");
        CostRules.ExpireTurn(game, game.Turn);
        Check(card.Cost == 2, "Portal scalar restored after temporary SET expiry");
    }

    private static void SameIdAdjacency(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var source = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var target = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        new HandAdjacencyCostAura(1).Activate(source);
        Check(target.Cost == 5, "tax before transform"); int id = target.Id;
        var transformed = Generic.ChangeEntityBlock(p, target, Cards.FromId("CS2_120"), true); game.AuraUpdate();
        Check(transformed.Id == id && transformed.Cost == 3, "same-ID transform rebinds neighbour tax");
        ((Playable)transformed).ResetCost(); game.AuraUpdate();
        Check(transformed.Cost == 3, "cost reset rebinds neighbour tax");
        p.SetasideZone.Add(p.HandZone.Remove(source));
        Check(transformed.Cost == 2, "rebound tax removed exactly once");
    }

    private static void OngoingApplications(bool history)
    {
        foreach (var pair in new[] { ("AT_041", "AT_041e", 7), ("OG_138", "OG_138e", 6), ("DAL_561", "DAL_561e", 10) })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            var card = Generic.DrawCard(p, Cards.FromId(pair.Item1)); var enchant = Cards.FromId(pair.Item2);
            Check(enchant.Power.Enchant is OngoingEnchant, "registered ongoing recipe " + pair.Item1);
            Generic.AddEnchantmentBlock(game, enchant, p.Hero, card);
            Check(card.OngoingEffect is OngoingEnchant, "virtual ongoing activation " + pair.Item1);
            Check(card.Cost == pair.Item3 - 1, "initial ongoing application");
            Generic.AddEnchantmentBlock(game, enchant, p.Hero, card);
            Generic.AddEnchantmentBlock(game, enchant, p.Hero, card);
            Check(((OngoingEnchant)card.OngoingEffect).Count == 3 && card.Cost == pair.Item3 - 1,
                "repeated applications wait for aura stabilization");
            Check((card.AppliedEnchantments?.Count ?? 0) == (history ? 1 : 0), "single presentation actor");
            game.AuraUpdate(); Check(card.Cost == pair.Item3 - 3, "ongoing accumulated delta");
        }
    }

    private static void EffectivePredicate(bool history)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            var card = Generic.DrawCard(p, Cards.FromId("CS2_168")); card.Cost = 5;
            if (reverse) CostRules.AddNextCardRule(p, CostPaymentKind.Health, race: Race.MURLOC, maxEffectiveCost: 3);
            CostRules.AddNextCardRule(p, setCost: 1);
            if (!reverse) CostRules.AddNextCardRule(p, CostPaymentKind.Health, race: Race.MURLOC, maxEffectiveCost: 3);
            var quote = CostRules.Quote(p, card);
            Check(card.Cost == 1 && quote.Amount == 1 && quote.Kind == CostPaymentKind.Health && quote.Rules.Length == 2,
                "resource predicate sees final controller numeric cost");
            Check(CostRules.CommitPayment(p, card, quote) && p.Hero.Health == 29 && game.CostState.Rules.Count == 0,
                "freeze and consume both matching handles");
        }
    }

    private static void RetireActors(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var hp = p.Hero.HeroPower;
        var actor = Enchantment.GetInstance(p, p.Hero, hp, Cards.FromId("CS2_087e"));
        CostRules.AddNextHeroPowerRule(p, setCost: 0, enchantmentId: actor.Id);
        Check(Generic.PayPhase(p, hp) && !hp.AppliedEnchantments.Contains(actor), "consumption retires persistent HP actor");
        var expiring = Enchantment.GetInstance(p, p.Hero, hp, Cards.FromId("CS2_087e"));
        CostRules.AddNextHeroPowerRule(p, setCost: 0, expiryTurn: game.Turn, enchantmentId: expiring.Id);
        CostRules.ExpireTurn(game, game.Turn);
        Check(!hp.AppliedEnchantments.Contains(expiring) && hp.Cost == 2, "expiry retires HP rule and actor");
        var bound = Enchantment.GetInstance(p, p.Hero, hp, Cards.FromId("CS2_087e"));
        long cost = CostRules.AddModifier(hp, EffectOperator.SET, 0, game.Turn, bound.Id);
        long payment = CostRules.AddPaymentOverride(hp, CostPaymentKind.Health, enchantmentId: bound.Id);
        CostRules.RemoveApplication(hp, cost);
        Check(hp.AppliedEnchantments.Contains(bound), "actor lives until last bound application ends");
        CostRules.RemoveApplication(hp, payment);
        Check(!hp.AppliedEnchantments.Contains(bound), "last application removal retires actor");
        if (history)
        {
            var definition = Cards.FromId("CS2_087e").Clone();
            definition.Tags[GameTag.TAG_ONE_TURN_EFFECT] = 1;
            definition.Power = new Power { Enchant = new Enchant(Effects.SetCost(0)) };
            Generic.AddEnchantmentBlock(game, definition, p.Hero, hp);
            var temporary = hp.AppliedEnchantments.Single();
            Check(hp.Cost == 0, "card-tag-bound temporary SET");
            SabberStoneCore.Tasks.SimpleTasks.RemoveEnchantmentTask.Task.Process(game, p, temporary, hp);
            Check(hp.Cost == 2 && hp.AppliedEnchantments.Count == 0,
                "explicit temporary actor removal uses handle, never generic SET inverse");
        }
    }
}
