using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Dreadseeds
    {
        internal static readonly string[] Ids = { "EDR_840t", "EDR_840t1", "EDR_840t2" };
        internal static void ConfigureGenerator(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Entourage = Ids;
        }
        internal static ISimpleTask Summon(SummonSide side = SummonSide.DEFAULT) => new CustomTask((g, c, s, t, stack) => {
            // Unpinned_DreadseedPool: one independent uniform fixed-token pick per requested summon.
            string id = Ids[g.Random.Next(Ids.Length)];
            g.OnRandomHappened(true);
            new SummonTask(id, side).Process(g, c, s, t, stack);
        });
        internal static void ConfigureHound(Card card)
        {
            card.Implemented = true;
            card.Dormant = new DormantDefinition(2, "EDR_840t1e", onAwaken: new AddEnchantmentTask("EDR_840te", EntityType.HERO));
        }
        internal static void ConfigureCrow(Card card)
        {
            card.Implemented = true;
            card.Dormant = new DormantDefinition(1, "EDR_840t1e1");
        }
        internal static void ConfigureSerpent(Card card)
        {
            card.Implemented = true;
            card.Dormant = new DormantDefinition(3, "EDR_840t2e1");
        }
        internal static void ConfigureFangs(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) { IsOneTurnEffect = true } };
        }
        internal static void ConfigureCondition(Card card)
        {
            card.Implemented = true; card.Power = new Power();
        }
    }
}
