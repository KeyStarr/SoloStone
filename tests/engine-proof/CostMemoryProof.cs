using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CostMemoryProof
{
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var card = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
            card.Cost = 2;
            CostRulesProof.Check(game.Process(PlayCardTask.Any(game.Player1, card)), "cost memory play");
            var fact = game.HistoryFacts.Query.Plays().Last();
            CostRulesProof.Check(fact.PaidCost == 2,
                "cost memory: immutable paid Cost missing after hand Cost resets");
            foreach (var branch in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
                CostRulesProof.Check(branch.HistoryFacts.Query.Plays().Last().PaidCost == 2,
                    "paid Cost survives clone/snapshot");
        }
        foreach (bool history in new[] { false, true })
        {
            PaymentAndDeath(history);
            Restoration(history);
            Affordability(history);
            GeneratedHandCost(history);
            ScalarAuraMemory(history);
            DefinitionChanges(history);
            BackwardMovement(history);
        }
        var report = SabberStoneCore.Model.OfflinePoolAdmission.Evaluate(SabberStoneCore.Model.OfflinePoolPolicy.Load().BuildManifest());
        foreach (var card in report.Cards.Where(c => new[] { "EDR_493", "JAIL_986", "CORE_CATA_006" }.Contains(c.CardId)))
        {
            CostRulesProof.Check(card.PoolRequirements.All(r => r.Count >= 3), card.CardId + " all class/binding pools admitted");
            Console.WriteLine("POOL " + card.CardId + " " + string.Join(";", card.PoolRequirements.Select(r => r.Class + "=" + r.Count)));
        }
        Console.WriteLine("PASS cost memory: paid/death values; zero/Health; clone/snapshot; restoration/expiry/Silence; pure affordability");
    }
    internal static void DefinitionChanges(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        p.BaseMana = 6;
        var pick = (Minion)Generic.DrawCard(p, Cards.FromId("JAIL_501")); game.AuraUpdate();
        CostRulesProof.Check(pick.AttackDamage == 6 && pick.Health == 6, "six-Mana transform source");
        foreach (var branch in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var actor = branch.IdEntityDic[pick.Id]; var owner = branch.Player1;
            actor = Generic.ChangeEntityBlock(owner, actor, Cards.FromId("CS2_231"), true);
            owner.BaseMana = 3;
            actor = Generic.ChangeEntityBlock(owner, actor, Cards.FromId("JAIL_501"), true); branch.AuraUpdate();
            var minion = (Minion)actor;
            CostRulesProof.Check(minion.AttackDamage == 3 && minion.Health == 3 && minion.Cost == 3,
                "transform round-trip rebases mana stats to new printed definition");
            // A transform to the same definition resets stats too.
            owner.BaseMana = 5;
            Generic.ChangeEntityBlock(owner, actor, actor.Card, true); branch.AuraUpdate();
            CostRulesProof.Check(minion.AttackDamage == 5 && minion.Health == 5 && minion.Cost == 5,
                "same-definition transform clears old mana baseline");
            actor = Generic.ChangeEntityBlock(owner, actor, Cards.FromId("BE_142"), true); branch.AuraUpdate();
            CostRulesProof.Check(actor.Cost == 5 && actor[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5,
                "cross-type mana definition receives fresh cost/text");
            actor = Generic.ChangeEntityBlock(owner, actor, Cards.FromId("JAIL_501"), true); branch.AuraUpdate();
            CostRulesProof.Check(((Minion)actor).AttackDamage == 5 && ((Minion)actor).Health == 5,
                "cross-type round-trip receives fresh stats");
        }
    }
    internal static void BackwardMovement(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var jail = (Minion)Generic.DrawCard(p, Cards.FromId("JAIL_453"));
        Generic.AddEnchantmentBlock(game, Cards.FromId("JAIL_453e"), p.Hero, jail, 20);
        CostRulesProof.Check(game.Process(PlayCardTask.Any(p, jail)) && jail.Cost == 0,
            "own Prepare reduction survives first forward play");
        foreach (var branch in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var actor = (Minion)branch.IdEntityDic[jail.Id]; var owner = branch.Player1;
            Generic.ReturnToHandBlock(owner, actor); branch.AuraUpdate();
            CostRulesProof.Check(actor.Cost == 5, "backward move resets displayed cost");
            CostRulesProof.Check(actor.AppliedEnchantments.All(e => e.Card.Id != "JAIL_453e"),
                "backward move retires old numeric attachments");
            owner.BaseMana = 10; owner.UsedMana = 0;
            CostRulesProof.Check(branch.Process(PlayCardTask.Any(owner, actor))
                && CostRules.LastPlayedCost(owner) == 5 && actor.Cost == 5,
                "replayed Jailbird must not regain expired residence discount");
            actor.Destroy(); branch.DeathProcessingAndAuraUpdate();
            CostRulesProof.Check(branch.HistoryFacts.Query.Deaths().Last().CostAtDeath == 5,
                "death rank uses fresh residence cost");
        }
        Console.WriteLine("PASS cost memory backward discount lifetime history=" + history);
    }
    private static void PaymentAndDeath(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Minion)Generic.DrawCard(p, Cards.FromId("CS2_200"));
        card.Cost = 5;
        CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        CostRulesProof.Check(game.Process(PlayCardTask.Any(p, card)) && p.Hero.Health == 25
            && CostRules.LastPlayedCost(p) == 5 && p.UsedMana == 0, "Health numeric Cost, not Mana spent");
        card.Cost = 2;
        CostRulesProof.Check(CostRules.LastPlayedCost(p) == 5, "later live mutation cannot rewrite payment");
        new Effect(GameTag.COST, EffectOperator.ADD, 3).ApplyAuraTo(card);
        card.Destroy(); game.DeathProcessingAndAuraUpdate();
        CostRulesProof.Check(game.HistoryFacts.Query.Deaths().Last().CostAtDeath == 5,
            "death Cost captured before offboard aura cleanup");
        foreach (var branch in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            CostRulesProof.Check(branch.HistoryFacts.Query.Deaths().Last().CostAtDeath == 5
                && CostRules.LastPlayedCost(branch.Player1) == 5, "independent death/payment snapshots");
        var zero = Generic.DrawCard(p, Cards.FromId("CS2_231"));
        CostRulesProof.Check(game.Process(PlayCardTask.Any(p, zero)) && CostRules.LastPlayedCost(p) == 0,
            "zero replaces previous paid Cost");
        var rejected = Generic.DrawCard(p, Cards.FromId("CS2_182")); rejected.Cost = 99;
        int facts = game.HistoryFacts.Count;
        CostRulesProof.Check(!game.Process(PlayCardTask.Any(p, rejected)) && game.HistoryFacts.Count == facts,
            "refused payment creates no play fact");
        CostRulesProof.Check(CostRules.StartingMinionCost(p) == 0, "original Wisp deck total");
    }
    private static void Restoration(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        CostRules.AddModifier(card, EffectOperator.SUB, 2);
        CostRules.AddModifier(card, EffectOperator.SET, 0, game.Turn);
        CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        new Effect(GameTag.COST, EffectOperator.SUB, 1).ApplyAuraTo(card);
        CostRulesProof.Check(CostRules.PersistentCost(card) == 0, "persistent snapshot excludes external aura");
        CostRules.RestorePrintedCost(card);
        CostRulesProof.Check(card.Cost == 3 && CostRules.PaymentKind(card) == CostPaymentKind.Health,
            "reset numeric state preserves external aura and payment replacement");
        Effects.ReduceCost(1).ApplyTo(card);
        CostRulesProof.Check(card.Cost == 2, "later reduction after reset");
        game.Process(EndTurnTask.Any(p));
        CostRulesProof.Check(card.Cost == 2, "removed overlay does not return on expiry");
        ((Minion)card).Silence();
        CostRulesProof.Check(card.Cost == 2, "Silence does not revive cleared overlay or remove external aura");
    }
    private static void Affordability(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        p.BaseMana = 3;
        HeroDivineShieldProof.Summon(game, "EX1_608");
        var candidates = new[] { Cards.FromId("CS2_029"), Cards.FromId("CS2_024") };
        byte[] before = game.SaveSnapshot(); long draws = game.Random.DrawCount;
        var affordable = CostRules.AffordableSpells(p, candidates);
        CostRulesProof.Check(affordable.Length == 2 && game.Random.DrawCount == draws
            && before.SequenceEqual(game.SaveSnapshot()), "definition affordability uses active discount without mutation/RNG");
        foreach (var branch in new[] { game.CloneExact(history), Game.LoadSnapshot(before) })
            CostRulesProof.Check(CostRules.AffordableSpells(branch.Player1, candidates).Select(c => c.Id)
                .SequenceEqual(affordable.Select(c => c.Id)), "affordability clone/snapshot");
        var owner = HeroDivineShieldProof.Summon(game, "CS2_231");
        new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.HAND,
            new Effect(GameTag.COST, EffectOperator.SET, 5)).Activate(owner);
        game.AuraUpdate();
        var actual = Generic.DrawCard(p, candidates[0]); game.AuraUpdate(); p.BaseMana = 4;
        CostRulesProof.Check(actual.Cost == 4, "mixed SET/subtract aura real hand Cost");
        CostRulesProof.Check(CostRules.AffordableSpells(p, new[] { candidates[0] }).Length == 1,
            "cost probe uses real aura application order");

    }
    private static void GeneratedHandCost(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var beam = Cards.FromId("CORE_BT_801"); p.BaseMana = 1;
        CostRulesProof.Check(CostRules.AffordableSpells(p, new[] { beam }).Length == 1,
            "fresh generated right-edge Outcast cost");
        var atlas = Cards.FromId("JAIL_514");
        p.BaseMana = Math.Max(0, atlas.Cost - p.HandZone.Count - 1);
        CostRulesProof.Check(CostRules.AffordableSpells(p, new[] { atlas }).Length == 1,
            "generated intrinsic hand-count discount includes entrant");
        new SabberStoneCore.Auras.HandAdjacencyCostAura(1).Activate(p.HandZone.Last());
        p.BaseMana = 4;
        CostRulesProof.Check(CostRules.AffordableSpells(p, new[] { Cards.FromId("CS2_029") }).Length == 0,
            "new hand slot receives removable adjacency tax");
        var actual = Generic.DrawCard(p, Cards.FromId("CS2_029")); game.AuraUpdate();
        CostRulesProof.Check(actual.Cost == 5, "actual entrant confirms hypothetical adjacency Cost");
        HeroDivineShieldProof.Summon(game, "EX1_315");
        CostRulesProof.Check(CostRules.AffordableSpells(p, new[] { Cards.FromId("CS2_029") }).Length == 1,
            "minion-only scalar aura does not affect spell probe");

    }

    private static void ScalarAuraMemory(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var portal = HeroDivineShieldProof.Summon(game, "EX1_315");
        CostRulesProof.Check(card.Cost == 2 && CostRules.PersistentCost(card) == 4,
            "legacy scalar aura excluded from persistent Cost memory");
        Effects.ReduceCost(1).ApplyTo(card);
        CostRulesProof.Check(card.Cost == 1 && CostRules.PersistentCost(card) == 3,
            "permanent modifier after scalar aura updates aura-free memory");
        foreach (var branch in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var held = (Playable)branch.IdEntityDic[card.Id];
            CostRulesProof.Check(CostRules.PersistentCost(held) == 3, "scalar aura memory clone/snapshot");
            ((Minion)branch.IdEntityDic[portal.Id]).Silence(); branch.AuraUpdate();
            CostRulesProof.Check(held.Cost == 3 && CostRules.PersistentCost(held) == 3,
                "scalar aura source removal leaves persistent modifier only");
        }
    }

}
