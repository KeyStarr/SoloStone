using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;

// Admission at logical effect entry, never final engine state or a pending SHOW's realtime identity.
// This checks the pinned consumers documented in draw-spell-impact-evidence.md / the admission sweep report;
// it is not a Unity completion emulator. Native lists split at blocks, subspells, flush metadata and packets.
internal static class NativeAdmission
{
    internal sealed record Fault(string Rule, int Row, int Source, int Entity, string Detail, string SourceCard = "");
    private sealed class EntityState
    {
        internal string Card = "";
        internal int Type, Zone, Controller, Secret;
        internal void Tags(IEnumerable<Tag> tags)
        {
            foreach (var tag in tags)
                switch ((GameTag)tag.Name)
                {
                    case GameTag.CARDTYPE: Type = tag.Value; break;
                    case GameTag.ZONE: Zone = tag.Value; break;
                    case GameTag.CONTROLLER: Controller = tag.Value; break;
                    case GameTag.SECRET: Secret = tag.Value; break;
                }
        }
    }
    private sealed class Block
    {
        internal PowerHistoryStart Start;
        internal readonly List<(int Row, PowerHistoryData Data)> Tasks = new();
    }

    private sealed class Segment
    {
        internal Block Block;
        internal PowerHistorySubSpellStart SubSpell;
        internal bool Initial;
        internal readonly List<(int Row, PowerHistoryData Data)> Tasks = new();
    }

    internal static bool RequiresTypedImpact(PowerHistoryStart start, string card, PowerHistorySubSpellStart sub = null)
    {
        if (sub != null)
            return (sub.SpellPrefabGuid ?? "").Split(':').Last() is
                "4694fc2ee619cd54c9352938dbf84940" or "2084d102cd356b04ca792130b749cc21";
        if (!string.IsNullOrEmpty(start.EffectCardId)) card = start.EffectCardId;
        return start.EffectIndex == 0 && ((int)start.Type == (int)BlockType.POWER
            && card is "CORE_BT_035" or "CORE_BT_491" or "RLK_708"
            || (int)start.Type == (int)BlockType.TRIGGER && card is "CORE_BT_351" or "RLK_708");
    }

    internal static List<Fault> Validate(IReadOnlyList<PowerHistoryData> rows) => ValidatePackets(new[] { rows });

