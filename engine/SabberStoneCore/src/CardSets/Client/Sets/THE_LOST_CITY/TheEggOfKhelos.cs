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
    internal static class TheEggOfKhelos
    {
        internal const int DbfId = 118487;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ConfigureStage(card, "DINO_410t2");
        }

        internal static void ConfigureSlightlyCracked(Card card) => ConfigureStage(card, "DINO_410t3");
        internal static void ConfigureMoreCracked(Card card) => ConfigureStage(card, "DINO_410t4");
        internal static void ConfigureVeryCracked(Card card) => ConfigureStage(card, "DINO_410t5");
        internal static void ConfigureMostCracked(Card card) => ConfigureStage(card, "DINO_410t");

        private static void ConfigureStage(Card card, string next)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = EffectContext.DeathrattlePosition(c, s);
                ctx.Summon(next, position);
            }) };
        }
    }
}
