using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RangerInitiateVereesa
    {
        internal const int DbfId = 119706;
        internal const string DeckPrefab = "TIME_609t2_Vereesa_DeckAE_Super:3140f2d6cd479104d9c77c8a61c08d4d";
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(PlayedPartner);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int repeats = PlayedPartner((IPlayable)s)
                    ? RangerGeneralSylvanas.Repetitions(ctx.History, ctx.ControllerId, RangerCaptainAlleria.DbfId, RangerGeneralSylvanas.DbfId) : 1;
                for (int i = 0; i < repeats; i++)
                {
                    // Her CardDef's one sub-spell is the deck buff; nothing else shows the buff, since the deck is hidden.
                    if (g.History)
                    {
                        g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { Source = s.Id, SpellPrefabGuid = DeckPrefab });
                        g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
                    }
                    foreach (var minion in c.DeckZone.OfType<Minion>().ToArray())
                        ctx.GiveBuff(minion.Id, "TIME_609t2e");
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }

        private static bool PlayedPartner(IPlayable source) => RangerGeneralSylvanas.Repetitions(
            source.Game.HistoryFacts.Query, source.Controller.Id, RangerCaptainAlleria.DbfId, RangerGeneralSylvanas.DbfId) > 1;
    }
}