    internal static List<Fault> ValidatePackets(IEnumerable<IReadOnlyList<PowerHistoryData>> packets)
    {
        var rows = new List<PowerHistoryData>();
        var received = new Dictionary<int, IReadOnlyList<PowerHistoryData>>();
        foreach (var packet in packets)
        {
            if (packet.Count == 0) continue;
            received.Add(rows.Count, packet);
            rows.AddRange(packet);
        }
        var blocks = new Dictionary<int, Block>();
        var segments = new Dictionary<int, Segment>();
        var stack = new Stack<(Block Block, Stack<PowerHistorySubSpellStart> Subs)>();
        Segment current = null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (received.ContainsKey(i)) current = null;
            var data = rows[i];
            if (data.HasPowerStart)
            {
                var block = new Block { Start = data.PowerStart };
                blocks[i] = block;
                stack.Push((block, new()));
                segments[i] = current = new() { Block = block, Initial = true };
            }
            else if (data.HasPowerEnd)
            {
                if (stack.Count > 0) stack.Pop();
                current = null;
            }
            else if (stack.Count > 0)
            {
                var frame = stack.Peek();
                if (data.HasSubSpellStart)
                {
                    frame.Subs.Push(data.SubSpellStart);
                    current = null;
                }
                if (current == null)
                    segments[i] = current = new() { Block = frame.Block,
                        SubSpell = frame.Subs.Count > 0 ? frame.Subs.Peek() : null };
                current.Tasks.Add((i, data));
                // Only the initial list is visible to strict own-list scanners.
                if (current.Initial)
                    frame.Block.Tasks.Add((i, data));
                if (data.HasSubSpellEnd)
                {
                    if (frame.Subs.Count > 0) frame.Subs.Pop();
                    current = null;
                }
                else if (data.HasMetaData && (int)data.MetaData.Type == 21) current = null;
            }
        }
        var state = new Dictionary<int, EntityState>();
        EntityState Entity(int id) { if (!state.TryGetValue(id, out var entity)) state[id] = entity = new(); return entity; }
        var faults = new List<Fault>();
        void ValidateSegment(Segment segment, int row)
        {
            var start = segment.Block.Start;
            int sourceId = segment.SubSpell?.SourceEntityId is > 0 ? segment.SubSpell.SourceEntityId : start.Source;
            if (!state.TryGetValue(sourceId, out var source) || source.Type is 1 or 2) return;
            if (segment.SubSpell == null && (int)start.Type is not ((int)BlockType.POWER) and not ((int)BlockType.TRIGGER)) return;
            bool requiresTyped = RequiresTypedImpact(start, source.Card, segment.SubSpell);
            void CheckTarget(int id, string kind, bool typedRequired = true)
            {
                if (!state.TryGetValue(id, out var target))
                    faults.Add(new("R1", row, sourceId, id, kind + " target does not exist at entry", source.Card));
                else if (typedRequired && target.Type is 0 or 1 or 2 or 6)
                    faults.Add(new("R1", row, sourceId, id, kind + " target has unsupported logical CARDTYPE=" + target.Type, source.Card));
            }
            if (segment.SubSpell?.TargetEntityIds.Count > 0)
            {
                foreach (int id in segment.SubSpell.TargetEntityIds.Distinct()) CheckTarget(id, "subspell", requiresTyped);
                return;
            }
            // Spell scans the complete current list, even metadata after an unsafe TAG_CHANGE.
            // SubSpellController's empty-ID fallback ignores metadata altogether (0x117d5).
            bool resolved = false;
            foreach (var task in segment.Tasks)
                if (task.Data.HasMetaData && (int)task.Data.MetaData.Type == 0
                    && (task.Data.MetaData.Data == 0 || task.Data.MetaData.Data == start.Source))
                    foreach (int id in task.Data.MetaData.Info)
                    {
                        CheckTarget(id, "metadata", requiresTyped);
                        if (state.TryGetValue(id, out var target) && target.Type is not (1 or 2 or 6)) resolved = true;
                    }
            if (resolved && segment.SubSpell == null) return;
            var inferred = new List<int>();
            var seen = new HashSet<int>();
            foreach (var task in segment.Tasks)
            {
                if (!task.Data.HasTagChange) continue;
                int id = task.Data.TagChange.Entity;
                if (id == sourceId || !seen.Add(id) || !state.TryGetValue(id, out var target)
                    || target.Type is 1 or 2 or 6
                    || segment.SubSpell == null && target.Zone == (int)SabberStoneCore.Enums.Zone.SETASIDE) continue;
                inferred.Add(id);
            }
            // Unknown FX may intentionally consume hidden cards (e.g. Vanessa's spawn-to-hand).
            // Mixed typed/untyped inference still admits unnecessary hidden targets; explicit IDs remain checked above.
            if (inferred.Any(id => state[id].Type != 0) || requiresTyped)
                foreach (int id in inferred.Where(id => state[id].Type == 0)) CheckTarget(id, "inferred");
        }
        var initializedFulls = new HashSet<PowerHistoryData>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < rows.Count; i++)
        {
            // Entity.OnRealTimeFullEntity (0x137c8) calls SetTags before its task runs. Only new FULLs in
            // this received packet qualify; never pre-apply a later SHOW or a FULL from a future packet.
            if (received.TryGetValue(i, out var packet))
                foreach (var entry in packet.Where(p => p.HasFullEntity))
                    if (!state.ContainsKey(entry.FullEntity.Entity))
                    {
                        var full = entry.FullEntity;
                        var fresh = Entity(full.Entity); fresh.Card = full.Name ?? ""; fresh.Tags(full.Tags);
                        initializedFulls.Add(entry);
                    }
            var data = rows[i];
            if (segments.TryGetValue(i, out var segment)) ValidateSegment(segment, i);
            var create = data.CreateGame ?? data.ResetGame?.CreateGame;
            if (create != null)
            {
                Entity(create.GameEntity.Id).Tags(create.GameEntity.Tags);
                foreach (var player in create.Players) Entity(player.Entity.Id).Tags(player.Entity.Tags);
            }
            var snapshot = data.HasFullEntity ? data.FullEntity : data.HasShowEntity ? data.ShowEntity : data.HasChangeEntity ? data.ChangeEntity : null;
            if (snapshot != null && !initializedFulls.Contains(data))
            {
                var entity = Entity(snapshot.Entity);
                entity.Card = snapshot.Name ?? "";
                entity.Tags(snapshot.Tags);
            }
            else if (data.HasHideEntity)
            {
                var entity = Entity(data.HideEntity.Entity);
                entity.Card = ""; entity.Zone = data.HideEntity.Zone; // HIDE does not erase logical CARDTYPE.
            }
            else if (data.HasTagChange)
            {
                var tag = data.TagChange;
                var entity = Entity(tag.Entity);
                int zone = tag.Tag == (int)GameTag.ZONE ? tag.Value : entity.Zone;
                int owner = tag.Tag == (int)GameTag.CONTROLLER ? tag.Value : entity.Controller;
                if ((zone != entity.Zone || owner != entity.Controller) && entity.Card.Length == 0
                    && entity.Type is not (1 or 2 or 6) && (zone == (int)SabberStoneCore.Enums.Zone.PLAY
                        || owner == 1 && zone is (int)SabberStoneCore.Enums.Zone.HAND or (int)SabberStoneCore.Enums.Zone.SECRET))
                    faults.Add(new("R3", i, 0, tag.Entity, $"public move before identity: zone={zone} controller={owner}"));
                entity.Tags(new[] { new Tag { Name = tag.Tag, Value = tag.Value } });
                if (zone == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME) state.Remove(tag.Entity);
            }
            else if (data.HasPowerStart)
            {
                var block = blocks[i];
                var start = block.Start;
                var source = Entity(start.Source);
                if ((int)start.Type == (int)BlockType.PLAY && source.Controller == 2 && source.Card.Length == 0
                    && !(source.Secret == 1 && source.Type == (int)CardType.SPELL))
                    faults.Add(new("R2", i, start.Source, start.Source, "anonymous opposing PLAY is not a typed Secret"));
                if ((int)start.Type == (int)BlockType.POWER && source.Card == "CORE_EX1_323")
                {
                    var types = block.Tasks.Where(t => t.Data.HasFullEntity)
                        .SelectMany(t => t.Data.FullEntity.Tags).Where(t => t.Name == (int)GameTag.CARDTYPE).Select(t => t.Value).ToHashSet();
                    if (!types.Contains((int)CardType.HERO_POWER) || !types.Contains((int)CardType.WEAPON))
                        faults.Add(new("R4", i, start.Source, start.Source, "Jaraxxus POWER needs own HERO_POWER and WEAPON FULL_ENTITY", source.Card));
                }
            }
        }
        return faults;
    }
}
