using System;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LingeringSpirit
    {
        internal const int DbfId = 127064;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                int healing = 3 * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE]);
                int excess = c.RestoreToDamage ? 0 : Math.Max(0, healing - c.Hero.Damage);
                c.Hero.TakeHeal((IPlayable)s, 3);
                if (excess == 0) return;
                stack = new TaskStack();
                if (new RandomTask(1, EntityType.ENEMIES).Process(g, c, s, t, stack) == TaskState.STOP) return;
                var enemy = (ICharacter)stack.Playables[0];
                Generic.DamageCharFunc((IPlayable)s, enemy, excess, false);
            }) };
        }
    }
}
