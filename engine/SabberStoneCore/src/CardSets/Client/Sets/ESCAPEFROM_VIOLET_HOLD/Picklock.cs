using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Picklock
    {
        internal const int DbfId = 125985;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> { {PlayReq.REQ_TARGET_IF_AVAILABLE,0}, {PlayReq.REQ_MINION_TARGET,0}, {PlayReq.REQ_ENEMY_TARGET,0} });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (t is Minion target) new EffectContext(g,c.Id,s.Id,target.Id).Damage(target.Id, CostRules.ManaNumbers((IPlayable)s));
            }) };
        }
    }
}
