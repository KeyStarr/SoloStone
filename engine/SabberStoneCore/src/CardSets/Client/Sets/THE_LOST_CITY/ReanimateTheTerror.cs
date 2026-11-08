using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ReanimateTheTerror
    {
        [StandardCard("TLC_433", 117427)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117427, 15, QuestRewardKind.CardToHand, "TLC_433t"));
            QuestRules.RegisterCorpseSpend(117427);
            card.Power = new Power();
        }
        [StandardCard("TLC_433t", 117707)]
        internal static void Tyrax(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                int position = EffectContext.DeathrattlePosition(c, s);
                var grave = (BoardObject)Entity.FromCard(c, Cards.FromId("TLC_433t2"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, c.SetasideZone);
                Generic.RemoveFromZone(c, grave);
                Generic.PlayLocation(c, g, grave, position, automatic: true);
            }) };
        }
        [StandardCard("TLC_433t2", 119897)]
        internal static void Grave(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                LocationActivation = new LocationActivation { Target = LocationTargetKind.Character,
                    Task = ComplexTask.Create(new DamageTask(4, EntityType.TARGET),
                        new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE)) },
                DeathrattleTask = EffectContext.Task(ctx => { ctx.SummonRightOfLocation("TLC_433t"); return TaskState.COMPLETE; })
            };
        }
    }
}
