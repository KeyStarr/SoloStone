using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TranquilClearing
    {
        [StandardCard("MEND_044", 123483)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.Minion,
                Task = new CustomTask((g, c, s, t, stack) => {
                    if (!(t is Minion minion)) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("MEND_044e4"), (IPlayable)s, minion);
                    PlayLockRules.Sleep(minion, (IPlayable)s, "MEND_044e3");
                })
            } };
        }

        [StandardCard("MEND_044e3", 131955)]
        internal static void ConfigureSleep(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = PlayLockRules.Expiry() };
        }

        [StandardCard("MEND_044e4", 131957)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(2), new Effect(GameTag.TAUNT, EffectOperator.SET, 1)) };
        }
    }
}
