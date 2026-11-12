using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Integration 2026-10-20: cost rules compose with history facts (payments, cost readers),
/// selector decision spells, Outcast, Death Knight Corpses and Dark Gifts.</summary>
internal static class CostCompositionProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "cost composition: " + message);
    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in ids.Reverse()) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }
    private static bool Pay(Game g, Controller p, IPlayable source, ICharacter target = null)
    {
        p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        return g.Process(PlayCardTask.Any(p, source, target));
    }

    internal static void Run()
    {
        DecisionSpellOneUseRule();
        HealthAndArmorPaymentFacts();
        HistoryCostReaderUnderTemporarySet();
        OutcastTemporaryCost();
        DeathKnightHealthCost();
        DarkGiftTemporaryCost();
        Console.WriteLine("PASS cost composition: selector one-use rule paid once across snapshot; Health/Armor payment facts; history cost aura with temporary SET (A1); Outcast; DK Health cost; Dark Gift expiry");
    }

    // A next-card SET 0 rule is consumed once by the selector's committed play; the pending answer pays nothing more.
    private static void DecisionSpellOneUseRule()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        Deck(p, "CS2_182", "CS2_029"); int pick = p.DeckZone.TopCard.Id;
        var source = Add(p, "TIME_770");
        CostRules.AddNextCardRule(p, setCost: 0);
        Check(source.Cost == 0 && g.CostState.Rules.Count == 1, "rule quotes the selector at zero");
        Check(Pay(g, p, source) && p.Choice != null && g.SpellCompletion != null, "selector accepted at zero and pending");
        var payment = g.HistoryFacts.Query.Payments().Last();
        Check(p.UsedMana == 0 && g.CostState.Rules.Count == 0 && payment.EntityId == source.Id && payment.Mana == 0
            && payment.ActionKind == HistoryPaymentKind.Play && payment.RelatedPlayId == g.HistoryFacts.Query.Plays().Last().PlayId,
            "rule consumed once; zero Play payment fact linked to the play");
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        Check(restored.CostState.Rules.Count == 0, "snapshot keeps consumed rule state");
        foreach (var game in new[] { g, restored })
        {
            int payments = game.HistoryFacts.Query.Payments().Count();
            PlayerDecisionProof.Answer(game, pick);
            Check(game.Player1.UsedMana == 0 && game.HistoryFacts.Query.Payments().Count() == payments
                && game.IdEntityDic[source.Id].Zone == game.Player1.GraveyardZone, "answer pays nothing more");
        }
    }

    // Alternate payments: Health is a Payment health change without a damage fact; Armor still annotates the play.
    private static void HealthAndArmorPaymentFacts()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1; var q = g.HistoryFacts.Query;
        var fireball = (Playable)Add(p, "CS2_029");
        CostRules.AddPaymentOverride(fireball, CostPaymentKind.Health);
        int health = p.Hero.Health, plays = q.Plays().Count();
        Check(Pay(g, p, fireball, p.Opponent.Hero), "Health-cost spell accepted");
        var paid = q.Payments().Last(); var change = q.Events<HeroHealthChangedFact>().Last(f => f.HeroId == p.Hero.Id);
        Check(p.UsedMana == 0 && p.Hero.Health == health - 4 && paid.Mana == 0 && paid.Health == 4 && paid.ActionKind == HistoryPaymentKind.Play
            && change.Reason == HistoryHealthChangeReason.Payment && change.OldHealth == health && change.NewHealth == health - 4
            && !q.Events<DamageResolvedHistoryFact>().Any(f => f.VictimId == p.Hero.Id) && q.Plays().Count() == plays + 1,
            "Health payment recorded as payment, not damage");
        var frostbolt = (Playable)Add(p, "CS2_024");
        CostRules.AddPaymentOverride(frostbolt, CostPaymentKind.Armor); p.Hero.Armor = 5;
        Check(Pay(g, p, frostbolt, p.Opponent.Hero) && p.Hero.Armor == 3 && p.UsedMana == 0, "Armor-cost spell accepted");
        paid = q.Payments().Last();
        Check(paid.EntityId == frostbolt.Id && paid.Mana == 0 && paid.Health == 0 && paid.ActionKind == HistoryPaymentKind.Play
            && q.Plays().Count() == plays + 2, "Armor payment annotates the play once");
        Game.LoadSnapshot(g.SaveSnapshot());
    }

    // A history-fact cost aura (Ravenous Felfisher) composes with a temporary SET (A1 order) and survives its expiry.
    private static void HistoryCostReaderUnderTemporarySet()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var held = (Playable)Add(p, "CATA_529");
        Play(g, p, "CATA_528");
        Check(held.Cost == 5, "history aura discounts after one Fel cast");
        CostRules.SetCostThisTurn(held, 2);
        // Binding A1: legacy layer order, so the history aura (-1) still applies after the temporary SET 2.
        Check(held.Cost == 1 && held[GameTag.COST] == 1, "history aura applies over temporary SET (A1 layer order)");
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var game in new[] { g, restored })
        {
            End(game);
            var h = game.IdEntityDic[held.Id];
            Check(h.Cost == 5 && h[GameTag.COST] == 5, "expiry returns to the history-driven cost");
            End(game); Play(game, game.Player1, "CATA_528");
            Check(h.Cost == 4, "history aura keeps counting after expiry");
        }
    }

    // Outcast primaries read hand edges, not costs: a temporary discount keeps the Outcast bonus.
    private static void OutcastTemporaryCost()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        var sight = (Playable)Add(p, "CORE_BT_491");
        CostRules.SetCostThisTurn(sight, 0);
        Check(Pay(g, p, sight) && p.UsedMana == 0 && p.HandZone.Count == 2, "discounted Outcast play still draws two");
        Check(g.HistoryFacts.Query.Payments().Last().Mana == 0, "zero Outcast payment recorded");
    }

    // Death Knight with Corpses: Blood Draw's Health cost is a payment; Corpses are untouched; its Discover is recorded.
    private static void DeathKnightHealthCost()
    {
        var g = new Game(new GameConfig { EnableCorpses = true, RandomSeed = 31, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.DEATHKNIGHT, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); var p = g.Player1;
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        Kill(g, Summon(p, "CS2_182"));
        int corpses = CorpseRules.Available(p), health = p.Hero.Health, discoveries = g.HistoryFacts.Query.Discoveries().Count();
        var draw = Add(p, "TIME_612");
        Check(CostRules.PaymentKind(draw) == CostPaymentKind.Health, "Blood Draw pays Health");
        int cost = draw.Cost;
        Check(Pay(g, p, draw), "DK Health spell accepted");
        Check(p.Hero.Health == health - cost && p.UsedMana == 0 && CorpseRules.Available(p) == corpses
            && g.HistoryFacts.Query.Payments().Last().Health == cost, "Health paid; Corpses untouched");
        Check(p.Choice != null && p.Choice.Choices.Count == 3, "DK-class Discover offered");
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Check(g.HistoryFacts.Query.Discoveries().Count() == discoveries + 1, "Blood Draw Discover recorded");
    }

    // Dark Gift cost bonuses live on the legacy path; a temporary SET sits on top and expires back to them.
    private static void DarkGiftTemporaryCost()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        var minion = (Playable)Add(p, "CS2_182");
        Check(DarkGifts.Give(minion, p.Hero) && DarkGifts.Has(minion), "gift granted in hand");
        int gifted = minion.Cost;
        CostRules.SetCostThisTurn(minion, 0);
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var game in new[] { g, restored })
        {
            var m = game.IdEntityDic[minion.Id];
            Check(m.Cost == 0 && DarkGifts.Has(m), "temporary SET over gifted cost");
            End(game);
            Check(m.Cost == gifted && DarkGifts.Has(m), "expiry restores gifted cost; gift kept");
        }
    }
}
