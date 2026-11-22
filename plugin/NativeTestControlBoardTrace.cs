#if !SOLOSTONE_RELEASE
using System;
using MiniJSON;
using UnityEngine;

internal sealed partial class NativeTestControl
{
    private const int BoardTraceMaxBytes = TestControlProtocol.MaxResponse - 16384;
    private sealed class BoardTrace
    {
        internal readonly JsonList Samples = new JsonList();
        internal readonly JsonNode Entities = new JsonNode();
        internal int Requested, Sampled, Records, LastFrame = -1, Bytes = 4;
        internal bool Truncated;
        internal string Error;
    }
    private BoardTrace boardTrace, runningBoardTrace;

    private JsonNode StartBoardTrace(int frames)
    {
        AssertMainThread();
        boardTrace = runningBoardTrace = new BoardTrace { Requested = frames };
        SampleBoardTrace();
        return BoardTraceStatus();
    }

    private JsonNode BoardTraceStatus() => new JsonNode {
        { "started", boardTrace != null }, { "status", runningBoardTrace == null ? "done" : "running" },
        { "framesRequested", boardTrace?.Requested ?? 0 }, { "framesSampled", boardTrace?.Sampled ?? 0 },
        { "recordCount", boardTrace?.Records ?? 0 }, { "maxFrames", TestControlProtocol.BoardTraceMaxFrames },
        { "byteBudget", BoardTraceMaxBytes }, { "budgetUsed", boardTrace?.Bytes ?? 0 },
        { "truncated", boardTrace?.Truncated ?? false }, { "error", boardTrace?.Error }
    };

    private JsonNode ReadBoardTrace()
    {
        AssertMainThread();
        var result = BoardTraceStatus();
        // Committed frames and entity entries never change; only their containing collections grow.
        result.Add("samples", boardTrace == null ? new JsonList() : TestControlProtocol.List(boardTrace.Samples));
        var entities = new JsonNode();
        if (boardTrace != null)
            foreach (var entry in boardTrace.Entities) entities.Add(entry.Key, entry.Value);
        result.Add("entities", entities);
        return result;
    }

    private void StopBoardTrace(string reason)
    {
        if (runningBoardTrace == null) return;
        runningBoardTrace.Error = reason.Length > 1024 ? reason.Substring(0, 1024) : reason;
        runningBoardTrace = null;
    }

    private void SampleBoardTrace()
    {
        // Test-control Update only; exceptions must never escape into Unity's frame loop.
        try
        {
            var trace = runningBoardTrace;
            int frame = Time.frameCount;
            if (trace.LastFrame == frame) return;
            trace.LastFrame = frame;
            float realtime = Time.realtimeSinceStartup;
            var front = GameState.Get();
            if (front == null) throw new InvalidOperationException("Board trace: GameState unavailable");
            var entities = new JsonNode();
            var friendly = SampleBoardTraceSide(front.GetFriendlySidePlayer(), "friendly", entities);
            var enemy = SampleBoardTraceSide(front.GetOpposingSidePlayer(), "enemy", entities);
            var sample = new JsonNode {
                { "frame", frame }, { "realtime", realtime }, { "friendly", friendly }, { "enemy", enemy }
            };
            // Include commas and dictionary growth, reserving 16 KiB for status and the reply envelope.
            int bytes = TestControlProtocol.Utf8.GetByteCount(Json.Serialize(sample)) + 1
                + (entities.Count == 0 ? 0 : TestControlProtocol.Utf8.GetByteCount(Json.Serialize(entities)) + 1);
            if (trace.Bytes + bytes > BoardTraceMaxBytes)
            {
                trace.Truncated = true;
                runningBoardTrace = null;
                return;
            }
            // Publish only complete frames, including their identities. An exception drops the entire candidate.
            trace.Samples.Add(sample);
            foreach (var entry in entities) trace.Entities.Add(entry.Key, entry.Value);
            trace.Bytes += bytes;
            trace.Records += friendly.Count + enemy.Count;
            trace.Sampled++;
            if (trace.Sampled >= trace.Requested) runningBoardTrace = null;
        }
        catch (Exception error) { StopBoardTrace(error.GetType().Name + ": " + error.GetBaseException().Message); }
    }

    private JsonList SampleBoardTraceSide(Player player, string side, JsonNode entities)
    {
        var zone = player?.GetBattlefieldZone();
        var cards = zone?.GetCards();
        if (cards == null) throw new InvalidOperationException("Board trace: " + side + " play zone unavailable");
        var rows = new JsonList();
        for (int index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var entity = card == null ? null : card.GetEntity();
            var actor = card == null ? null : card.GetActor();
            int id = entity?.GetEntityId() ?? 0;
            string key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!runningBoardTrace.Entities.ContainsKey(key) && !entities.ContainsKey(key))
                entities.Add(key, new JsonNode { { "cardId", entity?.GetCardId() }, { "side", side }, { "player", player.GetPlayerId() } });
            Vector3? zonePosition = card == null ? (Vector3?)null : zone.transform.InverseTransformPoint(card.transform.position);
            Vector3? local = card == null ? (Vector3?)null : card.transform.localPosition;
            Vector3? world = actor == null ? (Vector3?)null : actor.transform.position;
            bool cardShown = card != null && card.IsShown(), actorShown = actor != null && actor.IsShown();
            rows.Add(TestControlProtocol.List(new object[] {
                id, index, entity?.GetTag(GAME_TAG.ZONE_POSITION) ?? 0, cardShown && actorShown,
                zonePosition?.x, zonePosition?.y, zonePosition?.z, world?.x, world?.y, world?.z,
                local?.x, local?.y, local?.z, card != null && card.transform.parent == zone.transform, cardShown, actorShown
            }));
        }
        return rows;
    }
}
#endif
