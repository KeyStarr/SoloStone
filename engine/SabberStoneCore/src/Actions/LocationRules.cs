using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Actions
{
    public static class LocationRules
    {
        internal const GameTag PendingActivation = (GameTag)33122;
        internal static bool HasPending(Controller c) => c.Game.IdEntityDic.Values.Any(p => p.Controller == c && p[PendingActivation] != 0);
        internal static void Resume(Controller c)
        {
            if (c.Game.Resolution.HasSuspendedEffect) return;
            foreach (var source in c.Game.IdEntityDic.Values.OfType<BoardObject>().Where(p => p.Controller == c && p[PendingActivation] != 0).ToArray())
                Complete(source, source[PendingActivation] != 1, source[PendingActivation] == 3);
        }

        public static bool CanActivate(Controller controller, IPlayable actor, IPlayable target)
        {
            if (actor is Minion) return QuestRules.CanActivateRift(controller, actor, target);
            var source = actor as BoardObject;
            if (controller == null || source == null) return false;
            Game game = controller.Game;
            LocationActivation activation = source.Power?.LocationActivation;
            if (game.CurrentPlayer != controller || game.State != State.RUNNING || game.Step != Step.MAIN_ACTION ||
                game.Player1.Choice != null || game.Player2.Choice != null || game.Resolution.HasSuspendedEffect ||
                source.Controller != controller || source.Zone != controller.BoardZone ||
                !game.IdEntityDic.TryGetValue(source.Id, out var registered) || registered != source ||
                source.IsExhausted || source[GameTag.EXHAUSTED] != 0 || source[GameTag.LOCATION_ACTION_COOLDOWN] != 0 ||
                source.ToBeDestroyed || source.Durability <= 0 || source.Location.ConditionLocked || activation?.Task == null)
                return false;
            if (activation.RequiresFreeSlot && controller.BoardZone.IsFull && source.Durability != 1) return false;
            if (activation.Target == LocationTargetKind.None) return target == null &&
                (activation.TargetCondition == null || activation.TargetCondition(controller, null));
            if (activation.Target == LocationTargetKind.Character && target is Hero hero)
                return hero.Game == game && hero == hero.Controller.Hero && !hero.ToBeDestroyed &&
                    (hero.Controller == controller || !hero.IsImmune && !hero.CantBeTargetedByOpponents) &&
                    (activation.TargetCondition == null || activation.TargetCondition(controller, target));
            return (activation.Target == LocationTargetKind.Minion || activation.Target == LocationTargetKind.Character) && target is Minion minion &&
                minion.Game == game && game.IdEntityDic.TryGetValue(minion.Id, out var actual) && actual == minion &&
                (minion.Zone == controller.BoardZone || minion.Zone == controller.Opponent.BoardZone) &&
                (activation.TargetCondition == null || activation.TargetCondition(controller, target)) &&
                !minion.Untouchable && !minion.IsDormant && !minion.ToBeDestroyed &&
                (minion.Controller == controller ||
                 (!minion.IsImmune && !(minion._modifiedStealth ?? minion.Card.Stealth) && !minion.CantBeTargetedByOpponents));
        }

        public static bool Activate(Controller controller, IPlayable actor, IPlayable target = null)
        {
            if (actor is Minion) return QuestRules.ActivateRift(controller, actor, target);
            var source = actor as BoardObject;
            if (!CanActivate(controller, source, target)) return false;
            Game game = controller.Game;
            game.Facts.AcceptPlayerAction(source);
            using var resolution = game.Resolution.Sequence(ResolutionKind.EffectResolution, game, source);
            var activation = source.Power.LocationActivation;
            bool finalCharge = source.Durability == 1;
            bool clearBeforeEffect = finalCharge && activation.RequiresFreeSlot;
            bool resumable = activation.Task is SabberStoneCore.Tasks.SuspendableTask;
            if (resumable) source[PendingActivation] = clearBeforeEffect ? 3 : finalCharge ? 2 : 1;
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, target?.Id ?? 0));
            if (target != null) source.CardTarget = target.Id;
            // SubOption 0 selects a Choose One effect; -1 attaches POWER to the primary spell started by native input.
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, target?.Id ?? 0));
            if (game.History && target != null)
                game.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET,
                    Info = new System.Collections.Generic.List<int> { target.Id } });
            // Patch 32.0 clears only summoning locations before their final effect.
            if (clearBeforeEffect)
            {
                source[GameTag.DAMAGE]++;
                RetireFinalCharge(source);
                if (resumable) game.DeadBoardObjects.Remove(source);
            }
            game.TaskQueue.StartEvent();
            activation.Task.Process(game, controller, source, target);
            game.ProcessTasks();
            game.TaskQueue.EndEvent();
            if (game.Resolution.HasSuspendedEffect) return true;
            Complete(source, finalCharge, clearBeforeEffect);
            return true;
        }

        private static void Complete(BoardObject source, bool finalCharge, bool clearBeforeEffect)
        {
            var controller = source.Controller; var game = controller.Game;
            if (source[PendingActivation] == 3 && source.Zone == controller.GraveyardZone && !game.DeadBoardObjects.Contains(source))
                game.DeadBoardObjects.Add(source);
            if (source[PendingActivation] != 0) source[PendingActivation] = 0;
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            if (!clearBeforeEffect && source.Zone == controller.BoardZone)
            {
                source[GameTag.DAMAGE]++;
                if (finalCharge) source.Destroy();
                else
                {
                    source.IsExhausted = true;
                    source[GameTag.EXHAUSTED] = 1;
                }
            }
            source.Location.UsesCompleted++;
            // Deathrattles can summon; finish their deferred summon events before the next decision.
            do { game.DeathProcessingAndAuraUpdate(); }
            while (game.SummonedMinions.Count != 0 || game.DeadMinions.Count != 0 || game.DeadBoardObjects.Count != 0);
            controller.NumOptionsPlayedThisTurn++;
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        }

        public static void AdvanceStage(BoardObject source, string nextCardId)
        {
            // Recipes advance before the charge is spent; the last use must keep its current definition.
            if (source.Zone != source.Controller.BoardZone || source.Durability <= 1 || source.ToBeDestroyed) return;
            Card next = Cards.FromId(nextCardId);
            if (next.Type != CardType.LOCATION) throw new System.ArgumentException("Stage must be a location.", nameof(nextCardId));
            var tags = new EntityData();
            foreach (var tag in source.Card.Tags.Keys.Union(next.Tags.Keys)) tags[tag] = next[tag];
            // CHANGE_ENTITY preserves spent charges/readiness over the new stage's printed HEALTH.
            foreach (var tag in new[] { GameTag.DAMAGE, GameTag.EXHAUSTED, GameTag.LOCATION_ACTION_COOLDOWN })
            {
                tags[tag] = source[tag];
                source[tag] = tags[tag];
            }
            source.ActivatedTrigger?.Remove();
            source.OngoingEffect?.Remove();
            source.NativeTags.Remove(GameTag.HEALTH);
            source.Card = next;
            source.Location.Stage++;
            if (source.Game.History) source.Game.PowerHistory.Add(new PowerHistoryChangeEntity {
                CardId = next.Id, Entity = new PowerHistoryEntity { Id = source.Id, Name = "", Tags = tags }
            });
            source.Power?.Trigger?.Activate(source);
            source.Power?.Aura?.Activate(source);
        }

        public static BoardObject[] ReopenTargets(Controller c) => c.BoardZone.GetLocations().Concat(c.Opponent.BoardZone.GetLocations())
            .Where(e => !e.ToBeDestroyed && e.Durability > 0).ToArray();
        public static bool ValidReopenTarget(Controller c, IPlayable target) => target != null && ReopenTargets(c).Any(e => ReferenceEquals(e, target));
        public static void Reopen(BoardObject source) { source.IsExhausted = false; source[GameTag.EXHAUSTED] = 0; source[GameTag.LOCATION_ACTION_COOLDOWN] = 0; }
        public static bool TargetsLocations(Card card) => card.Power?.BattlecryLocationTarget is LocationTargetKind.EnemyLocation or LocationTargetKind.Location;
        public static BoardObject[] PlayTargets(Controller c, Card card) => card.Power?.BattlecryLocationTarget == LocationTargetKind.Location ? ReopenTargets(c) : BattlecryTargets(c);
        public static bool ValidPlayTarget(Controller c, Card card, IPlayable target) => card.Power?.BattlecryLocationTarget == LocationTargetKind.Location ? ValidReopenTarget(c, target) : IsValidBattlecryTarget(c, target);

        public static BoardObject[] BattlecryTargets(Controller controller) =>
            controller.Opponent.BoardZone.GetLocations().Where(location => !location.ToBeDestroyed && location.Durability > 0).ToArray();

        public static bool IsValidBattlecryTarget(Controller controller, IPlayable target)
        {
            var targets = BattlecryTargets(controller);
            return target == null ? targets.Length == 0 : targets.Any(location => ReferenceEquals(location, target));
        }

        public static void RetireFinalCharge(BoardObject source)
        {
            if (source.Durability != 0 || source.Zone != source.Controller.BoardZone) return;
            source.Destroy();
            source.LastBoardPosition = source.ZonePosition;
            if (source.Game.History) source.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.DEATHS, 1, "", 0, 0));
            source.Game.RetireBoardObject(source, queueDeathrattle: false);
            if (source.Game.History) source.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        }

        public static void RefreshAtTurnStart(Game game)
        {
            if (!game.Player1.BoardZone.HasLocations && !game.Player2.BoardZone.HasLocations) return;
            foreach (BoardObject location in game.CurrentPlayer.BoardZone.GetLocations())
            {
                if (location[GameTag.LOCATION_ACTION_COOLDOWN] != 0)
                    location[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
                else if (location.IsExhausted || location[GameTag.EXHAUSTED] != 0)
                {
                    location.IsExhausted = false;
                    location[GameTag.EXHAUSTED] = 0;
                }
            }
        }
    }
}
