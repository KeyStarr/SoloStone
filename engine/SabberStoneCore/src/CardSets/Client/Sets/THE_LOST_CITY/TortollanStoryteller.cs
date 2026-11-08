using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TortollanStoryteller
    {
        internal const int DbfId = 118257;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = MenagerieMug.Buff("TLC_254e", 7) } };
        }
        internal static void ConfigureBuff(Card card) => MenagerieMug.ConfigureBuff(card);
    }
}
