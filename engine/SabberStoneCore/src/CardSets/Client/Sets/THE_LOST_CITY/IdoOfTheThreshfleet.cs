using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IdoOfTheThreshfleet
    {
        [StandardCard("TLC_241", 117951)]
        internal static void Configure(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("TLC_241t", 117953)]
        internal static void ConfigureSpell(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> {
                [PlayReq.REQ_TARGET_TO_PLAY] = 0, [PlayReq.REQ_MINION_TARGET] = 0 });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (!(t is Minion minion) || minion.Zone != minion.Controller.BoardZone) return;
                Generic.AddEnchantmentBlock(g,Cards.FromId("TLC_241e"),(IPlayable)s,minion);
                minion.HasDivineShield = true;
            }) };
        }
        [StandardCard("TLC_241e", 119685)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
