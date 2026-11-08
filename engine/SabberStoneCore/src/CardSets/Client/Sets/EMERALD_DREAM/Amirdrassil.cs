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
    internal static class Amirdrassil
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(CardType.MINION);
        [StandardCard("FIR_907", 115631)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                RequiresFreeSlot = true,
                Task = new CustomTask((g, c, s, t, stack) => {
                    int n = ((BoardObject)s).Location.UsesCompleted + 1;
                    var context = new EffectContext(g, c.Id, s.Id, stack: new TaskStack { Number = n });
                    var minion = context.PickRandomCard(Pool(), PoolCostBinding.StackNumber, 115631);
                    if (minion != null) context.SummonRightOfLocation(minion.Id);
                    c.Hero.Armor += n;
                    context.Draw(n);
                    context.RefreshMana(n);
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1] = n + 1;
                    s[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                })
            } };
        }
    }
}
