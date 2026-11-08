using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ursol
    {
        private const int CapturedSpell = 1;
        [StandardCard("EDR_259", 114868)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var spells = c.HandZone.OfType<Spell>().ToArray();
                if (spells.Length == 0) return;
                int cost = spells.Max(p => p.Cost);
                var ties = spells.Where(p => p.Cost == cost).ToArray();
                var chosen = ties[g.Random.Next(ties.Length)];
                var aura = (Spell)Entity.FromCard(c, Cards.FromId("EDR_259e1"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                // "cast {0}" is builder 19: tag 2 is the captured spell's entity id, named to its owner only.
                var captured = PayloadRules.Store(aura, CapturedSpell, chosen, PayloadPolicy.Copy | PayloadPolicy.ShowOwner);
                aura[GameTag.TAG_SCRIPT_DATA_NUM_1] = captured.Id;
                c.SetasideZone.Add(c.HandZone.Remove(chosen));
                AuraSpellRules.Install(aura);
            }) };
        }
        [StandardCard("EDR_259e1", 116020)]
        internal static void ConfigureAura(Card card) => AuraSpellRules.Declare(card, effect:
            new CustomTask((g, c, s, t, stack) => {
                var capture = PayloadRules.Entries(s, CapturedSpell).SingleOrDefault();
                if (capture == null) return;
                var working = Generic.Copy(c, s, capture, Zone.SETASIDE, preserveCapturedState: true);
                // Only stored snapshots are private; the working cast is an ordinary public actor.
                working[PayloadRules.Stored] = 0;
                new PlayTask(PlayType.SPELL, randTarget: true).Process(g, c, s, null,
                    new TaskStack { Playables = new List<IPlayable> { working } });
            }));
    }
}
