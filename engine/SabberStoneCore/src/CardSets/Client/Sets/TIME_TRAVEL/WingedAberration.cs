using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WingedAberration
    {
        internal const int DbfId = 121195;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { ComboTask = new CustomTask((g, c, s, t, stack) => {
                c.OverloadOwed += 2;
                c.OverloadThisGame += 2;
                g.TriggerManager.OnOverloadTrigger((IPlayable)s, 2);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.GiveBuff(s.Id, "END_032e");
                ctx.RunLegacy(new SetGameTagTask(GameTag.WINDFURY, 1, EntityType.SOURCE));
            }) };
        }

        internal static void ConfigureImmune(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Immune) { IsOneTurnEffect = true } };
        }
    }
}
