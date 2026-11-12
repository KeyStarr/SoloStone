using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

internal static class PrepareProof
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Prepare: " + message);
    }

    internal static IPlayable Add(Controller player, int cost)
    {
        var card = Generic.DrawCard(player, Cards.FromId("CS2_182"));
        card.Cost = cost;
        PrepareRules.Grant(card);
        return card;
    }

    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (int cost in new[] { 0, 1, 2, 7 })
        foreach (int mana in new[] { 0, 1, 3, 10 })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var p = game.Player1;
            var source = Add(p, cost);
            p.BaseMana = mana;
            int hand = p.HandZone.Count, draws = p.NumCardsDrawnThisTurn, plays = p.NumCardsPlayedThisTurn;
            var deck = p.DeckZone.Select(c => c.Id).ToArray();
            var rng = (Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
            long cursor = rng.DrawCount;
            string before = Canonical.State(game);
            bool legal = cost > 0 && mana > 0;
            int events = 0;
            CardPreparedEvent? fact = null;
            game.TriggerManager.CardPreparedTrigger += _ => events++;
            game.TriggerManager.Events.Trace = (e, _, _) => { if (e is CardPreparedEvent prepared) fact = prepared; };
            Check(p.Options().Any(t => t is PrepareCardTask && t.Source == source) == legal, "option eligibility");
            Check(game.Process(PrepareCardTask.Any(p, source)) == legal, "formula eligibility");
            if (!legal) { Check(Canonical.State(game) == before && events == 0, "invalid no mutation"); continue; }
            int paid = Math.Min(mana, Math.Max(1, cost - 1));
            Check(p.RemainingMana == mana - paid && source.Cost == Math.Max(0, cost - paid - 1), $"formula result history={history} C={cost} M={mana}: remaining={p.RemainingMana} cost={source.Cost} expected={Math.Max(0, cost-paid-1)}");
            Check(events == 1 && fact.Value.SourceId == source.Id && fact.Value.ControllerId == p.Id
                && fact.Value.ManaSpent == paid && fact.Value.NominalDiscount == paid + 1
                && fact.Value.RealizedReduction == Math.Min(cost, paid + 1), "immutable action fact");
            Check(source.Zone == p.HandZone && p.HandZone.Count == hand && p.NumCardsDrawnThisTurn == draws
                && p.NumCardsPlayedThisTurn == plays && deck.SequenceEqual(p.DeckZone.Select(c => c.Id)) && cursor == rng.DrawCount,
                "no play, draw, shuffle or entity replacement");
            Check(source[GameTag.PREPARED] == 1 && source[GameTag.PREPARING] == 1 && source[GameTag.CANT_PLAY] == 1,
                "prepared and locked state");
            Check(!game.Process(PrepareCardTask.Any(p, source)) && !game.Process(PlayCardTask.Any(p, source, skipPrePhase: true))
                && !source.IsPlayableByPlayer && !p.Options().Any(t => t is PlayCardTask && t.Source == source), "repeat and fast play rejected");
        }
        foreach (bool history in new[] { false, true })
        {
            PrintedCards(history);
            PrintedCopyTransform(history);
            Theft(history);
            Lifecycle(history);
            Boundaries(history);
            Listeners(history);
        }
        Console.WriteLine("PASS Prepare primitive: formula matrix; temporary mana; immutable event; no draw/play/RNG; lock/expiry; printed-copy transform/same-entity theft/cross-controller copy/clone/snapshot; shuffle/bounce/transform; restriction composition");
    }

    private static void PrintedCards(bool history)
    {
        foreach (string id in new[] { "JAIL_906", "JAIL_909", "JAIL_913", "JAIL_395", "JAIL_718", "JAIL_326", "JAIL_890" })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var p = game.Player1;
            var source = Generic.DrawCard(p, Cards.FromId(id));
            p.BaseMana = 1;
            Check(source.Card.Implemented && source[GameTag.PREPARE] == 1 && game.Process(PrepareCardTask.Any(p, source)), id + " printed Prepare action");
            Check(source.Cost == source.Card.Cost - 2 && source[GameTag.PREPARING] == 1, id + " discount and lock");
            var clone = game.CloneExact(history);
            Check(clone.IdEntityDic[source.Id].Cost == source.Cost && clone.IdEntityDic[source.Id][GameTag.PREPARED] == 1, id + " clone state");
            Check(game.Process(EndTurnTask.Any(p)) && source[GameTag.PREPARING] == 0, id + " lock expiry");
        }
    }

    private static void PrintedCopyTransform(bool history)
    {
        foreach (bool prepared in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var p = game.Player1;
            var printed = Cards.FromId("CS2_182").Clone();
            printed.Tags[GameTag.PREPARE] = 1;
            printed.Tags[GameTag.DECK_ACTION_COST] = 1;
            var source = Generic.DrawCard(p, printed);
            if (prepared) Check(game.Process(PrepareCardTask.Any(p, source)), "prepare printed fixture");
            var copy = Generic.Copy(p, p.Hero, source, Zone.HAND);
            var transformed = Generic.ChangeEntityBlock(p, copy, Cards.FromId("CS2_120"), true);
            p.BaseMana = 10; p.UsedMana = 0;
            Check(transformed[GameTag.PREPARE] == 0 && transformed[GameTag.DECK_ACTION_COST] == 0
                && transformed[GameTag.PREPARED] == 0 && transformed[GameTag.PREPARING] == 0
                && !PrepareCardTask.IsValid(p, transformed), "printed Prepare copy transform clears capability and state");
            transformed = Generic.ChangeEntityBlock(p, transformed, printed, true);
            Check(transformed[GameTag.PREPARE] == 1 && transformed[GameTag.DECK_ACTION_COST] == 1
                && PrepareCardTask.IsValid(p, transformed), "transform rebases replacement printed capability");
        }
    }

    private static void Theft(bool history)
    {
        foreach (bool nextTurn in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var owner = game.Player1; var thief = game.Player2;
            var source = Add(owner, 7); owner.BaseMana = 3;
            Check(game.Process(PrepareCardTask.Any(owner, source)), "prepare before transfer");
            int id = source.Id, count = game.IdEntityDic.Count;
            var enchants = source.AppliedEnchantments.Select(e => e.Id).ToArray();
            if (nextTurn) Check(game.Process(EndTurnTask.Any(owner)), "turn ends before theft");
            // Hand-to-hand ownership transfer preserves the entity and enchantments (no zone reset).
            owner.HandZone.RemoveForTrade(source);
            source.Controller = thief;
            source[GameTag.CONTROLLER] = thief.PlayerId;
            thief.HandZone.Add(source);
            Check(ReferenceEquals(source, game.IdEntityDic[id]) && game.IdEntityDic.Count == count
                && !owner.HandZone.Contains(source) && thief.HandZone.Contains(source), "theft moves original; no copy created");
            Check(source.Cost == 3 && source[GameTag.PREPARED] == 1
                && !PrepareCardTask.IsValid(thief, source), "theft preserves discount and once-only preparation");
            Check(source[GameTag.PREPARING] == (nextTurn ? 0 : 1), "theft preserves global-turn lock");
            if (!nextTurn)
            {
                Check(source.AppliedEnchantments.Select(e => e.Id).SequenceEqual(enchants), "same enchantment entities transferred");
                Check(!source.IsPlayableByPlayer, "same-turn theft stays locked");
                Check(game.Process(EndTurnTask.Any(owner)), "expiry after same-turn theft");
            }
            thief.BaseMana = 10; thief.UsedMana = 0;
            Check(source[GameTag.PREPARING] == 0 && source.Cost == 3 && source.IsPlayableByPlayer
                && game.Process(PlayCardTask.Any(thief, source)), "thief plays original immediately on later turn");
        }
    }

    private static void Lifecycle(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var p = game.Player1;
        var source = Add(p, 7);
        p.BaseMana = 3; p.TemporaryMana = 2;
        Check(game.Process(PrepareCardTask.Any(p, source)) && p.TemporaryMana == 0 && p.UsedMana == 3
            && p.TotalManaSpentThisGame == 5 && source.Cost == 1, "temporary mana first");
        source[GameTag.CANT_PLAY] = 1; // Independent restriction must survive Prepare expiry.
        foreach (var resumed in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var cloneSource = resumed.IdEntityDic[source.Id];
            Check(cloneSource.Cost == 1 && cloneSource[GameTag.PREPARED] == 1 && cloneSource[GameTag.PREPARING] == 1, "resumed state");
            Check(resumed.Process(EndTurnTask.Any(resumed.Player1)), "end preparation turn");
            Check(cloneSource[GameTag.PREPARING] == 0 && cloneSource[GameTag.CANT_PLAY] == 1 && cloneSource.Cost == 1
                && !cloneSource.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_907e02"), "expiry preserves unrelated restriction and discount");
            Check(source[GameTag.PREPARING] == 1, "clone isolation");
        }
        source[GameTag.CANT_PLAY] = 0;
        var copy = Generic.Copy(p, p.Hero, source, Zone.HAND);
        var enemyCopy = Generic.Copy(p.Opponent, p.Opponent.Hero, source, Zone.HAND);
        Check(copy.Cost == 1 && copy[GameTag.PREPARED] == 1 && copy[GameTag.PREPARING] == 1
            && enemyCopy.Cost == 1 && enemyCopy[GameTag.PREPARING] == 1, "hand copies preserve state across controllers");
        game.Process(EndTurnTask.Any(p));
        Check(source[GameTag.PREPARING] == 0 && copy[GameTag.PREPARING] == 0 && enemyCopy[GameTag.PREPARING] == 0
            && enemyCopy.IsPlayableByPlayer, "global-turn expiry includes other controller");
        Check(game.Process(PlayCardTask.Any(p.Opponent, enemyCopy)), "later-turn opponent copy plays");
        ((Minion)enemyCopy).Silence();
        Check(enemyCopy[GameTag.PREPARING] == 0 && enemyCopy[GameTag.PREPARED] == 0, "play/Silence no hand lock resurrection");
        Generic.ReturnToHandBlock(p.Opponent, (Minion)enemyCopy);
        Check(enemyCopy.Cost == enemyCopy.Card.Cost && enemyCopy[GameTag.PREPARED] == 0, "bounce resets discount/eligibility");
        p.DeckZone.Add(p.HandZone.Remove(source));
        Check(source.Cost == source.Card.Cost && source[GameTag.PREPARED] == 0
            && !source.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_907e"), "shuffle resets discount and state");
        var transformed = Generic.ChangeEntityBlock(p, copy, Cards.FromId("CS2_120"), true);
        Check(transformed[GameTag.PREPARED] == 0 && transformed[GameTag.PREPARING] == 0
            && transformed[GameTag.PREPARE] == 0 && transformed.Cost == 2, "transform clears old capability and state");
    }

    private static void Listeners(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var p = game.Player1;
        var source = Add(p, 7);
        var listeners = new[] { Generic.DrawCard(p, Cards.FromId("CS2_182")), Generic.DrawCard(p, Cards.FromId("CS2_182")),
            Generic.DrawCard(p.Opponent, Cards.FromId("CS2_182")) };
        foreach (var listener in listeners)
            new Trigger(TriggerType.CARD_PREPARED) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = ComplexTask.Create(new GetEventNumberTask(), new SetGameTagNumberTask(GameTag.TAG_SCRIPT_DATA_NUM_2, EntityType.SOURCE))
            }.Activate(listener, TriggerActivation.HAND);
        var clone = game.CloneExact(history);
        Check(clone.Process(PrepareCardTask.Any(clone.Player1, clone.IdEntityDic[source.Id])), "cloned action with listeners");
        Check(listeners.Take(2).All(l => clone.IdEntityDic[l.Id][GameTag.TAG_SCRIPT_DATA_NUM_2] == 7)
            && clone.IdEntityDic[listeners[2].Id][GameTag.TAG_SCRIPT_DATA_NUM_2] == 0,
            $"each held friendly listener gets nominal discount once; opponent excluded: {string.Join(",", listeners.Select(l => clone.IdEntityDic[l.Id][GameTag.TAG_SCRIPT_DATA_NUM_2]))}");
        Check(listeners.All(l => l[GameTag.TAG_SCRIPT_DATA_NUM_2] == 0), "trigger clone owners isolated");
        p.SetasideZone.Add(p.HandZone.Remove(listeners[0]));
        Check(game.Process(PrepareCardTask.Any(p, source)), "original action");
        Check(listeners[0][GameTag.TAG_SCRIPT_DATA_NUM_2] == 0 && listeners[1][GameTag.TAG_SCRIPT_DATA_NUM_2] == 7,
            "removed hand listener inactive");
    }

    private static void Boundaries(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var p = game.Player1;
        var spell = Generic.DrawCard(p, Cards.FromId("CS2_108"));
        PrepareRules.Grant(spell);
        while (p.DeckZone.Count > 0) p.SetasideZone.Add(p.DeckZone.Remove(p.DeckZone.TopCard));
        while (!p.BoardZone.IsFull) HeroDivineShieldProof.Summon(game, "CS2_231");
        while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("CS2_231"));
        Check(PrepareCardTask.IsValid(p, spell) && !spell.IsPlayable, "no target/full board/full hand/empty deck allowed");
        if (history)
        {
        var options = PowerOptionsBuilder.AllOptions(game, p.Options());
        Check(options.PowerOptionList.Any(o => o.MainOption?.EntityId == spell.Id && o.MainOption.PowerKeyword == 4354
            && o.MainOption.Targets == null), "independent keyword option");
        }
        Check(!game.Process(PrepareCardTask.Any(p.Opponent, spell)), "wrong controller rejected");
        Check(game.Process(PrepareCardTask.Any(p, spell)), "targetless preparation accepted");
        var second = p.HandZone.First(c => c != spell);
        second.Cost = 4;
        PrepareRules.Grant(second);
        p.BaseMana = 10; p.UsedMana = 0;
        Check(game.Process(PrepareCardTask.Any(p, second)), "separate card can prepare same turn");
        var removed = p.HandZone.First(c => c != spell && c != second);
        removed.Cost = 2;
        PrepareRules.Grant(removed);
        p.HandZone.Remove(removed);
        int mana = p.RemainingMana;
        Check(!game.Process(PrepareCardTask.Any(p, removed)) && p.RemainingMana == mana,
            "stale removed hand entity rejected without payment");
    }
}
