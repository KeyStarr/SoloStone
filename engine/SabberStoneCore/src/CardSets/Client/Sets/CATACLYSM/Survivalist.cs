using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Survivalist
    {
        [StandardCard("CATA_613", 123384)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.SELF, new Effect(GameTag.IMMUNE, EffectOperator.ADD, 1)) {
                Restless = true, Condition = new SelfCondition(me => me.Controller.BoardZone.Count == 1)
            } };
        }
    }
}
