using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Shellnado
    {
        internal const int DbfId = 117715;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // Resolve deaths between waves, as legacy Defile does.
                for (int i = 0; i < 5 && c.Hero.Armor > 0; i++)
                {
                    c.Hero.Armor--;
                    new DamageTask(1, EntityType.ALLMINIONS, true).Process(g, c, s, t, stack);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
