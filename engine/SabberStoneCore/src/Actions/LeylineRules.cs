using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class LeylineRules
    {
        public const GameTag Keyword = (GameTag)4285;
        private const GameTag Discount = (GameTag)31120;
        private const GameTag ExtraTriggers = (GameTag)31121;
        private const GameTag EffectIncrease = (GameTag)31122;

        public static bool IsInternalTag(GameTag tag) => tag == Discount || tag == ExtraTriggers || tag == EffectIncrease;
        public static bool IsLeyline(Card card) => card[Keyword] != 0;
        public static int CostReduction(Controller controller) => controller[Discount];
        public static int Magnitude(IPlayable card) => checked(card.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] + card.Controller[EffectIncrease]);
        public static int Times(IPlayable card) => checked(1 + card.Controller[ExtraTriggers]);
        public static int AdjustNumericCost(IPlayable card, int cost) => IsLeyline(card.Card) ? Math.Max(0, cost - CostReduction(card.Controller)) : cost;

        // Closed printed family (leyline-printed.yaml); coordinator D3/D4 fixes this generation order.
        public static Card[] Family() => new[] { Cards.FromId("MEND_500"), Cards.FromId("MEND_502"), Cards.FromId("MEND_504") };

        public static void Upgrade(Controller controller, IPlayable source, int cost = 0, int extraTriggers = 0, int effect = 0)
        {
            if (cost < 0 || extraTriggers < 0 || effect < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            int discount = checked(controller[Discount] + cost);
            int triggers = checked(controller[ExtraTriggers] + extraTriggers);
            int increase = checked(controller[EffectIncrease] + effect);
            // Leave room for the derived total/base without a partial update on overflow.
            _ = checked(triggers + 1);
            _ = checked(increase + 6);
            var hand = controller.HandZone.Where(p => IsLeyline(p.Card)).Select(p => (Card: p, Cost: p.Cost)).ToArray();
            controller[Discount] = discount;
            controller[ExtraTriggers] = triggers;
            controller[EffectIncrease] = increase;
            foreach (var previous in hand)
            {
                previous.Card.Card.UpdateHistoryDisplays(previous.Card);
                if (controller.Game.History && previous.Cost != previous.Card.Cost)
                    controller.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(previous.Card.Id, GameTag.COST, previous.Card.Cost));
            }
        }

        internal static void ConfigureDisplay(Card card)
        {
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Magnitude);
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_2, Times);
        }
    }
}
