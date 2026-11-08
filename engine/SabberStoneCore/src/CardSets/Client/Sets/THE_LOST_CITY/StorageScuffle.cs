using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StorageScuffle
    {
        internal const int DbfId = 118266;
        // Rule: any completed own semantic Discover this global turn qualifies, including a burned reward (R8).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(DiscoveredThisTurn);
            card.ConsumesHistory = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => 0, EffectOperator.SET, new SelfCondition(DiscoveredThisTurn)),
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                    int? target = t is ICharacter character && ((Playable)s).IsValidPlayTarget(character)
                        ? t.Id : ctx.RandomLegalTarget(s.Id);
                    if (target.HasValue) ctx.Damage(target.Value, 3, spellDamage: true);
                })
            };
        }

        private static bool DiscoveredThisTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return history.Discoveries(history.ThisTurn).Any(f => f.ControllerId == source.Controller.Id);
        }
    }
}
