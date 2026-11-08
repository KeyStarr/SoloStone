using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Slagclaw
    {
        [StandardCard("TLC_482", 120344)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                new SummonTask("TLC_249", 2).Process(g,c,s,t,stack);
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                if (repeats == 0) return;
                // Snapshot after both summons; each living Cinder remains the source of its own Deathrattle.
                var cinders = c.BoardZone.Where(m => m.Card.Id == "TLC_249" && m.HasDeathrattle && !m.Untouchable).ToArray();
                for (int i = 0; i < repeats; i++)
                    foreach (var cinder in cinders.Where(m => i == 0 || m.Zone == c.BoardZone && !m.IsDead))
                        cinder.ActivateTask(PowerActivation.DEATHRATTLE, fromDeath: false);
            }) };
        }
    }
}
