using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Stranglevine
    {
        internal const int DbfId = 118241;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = Transfer() };
        }
        private static CustomTask Transfer() => new CustomTask((g,c,s,t,stack) => {
            var candidates = c.BoardZone.Where(m => !m.IsDead && !m.Untouchable && m.Id != s.Id).ToArray();
            if (candidates.Length == 0) return;
            var target = candidates[g.Random.Next(candidates.Length)];
            BonusEffects.GiveRandom(target, (IPlayable)s, 1);
            Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_465e"), (IPlayable)s, target);
        });
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = Transfer() };
        }
    }
}
