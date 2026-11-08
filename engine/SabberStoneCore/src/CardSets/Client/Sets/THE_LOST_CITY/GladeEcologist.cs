using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GladeEcologist
    {
        internal const int DbfId = 117565;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new AddCardTo("TLC_813", EntityType.HAND) };
        }

        internal static void ConfigureSpell(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                ctx.GiveBuff(t.Id, t.Controller == c ? "TLC_813e" : "TLC_813e2");
                if (minion.IsDead) ctx.Destroy(minion.Id);
            }) };
        }

        internal static void ConfigureFriendlyBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(2)) };
        }

        internal static void ConfigureEnemyBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(-2)) };
        }
    }
}
