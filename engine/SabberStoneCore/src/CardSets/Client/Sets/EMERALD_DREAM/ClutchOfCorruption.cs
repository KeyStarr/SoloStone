using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ClutchOfCorruption
    {
        internal const int DbfId = 114215;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.Minion, RequiresFreeSlot = true,
                TargetCondition = FriendlyDragon,
                Task = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var egg = (Minion)Entity.FromCard(c, Cards.FromId("EDR_454t"));
                    // The copied Dragon is public; the client names it through tag 2 read as an entity id.
                    var copied = PayloadRules.Store(egg, 1, t, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowAll);
                    egg.HasDeathrattle = true;
                    egg[GameTag.TAG_SCRIPT_DATA_NUM_1] = copied.Id;
                    Generic.SummonBlock(g, egg, s.Zone?.Type == Zone.PLAY ? ((IPlayable)s).ZonePosition + 1 : ((BoardObject)s).LastBoardPosition, s);
                })
            } };
        }
        private static bool FriendlyDragon(Controller c, IPlayable target) => target.Controller == c && target.Card.IsRace(Race.DRAGON);
        internal static void ConfigureEgg(Card card)
        {
            card.Implemented = true; card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, PayloadRules.FirstReference);
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var stored = PayloadRules.Entries(s, 1).FirstOrDefault();
                if (stored == null || c.BoardZone.IsFull) return;
                Generic.Copy(c, s, stored, Zone.PLAY, zonePosition: Math.Min(((Minion)s).LastBoardPosition, c.BoardZone.SlotCount), preserveCapturedState: true);
            }) };
        }
    }
}
