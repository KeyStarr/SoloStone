using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Wickerfang
    {
        [StandardCard("CATA_139", 122976)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            ColossalRules.Declare(card, "CATA_139t", "CATA_139t2", "CATA_139t3", "CATA_139t4");
            StatGainRules.DeclareListener(card, (body, gain) => {
                if (!(gain.Sender is Minion leg) || leg.Controller != body.Controller
                    || leg.Zone != body.Controller.BoardZone || !IsLeg(leg.Card.Id)) return;
                Generic.AddEnchantmentBlock(body.Game, Cards.FromId("CATA_139e"), body, body, gain.Attack, gain.Health);
            });
        }
        private static bool IsLeg(string id) => id == "CATA_139t" || id == "CATA_139t2" || id == "CATA_139t3" || id == "CATA_139t4";
        [StandardCard("CATA_139t", 122977)]
        [StandardCard("CATA_139t2", 128060)]
        [StandardCard("CATA_139t3", 128061)]
        [StandardCard("CATA_139t4", 128062)]
        internal static void ConfigureLeg(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new AddEnchantmentTask("CATA_139te", EntityType.SOURCE)
            } };
        }
        [StandardCard("CATA_139te", 123334)]
        internal static void ConfigureLegGrowth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
        [StandardCard("CATA_139e", 123336)]
        internal static void ConfigureSymbiosis(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
    }
}
