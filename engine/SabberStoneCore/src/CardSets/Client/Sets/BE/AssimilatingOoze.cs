using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AssimilatingOoze
    {
        internal const int DbfId = 130815;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => s[GameTag.TAG_SCRIPT_DATA_NUM_1]++),
                Trigger = new Trigger(TriggerType.TURN_END) {
                    EitherTurn = true,
                    Condition = new SelfCondition(p => p[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0),
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        var ooze = (Minion)s;
                        int copies = ooze[GameTag.TAG_SCRIPT_DATA_NUM_1];
                        ooze[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
                        int position = ooze.ZonePosition + 1;
                        for (int i = 0; i < copies && ooze.Zone == c.BoardZone && !ooze.IsDead && !c.BoardZone.IsFull; i++)
                            Generic.Copy(c, s, ooze, Zone.PLAY, zonePosition: position++, preserveBoardMinionState: true);
                    })
                }
            };
        }
    }
}
