using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AncientPterrordax
    {
        internal const int DbfId = 118232;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = AncientStegodon.Choose("TLC_246", "UNG_999t10", "UNG_999t5", "UNG_999t7") };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            if (card.Id == "UNG_999t5e") card.Text = "<b>Elusive</b>.";
            card.Power = card.Id == "UNG_999t10e" ? Power.OneTurnStealthEnchantmentPower
                : new Power { Enchant = new Enchant(card.Id == "UNG_999t5e" ? Effects.CantBeTargetedBySpellsAndHeroPowers : Effects.Windfury) };
        }
    }
}
