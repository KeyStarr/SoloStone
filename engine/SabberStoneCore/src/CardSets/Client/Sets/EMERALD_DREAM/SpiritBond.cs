using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpiritBond
    {
        [StandardCard("EDR_262", 115055)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> { {PlayReq.REQ_TARGET_TO_PLAY,0}, {PlayReq.REQ_MINION_TARGET,0} });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (!(t is Minion minion)) return;
                var ctx = new EffectContext(g,c.Id,s.Id,t.Id,stack);
                ctx.Damage(t.Id,3,true);
                if (minion.ToBeDestroyed || minion.Health <= 0)
                {
                    g.DeathProcessingAndAuraUpdate();
                    ctx.Summon("EDR_850pe");
                }
            }) };
        }
    }
}
