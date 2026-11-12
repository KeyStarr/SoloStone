using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class NightmareLordXaviusProof
{
    private static readonly string[] Minions = { "CS2_231", "CS2_182", "EX1_028", "EX1_319", "TIME_056" };

    private static void Deck(Controller p, int count)
    {
        PoolPilotProof.ClassDeck(p);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_106"), zone: p.DeckZone);
        foreach (string id in Minions.Take(count)) {
            Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        }
    }

    // D2/D4 / data/rules-evidence/dark-gift/dg18b-xavius-offer-gifts.yaml.
    private static int[] Offers(Game g, int source, int count)
    {
        var p = g.Player1; var ids = p.Choice.Choices.ToArray();
        Core2026Proof.Check(ids.Length == Math.Min(3, count) && ids.Select(id => g.IdEntityDic[id].Card.Id).Distinct().Count() == ids.Length,
            "at most three distinct own-deck minion card IDs");
        Core2026Proof.Check(ids.All(id => id != source && g.IdEntityDic[id] is Minion && g.IdEntityDic[id].Controller == p &&
            g.IdEntityDic[id].Zone == p.DeckZone && !DarkGifts.Has(g.IdEntityDic[id]) &&
            g.IdEntityDic[id][Generic.PrivateDecisionOfferTag] == p.PlayerId && g.IdEntityDic[id][GameTag.REVEALED] == 0),
            "actual private own-deck minions only, source excluded, no prechoice grants/reveal");
        var executor = g.IdEntityDic[p.Choice.SourceId];
        Core2026Proof.Check(executor.Card.Id == "EDR_102t" && executor[GameTag.CREATOR] == source && p.Choice.NextChoice == null &&
            g.Resolution.HasSuspendedEffect, "gift executor belongs to Xavius, single suspended prompt");
        var spells = ids.Select(id => g.IdEntityDic[g.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]]).ToArray();
        Core2026Proof.Check(spells.Select(s => s.Card.Id).Distinct().Count() == ids.Length && spells.All(s =>
            s.Controller == p && s.Zone == p.SetasideZone && s.Card.Type == CardType.SPELL && s[GameTag.CREATOR] == executor.Id),
            "distinct preassigned gift spells stay private in SETASIDE");
        foreach (int id in ids)
            Core2026Proof.Check(DarkGifts.Registry.Single(gift => gift.SpellId ==
                g.IdEntityDic[g.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id).IsEligible(g.IdEntityDic[id]), "every gift eligible for its offer");
        return ids;
    }

    private static void Choose(Game g, int selected, bool burn = false)
    {
        var p = g.Player1; var ids = p.Choice.Choices.ToArray();
        var target = g.IdEntityDic[selected];
        var gift = DarkGifts.Registry.Single(item => item.SpellId == g.IdEntityDic[target[GameTag.DARK_GIFT_ENTITY]].Card.Id);
        int[] deck = p.DeckZone.Select(card => card.Id).ToArray();
        int drawn = p.NumCardsDrawnThisTurn;
        var d = g.GetPendingDecisions().Single(); var answer = new DecisionAnswer(d.Id, d.ControllerId, new[] { selected });
        var before = g.SaveSnapshot();
        Core2026Proof.Check(!g.TryAnswer(new(d.Id, d.ControllerId, new[] { int.MaxValue }), out _) && before.SequenceEqual(g.SaveSnapshot()),
            "invalid answer preserves pending state/RNG");
        // D4 / dg18a-xavius-deck-card-drawn.yaml: selected deck entity is drawn; never a generated copy.
        PoolPilotProof.Resume(g, next => {
            var c = next.Player1; var chosen = next.IdEntityDic[selected];
            var random = next.Random.CaptureState();
            Core2026Proof.Check(next.TryAnswer(answer, out var error), error);
            Core2026Proof.Check(c.LastCardDrawn == selected && c.NumCardsDrawnThisTurn == drawn + 1 &&
                c.Choice == null && !next.Resolution.HasSuspendedEffect && next.Random.CaptureState().Equals(random),
                "same entity drawn once, no reroll, suspended Battlecry completes");
            Core2026Proof.Check(ids.Where(id => id != selected).All(id => !DarkGifts.Has(next.IdEntityDic[id]) &&
                next.IdEntityDic[id].Zone == c.DeckZone), "unchosen offers remain untouched in deck");
            if (burn) Core2026Proof.Check(chosen.Zone == c.GraveyardZone && !DarkGifts.Has(chosen) && c.HandZone.Count == 10,
                "full hand burns chosen entity and gift");
            else {
                Core2026Proof.Check(DarkGifts.GiftsOf(chosen).Single().Card.Id == gift.EnchantmentId,
                    "chosen entity receives exactly its stored gift");
                Core2026Proof.Check(gift.SpellId == "EDR_100t8" ? c.DeckZone.TopCard == chosen && chosen.Zone == c.DeckZone :
                    chosen.Zone == c.HandZone, "Sweet Dreams follows hand entry; other gifts stay in hand (D7 / dg11b-sweet-dreams-sequence.yaml)");
            }
            int[] expected = !burn && gift.SpellId == "EDR_100t8" ?
                deck.Where(id => id != selected).Concat(new[] { selected }).ToArray() : deck.Where(id => id != selected).ToArray();
            Core2026Proof.Check(expected.SequenceEqual(c.DeckZone.Select(card => card.Id)), "unchosen deck entities and order preserved");
            var after = next.SaveSnapshot();
            Core2026Proof.Check(!next.TryAnswer(answer, out _) && after.SequenceEqual(next.SaveSnapshot()), "stale answer cannot draw twice");
        });
    }

    private static void Sequential(Game original, string id)
    {
        foreach (bool rude in new[] { false, true })
        foreach (bool brann in new[] { false, true }) {
            var g = original.CloneExact(history: true); var p = g.Player1; Deck(p, 3);
            if (brann) Summon(p, "LOE_077");
            var source = Add(p, id);
            if (rude) WallowTheWretchedProof.Give(g, source, "EDR_100t7");
            PlayEntity(g, p, source);
            int draws = p.NumCardsDrawnThisTurn;
            PoolPilotProof.Resume(g, next => {
                int repetitions = rude || brann ? 2 : 1;
                var chosenIds = new List<int>();
                for (int i = 0; i < repetitions; i++) {
                    var c = next.Player1;
                    var offers = Offers(next, source.Id, 3);
                    Core2026Proof.Check(chosenIds.All(chosen => !offers.Contains(chosen)), "second prompt samples updated deck");
                    int selected = offers.First(entity => next.IdEntityDic[next.IdEntityDic[entity][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
                    chosenIds.Add(selected);
                    var decision = next.GetPendingDecisions().Single();
                    Core2026Proof.Check(next.TryAnswer(new(decision.Id, c.Id, new[] { selected }), out var error), error);
                    Core2026Proof.Check(next.IdEntityDic[selected].Zone == c.HandZone && DarkGifts.Has(next.IdEntityDic[selected]) &&
                        c.NumCardsDrawnThisTurn == draws + i + 1 && c.LastCardDrawn == selected,
                        "each sequential answer gifts/draws exactly once");
                    Core2026Proof.Check((c.Choice != null) == (i + 1 < repetitions), "only Boolean twice total for Rude Awakening/Brann");
                }
                Core2026Proof.Check(!next.Resolution.HasSuspendedEffect, "doubled Battlecry finishes after second answer");
            });
        }
    }

    private static void SweetDreams(Game original, string id)
    {
        bool found = false;
        for (int seed = 1; seed <= 64 && !found; seed++) {
            var g = original.CloneExact(history: true); var p = g.Player1; Deck(p, 1); g.SetRandomSeed(seed);
            Play(g, p, id);
            int selected = p.Choice.Choices.Single();
            if (g.IdEntityDic[g.IdEntityDic[selected][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8") continue;
            Choose(g, selected);
            Core2026Proof.Check(((Minion)g.IdEntityDic[selected]).AttackDamage == 5 &&
                ((Minion)g.IdEntityDic[selected]).Health == 6, "Sweet Dreams retains +4/+5 on deck top");
            found = true;
        }
        Core2026Proof.Check(found, "seed sweep exercised real Sweet Dreams pairing");
    }

    private static void ExistingSweetDreams(Game original, string id)
    {
        var g = original.CloneExact(history: true); var p = g.Player1; PoolPilotProof.ClassDeck(p);
        var target = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        WallowTheWretchedProof.Give(g, target, "EDR_100t8");
        // Real pairing: the new gift differs from the Sweet Dreams already attached.
        g.SetRandomSeed(1); Play(g, p, id);
        var gift = DarkGifts.Registry.Single(item => item.SpellId == g.IdEntityDic[target[GameTag.DARK_GIFT_ENTITY]].Card.Id);
        Core2026Proof.Check(gift.SpellId != "EDR_100t8", "fixture pairs a different new gift");
        PoolPilotProof.Resume(g, next => {
            var c = next.Player1; var chosen = next.IdEntityDic[target.Id]; int drawn = c.NumCardsDrawnThisTurn;
            var d = next.GetPendingDecisions().Single();
            Core2026Proof.Check(next.TryAnswer(new(d.Id, d.ControllerId, new[] { chosen.Id }), out var error), error);
            Core2026Proof.Check(chosen.Zone == c.HandZone && c.LastCardDrawn == chosen.Id && c.NumCardsDrawnThisTurn == drawn + 1 &&
                DarkGifts.GiftsOf(chosen).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100t8e1", gift.EnchantmentId }),
                "ExistingSweetDreams: new different gift stays drawn with both gifts");
        });
    }

    private static void SweetDreamsWallowShortClaws(Game original, string id)
    {
        bool found = false;
        for (int seed = 1; seed <= 64 && !found; seed++) {
            var g = original.CloneExact(history: true); var p = g.Player1; PoolPilotProof.ClassDeck(p);
            var wallow = Entity.FromCard(p, Cards.FromId("EDR_487"), zone: p.DeckZone);
            var donor = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone);
            WallowTheWretchedProof.Give(g, donor, "EDR_100t2");
            g.SetRandomSeed(seed); Play(g, p, id);
            if (g.IdEntityDic[g.IdEntityDic[wallow.Id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8") continue;
            PoolPilotProof.Resume(g, next => {
                var c = next.Player1; var chosen = (Minion)next.IdEntityDic[wallow.Id]; int drawn = c.NumCardsDrawnThisTurn;
                int history = next.PowerHistory.Full.Count;
                var d = next.GetPendingDecisions().Single();
                Core2026Proof.Check(next.TryAnswer(new(d.Id, d.ControllerId, new[] { chosen.Id }), out var error), error);
                Core2026Proof.Check(chosen.Zone == c.DeckZone && c.DeckZone.TopCard == chosen && chosen.Cost == 5 &&
                    chosen.AttackDamage == 12 && chosen.Health == 16 && DarkGifts.GiftsOf(chosen).Select(e => e.Card.Id)
                        .SequenceEqual(new[] { "EDR_100t2e", "EDR_100t8e1", "EDR_100t8e1" }),
                    "SweetDreamsWallowShortClaws: real grant plus mirror preserves every gift and discount");
                Core2026Proof.Check(c.LastCardDrawn == chosen.Id && c.NumCardsDrawnThisTurn == drawn + 1 &&
                    next.PowerHistory.Full.Skip(history).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                        .Where(change => change.EntityId == chosen.Id && change.Tag == GameTag.ZONE).Select(change => change.Value)
                        .SequenceEqual(new[] { (int)Zone.HAND, (int)Zone.DECK }),
                    "SweetDreamsWallowShortClaws: hand first, exactly one relocation per chosen grant");
                DarkGifts.ResolveSweetDreams(chosen);
                Generic.Draw(c, chosen);
                Core2026Proof.Check(chosen.Zone == c.HandZone && chosen.Cost == 5 && DarkGifts.GiftsOf(chosen).Count == 3,
                    "SweetDreamsWallowShortClaws: redraw keeps gifts and discount without repeating relocation");
            });
            found = true;
        }
        Core2026Proof.Check(found, "seed sweep exercises Xavius Sweet Dreams on Short Claws Wallow");
    }

    internal static void Run() => StandardSetsProof.Case("EDR_856", (g, p, id) => {
        foreach (int count in new[] { 0, 1, 2, 3, 5 }) {
            var next = g.CloneExact(history: true); var c = next.Player1; Deck(c, count);
            var source = Add(c, id); int[] deck = c.DeckZone.Select(card => card.Id).ToArray();
            var random = next.Random.CaptureState(); int draws = c.NumCardsDrawnThisTurn;
            PoolPilotProof.Resume(next, future => PlayEntity(future, future.Player1, future.IdEntityDic[source.Id]));
            Core2026Proof.Check(deck.SequenceEqual(c.DeckZone.Select(card => card.Id)), "opening choice leaves all deck entities in place");
            if (count == 0) Core2026Proof.Check(c.Choice == null && !next.Resolution.HasSuspendedEffect &&
                c.NumCardsDrawnThisTurn == draws && c.Hero.Fatigue == 0 && next.Random.CaptureState().Equals(random),
                "zero minions skips choice/draw/fatigue/RNG despite deck spells/weapons");
            else {
                int[] offers = Offers(next, source.Id, count);
                Choose(next, offers[^1]);
            }
        }
        var full = g.CloneExact(history: true); Deck(full.Player1, 3);
        var played = Play(full, full.Player1, id); Offers(full, played.Id, 3);
        PoolPilotProof.FillHand(full.Player1); Choose(full, full.Player1.Choice.Choices[0], burn: true);
        Sequential(g, id); SweetDreams(g, id); ExistingSweetDreams(g, id); SweetDreamsWallowShortClaws(g, id);
        Deck(p, 1);
        var location = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
        for (int i = 1; i < 6; i++) Summon(p, "CS2_231");
        var sourceOnFullBoard = Play(g, p, id); Offers(g, sourceOnFullBoard.Id, 1);
        Choose(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.GetSlot(location.ZonePosition).Id == location.Id,
            "Xavius takes last slot beside location; Discover creates no board minions");
        var blocked = Add(p, id); var before = g.Random.CaptureState();
        Core2026Proof.Check(!p.Options().Any(option => option.Source?.Id == blocked.Id) && g.Random.CaptureState().Equals(before),
            "full mixed board prevents another Xavius play");
    });
}
