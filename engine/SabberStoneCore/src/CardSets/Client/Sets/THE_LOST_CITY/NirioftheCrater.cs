using SabberStoneCore.Conditions;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NirioftheCrater
    {
        internal const int DbfId = 121029;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            RepeatRules.Declare(card,RepeatRules.OneCost);
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) { TriggerSource=TriggerSource.FRIENDLY,
                    OwnerCondition = new SelfCondition(owner => owner.Game.CurrentEventData?.EventSource is Minion minion && minion.Zone == minion.Controller.BoardZone && minion[RepeatRules.PlayedCost] == 1),
                    SingleTask = new CustomTask((g,c,s,t,k) => {
                        if (t is Minion minion && minion.Zone == c.BoardZone && t[RepeatRules.PlayedCost] == 1)
                            Generic.AddEnchantmentBlock(g,Cards.FromId("TLC_836e"),(IPlayable)s,minion,minion.AttackDamage,minion.Health);
                    }) }
            };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(0),Effects.Health_N(0)) { UseScriptTag=true } };
        }
    }
}
