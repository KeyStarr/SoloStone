using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NablyaTheWatcher
    {
        internal const int DbfId = 117919;
        [StandardCard("TLC_624", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var damaged = c.BoardZone.GetAll().Where(m => m.Damage > 0 && !m.IsDead && !m.Untouchable).ToArray();
                int position = s.Zone == c.BoardZone ? ((IPlayable)s).ZonePosition + 1 : c.BoardZone.SlotCount;
                foreach (var minion in damaged)
                {
                    if (c.BoardZone.IsFull) break;
                    if (minion.Zone != c.BoardZone || minion.IsDead) continue;
                    Generic.Copy(c, s, minion, Zone.PLAY, zonePosition: position++, preserveBoardMinionState: true,
                        initialize: copy => new Effect(GameTag.RUSH, EffectOperator.SET, 1).ApplyTo(copy));
                }
            }) };
        }
    }
}
