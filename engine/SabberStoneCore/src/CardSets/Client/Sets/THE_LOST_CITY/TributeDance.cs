using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TributeDance
    {
        [StandardCard("DINO_414", 118554)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.PlayAvailabilityPredicate += TransformRules.HasTwoSpellTargets;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/DINO_414/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.PlayTargetOrBoard, SelectorSide.Both,
                    cardType: CardType.MINION, groupKey: "tribute", ifEmpty: EmptySelectorAction.StopRecipe)),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board, SelectorSide.Both,
                    cardType: CardType.MINION, groupKey: "tribute", excludeEarlierPicks: true, ifEmpty: EmptySelectorAction.StopRecipe)),
                new CustomTask((g, c, s, t, stack) => {
                    var picks = g.Resolution.CurrentEffect.SelectedInGroup;
                    if (picks.Length != 2 || g.IdEntityDic[picks[0]] is not Minion first || g.IdEntityDic[picks[1]] is not Minion second ||
                        first.Id == second.Id || !TransformRules.Selectable(c, card, first) || !TransformRules.Selectable(c, card, second)) return;
                    new TransformCopyTask().ProcessBoardState(g, first.Controller, first, second, preserveBoardState: true);
                })) };
        }
    }
}
