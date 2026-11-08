using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VioletPunisher
    {
        internal const int DbfId = 124003;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (!(s is Minion thief) || !(t is Minion victim)) return;
                int count = BonusEffects.Steal(thief, victim);
                if (count > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_101e"), thief, thief, count, count);
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
