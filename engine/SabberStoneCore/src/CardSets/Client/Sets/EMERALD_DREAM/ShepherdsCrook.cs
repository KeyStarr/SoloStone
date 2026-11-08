using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShepherdsCrook
    {
        internal const int DbfId = 113568;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var sheep = (Minion)Entity.FromCard(c, Cards.FromId("EDR_416t"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    DormantRules.MarkPending(sheep, 2, "EDR_416te");
                    Generic.SummonBlock(g, sheep, c.BoardZone.SlotCount, s);
                })
            } };
        }
        internal static void ConfigureSheep(Card card)
        {
            card.Implemented = true;
            card.Dormant = new DormantDefinition(2, "EDR_416te");
        }
    }
}
