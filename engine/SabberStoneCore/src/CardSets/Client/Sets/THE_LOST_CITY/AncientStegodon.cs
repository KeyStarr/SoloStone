using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AncientStegodon
    {
        internal const int DbfId = 118220;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Choose("TLC_242", "UNG_999t6", "UNG_999t13", "UNG_999t14") };
        }

        internal static ISimpleTask Choose(string id, string first, params string[] rest) =>
            new DecisionBattlecryTask("client/" + id + "/v1", new DiscoverThenContinueTask(first, rest),
                new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion minion) || minion.Zone != c.BoardZone) return;
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen != null) new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, chosen.Card.Id + "e");
                }));

        internal static void ConfigureOption(Card card)
        {
            card.Implemented = true;
            // Legacy metadata predates the pinned export's hidden costs and Elusive wording.
            if (card.Type == CardType.SPELL) card.Tags[GameTag.HIDE_COST] = 1;
            if (card.Id == "UNG_999t5") card.Text = "<b>Elusive</b>";
            if (card.Id == "UNG_999t2t1") card.Text = string.Empty;
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = card.Id == "UNG_999t14e" ? new Enchant(Effects.AttackHealth_N(1))
                : new Enchant(new Effect(card.Id == "UNG_999t6e" ? GameTag.TAUNT : GameTag.POISONOUS, EffectOperator.SET, 1)) };
        }
    }
}
