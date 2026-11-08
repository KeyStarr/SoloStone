using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static partial class Generic
    {
        // Internal controller/entity tags are clone-safe and never sent to the native client.
        private const GameTag DecisionSource = (GameTag)(31001);
        private const GameTag DecisionTarget = (GameTag)(31002);
        private const GameTag DecisionRemaining = (GameTag)(31003);
        private const GameTag DecisionDefinition = (GameTag)(31004);
        private const GameTag DecisionEcho = (GameTag)(31005);
        internal const GameTag AutomaticDecisionTag = (GameTag)31007;
        public const GameTag PrivateDecisionOfferTag = (GameTag)(31006);
        // 31008 is live Outcast state; 31009 is reserved for future secret-pick proxies.
        public static bool IsDecisionInternalTag(GameTag tag) => (int)tag >= 31001 && (int)tag <= 31009
            || InfinityRules.IsInternalTag(tag) || tag == ImbueRules.UndeadPlayedTurn || RepeatRules.IsInternalTag(tag) || TurnQueueRules.IsInternalTag(tag) || AutoPlayRules.IsInternalTag(tag);
        internal static bool IsDecisionRecipe(Controller c) => HasDecisionPlay(c) || HasDecisionSpell(c) || c[AutomaticDecisionTag] != 0 || LocationRules.HasPending(c);
        internal static bool HasDecisionPlay(Controller controller) => controller[DecisionSource] != 0;

        private static void BeginDecisionPlay(Controller c, IPlayable source, ICharacter target, DecisionBattlecryTask task)
        {
            if (HasDecisionPlay(c)) throw new InvalidOperationException("A decision play is already pending.");
            c[DecisionSource] = source.Id;
            c[DecisionTarget] = target?.Id ?? 0;
            c[DecisionDefinition] = source.Card.AssetId;
            PayloadRules.CaptureGeneratedDecision(c, source.Card);
            c[DecisionRemaining] = ((c.ExtraBattlecry || CardSets.Client.Sets.MugZee.DoubleBattlecry(source)) && (source is not Minion m || m.HasBattleCry) || DarkGifts.DoublesBattlecry(source)
                || source.Combo && c.IsComboActive && c.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 1) ? 2 : 1;
            OverloadBlock(c, source, c.Game.History);
            RunDecisionBattlecries(c, task);
            if (!c.Game.Resolution.HasSuspendedEffect)
            {
                CancelDecisionPlay(c);
                FinishDecisionPlay(c,source);
            }
        }

        private static void FinishDecisionPlay(Controller c,IPlayable source)
        {
            if (source is Minion minion) FinishMinionPlay(c,c.Game,minion);
            else if (source is Hero hero) FinishHeroPlay(c,c.Game,hero);
            else if (source is Weapon weapon) FinishWeaponPlay(c,c.Game,weapon);
            else throw new InvalidOperationException("Unsupported decision Battlecry source.");
        }

        private static void RunDecisionBattlecries(Controller c, DecisionBattlecryTask task)
        {
            var game = c.Game;
            var source = game.IdEntityDic[c[DecisionSource]];
            var target = c[DecisionTarget] == 0 ? null : game.IdEntityDic[c[DecisionTarget]];
            while (c[DecisionRemaining] > 0)
            {
                c[DecisionRemaining]--;
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, target?.Id ?? 0));
                if (game.History && target != null) game.PowerHistory.TargetRange(new[] { target.Id });
                task.Recipe.Process(game, c, source, target);
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                game.ProcessTasks();
                if (game.Resolution.HasSuspendedEffect) return;
            }
        }

        internal static void RestoreDecisionPlayEvent(Controller c)
        {
            if (!HasDecisionPlay(c)) return;
            var game = c.Game;
            game.CurrentEventData = new EventMetaData(game.IdEntityDic[c[DecisionSource]],
                c[DecisionTarget] == 0 ? null : game.IdEntityDic[c[DecisionTarget]]);
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, c[DecisionSource], "", -1, 0));
            if (game.History && c[DecisionTarget] != 0) game.PowerHistory.TargetRange(new[] { c[DecisionTarget] });
        }

        internal static void ResumeDecisionPlay(Controller c)
        {
            if (!HasDecisionPlay(c)) return;
            var game = c.Game;
            // Resolve branch reactions before the next activation rebuilds its offers, without ending the Battlecry phase.
            game.ProcessTasks();
            if (game.Resolution.HasSuspendedEffect)
            {
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                game.CurrentEventData = null;
                return;
            }
            // Minion/hero plays share this continuation; spells use DecisionCompletion.
            var source = game.IdEntityDic[c[DecisionSource]];
            bool echo = c[DecisionEcho] != 0;
            var task = (DecisionBattlecryTask)PayloadRules.DecisionDefinition(c, c[DecisionDefinition]).Power.PowerTask;
            RunDecisionBattlecries(c, task);
            if (!game.Resolution.HasSuspendedEffect)
            {
                CancelDecisionPlay(c);
                FinishDecisionPlay(c,source);
                // FinishCardPlay closes the continuation block exactly as it closes the initial PLAY.
                FinishCardPlay(c, source, echo);
            }
            else
            {
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                game.CurrentEventData = null;
            }
        }

        internal static void ValidateDecisionPlay(Game game)
        {
            foreach (var c in new[] { game.Player1, game.Player2 })
            {
                if (!HasDecisionPlay(c)) continue;
                if (!game.Resolution.HasSuspendedEffect || DarkGifts.RecipeSource(game, c.Choice?.SourceId ?? 0) != c[DecisionSource] ||
                    !game.IdEntityDic.ContainsKey(c[DecisionSource]) || c[DecisionRemaining] < 0 || c[DecisionRemaining] > 1 ||
                    !(PayloadRules.DecisionDefinition(c, c[DecisionDefinition])?.Power?.PowerTask is DecisionBattlecryTask recipe) ||
                    game.Resolution.EffectFrames.First().RecipeKey != recipe.Recipe.RecipeKey)
                    throw new System.Runtime.Serialization.SerializationException("Invalid decision battlecry continuation.");
            }
        }

        internal static void CancelDecisionPlay(Controller c)
        {
            if (!HasDecisionPlay(c)) return;
            OutcastRules.Clear(c.Game.IdEntityDic[c[DecisionSource]]);
            PayloadRules.ClearGeneratedDecision(c);
            c[DecisionSource] = c[DecisionTarget] = c[DecisionRemaining] = c[DecisionDefinition] = c[DecisionEcho] = 0;
        }
    }
}
