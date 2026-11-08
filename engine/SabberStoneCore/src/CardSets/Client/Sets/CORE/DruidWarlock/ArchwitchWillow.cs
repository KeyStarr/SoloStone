using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArchwitchWillow
    {
        internal const int DbfId = 129850;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                {
                    var zone = i == 0 ? c.HandZone.GetAll() : c.DeckZone.GetAll();
                    var demons = zone.OfType<Minion>().Where(m => m.IsRace(Race.DEMON)).ToArray();
                    if (demons.Length == 0) continue;
                    var demon = ctx.SampleDistinct(demons, 1)[0];
                    if (demons.Length > 1) g.OnRandomHappened(true);
                    demon.Zone.Remove(demon);
                    Generic.SummonBlock(g, demon, position++, s);
                }
            }) };
        }
    }
}
