using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NefersetWeaponsmith
    {
        internal const int DbfId = 117698;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var acquisition = new CustomTask((g, c, s, t, stack) => {
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(TheLostCityPools.OtherClassWeapon(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var weapon = Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                // Combo modifies the acquisition itself, including a repeated Battlecry.
                if (c.IsComboActive) Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_516e"), (IPlayable)s, weapon);
                Generic.AddHandPhase(c, weapon);
            });
            card.Power = new Power { PowerTask = acquisition, ComboTask = acquisition };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) };
        }
    }
}
