using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SilentStrike
    {
        internal const int DbfId = 127028;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                ctx.GiveBuff(t.Id, "CAP_001e");
                if (!minion.HasStealth) return;
                var targets = new TaskStack();
                if (new RandomTask(1, EntityType.OP_MINIONS).Process(g, c, s, t, targets) == TaskState.STOP) return;
                ctx.Damage(targets.Playables[0].Id, minion.AttackDamage, spellDamage: true);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }
    }
}
