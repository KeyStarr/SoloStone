using System.Linq;
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
    internal static class ShadowRounds
    {
        internal const int DbfId = 126478;
        [StandardCard("JAIL_515", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> {
                { PlayReq.REQ_TARGET_TO_PLAY,0 }, { PlayReq.REQ_MINION_TARGET,0 }, { PlayReq.REQ_ENEMY_TARGET,0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var victim = t as Minion;
                // shortcut: owner-approved offline fallback is 50 hits per invocation; revisit with a retail trace.
                var budget = PayloadRules.StoreValue(s, DbfId, 50, (PayloadPolicy)0);
                try
                {
                    while (budget[PayloadRules.Value] > 0 && g.State != State.COMPLETE && victim != null)
                    {
                        budget[PayloadRules.Value]--;
                        if (victim.Zone != c.Opponent.BoardZone || victim.IsDormant) break;
                        new EffectContext(g,c.Id,s.Id,victim.Id).Damage(victim.Id,2,spellDamage:true);
                        bool died = victim.IsDead || victim.ToBeDestroyed;
                        g.DeathProcessingAndAuraUpdate();
                        if (!died || g.State == State.COMPLETE || budget[PayloadRules.Value] == 0) break;
                        var targets = c.Opponent.BoardZone.Where(m=>!m.Untouchable && !m.IsDead && !m.ToBeDestroyed).ToArray();
                        victim = targets.Length == 0 ? null : targets[g.Random.Next(targets.Length)];
                    }
                }
                finally { PayloadRules.Remove(budget); }
            }) };
        }
    }
}
