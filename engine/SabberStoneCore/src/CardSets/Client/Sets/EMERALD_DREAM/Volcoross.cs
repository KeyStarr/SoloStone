using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Volcoross
    {
        internal const int DbfId = 115748;
        [StandardCard("FIR_951", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpend);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/FIR_951/v1",
                new DiscoverThenContinueTask((g, c, s, stack) => new[] { (10, "FIR_951t2"), (20, "FIR_951t3"), (30, "FIR_951t4") }
                    .Where(o => g.EnableCorpses && CorpseRules.Available(c) >= o.Item1)
                    .Select(o => Cards.FromId(o.Item2)).ToArray()),
                new CustomTask((g, c, s, t, stack) => {
                    string chosen = stack.Playables?.LastOrDefault()?.Card.Id;
                    int amount = chosen == "FIR_951t2" ? 10 : chosen == "FIR_951t3" ? 20 : chosen == "FIR_951t4" ? 30 : 0;
                    if (amount > 0 && s.Zone == c.BoardZone && CorpseRules.TrySpend(c, amount))
                        new EffectContext(g, c.Id, s.Id).GiveBuff(s.Id, "FIR_951e", amount);
                })) };
        }

        private static bool CanSpend(IPlayable source) => source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 10;
        [StandardCard("FIR_951e", 115889)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
        [StandardCard("FIR_951t2", 115891)]
        [StandardCard("FIR_951t3", 115892)]
        [StandardCard("FIR_951t4", 115893)]
        internal static void ConfigureOption(Card card) { card.Implemented = true; }
    }
}
