using System;
using System.Collections.Generic;
using System.Linq;

// NPC difficulty ids and copy (owner 2026-11-15), dependency-free so the save code and every suite that links LocalProfile can
// read them; the tuning lives in Npc/World/NpcDifficulty.cs. A save stores the id (LocalProfileData.NpcDifficulty, absent = Hard,
// every save made before the setting); each match stamps the level it was played on (LocalMatchRecord.NpcDifficulty).
public static class NpcDifficultyIds
{
    public const string Easy = "easy", Medium = "medium", Hard = "hard", Extreme = "extreme";
    // Display order. Extreme is shown, locked ("coming later").
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { Easy, Medium, Hard, Extreme });
    public const string Default = Hard;          // saves without the field
    public const string NewSaveDefault = Medium;  // the Create save form's starting choice

    public static bool IsKnown(string id) => id != null && All.Contains(id);
    public static bool IsLocked(string id) => id == Extreme;
    public static bool IsSelectable(string id) => IsKnown(id) && !IsLocked(id);
    // null = Default; an unknown id throws (a save naming one is refused, never silently changed).
    public static string Resolve(string id)
    {
        if (id == null) return Default;
        if (!IsKnown(id)) throw new InvalidOperationException("Unknown NPC difficulty: " + id);
        return id;
    }

    public static string Name(string id)
    {
        switch (Resolve(id))
        {
            case Easy: return "Easy";
            case Medium: return "Medium";
            case Hard: return "Hard";
            default: return "Extreme";
        }
    }

    // One line per row on the save and Options pages.
    public static string Summary(string id)
    {
        switch (Resolve(id))
        {
            case Easy: return "Relaxed: softer decks and beginner NPCs";
            case Medium: return "Balanced: a step below the full ladder";
            case Hard: return "The full ladder: strong decks, sharp NPCs";
            default: return "Coming later";
        }
    }
}
