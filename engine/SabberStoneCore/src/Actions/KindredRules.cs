using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class KindredRules
    {
        public static bool SharesKindredType(Card left, Card right)
        {
            if (left == null || right == null) return false;
            if (left.Type == CardType.MINION && right.Type == CardType.MINION)
                return left.SharesRaceWith(right);
            // Tag 1635 is a scalar school, not flags (kindred-school-match.yaml lean).
            int school = left[(GameTag)1635];
            return left.Type == CardType.SPELL && right.Type == CardType.SPELL
                && school >= 1 && school <= 7 && school == right[(GameTag)1635];
        }

        public static bool Match(IPlayable source) => source.Controller.HasKindredMatch(source.Card);
        public static int HandDiscount(IPlayable source, int amount) => Match(source)
            ? amount * (Pending(source.Controller) ? 2 : 1) : 0;

        private static bool Pending(Controller controller) =>
            controller.AppliedEnchantments?.Any(e => e.Card.Id == "TLC_251e") == true;

        private static int Consume(Controller controller)
        {
            var pending = controller.AppliedEnchantments?.Where(e => e.Card.Id == "TLC_251e").ToArray();
            if (pending == null || pending.Length == 0) return 1;
            // Owner-approved co-consumption: any number of pending grants means twice total.
            foreach (var enchantment in pending) enchantment.Remove();
            return 2;
        }

        public static int BeginMatchedClause(IPlayable source) => Match(source) ? Consume(source.Controller) : 0;

        public static void CommitCostClause(IPlayable source)
        {
            if (source.Zone == source.Controller.HandZone && IsCostClause(source.Card) && Match(source)) Consume(source.Controller);
        }

        private static bool IsCostClause(Card card) => card.Id == "TLC_816" || card.Id == "TLC_366" || card.Id == "TLC_600";

        // Only the explicit Kindred clause belongs here; never wrap a whole card power.
        public static ISimpleTask Clause(ISimpleTask clause) => new CustomTask((g, c, s, t, stack) => {
            int count = BeginMatchedClause((IPlayable)s);
            for (int i = 0; i < count; i++) clause.Process(g, c, s, t, stack);
        });
    }
}
