using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VigilantSentry
    {
        internal const int DbfId = 125660;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(NoNeutralCards);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!NoNeutralCards((IPlayable)s)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < 2; i++) ctx.Summon("JAIL_035", position + i);
            }) };
        }
        private static bool NoNeutralCards(IPlayable source) =>
            !source.Controller.DeckZone.Any(p => p.Card.Class == CardClass.NEUTRAL && p.Card[GameTag.MULTIPLE_CLASSES] == 0 && p.Card.MultiClassGroup == 0);

    }
}
