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
    internal static class RitualOfLife
    {
        internal const int DbfId = 118628;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/DINO_426/v1",
                new DiscoverThenContinueTask(TheLostCityPools.ThreeCostMinionDiscover(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault() as Minion;
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen); Generic.AddHandPhase(c, chosen);
                    if (c.BoardZone.IsFull) return;
                    var made = (Minion)Entity.FromCard(c, chosen.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    new AddEnchantmentTask("DINO_426e", EntityType.TARGET).Process(g, c, s, made, stack);
                    Generic.SummonBlock(g, made, c.BoardZone.SlotCount, s);
                    stack.Playables.Clear();
                })) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(2), Effects.SetMaxHealth(3)) };
        }
    }
}
