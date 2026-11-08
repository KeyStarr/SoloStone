using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BoneFlurry
    {
        internal const int DbfId = 126261;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(FriendlyMinionDiedThisTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                Burst(g, c, s);
                // DeathProcessing increments this tag, including deaths caused by the first packet.
                if (FriendlyMinionDiedThisTurn((IPlayable)s)) Burst(g, c, s);
            }) };
        }

        private static void Burst(Game game, Controller controller, IEntity source)
        {
            int missiles = (3 + SabberStoneCore.Actions.SpellDamageRules.Bonus(source as SabberStoneCore.Model.Entities.IPlayable))
                * (int)Math.Pow(2, controller.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
            SplitHits.Inline(game, controller, source, missiles,
                ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: true);
        }
        private static bool FriendlyMinionDiedThisTurn(IPlayable source) =>
            source.Controller.NumFriendlyMinionsThatDiedThisTurn > 0;

    }
}
