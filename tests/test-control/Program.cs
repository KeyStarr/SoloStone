using MiniJSON;
using UnityEngine;

NativeTestControl.CheckBoardTrace();

internal sealed partial class NativeTestControl
{
    internal static void CheckBoardTrace()
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        string Request(string command, string fields = "") => "{\"v\":1,\"id\":\"trace\",\"cmd\":\"" + command + "\"" + fields + "}";
        const int maxFrames = TestControlProtocol.BoardTraceMaxFrames;
        foreach (int frames in new[] { 1, maxFrames })
        {
            var request = TestControlProtocol.Parse(Request("board-trace-start", ",\"frames\":" + frames));
            Check(!request.Mutation && request.Integer("frames") == frames, "trace start bounds and no gameplay mutation");
        }
        Check(!TestControlProtocol.Parse(Request("board-trace-read")).Mutation, "trace read validation");
        foreach (string fields in new[] { "", ",\"frames\":0", ",\"frames\":-1", ",\"frames\":1201", ",\"frames\":1.5",
            ",\"frames\":\"2\"", ",\"frames\":[2]", ",\"frames\":2,\"extra\":1" })
        {
            bool rejected = false;
            try { TestControlProtocol.Parse(Request("board-trace-start", fields)); } catch (FormatException) { rejected = true; }
            Check(rejected, "invalid start: " + fields);
        }
        foreach (string fields in new[] { ",\"frames\":1", ",\"after\":0", ",\"matchId\":\"m\"" })
        {
            bool rejected = false;
            try { TestControlProtocol.Parse(Request("board-trace-read", fields)); } catch (FormatException) { rejected = true; }
            Check(rejected, "read rejects fields: " + fields);
        }

