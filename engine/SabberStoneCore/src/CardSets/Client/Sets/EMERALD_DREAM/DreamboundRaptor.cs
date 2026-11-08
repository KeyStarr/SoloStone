using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DreamboundRaptor
    {
        internal const int DbfId = 114933;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (t is Minion target && target.Zone == c.BoardZone && !target.IsDead && !target.Untouchable)
                        BonusEffects.GiveRandom(target, (IPlayable)s, 1);
                })
            } };
        }
    }
}
