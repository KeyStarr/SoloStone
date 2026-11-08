using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CracklingCloudstrider
    {
        internal const int DbfId = 122950;
        [StandardCard("CATA_563", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_563/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL, maximumCurrentCost: 4)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE : ctx.RunLegacy(
                    new CustomTask((g, c, s, t, stack) => {
                        if (s.Zone == c.BoardZone)
                            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_563e2"), (SabberStoneCore.Model.Entities.IPlayable)s,
                                (SabberStoneCore.Model.Entities.IPlayable)s, t.Card.AssetId, entityId: t.Id);
                        // Absorb is removal, not discard; the capture deliberately keeps only the definition.
                        t[PayloadRules.Stored] = 1;
                        // Overcast's tag 2 names this entity (builder 23): its controller keeps seeing it, nobody else does.
                        t[PayloadRules.Policy] = (int)PayloadPolicy.ShowOwner;
                        c.SetasideZone.Add(c.HandZone.Remove(t));
                    }), ctx.LastChosenEntityId))) };
        }
        [StandardCard("CATA_563e2", 122998)]
        internal static void ConfigureCapture(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(GetCapturedCardTask.Task,
                new PlayTask(PlayType.SPELL, true)) };
        }
    }
}
