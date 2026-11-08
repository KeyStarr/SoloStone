using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GruesomeNightmare
    {
        [StandardCard("CATA_161", 123547)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_161/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.HandOrBoard, cardType: CardType.MINION)),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables?.LastOrDefault();
                    if (chosen != null) Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_161e"), (IPlayable)s, chosen, ((Minion)s).AttackDamage);
                })) };
        }
        [StandardCard("CATA_161e", 128017)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }
    }
}
