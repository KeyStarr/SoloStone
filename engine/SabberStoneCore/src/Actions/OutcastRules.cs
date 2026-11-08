using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>
    /// Outcast eligibility belongs to one primary activation of the played entity.
    /// It lives in an internal entity tag, so clones and snapshots carry it; it is never history.
    /// </summary>
    public static class OutcastRules
    {
        // Inside Generic.IsDecisionInternalTag's 31001-31009 range: clone/snapshot data; >=1000 emits no tag-change history.
        // id: the primary power is running synchronously (or a decision Battlecry play is pending).
        // id|Parked: the primary power ended while its registered effect is suspended on a decision.
        //      Only that effect's own resumed cursor reads it; after-play/after-cast triggers that run
        //      before the answer (and recasts/copies of the same entity) see false.
        //      (Tag reads clamp negatives to 0, hence a high bit; entity ids stay far below it.)
        // Binding the value to the original id stops raw-tag copies from claiming that entity's play.
        internal const GameTag PlayedFromEdgeTag = (GameTag)31008;
        private const int Parked = 1 << 30;

        public static bool PlayedFromEdge(IPlayable source)
        {
            if (source == null) return false;
            int value = source[PlayedFromEdgeTag];
            if (value == source.Id) return true;
            if (value != (source.Id | Parked)) return false;
            // Parked implies the primary's root effect is the only effect alive (RunEffect refuses a second root).
            var resolution = source.Game.Resolution;
            var effect = resolution.CurrentEffect;
            return effect != null && !resolution.HasSuspendedEffect && effect.SourceId == source.Id;
        }

        public static bool AtEdge(IPlayable source) => source.Card[GameTag.OUTCAST] == 1
            && source.Zone == source.Controller.HandZone
            && (source.ZonePosition == 0 || source.ZonePosition == source.Controller.HandZone.Count - 1);

        internal static void Capture(IPlayable source)
        {
            if (source.Card[GameTag.OUTCAST] != 1) return;
            Set(source, AtEdge(source) ? source.Id : 0);
        }

        /// <summary>The primary power returned: park eligibility for its own suspended continuation, else clear.</summary>
        internal static void EndPrimary(IPlayable source)
        {
            if (source[PlayedFromEdgeTag] != source.Id) return;
            var suspended = source.Game.Resolution.SuspendedEffect;
            // Nested frames inherit the root's source, so the top frame identifies the root activation.
            if (suspended != null && suspended.SourceId == source.Id) source[PlayedFromEdgeTag] = source.Id | Parked;
            else Clear(source);
        }

        /// <summary>
        /// After an answer resumes a parked play: drain the tasks its continuation queued while it is live
        /// (the answer path would drain them next anyway), then re-park if it suspended again, else release.
        /// </summary>
        internal static void AfterResume(Game game, int sourceId)
        {
            if (!game.IdEntityDic.TryGetValue(sourceId, out var source) || source[PlayedFromEdgeTag] != (sourceId | Parked)) return;
            source[PlayedFromEdgeTag] = sourceId;
            game.ProcessTasks();
            EndPrimary(source);
        }

        /// <summary>The suspended effect was cancelled (concede): release a parked play without running anything.</summary>
        internal static void EndContinuation(Game game, int sourceId)
        {
            if (game.IdEntityDic.TryGetValue(sourceId, out var source) && source[PlayedFromEdgeTag] == (sourceId | Parked))
                Clear(source);
        }

        internal static void Clear(IPlayable source)
        {
            int id = source.Id, value = source[PlayedFromEdgeTag] & ~Parked;
            if (value != id) return;
            // SummonCopyTask copies raw tags. Bind the marker to its original play, then clear those copies too.
            foreach (var pair in source.Game.IdEntityDic)
                if ((pair.Value[PlayedFromEdgeTag] & ~Parked) == id) pair.Value.NativeTags.Remove(PlayedFromEdgeTag);
        }

        private static void Set(IPlayable source, int value)
        {
            if (source[PlayedFromEdgeTag] == value) return;
            if (value == 0) source.NativeTags.Remove(PlayedFromEdgeTag);
            else source[PlayedFromEdgeTag] = value;
        }
    }
}
