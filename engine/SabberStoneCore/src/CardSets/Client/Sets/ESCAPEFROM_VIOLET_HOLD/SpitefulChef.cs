using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpitefulChef
    {
        internal const int DbfId = 125993;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasTenMana);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.ChefTaunt(HasTenMana((IPlayable)s) ? 6 : 2), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id,
                    s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount);
            }) };
        }

        private static bool HasTenMana(IPlayable source) => source.Controller.BaseMana >= 10;
    }
}
