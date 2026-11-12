using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using L = LocationProofs;

internal static class LocationTransformProof
{
    internal static void Run()
    {
        ChameleosHandChangeAndPlay();
        GnomereganStages();
        StageDamageEncoding();
        OldStageTriggerRemoved();
        HandAndDeckTransforms();
        OffZoneTransformsRefused();
        BoardChangesAndCopies();
        MinionOnlyOperationsRefuseLocations();
        Enchantments();
        AdditionalSharedPaths();
        Console.WriteLine("PASS location transform/copy/minion-only refusal/enchantments");
    }
    private static void StageDamageEncoding()
    {
        var game = L.Game(); var source = L.Play(game.Player1, "TIME_044");
        source[GameTag.DAMAGE] = 1;
        source.IsExhausted = true; source[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
        LocationRules.AdvanceStage(source, "JAIL_877");
        var change = game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryChangeEntity>().Last();
        L.Check(source[GameTag.HEALTH] == 2 && source[GameTag.DAMAGE] == 1 && source.Durability == 1 &&
            change.Entity.Tags[GameTag.HEALTH] == 2 && change.Entity.Tags[GameTag.DAMAGE] == 1 &&
            source.IsExhausted && source[GameTag.LOCATION_ACTION_COOLDOWN] == 1,
            "StageDamageEncoding new printed HEALTH, preserved DAMAGE/readiness");
    }
    private static void ChameleosHandChangeAndPlay()
    {
        var game = L.Game(); var p = game.Player1;
        var chameleos = Entity.FromCard(p, Cards.FromId("GIL_142"), zone: p.HandZone);
        // Exercise the shared change and attached trigger used by Chameleos.
        var changed = Generic.ChangeEntityBlock(p, chameleos, Cards.FromId("CORE_REV_990"), true);
        Generic.AddEnchantmentBlock(game, Cards.FromId("GIL_142e"), chameleos, changed);
        L.Check(changed is BoardObject && changed.Id == chameleos.Id && p.HandZone.Contains(changed), "Chameleos hand replacement registered");
        L.Check(changed.AppliedEnchantments?.Any(e => e.Card.Id == "GIL_142e" && e.ActivatedTrigger != null) == true && changed[GameTag.SHIFTING] == 1, "Chameleos location keeps Shifting enchantment and continuing trigger");
        foreach (var old in p.Opponent.HandZone.ToArray()) p.Opponent.HandZone.Remove(old);
        Entity.FromCard(p.Opponent, Cards.FromId("CS2_029"), zone:p.Opponent.HandZone);
        // Avoid the opponent turn's draw introducing a second possible copy.
        foreach (var old in p.Opponent.DeckZone.ToArray()) p.Opponent.DeckZone.Remove(old);
        L.End(game); L.End(game);
        var shiftedAgain = game.IdEntityDic[chameleos.Id];
        L.Check(shiftedAgain.Card.Id == "CS2_029" && shiftedAgain.Zone == p.HandZone && shiftedAgain[GameTag.SHIFTING] == 1, "copied location shifts again next owner turn");
        changed = Generic.ChangeEntityBlock(p, shiftedAgain, Cards.FromId("CORE_REV_990"), true);
        L.Check(game.IdEntityDic[chameleos.Id] == changed && game.Process(PlayCardTask.Any(p, changed, zonePosition: 0)), "transformed location playable");
        L.Check(p.BoardZone.GetSlot(0) == changed && ((BoardObject)changed).Durability == 3, "transformed location on board");
    }
    private static void GnomereganStages()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = L.Game(seed); var p = game.Player1;
            var left = L.Minion(p); var source = L.Play(p, "TIME_044", 0); var right = L.Minion(p);
            int id = source.Id, position = source.ZonePosition; var state = source.Location; var oldPower = source.Power;
            L.Use(source, left);
            L.Check(source.Card.Id == "TIME_044t1" && source.Durability == 2 && source.Location.Stage == 1 && ReferenceEquals(source.Location, state), "Past advances preserving spent durability and state");
            L.Check(source.Id == id && source.ZonePosition == position && p.BoardZone.GetSlot(position) == source && source.Power != oldPower && source.Power == Cards.FromId("TIME_044t1").Power, "stage swaps power preserving identity and physical position");
            L.Check(source.IsExhausted && source[GameTag.EXHAUSTED] == 1 && source[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "stage preserves closed tags");
            Unpinned_AdvanceOrder(game, source, left);
            foreach (var fork in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var location = (BoardObject)fork.IdEntityDic[id];
                L.End(fork); L.End(fork); L.End(fork); L.End(fork); L.Use(location, fork.IdEntityDic[right.Id]);
                L.Check(location.Card.Id == "TIME_044t2" && location.Durability == 1 && location.Location.Stage == 2 && location.ZonePosition == position, "Present advances without recharge after clone/snapshot");
                L.End(fork); L.End(fork); L.End(fork); L.End(fork); L.Use(location, fork.IdEntityDic[left.Id]);
                L.Check(location.Card.Id == "TIME_044t2" && location[GameTag.DAMAGE] == 0 && location[GameTag.HEALTH] == 3 && location.Zone == fork.Player1.GraveyardZone && location.Location.Stage == 2 && location.Location.UsesCompleted == 3, "Future final charge retires without further advance");
                L.Check(((Minion)fork.IdEntityDic[left.Id]).HasDivineShield, "Future power replaces Past power");
            }
        }
        Console.WriteLine("PASS Gnomeregan stage progression/id/position/no recharge/power/closed tags/clone/snapshot; Unpinned_AdvanceOrder");
    }
    private static void Unpinned_AdvanceOrder(Game game, BoardObject source, Minion target)
    {
        var history = game.PowerHistory.Last.ToList();
        int debit = history.FindIndex(e => e is SabberStoneCore.Kettle.PowerHistoryTagChange t && t.EntityId == source.Id && t.Tag == GameTag.DAMAGE && t.Value == 1);
        int closed = history.FindIndex(e => e is SabberStoneCore.Kettle.PowerHistoryTagChange t && t.EntityId == source.Id && t.Tag == GameTag.LOCATION_ACTION_COOLDOWN && t.Value == 1);
        int buff = history.FindIndex(e => e is SabberStoneCore.Kettle.PowerHistoryTagChange t && t.EntityId == target.Id && t.Tag == GameTag.ATK && t.Value == 6);
        int change = history.FindIndex(e => e is SabberStoneCore.Kettle.PowerHistoryChangeEntity c && c.Entity.Id == source.Id && c.CardId == "TIME_044t1");
        L.Check(buff >= 0 && change > buff && closed > change && debit > closed, "old-stage effect then CHANGE_ENTITY then cooldown then debit");
    }
    private static void OldStageTriggerRemoved()
    {
        var game = L.Game(); var p = game.Player1; var target = L.Minion(p);
        var definition = Cards.FromId("TIME_044").Clone();
        definition.Power = new Power { LocationActivation = definition.Power.LocationActivation,
            Aura = new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.CONTROLLER,
                new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 3)),
            Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
                SingleTask = new DamageTask(3, EntityType.OP_HERO) } };
        var source = (BoardObject)Entity.FromCard(p, definition, zone:p.BoardZone);
        definition.Power.Trigger.Activate(source);
        definition.Power.Aura.Activate(source); game.AuraUpdate();
        L.Check(source.OngoingEffect != null, "old stage aura attached");
        var trigger = source.ActivatedTrigger;
        L.Check(trigger != null, "old stage trigger attached");
        L.Use(source, target); int health = p.Opponent.Hero.Health; L.End(game);
        L.Check(source.ActivatedTrigger == null && source.OngoingEffect == null && p.Opponent.Hero.Health == health, "old stage subscription removed");
    }
    private static void HandAndDeckTransforms()
    {
        var game = L.Game(); var p = game.Player1;
        foreach (var zone in new SabberStoneCore.Model.Zones.IZone[] { p.HandZone, p.DeckZone })
        {
            var original = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: zone);
            new TransformTask("CORE_REV_990", EntityType.TARGET).Process(game, p, p.Hero, original);
            var location = game.IdEntityDic[original.Id];
            L.Check(location is BoardObject && location.Zone == zone && zone.GetAll.Contains(location), "transform into location in hand/deck");
            var restored = Generic.ChangeEntityBlock(p, location, Cards.FromId("CS2_182"), true);
            L.Check(restored is Minion && restored.Id == original.Id && zone.GetAll.Contains(restored), "transform location back in hand/deck");
        }
    }
    private static void OffZoneTransformsRefused()
    {
        var game = L.Game(); var p = game.Player1;
        foreach (var zone in new SabberStoneCore.Model.Zones.IZone[] { null, p.SetasideZone, p.GraveyardZone })
        foreach (string id in new[] { "CS2_182", "CORE_REV_990" })
        {
            var original = Entity.FromCard(p, Cards.FromId(id), zone: zone);
            string replacement = id == "CS2_182" ? "CORE_REV_990" : "JAIL_877";
            var result = Generic.ChangeEntityBlock(p, original, Cards.FromId(replacement), true);
            L.Check(result == original && original.Card.Id == id && original.Zone == zone && game.IdEntityDic[original.Id] == original,
                "location change outside hand/deck/board refused before mutation");
        }
    }
    private static void BoardChangesAndCopies()
    {
        var game = L.Game(); var p = game.Player1;
        var left = L.Minion(p); var location = L.Location(p); var right = L.Minion(p);
        var changed = Generic.ChangeEntityBlock(p, location, Cards.FromId("JAIL_877"), true);
        L.Check(changed == location && p.BoardZone.Slots.SequenceEqual(new IPlayable[] { left, location, right }), "same-type board change preserves identity and order");
        L.Check(Generic.ChangeEntityBlock(p, location, Cards.FromId("CS2_182"), true) == location && location.Card.Id == "JAIL_877", "board location-to-minion refused");
        L.Check(Generic.ChangeEntityBlock(p, left, Cards.FromId("CORE_REV_990"), true) == left && left.Card.Id == "CS2_182", "board minion-to-location refused");
        location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1; location.IsExhausted = true; location[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
        location.Location.UsesCompleted = 1;
        foreach (var zone in new[] { Zone.HAND, Zone.DECK, Zone.PLAY })
        {
            var copy = Generic.Copy(p, p.Hero, location, zone);
            L.Check(copy is BoardObject && copy.Id != location.Id && copy.Zone.Type == zone && copy.Card == location.Card, "location copy to " + zone);
            var copied = (BoardObject)copy;
            if (zone == Zone.PLAY)
            {
                L.Check(copied.Durability == 1 && copied.IsExhausted && copied[GameTag.LOCATION_ACTION_COOLDOWN] == 1 &&
                    copied.Location.UsesCompleted == 1 && !ReferenceEquals(copied.Location, location.Location), "board copy preserves independent location state");
            }
            else L.Check(copied.Durability == 2 && !copied.IsExhausted && copied.Location.UsesCompleted == 0, "backward copy resets location state");
        }
        p.BoardZone.Swap(left, right);
        L.Check(p.BoardZone.GetSlot(0) == right && p.BoardZone.GetSlot(1) == location && p.BoardZone.GetSlot(2) == left, "minion swap across location uses physical positions");
    }
    private static void MinionOnlyOperationsRefuseLocations()
    {
        var game = L.Game(); var p = game.Player1; var location = L.Location(p); var minion = L.Minion(p);
        new ControlTask(EntityType.TARGET).Process(game, p.Opponent, p.Opponent.Hero, location);
        new ReturnHandTask(EntityType.TARGET).Process(game, p, p.Hero, location);
        new TransformTask("CS2_182", EntityType.TARGET, useOldMechanism: true).Process(game, p, p.Hero, location);
        L.Check(new TransformCopyTask().Process(game, p, minion, location) == TaskState.STOP, "transform-copy location target refused");
        L.Check(new TransformCopyTask().Process(game, p, location, minion) == TaskState.STOP, "transform-copy location source refused");
        L.Check(new SummonCopyTask(EntityType.TARGET).Process(game, p, p.Hero, location) == TaskState.STOP, "summon-copy board location refused");
        var hand = Entity.FromCard(p, location.Card, zone: p.HandZone);
        L.Check(new SummonCopyTask(EntityType.TARGET).Process(game, p, p.Hero, hand) == TaskState.STOP, "summon-copy hand location refused");
        int entities = game.IdEntityDic.Count;
        var summonStack = new TaskStack { Playables = new List<IPlayable>() };
        L.Check(new SummonTask("CORE_REV_990", addToStack: true).Process(game, p, p.Hero, null, summonStack) == TaskState.STOP &&
            game.IdEntityDic.Count == entities && summonStack.Playables.Count == 0, "summon location refused without orphan entity or null stack item");
        L.Check(new SummonOpTask("CORE_REV_990").Process(game, p, p.Hero, null) == TaskState.STOP, "opponent summon location refused");
        L.Check(new SwapAttackHealthTask(EntityType.TARGET, "EX1_059e").Process(game, p, p.Hero, location) == TaskState.STOP, "stat swap refuses location");
        L.Check(location.Controller == p && location.Zone == p.BoardZone && location.Card.Id == "CORE_REV_990" && p.BoardZone.SlotCount == 2 && p.Opponent.BoardZone.SlotCount == 0, "minion-only operations leave location intact");
    }
    private static void AdditionalSharedPaths()
    {
        var failures = new List<Exception>();
        void Case(string name, Action test) { try { test(); } catch (Exception e) { failures.Add(new Exception(name, e)); } }
        Case("summon stack refusal", () => {
            var game = L.Game(); var p = game.Player1; var location = L.Location(p);
            var stack = new TaskStack { Playables = new List<IPlayable> { location } };
            new SummonStackTask(removeFromZone: true).Process(game, p, p.Hero, null, stack);
            L.Check(location.Zone == p.BoardZone && p.BoardZone.SlotCount == 1, "summon-stack leaves location in original zone");
        });
        Case("spell cast refusal", () => {
            var game = L.Game(); var p = game.Player1; var location = L.Location(p);
            byte[] before = game.SaveSnapshot();
            L.Check(new CastRandomSpellTask().Process(game, p, p.Hero, location) == TaskState.STOP &&
                before.SequenceEqual(game.SaveSnapshot()), "cast refuses location before RNG/mutation");
        });
        Case("location aura storage", () => {
            var game = L.Game(); var p = game.Player1;
            var location = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.HandZone);
            var effect = new Effect(GameTag.ECHO, EffectOperator.ADD, 1);
            effect.ApplyAuraTo(location);
            L.Check(location.AuraEffects.Echo && location[GameTag.HEALTH] == 3, "location playable aura preserves durability reads");
            foreach (var operation in new[] { EffectOperator.ADD, EffectOperator.SUB, EffectOperator.SET })
            {
                var charge = new Effect(GameTag.CHARGE, operation, 1);
                charge.ApplyAuraTo(location);
                L.Check(location[GameTag.CHARGE] == 0, "location refuses combat aura tags");
                charge.RemoveAuraFrom(location);
                L.Check(location[GameTag.CHARGE] == 0, "location combat aura removal stays harmless");
            }
            effect.RemoveAuraFrom(location);
            L.Check(!location.AuraEffects.Echo, "location playable aura removal");
        });
        Case("location summon source positions", () => {
            var game = L.Game(); var p = game.Player1; L.Minion(p); var location = L.Location(p); L.Minion(p);
            int alternate = 0;
            L.Check(SummonTask.GetPosition(location, SummonSide.LEFT, 0, ref alternate) == 1 &&
                SummonTask.GetPosition(location, SummonSide.RIGHT, 0, ref alternate) == 2, "location live summon positions");
            location[GameTag.DAMAGE] = location[GameTag.HEALTH]; LocationRules.RetireFinalCharge(location);
            L.Check(SummonTask.GetPosition(location, SummonSide.DEATHRATTLE, 0, ref alternate) == 1, "location death summon position");
            L.Check(SummonTask.GetPosition(location, SummonSide.RIGHT, 0, ref alternate) == 1, "retired location right means former slot");
        });
        Case("replay of played location", () => {
            var game = L.Game(); var p = game.Player1; var location = L.Play(p, "CORE_REV_990");
            SpecificTask.TessGreymane.Process(game, p, p.Hero, null);
            L.Check(p.BoardZone.GetLocations().Length == 1 && location.Zone == p.BoardZone, "legacy replay explicitly skips locations");
        });
        if (failures.Count != 0) throw new AggregateException(failures);
    }
    private static void Enchantments()
    {
        var game = L.Game(); var p = game.Player1; var location = L.Location(p);
        new AddEnchantmentTask("EX1_158e", EntityType.TARGET).Process(game, p, p.Hero, location);
        L.Check(location.HasDeathrattle && location.AppliedEnchantments.Single().Card.Id == "EX1_158e", "granted deathrattle supported");
        new AddEnchantmentTask("CS2_103e2", EntityType.TARGET).Process(game, p, p.Hero, location);
        L.Check(location.AppliedEnchantments.Count == 1 && location[GameTag.CHARGE] == 0, "combat enchantment refused before attachment");
    }
}