        var control = new NativeTestControl();
        Check(!(bool)control.ReadBoardTrace()["started"], "read before start");
        GameState.Current = new();
        var zone = GameState.Current.Friendly.Zone;
        zone.transform.localPosition = new Vector3 { x = 10, y = 20, z = 30 };
        var first = new Card { Entity = new Entity { Id = 10, Position = 1 } };
        var moving = new Card { Entity = new Entity { Id = 11, Position = 2 } };
        moving.transform.parent = zone.transform;
        moving.transform.localPosition = new Vector3 { x = 8, y = 2, z = 3 };
        moving.Actor.transform.parent = moving.transform;
        moving.Actor.transform.localPosition = new Vector3 { x = .5f, y = 1, z = 2 };
        moving.Actor.Shown = false;
        zone.Cards.AddRange(new[] { first, moving });
        GameState.Current.Enemy.Zone.Cards.Add(new Card { Entity = new Entity { Id = 20, Position = 1 }, Actor = null });
        Time.frameCount = 100; Time.realtimeSinceStartup = 5;
        var started = control.StartBoardTrace(2);
        Check((string)started["status"] == "running" && control.boardTrace.Samples.Count == 1
            && (int)started["recordCount"] == 3 && !started.ContainsKey("samples"), "start samples immediately and returns status only");
        control.SampleBoardTrace();
        Check(control.boardTrace.Sampled == 1 && control.boardTrace.Samples.Count == 1, "once per frame");
        var snapshot = control.ReadBoardTrace();
        string snapshotJson = Json.Serialize(snapshot);
        var frame = (JsonNode)((JsonList)snapshot["samples"])[0];
        var before = (JsonList)((JsonList)frame["friendly"])[1];
        Check(frame.Count == 4 && (int)frame["frame"] == 100 && (float)frame["realtime"] == 5, "one clock per compact frame");
        Check(before.Count == 16 && (int)before[0] == 11 && (int)before[1] == 1 && (int)before[2] == 2
            && before.Skip(4).Take(3).Cast<float>().SequenceEqual(new[] { 8f, 2f, 3f })
            && before.Skip(7).Take(3).Cast<float>().SequenceEqual(new[] { 18.5f, 23f, 35f })
            && before.Skip(10).Take(3).Cast<float>().SequenceEqual(new[] { 8f, 2f, 3f })
            && (bool)before[13], "card relative to zone, card local, actor world and direct parent (actor is a child)");
        Check(!(bool)before[3] && (bool)before[14] && !(bool)before[15], "actor hidden independently of card");
        var identity = (JsonNode)((JsonNode)snapshot["entities"])["11"];
        Check((string)identity["cardId"] == "TEST_CARD" && (string)identity["side"] == "friendly" && (int)identity["player"] == 1,
            "identity dictionary carries card ID and side/player once");
        var enemy = (JsonList)((JsonList)frame["enemy"])[0];
        Check((int)enemy[0] == 20 && enemy.Skip(7).Take(3).All(value => value == null)
            && !(bool)enemy[3] && (bool)enemy[14] && !(bool)enemy[15], "enemy missing actor retains card coordinates and visibility");
        zone.Cards.Reverse();
        moving.Entity.Position = 1;
        moving.transform.parent = new Transform { parent = zone.transform, localPosition = new Vector3 { x = 2 } };
        moving.transform.localPosition.x = 1;
        moving.Shown = false; moving.Actor.Shown = true;
        zone.Cards.Add(new Card { Entity = new Entity { Id = 12 } });
        Time.frameCount++; Time.realtimeSinceStartup += .016f;
        control.SampleBoardTrace();
        var after = (JsonList)((JsonList)((JsonNode)control.boardTrace.Samples[1])["friendly"])[0];
        Check((string)control.ReadBoardTrace()["status"] == "done" && control.boardTrace.Sampled == 2
            && (int)after[1] == 0 && (int)after[2] == 1 && (float)after[4] == 3 && (float)after[10] == 1 && !(bool)after[13],
            "reparented card zone x differs from local x; window completes");
        Check(!(bool)after[3] && !(bool)after[14] && (bool)after[15], "card hidden independently of actor");
        Check(!ReferenceEquals(snapshot["samples"], control.boardTrace.Samples) && !ReferenceEquals(snapshot["entities"], control.boardTrace.Entities)
            && Json.Serialize(snapshot) == snapshotJson && !((JsonNode)snapshot["entities"]).ContainsKey("12"),
            "queued snapshot keeps collections and committed values unchanged after sampling");
        control.StartBoardTrace(1);
        Check(control.boardTrace.Samples.Count == 1 && control.runningBoardTrace == null, "restart replaces samples and one frame completes immediately");
        Check(Json.Serialize(snapshot) == snapshotJson, "restarting does not change an older queued reply");
        control.StartBoardTrace(maxFrames);
        control.StopBoardTrace("disabled");
        Check(control.runningBoardTrace == null && control.boardTrace.Error == "disabled", "disable stops sampling and records reason");
        control.StartBoardTrace(2);
        int records = control.boardTrace.Records;
        zone.Cards.Add(new Card { Entity = new Entity { Id = 99 } });
        GameState.Current.Enemy.Zone.Fail = true;
        Time.frameCount++; control.SampleBoardTrace();
        Check(control.runningBoardTrace == null && control.boardTrace.Error.Contains("sample failure")
            && control.boardTrace.Sampled == 1 && control.boardTrace.Samples.Count == 1 && control.boardTrace.Records == records
            && !control.boardTrace.Entities.ContainsKey("99"), "second-side exception drops whole candidate frame and identities");
        GameState.Current = null;
        control.StartBoardTrace(2);
        Check(control.boardTrace.Error.Contains("GameState unavailable"), "missing game recorded");
        foreach (bool precise in new[] { false, true })
        {
            GameState.Current = new();
            for (int i = 0; i < 14; i++)
            {
                var side = i < 7 ? GameState.Current.Friendly : GameState.Current.Enemy;
                var card = new Card { Entity = new Entity { Id = i + 100, Position = i % 7 + 1 } };
                card.transform.parent = side.Zone.transform;
                card.Actor.transform.parent = card.transform;
                if (precise)
                {
                    card.transform.localPosition = new Vector3 { x = -12.345678f, y = 2.345678f, z = 3.456789f };
                    card.Actor.transform.localPosition = new Vector3 { x = .456789f, y = .567891f, z = .678912f };
                }
                side.Zone.Cards.Add(card);
            }
            Time.frameCount = 100000; Time.realtimeSinceStartup = 12345.678f;
            control.StartBoardTrace(maxFrames);
            int firstBytes = control.boardTrace.Bytes;
            int frameBytes = TestControlProtocol.Utf8.GetByteCount(Json.Serialize(control.boardTrace.Samples[0])) + 1;
            int fits = Math.Min(maxFrames, 1 + (BoardTraceMaxBytes - firstBytes) / frameBytes);
            while (control.runningBoardTrace != null) { Time.frameCount++; control.SampleBoardTrace(); }
            Check(control.boardTrace.Sampled == fits && control.boardTrace.Samples.Count == fits
                && control.boardTrace.Records == fits * 14 && control.boardTrace.Truncated == (fits < maxFrames), "byte budget admits exactly complete full-board frames");
            Check(precise ? fits < maxFrames : fits == maxFrames, "compact full-board window fits 1200 small-coordinate frames; full precision uses byte budget");
            Check(control.boardTrace.Entities.Count == 14 && control.boardTrace.Bytes <= BoardTraceMaxBytes
                && TestControlProtocol.Utf8.GetByteCount(Json.Serialize(control.ReadBoardTrace())) < TestControlProtocol.MaxResponse, "full window and dictionary bounded below response limit");
            Console.WriteLine($"WINDOW precise={precise} actors=14 frames={fits}/{maxFrames} frameBytes={frameBytes} budgetUsed={control.boardTrace.Bytes}/{BoardTraceMaxBytes}");
        }
        GameState.Current = new();
        GameState.Current.Friendly.Zone.Cards.Add(first);
        control.StartBoardTrace(maxFrames);
        records = control.boardTrace.Records;
        GameState.Current.Enemy.Zone.Cards.Add(new Card { Entity = new Entity { Id = 98, CardId = new string('x', BoardTraceMaxBytes) } });
        Time.frameCount++; control.SampleBoardTrace();
        Check(control.boardTrace.Truncated && control.boardTrace.Samples.Count == 1 && control.boardTrace.Sampled == 1
            && control.boardTrace.Records == records && control.boardTrace.Entities.Count == 1, "cap on second side drops entire frame and oversized identity");
        Check(TestControlProtocol.Utf8.GetByteCount(Json.Serialize(control.ReadBoardTrace())) < TestControlProtocol.MaxResponse, "response stays bounded");
        GameState.Current = new();
        control.StartBoardTrace(1);
        Check(control.boardTrace.Sampled == 1 && control.boardTrace.Records == 0 && control.boardTrace.Samples.Count == 1, "empty board still records complete frame");
        Console.WriteLine($"PASS {checks} board trace and protocol checks");
    }
}
