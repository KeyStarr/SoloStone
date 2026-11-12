using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

internal static class LocationActivationProof
{
    internal static void Run()
    {
        CooldownTraceTiming();
        Pinned_ActivationTargetsElusive();
        PinnedRule_ActivationTargetsStealthImmune();
        Unpinned_FullBoardSummonActivation();
        Unpinned_LocationSummonPosition();
        ActivationSignals();
        PureValidation();
        Rejections();
        Console.WriteLine("PASS location activation; DurabilityDamageEncoding, CooldownTraceTiming, ActivationTraceOrder, Pinned_ActivationTargetsElusive, PinnedRule_ActivationTargetsStealthImmune, Unpinned_FullBoardSummonActivation, Unpinned_LocationSummonPosition");
    }
    private static void CooldownTraceTiming()
    {
        var game = L.Game(); var p = game.Player1;
        var target = L.Minion(p); target.BaseHealth = 20;
        var location = L.Play(p, "CORE_REV_990", 0);
        L.Check(location.ZonePosition == 0 && !location.IsExhausted && location.Durability == 3, "ready on play");
        var play = game.PowerHistory.Last.ToList();
        int power = play.FindIndex(e => e is PowerHistoryBlockStart b && b.BlockType == BlockType.POWER && b.Source == location.Id);
        L.Check(play[0] is PowerHistoryBlockStart outer && outer.BlockType == BlockType.PLAY && power > 0 &&
            play[power + 1] is PowerHistoryBlockEnd, "location play contains empty POWER (T2 block 360)");
        int cards = p.CardsPlayedThisTurn.Count, count = p.NumCardsPlayedThisTurn, mana = p.RemainingMana, minions = p.NumMinionsPlayedThisTurn;
        for (int health = 2; health >= 0; health--)
        {
            int options = p.NumOptionsPlayedThisTurn;
            L.Use(location, target);
            L.Check(p.NumOptionsPlayedThisTurn == options + 1, "activation counts exactly one option");
            // Pinned: T1 176014_opponent_sideboard L1245/2285/4526; T2 friendly_etc_played_twice.195635 L9659/15969.
            L.Check(location[GameTag.HEALTH] == 3 && location[GameTag.DAMAGE] == (health == 0 ? 0 : 3 - health) &&
                (health == 0 || location.Durability == health), "DurabilityDamageEncoding; death clears DAMAGE");
            ActivationTraceOrder(game, location, target.Id, health == 0);
            if (health == 2) L.Check(p.CardsPlayedThisTurn.Count == cards && p.NumCardsPlayedThisTurn == count && p.RemainingMana == mana && p.NumMinionsPlayedThisTurn == minions, "activation has no card/mana/minion accounting");
            if (health == 0) break;
            L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "closed");
            L.Check(!LocationRules.CanActivate(p, location, target), "closed cannot activate");
            L.End(game);
            L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "opponent turn stays closed");
            L.End(game);
            L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "next owner turn ajar");
            CheckReadyPhase(game, location, GameTag.LOCATION_ACTION_COOLDOWN);
            L.End(game);
            L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "opponent turn stays ajar");
            L.End(game);
            L.Check(!location.IsExhausted && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0 && LocationRules.CanActivate(p, location, target), "owner turn open");
            CheckReadyPhase(game, location, GameTag.EXHAUSTED);
        }
        L.Check(location.Zone == p.GraveyardZone && p.BoardZone.LocationCount == 0, "final charge retired");
    }
    private static void CheckReadyPhase(Game game, BoardObject location, GameTag tag)
    {
        // T1/T2: readiness changes inside the owner's MAIN_READY trigger, before turn-start effects/draw.
        var blocks = new Stack<PowerHistoryBlockStart>();
        int step = 0, found = 0;
        foreach (var entry in game.PowerHistory.Last)
        {
            if (entry is PowerHistoryBlockStart b) blocks.Push(b);
            else if (entry is PowerHistoryBlockEnd) blocks.Pop();
            else if (entry is PowerHistoryTagChange t)
            {
                if (t.EntityId == game.Id && t.Tag == GameTag.STEP) step = t.Value;
                if (t.EntityId != location.Id || t.Tag != tag || t.Value != 0) continue;
                L.Check(step == (int)Step.MAIN_READY && blocks.Count > 0 && blocks.Peek().BlockType == BlockType.TRIGGER &&
                    blocks.Peek().Source == location.Controller.Id && blocks.Peek().Index == 1, "CooldownTraceTiming owner MAIN_READY trigger");
                found++;
            }
        }
        L.Check(found == 1, "exactly one readiness change");
    }
    internal static void ActivationTraceOrder(Game game, BoardObject location, int effectTarget, bool final)
    {
        // T2 block 363; T1 L1189-1252/4475-4545: PLAY, POWER(effect,2353), DAMAGE, exhaustion/deaths, option count.
        var entries = game.PowerHistory.Last.ToList();
        var sequence = new List<string>();
        foreach (var entry in entries)
        {
            if (entry is PowerHistoryBlockStart b)
            {
                sequence.Add(b.BlockType.ToString());
                if (b.BlockType == BlockType.PLAY || b.BlockType == BlockType.POWER)
                    L.Check(b.Source == location.Id && b.Target == effectTarget, "activation block source/target");
            }
            else if (entry is PowerHistoryBlockEnd) sequence.Add("END");
            else if (entry is PowerHistoryFullEntity f && game.IdEntityDic[f.Entity.Id] is Enchantment enchantment &&
                enchantment.Card.Id == "REV_990e" && enchantment.Target.Id == effectTarget) sequence.Add("buff");
            else if (entry is PowerHistoryMetaData m && m.Type == MetaDataType.TARGET)
            {
                L.Check(m.Info.SequenceEqual(new[] { effectTarget }), "POWER META TARGET");
                sequence.Add("TARGET");
            }
            else if (entry is PowerHistoryTagChange t)
            {
                if (t.EntityId == location.Id && new[] { GameTag.CARD_TARGET, GameTag.LOCATION_ACTION_COOLDOWN,
                    GameTag.DAMAGE, GameTag.EXHAUSTED, GameTag.ZONE_POSITION, GameTag.ZONE }.Contains(t.Tag))
                    sequence.Add(t.Tag + "=" + t.Value);
                if (t.EntityId == effectTarget && t.Tag == GameTag.DAMAGE) sequence.Add("effect");
                if (t.EntityId == location.Controller.Id && t.Tag == GameTag.NUM_OPTIONS_PLAYED_THIS_TURN) sequence.Add("option");
                L.Check(!(t.EntityId == location.Id && t.Tag == GameTag.HEALTH) && t.Tag != GameTag.NUM_CARDS_PLAYED_THIS_TURN,
                    "use changes neither HEALTH nor card-play count");
            }
        }
        var expected = new List<string> { "PLAY" };
        // Like ordinary targeted plays, unchanged CARD_TARGET is not re-emitted on repeated uses.
        if (entries.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == location.Id && t.Tag == GameTag.CARD_TARGET))
            expected.Add("CARD_TARGET=" + effectTarget);
        else L.Check(location.Location.UsesCompleted > 1 && location.CardTarget == effectTarget, "retained CARD_TARGET");
        expected.AddRange(new[] { "POWER", "TARGET", "buff", "TARGET", "effect", "LOCATION_ACTION_COOLDOWN=1", "END", "DAMAGE=" + location.Location.UsesCompleted });
        expected.AddRange(final ? new[] { "DEATHS", "ZONE_POSITION=0", "ZONE=4", "DAMAGE=0", "END" } : new[] { "EXHAUSTED=1" });
        expected.AddRange(new[] { "option", "END" });
        L.Check(sequence.SequenceEqual(expected), "ActivationTraceOrder: " + string.Join(",", sequence));
    }
    private static void Pinned_ActivationTargetsElusive()
    {
        var game = L.Game(); var p = game.Player1; var source = L.Location(p);
        L.Check(!p.Options().OfType<ActivateLocationTask>().Any(), "no minions means no option");
        var friend = L.Minion(p); var enemy = L.Minion(p.Opponent);
        foreach (var (spells, heroPowers) in new[] { (true, false), (false, true), (true, true) })
        {
            enemy.CantBeTargetedBySpells = spells;
            enemy.NativeTags[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = heroPowers ? 1 : 0;
            L.Check(LocationRules.CanActivate(p, source, enemy) && p.Options().OfType<ActivateLocationTask>().Any(t => t.ActionTarget == enemy),
                "spell/hero-power Elusive protections allow location targeting");
        }
        L.Check(!LocationRules.CanActivate(p, source, p.Hero) && !LocationRules.CanActivate(p, source, source), "heroes/locations not minions");
        L.Use(source, enemy);
        L.Check(enemy.Health == 4 && enemy.AttackDamage == 6, "location activation resolves through both Elusive protections");
    }
    private static void PinnedRule_ActivationTargetsStealthImmune()
    {
        var game = L.Game(); var p = game.Player1; var source = L.Location(p);
        var friend = L.Minion(p); var enemy = L.Minion(p.Opponent);
        enemy.HasStealth = true;
        L.Check(!LocationRules.CanActivate(p, source, enemy), "enemy Stealth excluded");
        friend.HasStealth = true;
        L.Check(LocationRules.CanActivate(p, source, friend), "friendly Stealth allowed");
        enemy.HasStealth = false; enemy.IsImmune = true;
        L.Check(!LocationRules.CanActivate(p, source, enemy), "enemy Immune excluded");
        friend.IsImmune = true;
        L.Check(p.Options().OfType<ActivateLocationTask>().Single().ActionTarget == friend, "friendly Immune allowed");
        enemy.IsImmune = false;
    }
    private static void Unpinned_FullBoardSummonActivation()
    {
        var game = L.Game(); var p = game.Player1; var network = L.Location(p, "JAIL_877", 0);
        for (int i = 0; i < 6; i++) L.Minion(p);
        L.Check(!p.Options().OfType<ActivateLocationTask>().Any(), "non-final full-board summon omitted");
        network[GameTag.DAMAGE] = network[GameTag.HEALTH] - 1;
        L.Check(p.Options().OfType<ActivateLocationTask>().Count() == 1, "final-charge full-board summon offered");
        L.Use(network);
        L.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.LocationCount == 0 && p.BoardZone.GetSlot(0).Card.Id == "JAIL_877t", "final charge frees exact slot before summon");
        var entries = game.PowerHistory.Last.ToList();
        int retire = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == network.Id && t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD);
        int summon = entries.FindIndex(e => e is PowerHistoryFullEntity f && f.Entity.Id == p.BoardZone.GetSlot(0).Id);
        L.Check(retire >= 0 && summon > retire, "graveyard before summoned entity");
    }
    private static void Unpinned_LocationSummonPosition()
    {
        foreach (int pos in new[] { 0, 1, 2 })
        {
            var game = L.Game(); var p = game.Player1;
            var left = L.Minion(p); var right = L.Minion(p);
            var network = L.Location(p, "JAIL_877", pos);
            L.Use(network);
            var firstRat = p.BoardZone.Single(m => m.Card.Id == "JAIL_877t");
            L.Check(firstRat.ZonePosition == pos + 1, "nonfinal summon immediately right");
            L.End(game); L.End(game); L.End(game); L.End(game); L.Use(network);
            var finalRat = p.BoardZone.Single(m => m.Card.Id == "JAIL_877t" && m.Id != firstRat.Id);
            L.Check(finalRat.ZonePosition == pos && p.BoardZone.GetSlot(pos).Id == finalRat.Id, "new final-charge Rat at former location index");
            int[][] expected = {
                new[] { finalRat.Id, firstRat.Id, left.Id, right.Id },
                new[] { left.Id, finalRat.Id, firstRat.Id, right.Id },
                new[] { left.Id, right.Id, finalRat.Id, firstRat.Id }
            };
            L.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(expected[pos]), "full physical Slots order after final charge");
        }
    }
    private static void ActivationSignals()
    {
        foreach (string card in new[] { "CORE_REV_990", "JAIL_877" })
        foreach (bool final in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            var target = L.Minion(p); target.BaseHealth = 20;
            var location = L.Play(p, card); if (final) location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1;
            var plays = new List<string>(); var summons = new List<(string channel, int id)>();
            var phases = new List<ResolutionKind>();
            game.TriggerManager.PlayCardTrigger += _ => plays.Add("card");
            game.TriggerManager.AfterPlayCardTrigger += _ => plays.Add("after-card");
            game.TriggerManager.PlayMinionTrigger += _ => plays.Add("minion");
            game.TriggerManager.AfterPlayMinionTrigger += _ => plays.Add("after-minion");
            game.TriggerManager.CastSpellTrigger += _ => plays.Add("spell");
            game.TriggerManager.AfterCastTrigger += _ => plays.Add("after-spell");
            game.TriggerManager.SummonTrigger += e => summons.Add(("summon", e.Id));
            game.TriggerManager.AfterSummonTrigger += e => summons.Add(("after-summon", e.Id));
            game.Resolution.Trace = o => { if (o.Stage == "start") phases.Add(o.Frame.Kind); };
            L.Use(location, card == "CORE_REV_990" ? target : null);
            L.Check(plays.Count == 0 && !phases.Any(k => k == ResolutionKind.PlayCard || k == ResolutionKind.PlayMinion ||
                k == ResolutionKind.SpellCast || k == ResolutionKind.OnPlay || k == ResolutionKind.AfterPlay || k == ResolutionKind.Battlecry), "activation has no play/cast/Battlecry signaling");
            if (card == "JAIL_877")
            {
                var rat = p.BoardZone.Single(m => m.Card.Id == "JAIL_877t");
                L.Check(summons.SequenceEqual(new[] { ("after-summon", rat.Id), ("summon", rat.Id) }), "only recipe Rat produces summon events, once each");
            }
            else L.Check(summons.Count == 0, "Depths has no summon events");
            var blocks = game.PowerHistory.Last.OfType<PowerHistoryBlockStart>().Select(b => b.BlockType).ToArray();
            var expected = new List<BlockType> { BlockType.PLAY, BlockType.POWER };
            if (final && card == "JAIL_877") expected.Add(BlockType.DEATHS); // Early retirement, deferred Deathrattle pass.
            if (final) expected.Add(BlockType.DEATHS);
            L.Check(blocks.SequenceEqual(expected), "activation history block types for " + card);
        }
    }
    private static void PureValidation()
    {
        var game = L.Game(); var source = L.Location(game.Player1); var enemy = L.Minion(game.Player2);
        game.DeathProcessingAndAuraUpdate();
        typeof(Character).GetField("_modifiedStealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(enemy, null);
        byte[] before = game.SaveSnapshot();
        L.Check(LocationRules.CanActivate(game.Player1, source, enemy), "pure validation legal target");
        L.Check(before.SequenceEqual(game.SaveSnapshot()), "CanActivate must not fill target caches");
        enemy.CantBeTargetedByOpponents = true;
        L.Check(!LocationRules.CanActivate(game.Player1, source, enemy), "opponent untargetable excluded");
    }
    private static void Rejections()
    {
        var game = L.Game(); var p = game.Player1; var source = L.Location(p); var target = L.Minion(p);
        game.DeathProcessingAndAuraUpdate();
        void Reject(PlayerTask task, string label)
        {
            byte[] before = game.SaveSnapshot(); string history = Canonical.History(game); string last = Canonical.Value(game.PowerHistory.Last);
            L.Check(!game.Process(task), label + " rejected");
            L.Check(before.SequenceEqual(game.SaveSnapshot()) && history == Canonical.History(game) && last == Canonical.Value(game.PowerHistory.Last), label + " no state/decision/RNG/history change");
        }
        Reject(new ActivateLocationTask(p, source), "missing target");
        Reject(new ActivateLocationTask(p, source, p.Hero), "invalid target");
        var clone = game.CloneExact();
        Reject(new ActivateLocationTask(clone.Player1, (BoardObject)clone.IdEntityDic[source.Id], clone.IdEntityDic[target.Id]), "foreign task");
        Reject(new ActivateLocationTask(p, source, clone.IdEntityDic[target.Id]), "foreign target");
        var offBoard = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.HandZone);
        Reject(new ActivateLocationTask(p, offBoard, target), "source in hand");
        source.Location.ConditionLocked = true;
        Reject(new ActivateLocationTask(p, source, target), "condition locked"); source.Location.ConditionLocked = false;
        source[GameTag.DAMAGE] = source[GameTag.HEALTH];
        Reject(new ActivateLocationTask(p, source, target), "zero charges"); source[GameTag.DAMAGE] = 0;
        p.Choice = new Choice(p) { SourceId = target.Id, ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.STACK, Choices = new List<int> { target.Id } };
        Reject(new ActivateLocationTask(p, source, target), "pending choice"); p.Choice = null;
        var stale = new ActivateLocationTask(p, source, target);
        L.Use(source, target); Reject(stale, "duplicate");
        L.End(game); Reject(new ActivateLocationTask(p, source, target), "opponent turn");
        L.End(game); Reject(new ActivateLocationTask(p, source, target), "skipped owner turn");
        L.End(game); L.End(game); Reject(stale, "stale after reopen");
        var pending = game.GetPendingDecisions().Single();
        var option = pending.Options.First(o => o.TaskType == PlayerTaskType.ACTIVATE_LOCATION);
        // The public decision route selects on a clone and must bind back to the authoritative game.
        L.Check(game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pending.Id, p.Id, new[] { option.OptionId }), out _), "decision route activates");
    }
}
