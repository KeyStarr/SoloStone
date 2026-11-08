using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AratortheRedeemer
    {
        internal const int DbfId = 123624;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var recruit in c.BoardZone.GetAll().Where(m => m.Card.Id == "CS2_101t" || m.Card[(GameTag)3444] == 1))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("MEND_804e"), (IPlayable)s, recruit, 2, recruit.Health, 0);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.MUL, 2), Effects.Health_N(0), new Effect(GameTag.TAUNT, EffectOperator.SET, 1)) { UseScriptTag = true } };
        }
    }
}
