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
    internal static class ChogallMastermind
    {
        [StandardCard("CATA_726", 125432)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_726t", "CATA_726t1");
        }
        [StandardCard("CATA_726t", 125433)]
        internal static void ConfigureArm(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g,c,s,t,stack) => HeraldRules.ConsumeNeighbor((Minion)s))
            } };
        }
        [StandardCard("CATA_726t1", 125435)]
        internal static void ConfigureOtherArm(Card card) => ConfigureArm(card);
        [StandardCard("CATA_726te", 127988)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
