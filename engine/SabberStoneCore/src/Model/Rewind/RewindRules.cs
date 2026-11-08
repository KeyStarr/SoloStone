using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Model.Rewind
{
    /// <summary>R1–R10 policy. Snapshot/replay mechanics stay independent of individual card effects.</summary>
    public static class RewindRules
    {
        public static long SkippedNonQuiescentBoundaries { get; private set; }
        private static readonly string[] Clocksworth = { "TIME_038", "TIME_038t1", "TIME_038t2", "TIME_038t3" };
        private static int Budget(string id) => Clocksworth.Contains(id) ? 3 : 1;

        private static bool HeroPowerLegal(Game game, IPlayable source, int targetId, int chooseOne)
        {
            if (!(source is HeroPower power) || !game.CurrentPlayer.HeroPowers.Contains(power) ||
                power.IsPassiveHeroPower || ImbueRules.IsPassivePower(power.Card) || !power.IsPlayable) return false;
            IPlayable target = null;
            if (targetId != 0 && (!game.IdEntityDic.TryGetValue(targetId, out target) || !(target is ICharacter))) return false;
            return power.IsValidPlayTarget(target as ICharacter) &&
                (power.ChooseOne ? chooseOne > 0 && chooseOne <= (power.ChooseOnePlayables?.Length ?? 0) : chooseOne == 0);
        }

        public static bool WouldBegin(Game game, PlayerTask task)
        {
            if (game.RewindDisabled || game.Rewind != null || game.State != State.RUNNING ||
                game.Step != Step.MAIN_ACTION || game.Player1.Choice != null || game.Player2.Choice != null ||
                task?.Controller?.Id != game.CurrentPlayer.Id) return false;
            if (task is HeroPowerTask)
            {
                var power = task.Source as HeroPower ?? game.CurrentPlayer.Hero.HeroPower;
                return power.GetNativeGameTag(GameTag.REWIND) == 1 &&
                    HeroPowerLegal(game, power, task.ActionTarget?.Id ?? 0, task.ChooseOne);
            }
            if (!(task is PlayCardTask) || task.Source == null ||
                !game.IdEntityDic.TryGetValue(task.Source.Id, out var source)) return false;
            return source.Controller == game.CurrentPlayer && source.Zone == source.Controller.HandZone &&
                ((Entity)source).GetNativeGameTag(GameTag.REWIND) == 1 && source[GameTag.USED_REWIND] < Budget(source.Card.Id);
        }

        public static bool TryBegin(Game game, PlayerTask task)
        {
            if (!WouldBegin(game, task)) return false;
            if (!game.IsSnapshotBoundary)
            {
                SkippedNonQuiescentBoundaries++;
                return false;
            }
            bool heroPower = task is HeroPowerTask;
            var source = heroPower ? task.Source ?? game.CurrentPlayer.Hero.HeroPower : game.IdEntityDic[task.Source.Id];
            game.Rewind = new RewindTransaction {
                Stage = RewindStage.Attempt, Baseline = game.SaveSnapshot(), ControllerId = source.Controller.Id,
                SourceId = source.Id, TargetId = task.ActionTarget?.Id ?? 0,
                ZonePosition = heroPower ? -1 : ((PlayCardTask)task).ZonePosition,
                ChooseOne = task.ChooseOne, SourceCardId = source.Card.Id,
                IsHeroPowerActivation = heroPower, Budget = heroPower ? 1 : Budget(source.Card.Id),
                Used = heroPower ? 0 : source[GameTag.USED_REWIND]
            };
            return true;
        }

        public static void TrySettle(Game game)
        {
            var transaction = game.Rewind;
            if (transaction == null) return;
            if (game.State != State.RUNNING)
            {
                if (game.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND) game.Player1.Choice = null;
                if (game.Player2.Choice?.ChoiceAction == ChoiceAction.REWIND) game.Player2.Choice = null;
                game.Rewind = null;
                return;
            }
            if (transaction.Stage != RewindStage.Attempt || game.Player1.Choice != null || game.Player2.Choice != null ||
                game.Resolution.HasSuspendedEffect || game.SpellCompletion != null ||
                Generic.HasDecisionPlay(game.Player1) || Generic.HasDecisionPlay(game.Player2) || !game.TaskQueue.IsQuiescent) return;
            if (transaction.Used >= transaction.Budget) { game.Rewind = null; return; }
            var controller = game.ControllerById(transaction.ControllerId);
            transaction.RewindEntityId = Entity.FromCard(controller, Cards.FromId("TIME_000tb"), zone: controller.SetasideZone).Id;
            transaction.KeepEntityId = Entity.FromCard(controller, Cards.FromId("TIME_000ta"), zone: controller.SetasideZone).Id;
            controller.Choice = new Choice(controller) { ChoiceAction = ChoiceAction.REWIND, ChoiceType = ChoiceType.GENERAL,
                SourceId = transaction.SourceId, Choices = new List<int> { transaction.RewindEntityId, transaction.KeepEntityId } };
            transaction.OfferChoiceId = controller.Choice.DecisionId;
            transaction.KeepBoth = controller.AppliedEnchantments?.Any(e => e.Card.Id == "END_036e") == true;
            transaction.Stage = RewindStage.Offered;
        }

        // Called before Process bookkeeping: invalid direct ChooseTasks must be mutation-free too.
        internal static bool Answer(Game game, ChooseTask answer)
        {
            var transaction = game.Rewind;
            if (transaction?.Stage != RewindStage.Offered || answer.Controller?.Id != transaction.ControllerId ||
                answer.Choices == null || answer.Choices.Count != 1 ||
                answer.Choices[0] != transaction.KeepEntityId && answer.Choices[0] != transaction.RewindEntityId) return false;
            var controller = game.ControllerById(transaction.ControllerId);
            if (controller.Choice?.DecisionId != transaction.OfferChoiceId || controller.Choice.ChoiceAction != ChoiceAction.REWIND) return false;
            // Live bytes this process saved: achievement-evidence failures mark the match ineligible, never block the answer.
            // Validation decodes and validates the exact baseline before any mutation. The in-place restore below repeats
            // that work without rollback, so it can only fail half-applied if the in-place decode diverges from this one.
            var baseline = Validate(game, strictEvidence: false);
            if (!baseline.HistoryFacts.EvidenceValid) game.InvalidateHistoryEvidence(baseline.HistoryFacts.EvidenceFailure);
            game.BeginRewindAnswer();
            controller.Choice = null;
            // SETASIDE has no visible actor once the choice closes; retain IDs without zone churn.
            game.Rewind = null;
            if (answer.Choices[0] == transaction.KeepEntityId) return true;
            if (transaction.KeepBoth)
            {
                RunKeepBoth(game, transaction);
                game.RefreshHistoryDisplays();
                return true;
            }

            game.RestoreRewindBaseline(transaction.Baseline, baseline);
            controller = game.ControllerById(transaction.ControllerId);
            var source = game.IdEntityDic[transaction.SourceId];
            if (game.History)
            {
                game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, source.Id, "", -1, 0));
                game.PowerHistory.Add(PowerHistoryBuilder.ResetGame(game));
                foreach (var entity in game.IdEntityDic.Select(p => p.Value).OrderBy(e => e.Id))
                    game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(entity, reset: true));
                game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
            transaction.Used++;
            if (!transaction.IsHeroPowerActivation && transaction.SourceCardId == "TIME_EVENT_999")
            {
                source = Generic.ChangeEntityBlock(controller, source, Cards.FromId("TIME_EVENT_999t"), false);
                source[GameTag.REWIND] = 0;
                game.AuraUpdate();
            }
            if (!transaction.IsHeroPowerActivation && Clocksworth.Contains(transaction.SourceCardId))
            {
                source = Generic.ChangeEntityBlock(controller, source, Cards.FromId(Clocksworth[transaction.Used]), false);
                // Variant tags are definition defaults; discard the previous variant's entity override.
                source[GameTag.REWIND] = source.Card[GameTag.REWIND];
                // Settle queued hand auras before retry payment and the next baseline.
                game.AuraUpdate();
            }
            if (!transaction.IsHeroPowerActivation) source[GameTag.USED_REWIND] = transaction.Used;
            transaction.SourceCardId = source.Card.Id;
            transaction.Stage = RewindStage.Attempt;
            transaction.OfferChoiceId = 0; transaction.RewindEntityId = 0; transaction.KeepEntityId = 0;
            transaction.KeepBoth = false;
            transaction.Baseline = game.SaveSnapshot();
            game.Rewind = transaction;
            var target = transaction.TargetId == 0 ? null : game.IdEntityDic[transaction.TargetId];
            PlayerTask replay = transaction.IsHeroPowerActivation
                ? HeroPowerTask.Any(controller, target as ICharacter, transaction.ChooseOne, heroPowerId: source == controller.Hero.HeroPower ? 0 : source.Id)
                : new PlayCardTask(controller, source, target, transaction.ZonePosition, transaction.ChooseOne);
            if ((!transaction.IsHeroPowerActivation && source.Zone != controller.HandZone) || !game.ReplayRewind(replay))
                throw new InvalidOperationException("Restored Rewind action could not be replayed.");
            return true;
        }

        /// <summary>Effect-only repeat: no payment, source movement, hand-play facts or after-play tail.</summary>
        internal static void RunKeepBoth(Game game, RewindTransaction transaction)
        {
            var source = game.IdEntityDic[transaction.SourceId];
            var target = transaction.TargetId == 0 ? null : game.IdEntityDic[transaction.TargetId];
            if (source.Card.Id == "TIME_EVENT_999")
            {
                source = Generic.ChangeEntityBlock(source.Controller, source, Cards.FromId("TIME_EVENT_999t"), false);
                source[GameTag.REWIND] = 0;
                source[GameTag.USED_REWIND] = transaction.Used + 1;
            }
            using var sequence = game.Resolution.Sequence(transaction.IsHeroPowerActivation ? ResolutionKind.EffectResolution :
                source is Spell ? ResolutionKind.SpellCast : ResolutionKind.PlayCard, game, source);
            using var phase = game.Resolution.Enter(transaction.IsHeroPowerActivation ? ResolutionKind.EffectResolution :
                source is Spell ? ResolutionKind.SpellText : ResolutionKind.Battlecry, source.Id);
            using var cast = source is Spell spell ? game.Facts.Cast(spell, HistoryCastKind.Effect) : null;
            if (cast != null) cast.AllowBodyClaim = true;
            // TaskProcessor opens the repeat's own POWER; a manual wrapper would nest same-source POWERs (no VFX targets).
            game.TaskQueue.StartEvent();
            source.ActivateTask(PowerActivation.POWER, target, transaction.ChooseOne);
            game.ProcessTasks();
            game.TaskQueue.EndEvent();
            game.DeathProcessingAndAuraUpdate();
            cast?.Complete();
            game.CheckDeadHeroes();
        }

        /// <summary>Validates the transaction and returns its decoded, validated baseline (null without a transaction).</summary>
        internal static Game Validate(Game game, bool strictEvidence = true)
        {
            var t = game.Rewind;
            if (t == null)
            {
                if (game.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND || game.Player2.Choice?.ChoiceAction == ChoiceAction.REWIND)
                    throw new SerializationException("Rewind choice has no transaction.");
                return null;
            }
            if (game.RewindDisabled || t.Baseline == null || t.Baseline.Length == 0 || t.Baseline.Length > Snapshots.SnapshotCodec.MaxBytes ||
                t.ControllerId != game.Player1.Id && t.ControllerId != game.Player2.Id ||
                !game.IdEntityDic.ContainsKey(t.SourceId) || string.IsNullOrEmpty(t.SourceCardId) ||
                t.Budget != (t.IsHeroPowerActivation ? 1 : Budget(t.SourceCardId)) || t.Used < 0 || t.Used > t.Budget ||
                t.Stage != RewindStage.Attempt && t.Stage != RewindStage.Offered)
                throw new SerializationException("Invalid Rewind transaction.");
            var baseline = Game.LoadRewindBaseline(t.Baseline, game.OfflinePools, strictEvidence);
            baseline.IdEntityDic.TryGetValue(t.SourceId, out var source);
            // Logging is receiver policy; exact clones retain baseline bytes from their logging source.
            if (baseline.FormatType != game.FormatType || baseline.History != game.History ||
                baseline.EnableCorpses != game.EnableCorpses ||
                baseline.State != State.RUNNING || baseline.Step != Step.MAIN_ACTION ||
                baseline.CurrentPlayer.Id != t.ControllerId || baseline.Player1.Choice != null || baseline.Player2.Choice != null ||
                source == null ||
                source.Controller.Id != t.ControllerId || source.Card.Id != t.SourceCardId ||
                ((Entity)source).GetNativeGameTag(GameTag.REWIND) != (!t.IsHeroPowerActivation && (t.SourceCardId == "TIME_038t3" || t.SourceCardId == "TIME_EVENT_999t") ? 0 : 1) ||
                !t.IsHeroPowerActivation && (source.Zone != source.Controller.HandZone || source[GameTag.USED_REWIND] != t.Used ||
                    Clocksworth.Contains(t.SourceCardId) && Array.IndexOf(Clocksworth, t.SourceCardId) != t.Used) ||
                t.TargetId != 0 && !baseline.IdEntityDic.ContainsKey(t.TargetId) ||
                (t.IsHeroPowerActivation ? t.ZonePosition != -1 : t.ZonePosition < -1 || t.ZonePosition > source.Controller.BoardZone.SlotCount) ||
                t.ChooseOne < -1 || t.ChooseOne > (source.ChooseOnePlayables?.Length ?? 0))
                throw new SerializationException("Invalid Rewind replay metadata.");
            var target = t.TargetId == 0 ? null : baseline.IdEntityDic[t.TargetId];
            if (t.IsHeroPowerActivation
                ? !HeroPowerLegal(baseline, source, t.TargetId, t.ChooseOne)
                : !Generic.PrePlayPhase(source.Controller, source, target, t.ZonePosition, t.ChooseOne))
                throw new SerializationException("Invalid Rewind replay action.");
            if (t.Stage == RewindStage.Offered)
            {
                var choice = game.ControllerById(t.ControllerId).Choice;
                if (t.Used >= t.Budget || choice == null || choice.ChoiceAction != ChoiceAction.REWIND ||
                    choice.DecisionId != t.OfferChoiceId || choice.SourceId != t.SourceId || choice.ChoiceType != ChoiceType.GENERAL ||
                    !choice.Choices.SequenceEqual(new[] { t.RewindEntityId, t.KeepEntityId }) ||
                    !game.IdEntityDic.TryGetValue(t.RewindEntityId, out var rewind) || rewind.Card.Id != "TIME_000tb" ||
                    !game.IdEntityDic.TryGetValue(t.KeepEntityId, out var keep) || keep.Card.Id != "TIME_000ta")
                    throw new SerializationException("Invalid Rewind offer.");
            }
            return baseline;
        }
    }
}
