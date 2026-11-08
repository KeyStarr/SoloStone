using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GelbinOfTomorrow
    {
        [StandardCard("TIME_009", 119918)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var aura in c.DeckZone.OfType<Spell>().Where(a => a.IsAura)
                    .GroupBy(a => a.Card.Id).Select(group => group.First()).ToArray())
                    AuraSpellRules.Install(aura);
            }) };
        }
        [StandardCard("TIME_009t1", 119919)]
        internal static void ConfigureGnomish(Card card) => AuraSpellRules.Declare(card, effect: new HealTask(4, EntityType.FRIENDS));
        [StandardCard("TIME_009t2", 119920)]
        internal static void ConfigureMekkatorque(Card card) => AuraSpellRules.Declare(card, effect:
            new CustomTask((g, c, s, t, stack) => {
                var candidates = c.BoardZone.Where(m => !m.IsDormant && !m.IsDead).ToArray();
                if (candidates.Length == 0) return;
                var chosen = candidates[g.Random.Next(candidates.Length)];
                Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_009t2e"), (IPlayable)s, chosen);
                chosen.HasDivineShield = true;
            }));
        [StandardCard("TIME_009t2e", 120815)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(4)) };
        }
    }
}
