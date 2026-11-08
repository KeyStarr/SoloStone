using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Conditions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RubySanctum
    {
        internal const int DbfId = 122757;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None,
                Task = new CustomTask((g, c, s, t, stack) => {
                    Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("CATA_301e")).ExpireAtEndOfTurn();
                    ((IPlayable)s)[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                })
            } };
        }
        internal static void ConfigureBuff(Card card) { card.Implemented = true; card.Power = new Power(); }
    }
}
