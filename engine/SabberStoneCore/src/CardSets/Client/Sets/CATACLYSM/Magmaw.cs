using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Magmaw
    {
        [StandardCard("CATA_550", 122865)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareRefill(card, "CATA_550t", 99);
        }

        [StandardCard("CATA_550t", 122866)]
        internal static void ConfigureBody(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var candidates = c.BoardZone.GetAll().Where(m => !m.ToBeDestroyed && !m.IsDormant).ToArray();
                if (candidates.Length == 0) return;
                var recipient = candidates[g.Random.Next(candidates.Length)]; g.OnRandomHappened(true);
                Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_550e1"), (IPlayable)s, recipient);
            }) };
        }

        [StandardCard("CATA_550e1", 123412)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) };
        }
    }
}
