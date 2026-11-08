using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CostumeMerchant
    {
        internal const int DbfId = 118632;
        private static OfflinePoolFilter Filter() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass,
            cardIds: new[] { "DINO_402", "DINO_403", "DINO_428", "DINO_429", "DINO_432" });
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("other-class-mask", Filter(), new[] { "ROGUE" }) };
        [StandardCard("DINO_427", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(Combo);
            var acquisition = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(Filter(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var mask = Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                if (c.IsComboActive) Effects.ReduceCost(2).ApplyTo(mask);
                Generic.AddHandPhase(c, mask);
            });
            card.Power = new Power { PowerTask = acquisition, ComboTask = acquisition };
        }
        private static bool Combo(IPlayable source) => source.Controller.IsComboActive;
    }
}
