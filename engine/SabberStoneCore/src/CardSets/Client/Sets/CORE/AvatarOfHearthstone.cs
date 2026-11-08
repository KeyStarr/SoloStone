using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AvatarOfHearthstone
    {
        [StandardCard("CORE_WON_145", 127595)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var pack = OfflinePoolPolicy.OpenStandardPack(g, s.Card.AssetId);
                foreach (var definition in pack)
                {
                    if (g.State == State.COMPLETE) break;
                    var actor = Entity.FromCard(c, definition, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, zone: c.SetasideZone);
                    AutoPlayRules.Play(actor, s);
                }
            }) };
        }
        [OfflinePool("CORE_WON_145", 127595)]
        internal static PoolRequirement[] DeclarePools() => new[] { Rarity.COMMON, Rarity.RARE, Rarity.EPIC, Rarity.LEGENDARY }
            .Select(r => new PoolRequirement("avatar-pack-" + r.ToString().ToLowerInvariant(),
                OfflinePoolPolicy.StandardPackFilter(r), new[] { "NEUTRAL" })).ToArray();
    }
}
