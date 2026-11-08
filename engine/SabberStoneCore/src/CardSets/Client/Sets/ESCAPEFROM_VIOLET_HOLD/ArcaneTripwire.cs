using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArcaneTripwire
    {
        internal const int DbfId = 126423;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                Deal(g, c, s);
                new AddCardTo("JAIL_881t", EntityType.DECK, 2).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureTripped(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => Deal(g, c, s)),
                TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                    Deal(g, c, s);
                    new DrawTask().Process(g, c, s, t, stack);
                })
            };
        }

        private static void Deal(Game game, Controller controller, IEntity source)
        {
            // Resolve all missiles before shuffling or drawing the replacement card.
            int missiles = (4 + SabberStoneCore.Actions.SpellDamageRules.Bonus(source as SabberStoneCore.Model.Entities.IPlayable))
                * (int)Math.Pow(2, controller.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
            SplitHits.Inline(game, controller, source, missiles,
                ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: true);
        }
    }
}
