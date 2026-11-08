using System;
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.Effects
{
    internal sealed class SelectorStepHistory
    {
        internal int StepIndex;
        internal int ChosenIndex = -1;
        internal int EntityId;
        internal int ControllerId;
        internal Enums.Zone Zone;
        internal Enums.CardType CardType;

        internal SelectorStepHistory Clone() => (SelectorStepHistory)MemberwiseClone();
    }

    /// <summary>Remaining work in a registered recipe. Saved state contains values and entity IDs only.</summary>
    public sealed class EffectFrame
    {
        public string RecipeKey { get; private set; }
        public int NextStep { get; internal set; }
        public int ControllerId { get; private set; }
        public int SourceId { get; private set; }
        public int TargetId { get; private set; }
        public int LastSelectionEntityId { get; internal set; }
        public long DecisionId { get; internal set; }
        public RngState RngAtSuspension { get; internal set; }
        internal int[] CapturedCandidateIds = Array.Empty<int>();
        internal int[] ChosenEntities = Array.Empty<int>();
        internal string SelectionGroupKey;
        internal int[] SelectedInGroup = Array.Empty<int>();
        internal int[] OfferEntityIds = Array.Empty<int>();
        internal SelectorStepHistory[] SelectorHistory = Array.Empty<SelectorStepHistory>();
        private int[] _playables;
        private bool _flag;
        private int _number, _number1, _number2, _number3, _number4;

        internal EffectFrame(string recipeKey, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            RecipeKey = recipeKey;
            ControllerId = controller.Id;
            SourceId = source?.Id ?? 0;
            TargetId = target?.Id ?? 0;
            Capture(stack);
        }

        internal void Capture(TaskStack stack)
        {
            _playables = stack.Playables?.Select(p => p.Id).ToArray();
            _flag = stack.Flag;
            _number = stack.Number; _number1 = stack.Number1; _number2 = stack.Number2;
            _number3 = stack.Number3; _number4 = stack.Number4;
        }

        internal TaskStack Restore(Game game) => new TaskStack {
            Playables = _playables?.Select(id => game.IdEntityDic[id]).ToList(), Flag = _flag,
            Number = _number, Number1 = _number1, Number2 = _number2, Number3 = _number3, Number4 = _number4
        };

        internal void Choose(Game game, int entityId)
        {
            if (EffectFrameRecipes.Resolve(RecipeKey)[NextStep - 1] is Tasks.SimpleTasks.SelectEntityThenContinueTask selector)
            {
                LastSelectionEntityId = entityId;
                if (selector.Spec != null)
                {
                    var pick = SelectorHistory[SelectorHistory.Length - 1];
                    var entity = game.IdEntityDic[entityId];
                    pick.ChosenIndex = ChosenEntities.Length;
                    pick.EntityId = entityId;
                    pick.ControllerId = entity.Controller.Id;
                    pick.Zone = entity is Hero ? Enums.Zone.PLAY : entity.Zone.Type;
                    pick.CardType = entity.Card.Type;
                    if (selector.Spec.GroupKey != null)
                        SelectedInGroup = SelectedInGroup.Concat(new[] { entityId }).Distinct().ToArray();
                    OfferEntityIds = Array.Empty<int>();
                }
            }
            ChosenEntities = ChosenEntities.Concat(new[] { entityId }).ToArray();
            var stack = Restore(game);
            stack.AddPlayable(game.IdEntityDic[entityId]);
            Capture(stack);
        }

        internal EffectFrame Clone()
        {
            var copy = (EffectFrame)MemberwiseClone();
            copy._playables = _playables?.ToArray();
            copy.CapturedCandidateIds = CapturedCandidateIds.ToArray();
            copy.ChosenEntities = ChosenEntities.ToArray();
            copy.SelectedInGroup = SelectedInGroup.ToArray();
            copy.OfferEntityIds = OfferEntityIds.ToArray();
            copy.SelectorHistory = SelectorHistory.Select(p => p.Clone()).ToArray();
            return copy;
        }
    }
}
