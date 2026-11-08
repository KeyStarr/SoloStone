using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AncientRaptor
    {
        internal const int DbfId = 118224;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = AncientStegodon.Choose("TLC_245", "UNG_999t3", "UNG_999t8", "UNG_999t2") };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            if (card.Id == "UNG_999t2e") card.Text = " <b>Deathrattle:</b> Summon two 1/1 Plants.";
            card.Power = card.Id == "UNG_999t2e" ? new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = EffectContext.DeathrattlePosition(c, s);
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++) ctx.Summon("UNG_999t2t1", position++);
            }) } : new Power { Enchant = card.Id == "UNG_999t3e" ? new Enchant(Effects.Attack_N(3))
                : new Enchant(new Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1)) };
        }
    }
}
