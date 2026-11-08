using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShelteredSurvivor
    {
        internal const int DbfId = 122637;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_721/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => {
                    if (ctx.LastChosenEntityId != 0)
                        ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                            c.HandZone.Remove(t);
                            if (t.AppliedEnchantments != null)
                                for (int i = t.AppliedEnchantments.Count - 1; i >= 0; i--) t.AppliedEnchantments[i].Remove();
                            t.Reset();
                            if (!Generic.ShuffleIntoDeck(c, s, t)) c.GraveyardZone.Add(t);
                            // ShuffleIntoDeck only inserts; this effect also randomizes the existing deck.
                            c.DeckZone.Shuffle();
                        }), ctx.LastChosenEntityId);
                    return ctx.Draw();
                })) };
        }
    }
}
