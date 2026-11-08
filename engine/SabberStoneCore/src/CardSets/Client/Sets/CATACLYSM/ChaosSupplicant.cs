using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChaosSupplicant
    {
        internal const int DbfId = 123606;
        [StandardCard("CATA_786", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (!(s is Minion source) || source.Zone != c.BoardZone || source.IsSilenced || !(t is Spell)) return;
                    // Sample the committed modified Cost before consumed discounts, including zero.
                    var binding = new TaskStack { Number = Math.Max(0,t[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]) };
                    var chosen = new EffectContext(g,c.Id,s.Id,stack:binding).PickRandomCard(
                        B14K6CataclysmPools.OtherSpell(),PoolCostBinding.StackNumber,DbfId);
                    if (chosen == null) return;
                    var spell = Entity.FromCard(c,chosen,new EntityData { [GameTag.CREATOR]=s.Id,[GameTag.DISPLAYED_CREATOR]=s.Id });
                    new PlayTask(PlayType.SPELL,true).Process(g,c,s,null,new TaskStack { Playables=new[]{spell} });
                })
            } };
        }
    }
}
