using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NeptulonLordOfTides
    {
        [StandardCard("BE_209", 131240)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("BE_209e2", EntityType.CONTROLLER) };
        }
        [StandardCard("BE_209e1", 132272)]
        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttackHealth(5)) };
        }
        [StandardCard("BE_209e2", 132273)]
        internal static void ConfigurePlayerEffect(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
            StatGainRules.DeclareEntrySetter(card, (minion, actor) => {
                if (!minion.IsRace(Race.ELEMENTAL)) return;
                // The entry setter replaces earlier temporary stat layers as well as their realized values.
                minion.Game.OneTurnEffects.RemoveAll(e => e.entityId == minion.Id
                    && (e.effect.Tag == GameTag.ATK || e.effect.Tag == GameTag.HEALTH));
                Generic.AddEnchantmentBlock(minion.Game, Cards.FromId("BE_209e1"), actor, minion);
            });
        }
    }
}
