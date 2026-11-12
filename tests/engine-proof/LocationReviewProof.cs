using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using L = LocationProofs;

internal static class LocationReviewProof
{
    internal static void Run()
    {
        var failures = new List<Exception>();
        foreach (Action proof in new Action[] { NoggenfoggerRenovator, EmptyLocationRedirectKeepsRng, EmptySpellRedirectKeepsRng,
            ThaurissanDiscountsLocation, GenericPlayCountMeansMinions, CompetitiveSpiritNeedsMinion,
            MechathunIgnoresLocations, MagneticUsesPhysicalSlots, CooldownSkipsOwnersNextTurn, FriendlyImmuneActivationTarget,
            PlayedLocationSubscriptions, MixedDeathsKeepOriginalPositions })
        {
            try { proof(); Console.WriteLine("PASS " + proof.Method.Name); }
            catch (Exception e) { failures.Add(new Exception(proof.Method.Name, e)); }
        }
        if (failures.Count != 0) throw new AggregateException(failures);
    }

    private static void NoggenfoggerRenovator()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = L.Game(seed); var p = game.Player1;
            L.Minion(p, "CFM_670");
            var first = L.Location(p.Opponent); var second = L.Location(p.Opponent, "JAIL_877");
            var source = Entity.FromCard(p, Cards.FromId("CORE_REV_023"), zone: p.HandZone);
            var play = p.Options().OfType<PlayCardTask>().First(t => t.Source == source && t.ActionTarget == first);
            int mana = p.RemainingMana;
            L.Check(game.Process(play), "Renovator with Noggenfogger plays without exception");
            var destroyed = new[] { first, second }.Single(e => e.Zone == p.Opponent.GraveyardZone);
            L.Check(p.Opponent.BoardZone.LocationCount == 1 && source.Zone == p.BoardZone &&
                p.RemainingMana == mana - 3 && game.NumMinionsKilledThisTurn == 0, "redirect destroys exactly one legal enemy location");
            var history = game.PowerHistory.Last;
            L.Check(history.OfType<PowerHistoryTagChange>().Count(t => t.EntityId == destroyed.Id &&
                t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD) == 1 &&
                history.OfType<PowerHistoryBlockStart>().Count() == history.OfType<PowerHistoryBlockEnd>().Count(),
                "redirect history records one retirement and balanced blocks");
        }
    }

    private static void EmptyLocationRedirectKeepsRng()
    {
        var game = L.Game(); var p = game.Player1; var mayor = L.Minion(p, "CFM_670");
        var source = Entity.FromCard(p, Cards.FromId("CORE_REV_023"), zone: p.HandZone);
        source.CardTarget = p.Opponent.Hero.Id;
        var rng = (Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var before = rng.CaptureState();
        SpecificTask.MayorNoggenfogger.Process(game, p, mayor, source);
        L.Check(source.CardTarget == p.Opponent.Hero.Id && rng.CaptureState().Equals(before),
            "empty location redirect keeps original target without RNG draw");
    }

    private static void EmptySpellRedirectKeepsRng()
    {
        var game = L.Game(); var p = game.Player1; var mayor = L.Minion(p, "CORE_CFM_670");
        var source = Entity.FromCard(p, Cards.FromId("CORE_EDR_002"), zone: p.HandZone);
        source.CardTarget = mayor.Id;
        L.Check(!source.ValidPlayTargets.Any(), "Poison Breath has no friendly Undead target");
        var rng = (Util.DeepCloneableRandom)typeof(Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var before = rng.CaptureState();
        SpecificTask.MayorNoggenfogger.Process(game, p, mayor, source);
        L.Check(source.CardTarget == mayor.Id && rng.CaptureState().Equals(before),
            "empty spell redirect keeps target for ordinary fizzle without RNG draw");
    }

    private static void ThaurissanDiscountsLocation()
    {
        foreach (bool history in new[] { false, true })
        {
            var game = L.Game(history: history); var p = game.Player1;
            L.Minion(p, "BRM_028");
            var source = Entity.FromCard(p, Cards.FromId("TIME_044"), zone: p.HandZone);
            L.End(game); L.End(game);
            L.Check(source.Cost == 1, "Thaurissan reduces hand location cost");
            p.BaseMana = 1; p.UsedMana = 0;
            var play = p.Options().OfType<PlayCardTask>().First(t => t.Source == source);
            L.Check(game.Process(play) && p.RemainingMana == 0 && source.Zone == p.BoardZone,
                "discounted location offered and reduced cost paid");
            int boardCost = source.Cost;
            Generic.AddEnchantmentBlock(game, Cards.FromId("BRM_028e"), p.Hero, source);
            L.Check(source.Cost == boardCost, "board location refuses cost enchantment");
        }
    }

    private static void GenericPlayCountMeansMinions()
    {
        var game = L.Game(); var p = game.Player1; var location = L.Location(p);
        L.Check(SelfCondition.IsZoneCount(Zone.PLAY, 0).Eval(p.Hero) &&
            SelfCondition.IsOpZoneCount(Zone.PLAY, 0).Eval(p.Opponent.Hero), "generic conditions ignore locations");
        var stack = new TaskStack();
        foreach (bool opponent in new[] { false, true })
        {
            var reader = opponent ? p.Opponent : p;
            new CountTask(Zone.PLAY, false, opponent).Process(game, reader, reader.Hero, null, stack);
            L.Check(stack.Number == 0, "generic CountTask ignores location");
            new CountTask(Zone.PLAY, true, opponent).Process(game, reader, reader.Hero, null, stack);
            L.Check(stack.Number == 6, "CountTask free space counts physical slots");
        }
        L.Minion(p);
        L.Check(p.ControlledZones[Zone.PLAY].Count == 1 && p.ControlledZones[Zone.PLAY].FreeSpace == 5,
            "generic board count retains minions, capacity includes locations");
    }

    private static void CompetitiveSpiritNeedsMinion()
    {
        var game = L.Game(); var p = game.Player1; L.Location(p);
        var secret = Entity.FromCard(p, Cards.FromId("AT_073"), zone: p.HandZone);
        L.Check(game.Process(PlayCardTask.Any(p, secret)), "play Competitive Spirit");
        L.End(game); L.End(game);
        L.Check(secret.Zone == p.SecretZone, "Competitive Spirit stays armed with only location");
        var minion = L.Minion(p); L.End(game); L.End(game);
        L.Check(secret.Zone == p.GraveyardZone && minion.AttackDamage == 5 && minion.Health == 6,
            "Competitive Spirit still fires with minion beside location");
    }

    private static void MechathunIgnoresLocations()
    {
        var game = L.Game(); var p = game.Player1; var location = L.Location(p);
        var mechathun = L.Minion(p, "BOT_424");
        foreach (var card in p.HandZone.ToArray()) p.HandZone.Remove(card);
        foreach (var card in p.DeckZone.ToArray()) p.DeckZone.Remove(card);
        mechathun.Destroy(); game.DeathProcessingAndAuraUpdate();
        L.Check(p.Opponent.Hero.ToBeDestroyed && location.Zone == p.BoardZone,
            "Mecha'thun empty-board deathrattle ignores surviving location");
    }

    private static void MagneticUsesPhysicalSlots()
    {
        foreach (bool blocked in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1; L.Location(p);
            var mech = L.Minion(p, "BOT_020");
            var source = Entity.FromCard(p, Cards.FromId("BOT_563"), zone: p.HandZone);
            L.Check(game.Process(PlayCardTask.Any(p, source, zonePosition: blocked ? 0 : 1)), "play Magnetic beside location");
            L.Check(blocked ? source.Zone == p.BoardZone && mech.AttackDamage == 1 :
                source.Zone == p.SetasideZone && mech.AttackDamage == 6 && mech.Health == 6,
                "Magnetic uses physical right neighbour and cannot cross location");
        }
    }

    private static void CooldownSkipsOwnersNextTurn()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool secondPlayer in new[] { false, true })
        {
            var game = L.Game(seed);
            if (secondPlayer) L.End(game);
            var p = game.CurrentPlayer;
            var source = L.Location(p, "JAIL_877");
            L.Check(!source.IsExhausted && source[GameTag.EXHAUSTED] == 0 &&
                source[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "new location open on owner turn N");
            L.Use(source);
            L.Check(source.IsExhausted && source[GameTag.EXHAUSTED] == 1 &&
                source[GameTag.LOCATION_ACTION_COOLDOWN] == 1, "use closes location on owner turn N");
            L.End(game); L.End(game);
            L.Check(game.CurrentPlayer == p && source.IsExhausted && source[GameTag.EXHAUSTED] == 1 &&
                source[GameTag.LOCATION_ACTION_COOLDOWN] == 0 && !LocationRules.CanActivate(p, source, null) &&
                !p.Options().OfType<ActivateLocationTask>().Any(t => t.Source == source),
                "ajar location skips owner turn N+1");
            byte[] before = game.SaveSnapshot();
            L.Check(!game.Process(new ActivateLocationTask(p, source)) && before.SequenceEqual(game.SaveSnapshot()),
                "activation on skipped owner turn rejected without mutation");
            L.End(game); L.End(game);
            L.Check(game.CurrentPlayer == p && !source.IsExhausted && source[GameTag.EXHAUSTED] == 0 &&
                source[GameTag.LOCATION_ACTION_COOLDOWN] == 0 && LocationRules.CanActivate(p, source, null),
                "location opens on owner turn N+2");
            L.Use(source);
            L.Check(source.Zone == p.GraveyardZone, "owner turn N+2 activation resolves final charge");
        }
    }

    private static void FriendlyImmuneActivationTarget()
    {
        var game = L.Game(); var p = game.Player1; var source = L.Location(p);
        var friend = L.Minion(p); friend.IsImmune = true;
        var enemy = L.Minion(p.Opponent); enemy.IsImmune = true;
        L.Check(LocationRules.CanActivate(p, source, friend) && !LocationRules.CanActivate(p, source, enemy) &&
            p.Options().OfType<ActivateLocationTask>().Single().ActionTarget == friend,
            "friendly Immune target offered; enemy Immune excluded");
        L.Use(source, friend);
        L.Check(friend.Health == 5 && friend.AttackDamage == 6 && source.Durability == 2,
            "friendly Immune prevents damage but still receives activation buff");
    }

    private static void PlayedLocationSubscriptions()
    {
        foreach (var triggerType in new[] { TriggerType.TURN_END, TriggerType.PLAY_CARD })
        foreach (bool destroyDuringPlay in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            var definition = Cards.FromId("JAIL_877").Clone();
            definition.Power = new Power { LocationActivation = definition.Power.LocationActivation,
                Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 3)),
                Trigger = new Trigger(triggerType) { TriggerSource = TriggerSource.ALL,
                    SingleTask = new DamageTask(3, EntityType.OP_HERO) } };
            var source = (BoardObject)Entity.FromCard(p, definition, zone: p.HandZone);
            int onPlay = 0;
            var listener = new Trigger(TriggerType.PLAY_CARD) { TriggerSource = TriggerSource.ALL,
                SingleTask = new CustomTask((_, _, _, _, _) => {
                    onPlay++;
                    L.Check(source.ActivatedTrigger == null && source.OngoingEffect == null && p.CurrentSpellPower == 0,
                        "On Play resolves before location's own trigger/aura activate");
                    if (destroyDuringPlay) source.Destroy();
                }) }.Activate(L.Minion(p));
            int health = p.Opponent.Hero.Health;
            L.Check(game.Process(PlayCardTask.Any(p, source, zonePosition: 0)), "play test location with aura/trigger");
            listener.Remove();
            L.Check(onPlay == 1 && p.Opponent.Hero.Health == health,
                "existing On Play trigger fires; location PLAY_CARD trigger does not see own play");
            if (destroyDuringPlay)
            {
                L.Check(source.Zone == p.GraveyardZone && p.CurrentSpellPower == 0 &&
                    source.ActivatedTrigger == null && source.OngoingEffect == null,
                    "location destroyed during On Play never activates subscriptions");
                continue;
            }
            L.Check(p.CurrentSpellPower == 3 && source.ActivatedTrigger != null && source.OngoingEffect != null,
                "played location activates own aura and trigger after On Play");
            if (triggerType == TriggerType.TURN_END) L.End(game);
            else L.Check(game.Process(PlayCardTask.Any(p, Entity.FromCard(p, Cards.FromId("GAME_005"), zone: p.HandZone))),
                "play subsequent card");
            L.Check(p.Opponent.Hero.Health == health - 3, "played location trigger fires once on subsequent event");
            source.Destroy(); game.DeathProcessingAndAuraUpdate();
            L.Check(p.CurrentSpellPower == 0 && source.ActivatedTrigger == null && source.OngoingEffect == null,
                "location removal clears subscriptions");
            L.End(game); L.End(game);
            L.Check(p.Opponent.Hero.Health == health - 3, "removed location trigger stays detached");
        }
    }

    private static void MixedDeathsKeepOriginalPositions()
    {
        foreach (bool locationFirst in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            Minion minion; BoardObject location;
            if (locationFirst) { location = L.Location(p); minion = L.Minion(p, position: 0); }
            else { minion = L.Minion(p); location = L.Location(p); }
            L.Minion(p);
            minion.Destroy(); location.Destroy(); game.DeathProcessingAndAuraUpdate();
            L.Check(minion.LastBoardPosition == 0 && location.LastBoardPosition == 1,
                "mixed deaths retain original physical positions despite earlier removal");
        }
    }
}
