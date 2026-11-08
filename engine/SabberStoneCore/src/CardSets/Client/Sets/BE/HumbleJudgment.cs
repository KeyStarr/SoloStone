using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HumbleJudgment
    {
        internal const int DbfId = 131122;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var drawn = new List<Minion>();
                // DrawTask stops after the first fatigue draw; this spell must attempt all three.
                for (int i = 0; i < 3; i++)
                    if (Generic.Draw(c) is Minion minion && minion.Zone == c.HandZone) drawn.Add(minion);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var minion in drawn) ctx.GiveBuff(minion.Id, "BE_154e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(2), Effects.SetMaxHealth(2)) };
        }
    }
}
