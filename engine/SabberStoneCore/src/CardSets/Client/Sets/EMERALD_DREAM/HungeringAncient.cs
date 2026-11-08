using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HungeringAncient
    {
        internal const int DbfId = 117498;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: randomly consume one deck minion, capture definition/current stats; meal Deathrattles return newest first (live-check order).
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var food = c.DeckZone.OfType<Minion>().ToArray();
                    if (food.Length == 0) return;
                    var meal = food[g.Random.Next(food.Length)];
                    g.OnRandomHappened(true);
                    int attack = meal.AttackDamage, health = meal.Health;
                    c.SetasideZone.Add(c.DeckZone.Remove(meal));
                    // Feed Me!'s text names the meal through an entity reference (builder 19): its owner's client must be
                    // told what the hidden deck card was; the opponent never is (PayloadRules.SharedWith).
                    meal[PayloadRules.Stored] = 1; meal[PayloadRules.Policy] = (int)PayloadPolicy.ShowOwner;
                    if (g.History) g.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(meal));
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_494e"), (IPlayable)s, s, attack, health, meal.Id);
                }) }
            };
        }

        internal static void ConfigureMeal(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true },
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => Generic.DrawCard(c, ((Enchantment)t).CapturedCard))
            };
        }
    }
}
