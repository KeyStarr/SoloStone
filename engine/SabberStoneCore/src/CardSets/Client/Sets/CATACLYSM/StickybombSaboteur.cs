using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StickybombSaboteur
    {
        internal const int DbfId = 123783;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                Entity.FromCard(c.Opponent, Cards.FromId("CATA_186t"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, zone: c.Opponent.HandZone);
            }) };
        }
        internal static void ConfigureSabotage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new HandAdjacencyCostAura(1, "CATA_186te") };
        }
    }
}
