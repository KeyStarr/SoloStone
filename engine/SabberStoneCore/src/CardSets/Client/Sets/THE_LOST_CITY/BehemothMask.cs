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
    internal static class BehemothMask
    {
        internal const int DbfId = 118649;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t == null) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                ctx.GiveBuff(t.Id, "DINO_428e");
                var enemies = c.Opponent.BoardZone.GetAll().Where(m => m.Id != t.Id).Cast<IPlayable>().ToList();
                if (enemies.Count == 0) return;
                var attack = new TaskStack { Playables = enemies };
                new RandomTask(1, EntityType.STACK).Process(g, c, s, t, attack);
                new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, t, attack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(8), Effects.SetMaxHealth(10), Effects.Lifesteal) };
        }
    }
}
