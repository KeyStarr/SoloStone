using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SplinteredReality
    {
        internal const int DbfId = 121107;
        private static int TreantDeaths(IPlayable source) => source.Game.HistoryFacts.Query.Deaths()
            .Count(f => f.ControllerId == source.Controller.Id && f.Definition.ReadDefinition()[(GameTag)2831] == 1);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: friendly deaths count the frozen death-form Treant tag 2831, including repeat deaths (design R7).
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, TreantDeaths);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int count = ctx.History.Deaths().Count(f => f.ControllerId == c.Id && f.Definition.ReadDefinition()[(GameTag)2831] == 1);
                var tokens = new[] { ctx.Summon("END_009t"), ctx.Summon("END_009t") };
                if (count > 0)
                    foreach (int token in tokens)
                        if (token != 0) ctx.GiveBuff(token, "END_009e", count);
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
    }
}
