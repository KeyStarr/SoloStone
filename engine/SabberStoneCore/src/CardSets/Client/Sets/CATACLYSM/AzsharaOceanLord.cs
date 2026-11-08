using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AzsharaOceanLord
    {
        internal const int DbfId = 124116;
        [StandardCard("CATA_151", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card,"CATA_151t","CATA_151t1");
            card.Power = new Power { Aura = new Aura(AuraType.HERO,"CATA_151e") };
        }

        [StandardCard("CATA_151e", 124135)]
        internal static void ConfigureWindfury(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.WINDFURY,EffectOperator.ADD,1)) };
        }
        [StandardCard("CATA_151t", 124108)]
        [StandardCard("CATA_151t1", 125413)]
        internal static void ConfigureTentacle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource=TriggerSource.SELF, SingleTask = new CustomTask((g,c,s,t,stack) => {
                    var amount = new TaskStack { Number=s[GameTag.TAG_SCRIPT_DATA_NUM_1] };
                    new AddEnchantmentTask("CATA_151te",EntityType.HERO,useScriptTag:true).Process(g,c,s,t,amount);
                }) } };
        }
        [StandardCard("CATA_151te", 123603)]
        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true, IsOneTurnEffect = true } };
        }
    }
}
