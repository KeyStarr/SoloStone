using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static TimeTravelPoolProof;

// Rule: "reduce the Cost of any in your hand" = every minion in hand. Launch text (34.0) was "Discover a minion. If your
// deck has no minions, reduce the Cost of any in your hand by (2)" (wiki.gg Solitude, patch changes; tier C); 34.2.2
// patch notes (tier A) changed only the Discover count ("Discover a minion" -> "Discover 2 minions").
internal static class SolitudeProof
{
    private static void TriggerOrder(Game g, Controller p, string id)
    {
        End(g);
        var secret = Play(g, p.Opponent, "CORE_ULD_152");
        End(g);
        EmptyHand(p); ClassDeck(p);
        var lastMinion = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var hoarder = Summon(p, "CORE_EX1_096");
        var held = Add(p, "CS2_182");
        int spells = p.NumSpellsPlayedThisGame, cards = p.NumCardsPlayedThisTurn;
        var source = Play(g, p, id);
        // Existing decision-spell timing: selectors report, Story of Umbra integration. Printed "after" (tier D,
        // pinned CORE_ULD_152 text) runs after Solitude's whole text, including its deck condition.
        Core2026Proof.Check(hoarder.Zone == p.BoardZone && secret.Zone == p.Opponent.SecretZone &&
            lastMinion.Zone == p.DeckZone && source.Zone != p.GraveyardZone && p.NumSpellsPlayedThisGame == spells,
            "after-cast Pressure Plate and Loot Hoarder draw wait for both Solitude choices");
        int first = Answer(g, source);
        Core2026Proof.Check(hoarder.Zone == p.BoardZone && lastMinion.Zone == p.DeckZone && held.Cost == 4 &&
            source.Zone != p.GraveyardZone && p.NumSpellsPlayedThisGame == spells,
            "first Discover cannot finish the spell or run after-cast triggers");
        int second = Answer(g, source, 1);
        Core2026Proof.Check(held.Cost == 4 && g.IdEntityDic[first].Cost == g.IdEntityDic[first].Card.Cost &&
            g.IdEntityDic[second].Cost == g.IdEntityDic[second].Card.Cost && lastMinion.Cost == 4 &&
            lastMinion.Zone == p.HandZone && p.DeckZone.Count == 0 && hoarder.Zone == p.GraveyardZone &&
            secret.Zone == p.Opponent.GraveyardZone && source.Zone == p.GraveyardZone && g.SpellCompletion == null &&
            p.NumSpellsPlayedThisGame == spells + 1 && p.NumCardsPlayedThisTurn == cards + 1,
            "deck minion blocks discount before after-cast death/draw empties deck; spell tails finish once");
        EmptyHand(p);
    }

    private static void AutomaticCast(Game g, Controller p, string id)
    {
        foreach (bool deckMinion in new[] { false, true })
        {
            EmptyHand(p); ClassDeck(p);
            if (deckMinion) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
            var held = Add(p, "CS2_182");
            var source = (Spell)Entity.FromCard(p, Cards.FromId(id));
            p.UsedMana = 0;
            int spells = p.NumSpellsPlayedThisGame, cards = p.NumCardsPlayedThisTurn;
            Resume(g, next => {
                var owner = next.Player1;
                Generic.CastSpell(owner, next, (Spell)next.IdEntityDic[source.Id], null, 0);
                next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(owner.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null &&
                    next.IdEntityDic[source.Id].Zone == owner.GraveyardZone && owner.HandZone.Count == 3 &&
                    next.IdEntityDic[held.Id].Cost == (deckMinion ? 4 : 2) && owner.UsedMana == 0 &&
                    owner.NumSpellsPlayedThisGame == spells && owner.NumCardsPlayedThisTurn == cards &&
                    owner[Generic.AutomaticDecisionTag] == 0,
                    "indirect Solitude auto-picks twice inline, checks deck and discounts without hand-play bookkeeping");
                var candidates = OfflineCardPools.Candidates(next, owner.HeroClass, TimeTravelPoolProof.Filter(id), source.Card.AssetId);
                Core2026Proof.Check(owner.HandZone.Where(c => c.Id != held.Id).All(c =>
                    c.Card.Type == CardType.MINION && candidates.Any(member => member.Id == c.Card.Id) &&
                    c[GameTag.DISPLAYED_CREATOR] == source.Id && c.Card.Id != id &&
                    ((Minion)c).AttackDamage == c.Card.ATK && ((Minion)c).Health == c.Card.Health),
                    "both automatic picks are declared class-and-neutral minions with pinned stats and creator");
            });
        }
        EmptyHand(p);
    }

