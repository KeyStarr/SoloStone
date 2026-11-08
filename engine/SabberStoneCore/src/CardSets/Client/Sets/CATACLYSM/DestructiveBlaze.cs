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
    internal static class DestructiveBlaze
    {
        internal const int DbfId = 123166;
        [StandardCard("CATA_586", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                    TriggerSource = TriggerSource.SELF, Condition = SelfCondition.IsNotDead,
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        var ctx = new EffectContext(g, c.Id, s.Id);
                        ctx.Summon("CATA_586", s.Zone == c.BoardZone ? ((IPlayable)s).ZonePosition + 1 : -1);
                    })
                },
                DeathrattleTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 2)
            };
        }
    }
}
