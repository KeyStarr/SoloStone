using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FiendishServant
    {
        [StandardCard("CORE_YOD_026", 117061)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.CaptureDeath] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var targets = c.BoardZone.Where(m => !m.Untouchable && !m.ToBeDestroyed).ToArray();
                if (targets.Length > 0)
                    new Enchant(Effects.Attack_N(DeathContextRules.AttackAtDeath((IPlayable)s)))
                        .ActivateTo(targets[g.Random.Next(targets.Length)]);
            }) };
        }
    }
}
