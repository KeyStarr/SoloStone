using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AshleafPixie
    {
        internal const int DbfId = 115972;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingExpensiveSpell);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!EmeraldDreamConditions.HoldingExpensiveSpell((IPlayable)s)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.RunLegacy(new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.SOURCE));
                ctx.RunLegacy(new SetGameTagTask(GameTag.LIFESTEAL, 1, EntityType.SOURCE));
            }) };
        }
    }
}
