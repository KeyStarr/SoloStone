using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Schism
    {
        internal const int DbfId = 122874;
        internal static void Configure(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("CATA_306e", EntityType.TARGET), Copy()) };
        }
        internal static void ConfigureFirst(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_306e", EntityType.TARGET) };
        }
        internal static void ConfigureSecond(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = Copy() };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(3), Effects.CantBeTargetedBySpellsAndHeroPowers) };
        }
        private static CustomTask Copy() => new CustomTask((g, c, s, t, stack) => {
            if (!c.BoardZone.IsFull && t is SabberStoneCore.Model.Entities.Minion && t.Zone == c.BoardZone)
                Generic.Copy(c, s, t, Zone.PLAY, preserveBoardMinionState: true);
        });
        private static void ConfigureFamily(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Shatter = new ShatterDefinition("CATA_306", "CATA_306t1", "CATA_306t2");
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 },
                { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
        }
    }
}
