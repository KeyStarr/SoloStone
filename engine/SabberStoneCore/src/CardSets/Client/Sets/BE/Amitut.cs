using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Amitut
    {
        [StandardCard("BE_143", 130327)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var holy = c.DeckZone.Where(e => e is Spell && e.Card[GameTag.SPELL_SCHOOL] == 5).ToArray();
                foreach (var spell in holy)
                {
                    if (c.DeckZone.IsFull) break;
                    Generic.Copy(c, s, spell, Zone.DECK, initialize: copy => {
                        CardStateBoundary.EnrollAttachments(copy);
                        Generic.AddEnchantmentBlock(g, Cards.FromId("BE_143t2e"), (IPlayable)s, copy);
                    });
                }
            }) };
        }
        [StandardCard("BE_143t2e", 134801)]
        internal static void ConfigureGrant(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.CASTSWHENDRAWN, EffectOperator.SET, 1)) };
        }
    }
}
