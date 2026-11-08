using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EntomologistToru
    {
        internal const int DbfId = 119755;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var held in c.HandZone.OfType<Minion>().ToArray())
                {
                    var identity = held.Card;
                    var jar = Generic.ChangeEntityBlock(c, held, Cards.FromId("TLC_841t"), true);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_841e"), (IPlayable)s, jar, identity.AssetId);
                    var jarring = jar.AppliedEnchantments.Last(e => e.Card.Id == "TLC_841e");
                    // "Free {0}!" is builder 19: tag 2 names the jarred minion's entity, to the jar's owner only.
                    var jarred = PayloadRules.StoreCard(jarring, 1, identity, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
                    jarring[GameTag.TAG_SCRIPT_DATA_NUM_1] = jarred.Id;
                    jar[GameTag.TAG_SCRIPT_DATA_NUM_6] = identity.AssetId; jar[(GameTag)955] = 1;
                }
            }) };
        }
        private static int Contained(IPlayable jar) => jar.AppliedEnchantments?.Where(e => e.Card.Id == "TLC_841e").SelectMany(e => PayloadRules.Entries(e, 1)).FirstOrDefault()?.Card.AssetId ?? 0;
        private static int Filled(IPlayable jar) => Contained(jar) == 0 ? 0 : 1;
        internal static void ConfigureJar(Card card)
        {
            card.Implemented = true; card.Power = new Power();
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_6, Contained); card.HistoryDisplay((GameTag)955, Filled);
        }
        internal static void ConfigurePayload(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var entry = PayloadRules.Entries(t, 1).FirstOrDefault(); if (entry == null || c.BoardZone.IsFull) return;
                var minion = (Minion)Entity.FromCard(c, entry.Card);
                Generic.SummonBlock(g, minion, Math.Min(((Minion)s).LastBoardPosition, c.BoardZone.SlotCount), s);
            }) };
        }
    }
}