    internal static void Run() => StandardSetsProof.Case("TIME_448", (g, p, id) => {
        KnownMetaProofFixture.UseBlackEmpire(g);
        p.AddHeroAndPower(Cards.FromId("HERO_10"));
        Core2026Proof.Check(p.HeroClass == CardClass.DEMONHUNTER, "Demon Hunter fixture");
        Core2026Proof.Check(TimeTravelPoolProof.Filter(id) == OfflinePoolFilter.Discover(type: CardType.MINION), "class-and-neutral minion Discover");
        SourceExcluded(g, id);
        TriggerOrder(g, p, id);

        // A minion in the deck prevents the discount.
        ClassDeck(p);
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var held = Add(p, "CS2_182");
        var spell = Add(p, "CS2_029");
        var enemy = Add(p.Opponent, "CS2_182");
        var source = Add(p, id);
        PlayEntity(g, p, source);
        var offers = Offers(g, source);
        Core2026Proof.Check(offers.All(c => c.Card.Type == CardType.MINION) && p.HandZone.Count == 2, "no generated card before the answer");
        BotAnswers(g);
        int first = Answer(g, source);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.Last().Id == first && held.Cost == 4, "first answer adds the exact entity; discount waits");
        BotAnswers(g);
        int second = Answer(g, source, 1);
        Core2026Proof.Check(p.HandZone.Count == 4 && p.HandZone.Last().Id == second && held.Cost == 4 && spell.Cost == 4 && enemy.Cost == 4 &&
            g.IdEntityDic[first].Cost == g.IdEntityDic[first].Card.Cost && p.Choice == null && !g.Resolution.HasSuspendedEffect,
            "deck minion prevents discount; exactly two Discover answers");
        foreach (int chosen in new[] { first, second })
        {
            var card = g.IdEntityDic[chosen];
            Core2026Proof.Check(card.Controller == p && card.Zone == p.HandZone && card[GameTag.DISPLAYED_CREATOR] == source.Id &&
                card.Id != source.Id && !p.SetasideZone.Any(c => c.Id == card.Id), "chosen entity has unique hand membership and creator");
        }

        // A spell-only deck discounts every friendly minion in hand, discovered ones included, with a zero floor.
        EmptyHand(p); ClassDeck(p);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        held = Add(p, "CS2_182"); held.Cost = 1;
        var expensive = Add(p, "CS2_182");
        spell = Add(p, "CS2_029");
        source = Add(p, id);
        PlayEntity(g, p, source);
        first = Answer(g, source);
        Core2026Proof.Check(held.Cost == 1 && expensive.Cost == 4, "discount waits until both choices");
        var firstCard = g.IdEntityDic[first];
        int firstCost = firstCard.Cost;
        second = Answer(g, source, 1);
        var secondCard = g.IdEntityDic[second];
        Core2026Proof.Check(held.Cost == 0 && held[GameTag.COST] == 0 && expensive.Cost == 2 && expensive[GameTag.COST] == 2 &&
            spell.Cost == 4 && enemy.Cost == 4, "spell-only deck discounts all held minions with zero floor and visible COST; spells/enemy untouched");
        Core2026Proof.Check(firstCard.Cost == Math.Max(0, firstCost - 2) && secondCard.Cost == Math.Max(0, secondCard.Card.Cost - 2) &&
            secondCard[GameTag.COST] == secondCard.Cost, "both discovered minions receive the discount");
        End(g); End(g);
        Core2026Proof.Check(expensive.Cost == 2, "discount persists across turns");

        // An empty deck also counts as "no minions"; empty and tiny pools still finish the recipe.
        foreach (int size in new[] { 0, 1, 2, 3 })
        {
            EmptyHand(p); ClassDeck(p);
            held = Add(p, "CS2_182");
            WithPool(g, id, size, () => {
                long draws = g.Random.DrawCount;
                source = Add(p, id);
                PlayEntity(g, p, source);
                for (int stage = 0; stage < (size == 0 ? 0 : 2); stage++)
                {
                    Core2026Proof.Check(Offers(g, source).Length == size, "tiny Discover offers all members");
                    Answer(g, source, stage, resume: false);
                }
                Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && p.HandZone.Count == 1 + (size == 0 ? 0 : 2) &&
                    held.Cost == 2 && g.Random.DrawCount == draws, "empty/tiny pool completes both steps without RNG; discount resolves");
                if (size == 1) Core2026Proof.Check(p.HandZone.Skip(1).Select(c => c.Card.Id).Distinct().Count() == 1,
                    "separate Discovers may repeat an identity");
            });
        }

        // Hand capacity: both Discovers are offered; picks beyond hand space are burned.
        foreach (int slots in new[] { 0, 1 })
        {
            EmptyHand(p); FillHand(p, 9);
            source = Add(p, id);
            long draws = g.Random.DrawCount;
            PlayEntity(g, p, source);
            if (slots == 0) Add(p, "CS2_182");
            var old = p.HandZone.Select(c => c.Id).ToHashSet();
            first = Answer(g, source); second = Answer(g, source, 1);
            Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => !old.Contains(c.Id)) == slots &&
                g.IdEntityDic[second].Zone.Type == Zone.GRAVEYARD &&
                (slots == 1 ? g.IdEntityDic[first].Zone.Type == Zone.HAND : g.IdEntityDic[first].Zone.Type == Zone.GRAVEYARD) &&
                g.Random.DrawCount == draws + 6, "hand capacity burns excess picks while sampling both offers");
        }
        EmptyHand(p);
        AutomaticCast(g, p, id);
    });
}
