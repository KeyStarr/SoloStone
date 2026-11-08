using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StellarBalance
    {
        internal const int DbfId = 116980;
        [StandardCard("EDR_874", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (string id in new[] { "CS2_008", "EX1_173" })
                {
                    using (c.HandZone.DeferCardStateEntry())
                    {
                        var spell = Entity.FromCard(c, Cards.FromId(id), new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                        Generic.AddHandPhase(c, spell);
                        if (spell != null && spell.Zone == c.HandZone)
                            Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_874e"), (IPlayable)s, spell);
                    }
                }
            }) };
        }
        [StandardCard("EDR_874e", 119165)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 1)) };
        }
    }
}
