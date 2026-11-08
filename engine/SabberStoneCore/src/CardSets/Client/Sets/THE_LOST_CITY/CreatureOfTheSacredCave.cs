using System.Linq;
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
    internal static class CreatureOfTheSacredCave
    {
        internal const int DbfId = 117384;
        // Rule: sample completed Player Holy cast occurrences, then prefer this legal target (R1 lean; wiki/Creature_of_the_Sacred_Cave, 36.0 fix).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    var casts = ctx.History.Casts(ctx.History.ThisTurn).Where(f => f.ControllerId == c.Id
                        && f.CasterKind == HistoryCastKind.Player && f.Outcome == HistoryCastOutcome.Completed && f.SpellSchool == 5).ToArray();
                    if (casts.Length == 0) return;
                    g.OnRandomHappened(true);
                    var spell = (Spell)Entity.FromCard(c, casts[g.Random.Next(casts.Length)].Definition.Materialize());
                    if (!HeraldRules.CanAutoCast(spell)) return;
                    if (!ImbueRules.CanAutoCast(spell)) return;
                    var targets = spell.Card.GetValidPlayTargets(c).Where(spell.IsValidPlayTarget).ToList();
                    ICharacter target = targets.FirstOrDefault(p => p.Id == s.Id);
                    if (target == null && targets.Count > 0) target = targets[g.Random.Next(targets.Count)];
                    if (target == null && spell.Card.MustHaveTargetToPlay) return;
                    ctx.CastSpell(spell.Id, HistoryCastKind.Repeat, target?.Id);
                    while (c.Choice != null) Generic.ChoicePick(c, g, c.Choice.Choices.Choose(g.Random));
                })
            } };
        }
    }
}
