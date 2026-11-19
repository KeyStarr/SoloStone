using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    internal static class TaughtSpellRules
    {
        internal static void ConfigureBattlecry(Card card)
        {
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { [PlayReq.REQ_TARGET_IF_AVAILABLE] = 0 });
            // Probe the spell as its current caster, without enrolling an entity or applying mana/board play gates.
            card.SourceTargetingPredicate = (source, target) => {
                var taught = PayloadRules.Entries(source, 1).FirstOrDefault();
                if (taught == null) return false;
                var spell = new Spell(source.Controller, taught.Card, costProbe: true);
                return spell.ValidPlayTargets.Contains(target) && spell.IsValidPlayTarget(target);
            };
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var taught = PayloadRules.Entries(s, 1).FirstOrDefault(); if (taught == null) return;
                var spell = Entity.FromCard(c, taught.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                new PlayTask(PlayType.SPELL, EntityType.TARGET).Process(g, c, s, t,
                    new TaskStack { Playables = new[] { spell } });
            }) };
        }
    }
}
