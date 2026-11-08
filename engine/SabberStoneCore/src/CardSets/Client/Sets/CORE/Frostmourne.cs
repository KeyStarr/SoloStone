using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Frostmourne
    {
        [StandardCard("CORE_RLK_086", 111316)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.TrackCombatKills] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var entry in PayloadRules.Entries(s, DeathContextRules.KillKey))
                {
                    if (c.BoardZone.IsFull) break;
                    var body = (Minion)Entity.FromCard(c, entry.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    Generic.SummonBlock(g, body, -1, s);
                }
            }) };
        }
    }
}
