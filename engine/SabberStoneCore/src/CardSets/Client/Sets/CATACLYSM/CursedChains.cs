using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CursedChains
    {
        [StandardCard("CATA_496",122720)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> {
                {PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_MINION_TARGET,0},{PlayReq.REQ_ENEMY_TARGET,0} });
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>TemporaryRules.Borrow(t as Minion,(IPlayable)s)) };
        }
        [StandardCard("CATA_496e",130559)]
        internal static void Lease(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.TURN_END) { EitherTurn=true,
                SingleTask=new CustomTask((g,c,s,t,stack)=>TemporaryRules.ControlEnd((Enchantment)s)) } };
        }
    }
}
