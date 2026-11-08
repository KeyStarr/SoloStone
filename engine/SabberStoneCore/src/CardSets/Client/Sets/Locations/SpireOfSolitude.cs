using System;
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
    internal static class SpireOfSolitude
    {
        internal const int DbfId = 126129;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None, RequiresFreeSlot = true,
                Task = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var location = (BoardObject)s;
                    int position = location.Zone == c.BoardZone ? location.ZonePosition + 1 : location.LastBoardPosition;
                    int size = c.HandZone.Count;
                    var demon = (Minion)Entity.FromCard(c, Cards.FromId("JAIL_511t"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    demon.AttackDamage = size;
                    demon.BaseHealth = size;
                    Generic.SummonBlock(g, demon, Math.Clamp(position, 0, c.BoardZone.SlotCount), s);
                    if (demon.Health <= 0) demon.ToBeDestroyed = true;
                    g.DeathProcessingAndAuraUpdate();
                    var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsDead).ToArray();
                    if (demon.Zone == c.BoardZone && !demon.IsDead && enemies.Length != 0 && g.State != State.COMPLETE)
                    {
                        var selected = enemies[g.Random.Next(enemies.Length)];
                        int attacks = demon.NumAttacksThisTurn; bool exhausted = demon.IsExhausted;
                        try { new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, demon, selected); }
                        finally { demon.NumAttacksThisTurn = attacks; demon.IsExhausted = exhausted; }
                    }
                    new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE).Process(g, c, s, t, stack);
                })
            } };
        }
    }
}
