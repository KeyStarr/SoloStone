using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AlgetharInstructor
    {
        internal const int DbfId = 119632;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Client SPELLPOWER is a presence flag (1); the text supplies the second point.
            card.Power = new Power { Aura = new Aura(AuraType.CONTROLLER, new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 1)) };
        }
    }
}
