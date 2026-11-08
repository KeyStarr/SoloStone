using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RelicMiner
    {
        internal const int DbfId = 117889;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.DeckZone.Count == 0) return;
                var top = c.DeckZone.TopCard; var rarity = top.Card.Rarity;
                new MoveToGraveYard(EntityType.TARGET, destroy: true).Process(g, c, s, top, stack);
                if (rarity == Rarity.COMMON || rarity == Rarity.RARE || rarity == Rarity.EPIC || rarity == Rarity.LEGENDARY)
                    new EffectContext(g, c.Id, s.Id, stack: stack).Discover(TheLostCityPools.SameRarity(rarity), recipeDbfId: DbfId);
            }) };
        }
    }
}
