using System;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class DeathrattleRules
    {
        internal static bool IsMultiplier(GameTag tag) => tag == GameTag.EXTRA_DEATHRATTLES_BASE
            || tag == GameTag.EXTRA_MINION_DEATHRATTLES_BASE || tag == GameTag.EXTRA_DEATHRATTLES_ADDITIONAL;
        internal static int AuraValue(Controller owner, GameTag tag)
        {
            int value = owner.Game.Auras?.OfType<Aura>().Sum(a => a.DeathrattleValue(owner,tag)) ?? 0;
            return tag == GameTag.EXTRA_DEATHRATTLES_ADDITIONAL ? value : Math.Min(1,Math.Max(0,value));
        }
        public static int Repeats(Controller owner, IEntity source) => 1
            + (owner[GameTag.EXTRA_DEATHRATTLES_BASE] > 0
                || source is Minion && owner[GameTag.EXTRA_MINION_DEATHRATTLES_BASE] > 0 ? 1 : 0)
            + Math.Max(0, owner[GameTag.EXTRA_DEATHRATTLES_ADDITIONAL]);
    }
}
