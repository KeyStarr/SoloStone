using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HornOfFeasting
    {
        internal const int DbfId = 118503;
        [StandardCard("DINO_136", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                bool outcast = OutcastRules.PlayedFromEdge((IPlayable)s);
                var ctx = new EffectContext(g,c.Id,s.Id);
                for (int i=0; i<3; i++) {
                    int summoned = ctx.Summon("DINO_136t");
                    if (summoned != 0 && outcast) Generic.AddEnchantmentBlock(g,Cards.FromId("DINO_136e"),(IPlayable)s,g.IdEntityDic[summoned]);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }

        [StandardCard("DINO_136t", 118502)]
        internal static void ConfigureRaptor(Card card) { card.Implemented = true; }
        [StandardCard("DINO_136e", 126207)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.IMMUNE_WHILE_ATTACKING, EffectOperator.SET,1)) { IsOneTurnEffect = true } };
        }
    }
}
