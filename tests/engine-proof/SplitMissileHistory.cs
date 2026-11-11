using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static class SplitMissileHistory
{
    // The native split-damage spell reads TARGET metadata from the card's own POWER block;
    // per-missile POWER sub-blocks leave it without targets, so no VFX plays (live JAIL_445, 2026-10-21).
    // Native Last and replay Full must carry the same activation shape.
    internal static void Flat(Game g, int cursor, IPlayable source, int missiles, BlockType type = BlockType.POWER)
    {
        var batch = g.PowerHistory.Last.Skip(cursor).ToArray();
        int fullCursor = batch.Length == 0 ? g.PowerHistory.Full.Count : g.PowerHistory.Full.IndexOf(batch[0]);
        Core2026Proof.Check(fullCursor >= 0 && g.PowerHistory.Full.Skip(fullCursor).SequenceEqual(batch),
            $"{source.Card.Id} Full and Last retain identical entries");
        Flat(batch, source, missiles, type, "Last");
        Flat(g.PowerHistory.Full.Skip(fullCursor).ToArray(), source, missiles, type, "Full");
    }

    private static void Flat(IPowerHistoryEntry[] entries, IPlayable source, int missiles, BlockType type, string history)
    {
        int start = Array.FindIndex(entries, e => e is PowerHistoryBlockStart b && b.BlockType == type && b.Source == source.Id);
        Core2026Proof.Check(start >= 0, $"{source.Card.Id} opens its {type} block");
        int depth = 0, targets = 0, damage = 0, victim = 0;
        var subSpells = new Stack<string>();
        bool nested = false;
        for (int i = start; i < entries.Length; i++)
        {
            if (entries[i] is PowerHistoryBlockStart block)
            {
                nested |= depth > 0 && block.BlockType == type && block.Source == source.Id;
                depth++;
            }
            else if (entries[i] is PowerHistoryBlockEnd) { if (--depth == 0) break; }
            else if (entries[i] is PowerHistorySubSpellStart subSpell) subSpells.Push(subSpell.SpellPrefabGuid);
            else if (entries[i] is PowerHistorySubSpellEnd) subSpells.Pop();
            else if (depth == 1 && !subSpells.Contains(PowerHistory.GeneratedShufflePrefab) && entries[i] is PowerHistoryMetaData meta)
            {
                if (meta.Type == MetaDataType.TARGET)
                {
                    Core2026Proof.Check(meta.Data == source.Id && meta.Info.Count == 1 && victim == 0,
                        $"{source.Card.Id} each flat TARGET binds source and one hit");
                    victim = meta.Info[0]; targets++;
                }
                if (meta.Type == MetaDataType.DAMAGE)
                {
                    Core2026Proof.Check(meta.Info.Count == 1 && meta.Info[0] == victim,
                        $"{source.Card.Id} each flat DAMAGE follows its TARGET");
                    victim = 0; damage++;
                }
            }
        }
        Core2026Proof.Check(depth == 0 && subSpells.Count == 0 && !nested && targets == missiles && damage == missiles && victim == 0,
            $"{source.Card.Id} {history} missiles stay flat in its {type} block: nested={nested} targets={targets}/{missiles} damage={damage}/{missiles}");
    }
}
