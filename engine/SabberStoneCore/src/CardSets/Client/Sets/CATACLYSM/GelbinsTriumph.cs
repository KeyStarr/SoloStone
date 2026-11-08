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
    internal static class GelbinsTriumph
    {
        internal const int DbfId = 123403;
        internal static OfflinePoolFilter Filter() => new OfflinePoolFilter(type: CardType.SPELL,
            cardIds: new[] { "TTN_851", "TIME_700", "END_011", "CATA_480", "JAIL_327", "BE_146" });
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("paladin-auras", Filter(), new[] { "PALADIN" }) };
        [StandardCard("CATA_621", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(Filter(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var aura = (Spell)Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                AuraSpellRules.Extend(aura, 1);
                Generic.AddHandPhase(c, aura);
            }) };
        }
    }
}
