using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class R4TC4TCH3R
    {
        internal const int DbfId = 126425;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    var spells = c.DeckZone.Where(p => p is Spell).ToArray();
                    foreach (var spell in spells) {
                        if (c.DeckZone.IsFull) break;
                        Generic.Copy(c, s, spell, Zone.DECK);
                    }
                }),
                DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.DrawFromDeck(1, SelfCondition.IsSpell)))
            };
        }
    }
}
