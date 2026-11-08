using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MenagerieMug
    {
        internal const int DbfId = 120352;
        private static readonly Race[] Types = { Race.BEAST, Race.DEMON, Race.DRAGON, Race.ELEMENTAL, Race.MECHANICAL,
            Race.MURLOC, Race.PIRATE, Race.TOTEM, Race.QUILBOAR, Race.NAGA, Race.UNDEAD, Race.DRAENEI };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Buff("WON_141e", 3) };
        }

        internal static ISimpleTask Buff(string enchantment, int maximum) => new CustomTask((g, c, s, t, stack) => {
            var minions = c.BoardZone.GetAll().Where(m => m.Card.Races.Count > 0).ToArray();
            var sets = new List<Minion[]>();
            int largest = 0;
            // At most seven recipients: enumerate entity subsets once, not their potentially multiple type assignments.
            for (int mask = 1; mask < (1 << minions.Length); mask++)
            {
                var set = minions.Where((m, i) => (mask & (1 << i)) != 0).ToArray();
                if (set.Length > maximum || set.Length < largest || !Assignable(set, 0, 0)) continue;
                if (set.Length > largest) { sets.Clear(); largest = set.Length; }
                sets.Add(set);
            }
            if (sets.Count == 0) return;
            int selected = 0;
            if (sets.Count > 1) { g.OnRandomHappened(true); selected = g.Random.Next(sets.Count); }
            var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
            foreach (var minion in sets[selected].OrderBy(m => m.OrderOfPlay)) ctx.GiveBuff(minion.Id, enchantment);
        });

        private static bool Assignable(Minion[] set, int index, int used)
        {
            if (index == set.Length) return true;
            for (int i = 0; i < Types.Length; i++)
                if ((used & (1 << i)) == 0 && set[index].Card.IsRace(Types[i]) && Assignable(set, index + 1, used | (1 << i))) return true;
            return false;
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
