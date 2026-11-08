using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DemonicConfinement
    {
        internal const int DbfId = 125915;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion) || minion.Zone != minion.Controller.BoardZone || minion.IsDormant
                    || minion.ToBeDestroyed || minion.Health <= 0) return;
                if (minion.Controller == c && minion.IsRace(Race.DEMON))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_997e1"), (IPlayable)s, minion);
                else DormantRules.Make(minion, 2, "JAIL_997e3", s);
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3), Effects.Health_N(3)) };
        }
    }
}
