using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EternalFirebolt
    {
        internal const int DbfId = 121188;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                ctx.Damage(t.Id, 3, spellDamage: true);
                if (minion.Health <= 0 || minion.ToBeDestroyed)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("END_025e"), (IPlayable)s, c);
            }) };
        }

        internal static void ConfigureReturn(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var timer = (Enchantment)s;
                    var spell = timer.Creator;
                    if (spell.Zone == c.GraveyardZone && !c.HandZone.IsFull) {
                        c.GraveyardZone.Remove(spell);
                        Generic.AddHandPhase(c, spell);
                    }
                    timer.Remove();
                })
            } };
        }
    }
}
