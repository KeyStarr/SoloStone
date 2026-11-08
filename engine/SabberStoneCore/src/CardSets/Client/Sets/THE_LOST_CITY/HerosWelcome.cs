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
    internal static class HerosWelcome
    {
        internal const int DbfId = 118625;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/DINO_424/v1",
                new DiscoverThenContinueTask(TheLostCityPools.LegendaryMinionDiscover(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault() as Minion;
                    if (chosen == null) return;
                    if (c.BoardZone.IsFull) return;
                    var made = chosen; Generic.RemoveFromZone(c, made);
                    Generic.SummonBlock(g, made, c.BoardZone.SlotCount, s);
                    new AddEnchantmentTask("DINO_424e", EntityType.TARGET).Process(g, c, s, made, stack);
                    stack.Playables.Clear();
                })) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(10), Effects.SetMaxHealth(10)) };
        }
    }
}
