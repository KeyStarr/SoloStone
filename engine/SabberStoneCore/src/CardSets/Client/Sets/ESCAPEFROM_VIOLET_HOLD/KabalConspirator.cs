using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KabalConspirator
    {
        internal const int DbfId = 127033;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new AddCardTo("CAP_400t2t", EntityType.OP_DECK, 2) };
        }

        internal static void ConfigureImp(Card card)
        {
            card.Implemented = true;
            // DrawBlock moves topdeck cards to Setaside first; Copy retains their stat buffs.
            card.Power = new Power { TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                if (!c.Opponent.BoardZone.IsFull) Generic.Copy(c.Opponent, s, (IPlayable)s, Zone.PLAY);
                new DrawTask().Process(g, c, s, null);
            }) };
        }
    }
}
