using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SteamfinThief
    {
        internal const int DbfId = 117383;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                for (int repeat = 0; repeat < repeats; repeat++)
                {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    for (int i = 0; i < 2; i++) ctx.Summon("TLC_429t", position + i);
                }
            }) };
        }
    }
}
