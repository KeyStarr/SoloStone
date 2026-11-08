using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ThawFromIce
    {
        internal const int DbfId = 129801;
        [StandardCard("BE_257", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_257", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "MAGE" }, destination: PoolDestination.Other)
        };

        [StandardCard("BE_257e", 129976)]
        internal static void ConfigureThaw(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.CopyHandTrigger = true; card.ExpiresOnHandExit = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var held = (IPlayable)((Enchantment)s).Target;
                    if (held.Zone == c.HandZone && held.Cost > 0)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("BE_257e2"), held, held);
                })
            } };
        }

        [StandardCard("BE_257e2", 131318)]
        internal static void ConfigureMelting(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(1)) };
        }
    }
}
