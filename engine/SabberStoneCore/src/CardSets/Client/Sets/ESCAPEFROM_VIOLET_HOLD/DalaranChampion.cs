using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DalaranChampion
    {
        [StandardCard("JAIL_330", 126620)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            StatGainRules.DeclareListener(card, (champion, gain) => {
                if (gain.TargetId != champion.Id || gain.EffectId == "JAIL_330e") return;
                Generic.AddEnchantmentBlock(champion.Game, Cards.FromId("JAIL_330e"), champion, champion, 1, 1);
            });
        }
        [StandardCard("JAIL_330e", 133047)]
        internal static void ConfigureBonus(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
    }
}
