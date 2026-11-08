using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Flamespitter
    {
        internal const int DbfId = 129820;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new AddCardTo("BE_553t", EntityType.HAND)
            } };
        }

        /// <summary>BE_553t Overdrive: Give a friendly Mech +2 Attack and Rush. It dies at end of turn.</summary>
        internal static void ConfigureOverdrive(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 },
                { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_RACE, (int)Race.MECHANICAL } });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("BE_553e", EntityType.TARGET) };
        }

        /// <summary>BE_553e Burning Out: the Rush is an enchantment keyword, so Silence removes it (silence-readiness rules)
        /// and also cancels the end-of-turn death.</summary>
        internal static void ConfigureBurningOut(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.Attack_N(2), Effects.Rush),
                Trigger = new Trigger(TriggerType.TURN_END) {
                    EitherTurn = true,
                    SingleTask = new DestroyTask(EntityType.TARGET)
                }
            };
        }
    }
}
