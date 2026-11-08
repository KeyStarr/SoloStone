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
    internal static class SylvanassTriumph
    {
        internal const int DbfId = 122932;
        // Exclude the in-flight occurrence; earlier plays of this entity still qualify replays and effect-only recasts.
        private static bool Played(Controller controller, int sourceId)
        {
            var plays = controller.Game.HistoryFacts.Query.Plays();
            long ownPlay = sourceId != 0 && controller.Game.Facts.PlayingEntity == sourceId
                ? plays.LastOrDefault(p => p.EntityId == sourceId)?.PlayId ?? 0 : 0;
            return plays.Any(p => p.ControllerId == controller.Id && p.DefinitionKnown && p.DbfId == DbfId && p.PlayId != ownPlay);
        }

        private static bool PlayedAnotherCopy(IPlayable source) => Played(source.Controller, source.Id);

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(PlayedAnotherCopy);
            // Rule: R1 counts earlier committed own hand-plays, even countered; automatic/repeated casts do not add plays.
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
            card.TargetingAvailabilityPredicate = (c, _) => !Played(c, c.Game.Facts.PlayingEntity);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                if (PlayedAnotherCopy((IPlayable)s))
                    ctx.RunLegacy(new DamageTask(3, EntityType.ENEMIES, true));
                else
                {
                    int? target = t is ICharacter character
                        && (character == character.Controller.Hero || character.Zone == character.Controller.BoardZone)
                        && ((Playable)s).TargetingRequirements(character)
                        ? t.Id : ctx.RandomLegalTarget(s.Id);
                    if (target.HasValue) ctx.Damage(target.Value, 3, spellDamage: true);
                }
            }) };
        }
    }
}
