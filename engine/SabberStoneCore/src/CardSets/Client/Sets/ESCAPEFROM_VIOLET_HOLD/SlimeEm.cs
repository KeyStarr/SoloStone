using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SlimeEm
    {
        internal const int DbfId = 127092;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var friendly = c.BoardZone.GetAll().Select(m => m.Card).ToArray();
                var enemy = c.Opponent.BoardZone.GetAll().Select(m => m.Card).ToArray();
                new DestroyTask(EntityType.ALLMINIONS).Process(g, c, s, t, stack);
                g.DeathProcessingAndAuraUpdate();
                GiveSpell(c, (IPlayable)s, friendly);
                GiveSpell(c.Opponent, (IPlayable)s, enemy);
            }) };
        }

        private static void GiveSpell(Controller owner, IPlayable source, Card[] minions)
        {
            if (owner.HandZone.IsFull) return;
            var spell = Generic.DrawCard(owner, Cards.FromId("CAP_805t"));
            foreach (var minion in minions)
                Enchantment.GetInstance(owner, source, spell, Cards.FromId("CAP_805e")).CapturedCard = minion;
        }

        internal static void ConfigureEctoplasm(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var capture in ((IPlayable)s).AppliedEnchantments?.Where(e => e.Card.Id == "CAP_805e").ToArray() ?? Array.Empty<Enchantment>())
                {
                    if (c.BoardZone.IsFull) break;
                    new SummonTask(card: capture.CapturedCard).Process(g, c, s, t, stack);
                }
            }) };
        }
    }
}
