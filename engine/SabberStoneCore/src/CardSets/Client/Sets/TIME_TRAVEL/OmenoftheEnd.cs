using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OmenoftheEnd
    {
        internal const int DbfId = 121199;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(DeckIsEmpty);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!DeckIsEmpty((IPlayable)s)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 5; i++) ctx.RunLegacy(new MoveToGraveYard(EntityType.OP_TOPDECK, destroy: true));
            }) };
        }

        private static bool DeckIsEmpty(IPlayable source) => source.Controller.DeckZone.Count == 0;
    }
}
