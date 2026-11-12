using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;

internal static class Unpinned_MixedCostProof
{
    [GroupProof("mixed-cost")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (bool history in new[] { false, true })
        {
            foreach (var proof in new Action<bool>[] { Unpinned_MillenniumCorpses, Unpinned_ApplicationOrder, Unpinned_IntrinsicAndRemoval })
                try { proof(history); }
                catch (Exception e) { failures.Add(proof.Method.Name + " history=" + history + ": " + e.Message); }
        }
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS Unpinned_MixedCost: Millennium Corpses; both orders across replacements; clone/snapshot; projection; expiry");
    }

    private static void Kind(IPlayable card, CostPaymentKind expected)
    {
        CostRulesProof.Check(CostRules.Quote(card.Controller, card).Kind == expected, "Unpinned newest payment " + expected);
        CostSliceProof.Visible(card, card.Cost, expected);
    }

    private static void Unpinned_MillenniumCorpses(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var p = game.Player1;
        PoolPilotProof.EmptyHand(p);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(r => (string)r.Attribute("cardId") == "TLC_436").ToArray());
        var installed = game.OfflinePools;
        try
        {
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, OfflinePoolFixtures.Manifest(xml));
            var source = Entity.FromCard(p, Cards.FromId("TIME_615"), zone: p.SetasideZone);
            PoolPilotProof.Resolve(game, source);
            CostRulesProof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id == "TLC_436"
                && c.Card[GameTag.CARD_ALTERNATE_COST] == 3), "Unpinned Millennium generated printed Corpse cost");
            foreach (var card in p.HandZone) Kind(card, CostPaymentKind.Health);
            CostRules.ExpireTurn(game, game.Turn);
            foreach (var card in p.HandZone) Kind(card, CostPaymentKind.Corpses);
        }
        finally { typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game, installed); }
    }

    private static void Unpinned_IntrinsicAndRemoval(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
        var card = (Playable)Generic.DrawCard(p, Cards.FromId("BE_042"));
        Kind(card, CostPaymentKind.Armor);
        long health = CostRules.AddPaymentOverride(card, CostPaymentKind.Health);
        Kind(card, CostPaymentKind.Health);
        long armor = CostRules.AddPaymentOverride(card, CostPaymentKind.Armor);
        Kind(card, CostPaymentKind.Armor);
        CostRules.RemoveApplication(card, armor); Kind(card, CostPaymentKind.Health);
        CostRules.RemoveApplication(card, health); Kind(card, CostPaymentKind.Armor);
        var knight = (Playable)Generic.DrawCard(p, Cards.FromId("CORE_ETC_523"));
        long overrideId = CostRules.AddPaymentOverride(knight, CostPaymentKind.Armor);
        p.Hero.TakeDamage(p.Opponent.Hero, 2); p.Hero.TakeHeal(p.Hero, 1);
        CostRulesProof.Check(knight.Card.HealthCostCondition(knight), "Unpinned intrinsic condition active");
        Kind(knight, CostPaymentKind.Armor);
        CostRules.RemoveApplication(knight, overrideId); Kind(knight, CostPaymentKind.Health);
        long clock = game.CostState.PaymentClock;
        for (int i = 0; i < 3; i++) Kind(knight, CostPaymentKind.Health);
        CostRulesProof.Check(game.CostState.PaymentClock == clock, "Unpinned quotes never advance application order");
    }

    private static void Unpinned_ApplicationOrder(bool history)
    {
        // Pair each source against Armor in both orders, including cross-bucket rule/overlay order.
        foreach (string source in new[] { "overlay", "rule", "controller-aura", "card-aura", "opponent" })
        foreach (bool armorFirst in new[] { true, false })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var p = game.Player1;
            var card = (Playable)Generic.DrawCard(p, Cards.FromId("CS2_029"));
            void Armor() => CostRules.AddNextCardRule(p, CostPaymentKind.Armor);
            void Health()
            {
                switch (source)
                {
                    case "overlay": CostRules.AddPaymentOverride(card, CostPaymentKind.Health); break;
                    case "rule": CostRules.AddNextCardRule(p, CostPaymentKind.Health); break;
                    case "controller-aura": p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1; break;
                    case "card-aura": new Effect(GameTag.CARD_COSTS_HEALTH, EffectOperator.SET, 1).ApplyAuraTo(card); break;
                    case "opponent": CostRules.GrantOpponentHealth(p.Hero); break;
                }
            }
            if (armorFirst) { Armor(); Health(); } else { Health(); Armor(); }
            var expected = armorFirst ? source == "opponent" ? (CostPaymentKind)4 : CostPaymentKind.Health : CostPaymentKind.Armor;
            Kind(card, expected);
            byte[] snapshot = game.SaveSnapshot();
            var loaded = Game.LoadSnapshot(snapshot);
            CostRulesProof.Check(snapshot.SequenceEqual(loaded.SaveSnapshot()), "Unpinned snapshot byte roundtrip");
            foreach (var resumed in new[] { game.CloneExact(history), loaded })
            {
                var target = (Playable)resumed.IdEntityDic[card.Id];
                Kind(target, expected);
                CostRules.AddPaymentOverride(target, CostPaymentKind.Corpses);
                Kind(target, CostPaymentKind.Corpses);
                Kind(card, expected);
            }
        }
    }
}
