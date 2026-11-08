using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AshalonRidgeGuardian
    {
        internal static readonly string[] Adaptations = { "UNG_999t2", "UNG_999t3", "UNG_999t4", "UNG_999t5", "UNG_999t6",
            "UNG_999t7", "UNG_999t8", "UNG_999t10", "UNG_999t13", "UNG_999t14" };
        [StandardCard("TLC_229t14", 120778)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var choose = new DiscoverThenContinueTask((g, c, s, stack) => Adaptations.ChooseNElements(3, g.Random).Select(Cards.FromId).ToArray());
            var remember = new CustomTask((g, c, s, t, stack) => {
                var chosen = stack.Playables.Single();
                var memory = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TLC_229t14e"));
                memory[GameTag.TAG_SCRIPT_DATA_NUM_1] = chosen.Card.AssetId;
                if (s is Minion minion && minion.Zone == c.BoardZone) Apply(memory, minion);
            });
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TLC_229t14/v1", choose, remember, choose, remember) };
        }
        [StandardCard("TLC_229t14e", 120779)]
        internal static void Memory(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            QuestRules.RegisterPlayedMinionEffect(120779, Apply);
        }
        [StandardCard("UNG_999t4", 41059)]
        internal static void RockyCarapace(Card card)
        {
            AncientStegodon.ConfigureOption(card);
            card.ExcludeFromLegacyRandomPools = true;
        }
        private static void Apply(Enchantment memory, Minion minion) => Generic.AddEnchantmentBlock(minion.Game,
            Cards.FromId(Cards.FromAssetId(memory.ScriptTag1).Id + "e"), memory, minion);
    }
}
