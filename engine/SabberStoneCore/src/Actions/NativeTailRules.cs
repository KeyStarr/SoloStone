using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class NativeTailRules
    {
        public const GameTag SorryEnabled = (GameTag)33200;
        public const GameTag NozdormuEnabled = (GameTag)33201;
        public const int IllusionKey = 115174;
        public static bool IsInternalTag(GameTag tag) => tag == SorryEnabled || tag == NozdormuEnabled;

        public static void CopyIllusion(IPlayable source)
        {
            var c = source.Controller;
            if (!(source is Minion) || source.Zone != c.BoardZone || c.BoardZone.IsFull) return;
            var copy = Generic.Copy(c, source, source, Zone.PLAY, zonePosition: source.ZonePosition + 1,
                preserveBoardMinionState: true);
            int selected = c.Game.Random.Next(2), pair = copy.Id;
            var bodies = new[] { source, copy };
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.Zone != body.Controller.BoardZone) continue;
                Enchantment.GetInstance(body.Controller, source, body, Cards.FromId("EDR_780e1"));
                var entry = PayloadRules.StoreValue(body, IllusionKey, selected == i ? 1 : 0);
                entry[PayloadRules.Link] = pair; entry[PayloadRules.Recipient] = i;
            }
        }

        public static void DamageResolved(Character target, int dealt)
        {
            if (dealt > 0 && target is Minion && target.Zone == target.Controller.BoardZone
                && target.AppliedEnchantments?.Any(e => e.Card.Id == "EDR_780e1") == true
                && PayloadRules.Entries(target, IllusionKey).Any(e => e[PayloadRules.Value] == 1))
                target.ToBeDestroyed = true;
        }

        public static void RedactIllusions(Game game)
        {
            // Sample a belief per pair; copied members retain their shared designation in that belief.
            foreach (var pair in game.IdEntityDic.Values.Where(e => PayloadRules.IsStored(e)
                && e[PayloadRules.Key] == IllusionKey).GroupBy(e => e[PayloadRules.Link]).OrderBy(g => g.Key))
            {
                int selected = game.Random.Next(2);
                foreach (var entry in pair) entry[PayloadRules.Value] = entry[PayloadRules.Recipient] == selected ? 1 : 0;
            }
        }
    }
}
