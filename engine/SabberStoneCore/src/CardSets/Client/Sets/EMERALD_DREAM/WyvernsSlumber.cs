using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WyvernsSlumber
    {
        internal const int DbfId = 115198;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Entourage = new[] { "EDR_820a", "EDR_820b" };
        }
        internal static void ConfigureSummon(Card card)
        {
            Dreadseeds.ConfigureGenerator(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(Dreadseeds.Summon(), Dreadseeds.Summon()) };
        }
        internal static void ConfigureDamage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new DamageTask(2, EntityType.ALLMINIONS, true) };
        }
    }
}
