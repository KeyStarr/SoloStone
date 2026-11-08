using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BronzeRedeemer
    {
        internal const int DbfId = 123123;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var source = (Minion)s;
                    var token = (Minion)Entity.FromCard(c, Cards.FromId("CATA_478t"));
                    token.AttackDamage = source.AttackDamage;
                    token.BaseHealth = source.Health;
                    int position = source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    Generic.SummonBlock(g, token, position, source);
                    if (token.Health <= 0) token.ToBeDestroyed = true;
                })
            } };
        }
    }
}
