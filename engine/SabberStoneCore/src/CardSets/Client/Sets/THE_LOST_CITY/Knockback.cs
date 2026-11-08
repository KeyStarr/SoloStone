using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Knockback
    {
        internal const int DbfId = 117699;
        // Rule: +1 per successful gameplay shuffle operation by you into your deck, not per card; Trade excluded (history-facts R4, wiki.gg/Knockback).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, DisplayDamage);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                int? target = t is ICharacter character && ((Playable)s).IsValidPlayTarget(character)
                    ? t.Id : ctx.RandomLegalTarget(s.Id);
                if (target.HasValue) ctx.Damage(target.Value, Damage(ctx.History, c.Id), spellDamage: true);
            }) };
        }

        private static int Damage(HistoryQuery history, int controllerId) => 1 + history.Events<ShuffleOperationFact>()
            .Where(f => f.ControllerId == controllerId && f.DestinationControllerId == controllerId)
            .Select(f => f.OperationId).Distinct().Count();

        private static int DisplayDamage(IPlayable source) => Damage(source.Game.HistoryFacts.Query, source.Controller.Id);
    }
}
