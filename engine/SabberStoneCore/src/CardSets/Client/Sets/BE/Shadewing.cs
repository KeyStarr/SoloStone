using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Shadewing
    {
        internal const int DbfId = 131456;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var candidates = c.HandZone.GetAll().Where(p => p is Spell && p.Card[GameTag.SPELL_SCHOOL] == 6).ToList();
                if (candidates.Count == 0) return;
                var selection = new TaskStack { Playables = candidates };
                new RandomTask(1, EntityType.STACK).Process(g, c, s, null, selection);
                new Enchant(Effects.ReduceCost(2)).ActivateTo(selection.Playables[0]);
            }) };
        }
    }
}
