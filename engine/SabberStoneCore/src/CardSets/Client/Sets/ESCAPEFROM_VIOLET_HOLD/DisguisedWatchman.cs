using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisguisedWatchman
    {
        [StandardCard("JAIL_455", 127260)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                for (int wave = 0; wave < 2 && g.State != State.COMPLETE; wave++)
                {
                    var minions = c.BoardZone.Where(m => m.Id != s.Id && !m.Untouchable && !m.ToBeDestroyed).ToArray();
                    using (g.History ? g.PowerHistory.DamageWave(minions.Length) : null)
                        foreach (var minion in minions)
                            Generic.DamageCharFunc((IPlayable)s, minion, 1, false);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
        [StandardCard("JAIL_455a", 130024)]
        [StandardCard("JAIL_455b", 130025)]
        internal static void Side(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
