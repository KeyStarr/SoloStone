using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SurvivalOfTheFittest
    {
        [StandardCard("SCH_609", 59450)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var minion in c.HandZone.Concat(c.DeckZone).Concat(c.BoardZone.GetAll()).OfType<Minion>().ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("SCH_609e"), (IPlayable)s, minion);
            }) };
        }
        [StandardCard("SCH_609e", 59443)]
        internal static void Enchantment(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(4)) };
        }
    }
}
