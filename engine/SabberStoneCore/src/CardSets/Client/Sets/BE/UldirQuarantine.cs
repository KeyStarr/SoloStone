using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UldirQuarantine
    {
        internal const int DbfId = 131126;
        [StandardCard("BE_780", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                    TriggerSource=TriggerSource.FRIENDLY,
                    SingleTask=new CustomTask((g,c,s,t,stack)=>{
                        if(!(s is BoardObject location) || location.Zone!=c.BoardZone) return;
                        if(t?.Id==s.Id || !location.Location.ConditionLocked) return;
                        if(t!=null)
                        {
                            int bits=(t.Card[GameTag.COST_BLOOD]>0?1:0)|(t.Card[GameTag.COST_FROST]>0?2:0)|(t.Card[GameTag.COST_UNHOLY]>0?4:0);
                            location.Location.Stage |= bits;
                            location.Location.ConditionLocked=location.Location.Stage!=7;
                        }
                        location[GameTag.TAG_SCRIPT_DATA_NUM_1]=location.Location.ConditionLocked?1:0;
                        location[GameTag.EXHAUSTED]=location.Location.ConditionLocked?1:0;
                        location.IsExhausted=location.Location.ConditionLocked;
                    }) },
                LocationActivation = new LocationActivation { RequiresFreeSlot=true,
                    Task=new CustomTask((g,c,s,t,stack)=>{
                        s[GameTag.LOCATION_ACTION_COOLDOWN]=1;
                        var ctx=new EffectContext(g,c.Id,s.Id,stack:stack);
                        var chosen=ctx.PickRandomCard(B14H4BEPools.Five(),recipeDbfId:DbfId);
                        if(chosen!=null)ctx.Summon(chosen.Id,s.Zone==c.BoardZone?((IPlayable)s).ZonePosition+1:((BoardObject)s).LastBoardPosition);
                    }) }
            };
        }
    }
}
