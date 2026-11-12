using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class NightmareFuelProof
{
    private static readonly string[] Minions = { "CS2_182", "CS2_231", "BE_228", "TIME_056", "EX1_028" };

    private static void Deck(Controller p, int count)
    {
        PoolPilotProof.ClassDeck(p);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_106"), zone: p.DeckZone);
        foreach (string id in Minions.Take(count)) {
            Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            Entity.FromCard(p, Cards.FromId(id).Clone(), zone: p.DeckZone);
        }
    }

    private static void Offers(Game g, Controller p, int source, int count, bool combo)
    {
        var offers = p.Choice.Choices.Select(id => g.IdEntityDic[id]).ToArray();
        Core2026Proof.Check(offers.Length == Math.Min(3, count) && offers.Select(o => o.Card.Id).Distinct().Count() == offers.Length,
            "at most three distinct opponent minion definitions");
        Core2026Proof.Check(offers.All(o => o is Minion && o.Card.Id != "EDR_528" && o.Id != source &&
            o.Controller == p && o.Zone == p.SetasideZone && o[GameTag.CREATOR] == source && o[GameTag.DISPLAYED_CREATOR] == source &&
            o[Generic.PrivateDecisionOfferTag] == p.PlayerId && p.Opponent.DeckZone.Any(original => original.Card.Id == o.Card.Id)),
            "private new SETASIDE copies from opponent deck, never spells/weapons/source");
        Core2026Proof.Check(offers.All(o => !DarkGifts.Has(o) && o.Cost == o.Card.Cost &&
            ((Minion)o).AttackDamage == o.Card.ATK && ((Minion)o).Health == o.Card.Health), "copies start at printed stats without source enchantments");
        if (combo) {
            var executor = g.IdEntityDic[p.Choice.SourceId];
            Core2026Proof.Check(executor.Card.Id == "EDR_102t" && executor[GameTag.CREATOR] == source, "gift executor links actual spell");
            var gifts = offers.Select(o => g.IdEntityDic[o[GameTag.DARK_GIFT_ENTITY]]).ToArray();
            Core2026Proof.Check(gifts.Select(spell => spell.Card.Id).Distinct().Count() == offers.Length && gifts.All(spell =>
                spell.Card.Type == CardType.SPELL && spell.Controller == p && spell.Zone == p.SetasideZone &&
                spell[GameTag.CREATOR] == executor.Id), "each offer gets distinct stored gift spell, not attachment");
            foreach (var offer in offers)
                Core2026Proof.Check(DarkGifts.Registry.Single(gift => gift.SpellId == g.IdEntityDic[offer[GameTag.DARK_GIFT_ENTITY]].Card.Id).IsEligible(offer),
                    "stored gift eligible for copied current state");
        }
        else Core2026Proof.Check(p.Choice.SourceId == source && offers.All(o => o[GameTag.DARK_GIFT_ENTITY] == 0), "non-Combo ordinary offers lack gifts");
    }

    private static void Choose(Game g, int ownerId, bool combo, bool burn = false)
    {
        var owner = g.ControllerById(ownerId);
        var ids = owner.Choice.Choices.ToArray();
        int selected = ids[^1];
        var chosen = g.IdEntityDic[selected];
        string giftSpell = combo ? g.IdEntityDic[chosen[GameTag.DARK_GIFT_ENTITY]].Card.Id : null;
        string enchant = combo ? DarkGifts.Registry.Single(gift => gift.SpellId == giftSpell).EnchantmentId : null;
        int[] deck = owner.Opponent.DeckZone.Select(p => p.Id).ToArray();
        var decision = g.GetPendingDecisions().Single();
        var answer = new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected });
        var before = g.SaveSnapshot();
        Core2026Proof.Check(!g.TryAnswer(new(decision.Id, decision.ControllerId, new[] { int.MaxValue }), out _) &&
            before.SequenceEqual(g.SaveSnapshot()), "invalid copy selection preserves bytes and RNG");
        PoolPilotProof.Resume(g, next => {
            var p = next.ControllerById(ownerId); var target = next.IdEntityDic[selected];
            var rng = next.Random.CaptureState(); int grants = 0;
            TriggerManager.TriggerHandler handler = _ => {
                grants++;
                Core2026Proof.Check(DarkGifts.GiftsOf(target).Single().Card.Id == enchant && target.Zone == p.SetasideZone,
                    "only selected stored gift granted before movement/burn");
            };
            next.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(handler);
            Core2026Proof.Check(next.TryAnswer(answer, out var error), error);
            next.TriggerManager.Events.Unsubscribe<DarkGiftGivenEvent>(handler);
            Core2026Proof.Check(grants == (combo ? 1 : 0) && ids.Where(id => id != selected).All(id =>
                !DarkGifts.Has(next.IdEntityDic[id]) && next.IdEntityDic[id].Zone == p.SetasideZone), "no grants to unchosen copies");
            if (burn) Core2026Proof.Check(target.Zone == p.GraveyardZone && !DarkGifts.Has(target) && p.HandZone.Count == 10, "full hand burns chosen copy and gift");
            else {
                Core2026Proof.Check(target.Zone == p.HandZone || combo && giftSpell == "EDR_100t8" && p.DeckZone.TopCard == target,
                    "chosen copy enters hand, Sweet Dreams may then top-deck it (D7 / dg11b-sweet-dreams-sequence.yaml)");
                Core2026Proof.Check(DarkGifts.Has(target) == combo && (!combo || DarkGifts.GiftsOf(target).Single().Card.Id == enchant),
                    "only chosen copy has preassigned gift");
            }
            Core2026Proof.Check(deck.SequenceEqual(p.Opponent.DeckZone.Select(card => card.Id)) &&
                next.Random.CaptureState().Equals(rng) && p.Choice == null && !next.Resolution.HasSuspendedEffect,
                "opponent deck intact, no choice reroll, choice completes");
            var after = next.SaveSnapshot();
            Core2026Proof.Check(!next.TryAnswer(answer, out _) && after.SequenceEqual(next.SaveSnapshot()), "stale copy answer harmless");
        });
    }

    private static void WeightedOffers(Game g, Controller p, string id)
    {
        Deck(p.Opponent, 0);
        foreach (string minion in Minions)
            Entity.FromCard(p.Opponent, Cards.FromId(minion), zone: p.Opponent.DeckZone);
        Entity.FromCard(p.Opponent, Cards.FromId(Minions[0]).Clone(), zone: p.Opponent.DeckZone);
        foreach (bool combo in new[] { false, true }) {
            var counts = Minions.ToDictionary(minion => minion, _ => 0);
            for (int seed = 1; seed <= 256; seed++) {
                var next = g.CloneExact(history: true);
                if (combo) Play(next, next.Player1, "GAME_005");
                next.SetRandomSeed(seed);
                Play(next, next.Player1, id);
                var offers = next.Player1.Choice.Choices.Select(entity => next.IdEntityDic[entity].Card.Id).ToArray();
                Core2026Proof.Check(offers.Length == 3 && offers.Distinct().Count() == 3,
                    "uneven-copy seed sweep always offers three distinct card IDs");
                foreach (string offer in offers) counts[offer]++;
            }
            Core2026Proof.Check(counts[Minions[0]] > counts.Where(pair => pair.Key != Minions[0]).Max(pair => pair.Value) &&
                counts[Minions[0]] * 20 > counts.Where(pair => pair.Key != Minions[0]).Sum(pair => pair.Value) * 6,
                "full-deck duplicate weighting: two-copy card offered more often than singletons (Combo=" + combo + "): " +
                string.Join(", ", counts.Select(pair => pair.Key + "=" + pair.Value)));
        }
    }

    internal static void Run() => StandardSetsProof.Case("EDR_528", (g, p, id) => {
        // Literal Combo fork: data/rules-evidence/dark-gift/dg19-nightmare-fuel-combo.yaml.
        // OP_DECK_MINION / BOT_435 samples the full deck, retaining duplicate weighting.
        WeightedOffers(g, p, id);
        foreach (bool combo in new[] { false, true })
        foreach (int count in new[] { 0, 1, 2, 3, 5 }) {
            PoolPilotProof.EmptyHand(p); Deck(p.Opponent, count);
            if (p.IsComboActive) { End(g); End(g); PoolPilotProof.EmptyHand(p); }
            if (combo) Play(g, p, "GAME_005");
            var originals = p.Opponent.DeckZone.ToArray();
            if (count > 0) {
                var original = originals.First(card => card is Minion);
                Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), p.Opponent.Hero, original);
                original.Cost = 9;
                Core2026Proof.Check(DarkGifts.Give(original, p.Opponent.Hero), "gifted opponent source fixture");
            }
            var states = originals.ToDictionary(card => card.Id, card => string.Join(",", card.NativeTags.OrderBy(pair => pair.Key)) + ":" +
                card.Cost + ":" + string.Join(",", DarkGifts.GiftsOf(card).Select(e => e.Id)));
            var source = Add(p, id); p.UsedMana = 0;
            var reference = g.Random.Clone();
            var candidates = p.Opponent.DeckZone.Where(card => card is Minion).Select(card => card.Card)
                .Select(card => Cards.FromId(card.Id)).ToArray();
            var expected = DiscoverTask.GetChoices(new[] { candidates }, 3, reference).Select(card => card.Id).ToArray();
            int history = g.PowerHistory.Full.Count;
            long draws = g.Random.DrawCount;
            PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
            Core2026Proof.Check(!g.PowerHistory.Full.Skip(history).OfType<PowerHistoryShowEntity>().Any(e => states.ContainsKey(e.Entity.Id)) &&
                originals.All(card => card[GameTag.REVEALED] == 0), "Discover never reveals actual opponent deck entities");
            if (count == 0) Core2026Proof.Check(p.Choice == null && draws == g.Random.DrawCount, "no opponent minions: no choice or RNG");
            else {
                Offers(g, p, source.Id, count, combo);
                Core2026Proof.Check(p.Choice.Choices.Select(entity => g.IdEntityDic[entity].Card.Id).SequenceEqual(expected), "legacy deck Discover sampling/order copied exactly");
                Core2026Proof.Check(g.Random.DrawCount == reference.DrawCount + (combo ? expected.Length : 0), "only candidate sampling plus one gift draw per offer");
                if (!combo) Core2026Proof.Check(g.Random.CaptureState().Equals(reference.CaptureState()), "ordinary candidate RNG exact");
                Choose(g, p.Id, combo);
            }
            Core2026Proof.Check(originals.Select(card => card.Id).SequenceEqual(p.Opponent.DeckZone.Select(card => card.Id)) && originals.All(card =>
                states[card.Id] == string.Join(",", card.NativeTags.OrderBy(pair => pair.Key)) + ":" + card.Cost + ":" + string.Join(",", DarkGifts.GiftsOf(card).Select(e => e.Id))),
                "opponent entities, enchantments, costs and deck order unchanged");
        }
        foreach (bool combo in new[] { false, true }) {
            End(g); End(g); PoolPilotProof.EmptyHand(p); Deck(p.Opponent, 3);
            if (combo) Play(g, p, "GAME_005");
            Play(g, p, id); PoolPilotProof.FillHand(p);
            Choose(g, p.Id, combo, burn: true);
        }
        PoolPilotProof.EmptyHand(p); End(g); Deck(p, 3);
        var enemy = g.CurrentPlayer;
        PoolPilotProof.EmptyHand(enemy);
        var location = Entity.FromCard(enemy, Cards.FromId("CORE_REV_990"), zone: enemy.BoardZone);
        for (int i = 1; i < 7; i++) Summon(enemy, "CS2_231");
        Play(g, enemy, "GAME_005"); var otherSource = Play(g, enemy, id);
        Offers(g, enemy, otherSource.Id, 3, combo: true); Choose(g, enemy.Id, combo: true);
        Core2026Proof.Check(enemy.BoardZone.SlotCount == 7 && enemy.BoardZone.GetSlot(0).Id == location.Id, "full mixed board does not block spell/copy or create minions");
    });
}
