using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NespirahEnthralled
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(CardType.MINION, race: Race.NAGA);
        private static Trigger Fel(ISimpleTask task) => new Trigger(TriggerType.AFTER_CAST) {
            TriggerSource = TriggerSource.FRIENDLY,
            Condition = new SelfCondition(p => p is Spell && p.Card[GameTag.SPELL_SCHOOL] == 7),
            SingleTask = task
        };
        [StandardCard("CATA_527", 122772)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                LocationActivation = new LocationActivation { Target = LocationTargetKind.Character,
                    Task = ComplexTask.Create(new DamageTask(1, EntityType.TARGET),
                        new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE)) },
                DeathrattleTask = EffectContext.Task(ctx => { ctx.SummonRightOfLocation("CATA_527t2"); return TaskState.COMPLETE; }),
                Trigger = Fel(new CustomTask((g, c, s, t, stack) => {
                    if (s.Zone != c.BoardZone || ((IPlayable)s).ToBeDestroyed) return;
                    ((IPlayable)s).IsExhausted = false;
                    s[GameTag.EXHAUSTED] = 0; s[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
                }))
            };
        }
        [StandardCard("CATA_527t2", 122845)]
        internal static void Unshackled(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = Fel(new CustomTask((g, c, s, t, stack) => {
                var pool = OfflineCardPools.Candidates(g, c.HeroClass, Pool(), 122845)
                    .Where(candidate => candidate[(GameTag)2247] <= 0 &&
                        (candidate.ColossalAppendageIds == null || candidate.ColossalAppendageIds.Count == 0)).ToArray();
                if (pool.Length == 0) return;
                var chosen = pool[g.Random.Next(pool.Length)];
                var generated = Entity.FromCard(c, chosen, new EntityData { [GameTag.ZONE] = (int)Zone.HAND, [GameTag.DISPLAYED_CREATOR] = s.Id });
                Generic.AddHandPhase(c, generated);
                if (generated.Zone == c.HandZone)
                    CostRules.AddModifier((Playable)generated, EffectOperator.SET, 1);
            })) };
        }
    }
}
