using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Conditions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EldritchTentacles
    {
        internal const int DbfId = 122711;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int wave = 0; g.State != State.COMPLETE; wave++)
                {
                    int damage = SpellDamageRules.Amount((IPlayable)s, 3 - wave);
                    if (damage <= 0) break;
                    var targets = c.BoardZone.Concat(c.Opponent.BoardZone).Where(m => !m.Untouchable).Select(m => m.Id).ToArray();
                    new EffectContext(g, c.Id, s.Id).DamageBatch(targets, damage);
                    g.DeathProcessingAndAuraUpdate();
                    if (c.Hero.ToBeDestroyed || c.Opponent.Hero.ToBeDestroyed) break;
                }
            }) };
        }

    }
}
