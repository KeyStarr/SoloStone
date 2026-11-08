using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkTerraforming
    {
        [StandardCard("BE_EVENT_102", 132673)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target) || target.Zone != target.Controller.BoardZone) return;
                var slots = System.Linq.Enumerable.ToArray(target.Controller.BoardZone.Slots);
                int center = Array.IndexOf(slots, target);
                int damage = SpellDamageRules.Amount((IPlayable)s, 5);
                // Freeze physical distances before any lethal hit; ordinary DamageTask defers deaths.
                for (int distance = 0; distance < damage && distance < slots.Length; distance++)
                {
                    Hit(center - distance, damage - distance);
                    if (distance > 0) Hit(center + distance, damage - distance);
                }
                void Hit(int position, int amount)
                {
                    if (position < 0 || position >= slots.Length || !(slots[position] is Minion victim)
                        || victim.IsDormant || victim.Zone != target.Controller.BoardZone) return;
                    new DamageTask(amount, EntityType.TARGET).Process(g, c, s, victim, stack);
                }
            }) };
        }
    }
}
