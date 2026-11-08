using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BashanaRunetotem
    {
        internal const int DbfId = 123493;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int count = System.Math.Min(3, c.HandZone.FreeSpace); if (count == 0) return;
                var pool = OfflineCardPools.Candidates(g, c.BaseClass, CataclysmPools.NatureSpell(), DbfId);
                var tuples = new List<Card[]>();
                var none = new Card[] { null };
                foreach (var first in pool)
                foreach (var second in count > 1 ? pool : none)
                foreach (var third in count > 2 ? pool : none)
                    if (first.Cost + (second?.Cost ?? 0) + (third?.Cost ?? 0) == 12)
                        tuples.Add(new[] { first, second, third }.Take(count).ToArray());
                if (tuples.Count == 0) return;
                var chosen = tuples[g.Random.Next(tuples.Count)]; g.OnRandomHappened(true);
                foreach (var spell in chosen)
                {
                    var treant = Entity.FromCard(c, Cards.FromId("MEND_046t"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    // The client's text builder reads tag 2 as an ENTITY id (GetEntity(id).GetName()) once, when the
                    // Treant enters the hand, so the owner-visible carved entity must exist before the hand phase.
                    var carved = PayloadRules.StoreCard(treant, 1, spell,
                        PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
                    treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
                    Generic.AddHandPhase(c, treant);
                }
            }) };
        }
        internal static void ConfigureTreant(Card card)
        {
            card.Implemented = true; card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, PayloadRules.FirstReference);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var carved = PayloadRules.Entries(s, 1).FirstOrDefault(); if (carved == null) return;
                var spell = Entity.FromCard(c, carved.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                var cast = new TaskStack(); cast.AddPlayable(spell);
                new PlayTask(PlayType.SPELL, true).Process(g, c, s, null, cast);
            }) };
        }
    }
}
