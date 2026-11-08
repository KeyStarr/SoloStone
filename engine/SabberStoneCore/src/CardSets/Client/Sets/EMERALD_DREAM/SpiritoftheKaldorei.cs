using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpiritoftheKaldorei
    {
        internal const int DbfId = 117292;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(UsedHeroPowerThisTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // Actions/HeroPower.cs increments; Game.MainReady resets for the new turn.
                if (UsedHeroPowerThisTurn((IPlayable)s))
                    new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, "FIR_777e2");
            }) };
        }

        private static bool UsedHeroPowerThisTurn(IPlayable source) => source.Controller.HeroPowerActivationsThisTurn > 0;

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3), Effects.Health_N(3)) };
        }
    }
}
