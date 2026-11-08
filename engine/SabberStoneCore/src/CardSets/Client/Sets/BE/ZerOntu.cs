using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ZerOntu
    {
        internal const int DbfId = 130000;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                int attack = 0, health = 0;
                for (int value = 0; value <= 2 && !c.BoardZone.IsFull; value++)
                {
                    var candidates = c.DeckZone.GetAll().OfType<Minion>().Where(m => m.AttackDamage == value).ToArray();
                    if (candidates.Length == 0) continue;
                    var recruited = candidates[candidates.Length == 1 ? 0 : g.Random.Next(candidates.Length)];
                    if (candidates.Length > 1) g.OnRandomHappened(true);
                    c.DeckZone.Remove(recruited);
                    Generic.SummonBlock(g, recruited, position++, s);
                    attack += recruited.AttackDamage;
                    health += recruited.Health;
                }
                if (attack != 0 || health != 0)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_790e"), (IPlayable)s, (IPlayable)s, attack, health, 0);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
