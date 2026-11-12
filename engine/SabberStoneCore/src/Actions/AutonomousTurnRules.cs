using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>One controller-owned extra turn; policy reads legal options and visible offers only.</summary>
    public static class AutonomousTurnRules
    {
        private const GameTag Used = (GameTag)33420, Turn = (GameTag)33421, Actions = (GameTag)33422;
        // Mastermind Yogg-Saron's extra turn is played by the engine, not by a player. After this many actions in one
        // controlled turn the engine ends it, so the turn is guaranteed to finish. Pending choices are still answered
        // first. The optional Discover refresh (JAIL_319t) is never taken (see Run): it is the only offer that can
        // cycle without making progress.
        public const int ActionLimit = 128;
        public static bool IsInternalTag(GameTag tag) => tag == Used || tag == Turn || tag == Actions;
        public static bool HasUsed(Controller owner) => owner[Used] != 0;
        public static bool IsActive(Game game) => game != null && game.State == State.RUNNING &&
            game.CurrentPlayer[Turn] != 0 && game.CurrentPlayer[Turn] == game.Turn;

        public static void Grant(Controller owner, IPlayable source)
        {
            var game = owner.Game;
            if (HasUsed(owner) || game.State != State.RUNNING || game.CurrentPlayer != owner) return;
            owner[Used] = 1;
            owner[Turn] = game.Turn + 1;
            owner[Actions] = 0;
            Enchantment.GetInstance(owner,source,owner,Cards.FromId("BE_009e2"));
            TurnQueueRules.Prepend(game,owner);
            AutoPlayRules.RequestTurnEnd(owner);
        }

        private static void Control(Controller owner, bool active)
        {
            owner[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] = active ? 1 : 0;
            // Controller's legacy tag writer publishes only its fixed whitelist.
            if (owner.Game.History) owner.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(
                owner.Id,GameTag.AI_MAKES_DECISIONS_FOR_PLAYER,active ? 1 : 0));
        }

        public static void Run(Game game)
        {
            if (game == null || game.ResolvingPlayerAction) return;
            if (game.State == State.COMPLETE)
            {
                ReleaseControl(game.Player1); ReleaseControl(game.Player2);
                return;
            }
            if (!IsActive(game)) return;
            var owner = game.CurrentPlayer;
            Control(owner,true);
            while (IsActive(game) && game.CurrentPlayer == owner)
            {
                var chooser = game.Player1.Choice != null ? game.Player1 : game.Player2.Choice != null ? game.Player2 : null;
                if (chooser != null)
                {
                    var choice = chooser.Choice;
                    // Optional refresh is the only cyclic offer: taking a real offer guarantees progress.
                    var legal = choice.Choices.Where(id => !DiscoverRealRules.IsRefresh(choice,id) &&
                        (!game.Resolution.HasSuspendedEffect || game.Resolution.ValidateSelection(game,id))).ToArray();
                    if (legal.Length == 0) throw new InvalidOperationException("Autonomous choice has no legal answer.");
                    int pick = PickChoice(chooser,legal);
                    if (!game.AnswerAutonomous(new DecisionAnswer(choice.DecisionId,chooser.Id,new[] { pick }),out var error))
                        throw new InvalidOperationException(error);
                    continue;
                }
                var tasks = owner.Options();
                var task = owner[Actions] >= ActionLimit ? tasks.OfType<EndTurnTask>().Single() : PickAction(tasks);
                owner[Actions]++;
                if (!game.ProcessAutonomous(task)) throw new InvalidOperationException("Autonomous legal action rejected.");
            }
            ReleaseControl(owner);
        }

        private static void ReleaseControl(Controller owner)
        {
            if (owner[Turn] == 0) return;
            Control(owner,false);
            owner.NativeTags.Remove(Turn); owner.NativeTags.Remove(Actions);
            foreach (var actor in owner.AppliedEnchantments?.Where(e => e.Card.Id == "BE_009e2").ToArray() ?? Array.Empty<Enchantment>()) actor.Remove();
        }

        private static int PickChoice(Controller chooser, int[] legal)
        {
            var game = chooser.Game;
            if (chooser.Choice.ChoiceAction == ChoiceAction.REWIND) return game.Rewind.KeepEntityId;
            // Only inspect identities offered to this chooser; hidden opposing zones never enter policy.
            if (game.IdEntityDic.TryGetValue(chooser.Choice.SourceId,out var source) && source.Card.Id == "BE_009")
                foreach (string ability in new[] { "BE_009t4", "BE_009t3" })
                    foreach (int id in legal) if (game.IdEntityDic[id].Card.Id == ability) return id;
            var enemies = legal.Where(id => game.IdEntityDic[id] is ICharacter c && c.Controller == chooser.Opponent).ToArray();
            if (enemies.Length > 0) legal = enemies;
            return legal[game.Random.Next(legal.Length)];
        }

        private static PlayerTask PickAction(System.Collections.Generic.List<PlayerTask> tasks)
        {
            // shortcut: deterministic visible-information policy; replace only when retail AI evidence pins preferences.
            return (PlayerTask)tasks.OfType<ActivateOldGodTask>().FirstOrDefault()
                ?? tasks.Where(t => t is PlayCardTask).OrderByDescending(t => t.Source.Cost).FirstOrDefault()
                ?? tasks.FirstOrDefault(t => (t is MinionAttackTask || t is HeroAttackTask) && t.ActionTarget is Hero)
                ?? tasks.FirstOrDefault(t => t is MinionAttackTask || t is HeroAttackTask)
                ?? tasks.FirstOrDefault(t => t is HeroPowerTask && t.ActionTarget is Hero hero && hero.Controller != t.Controller)
                ?? tasks.FirstOrDefault(t => t is HeroPowerTask || t is ActivateLocationTask)
                ?? tasks.OfType<EndTurnTask>().Single();
        }
    }
}
