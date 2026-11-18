using System;
using System.Collections.Generic;
using System.Linq;

// Beginner NPC decks (owner 2026-11-15): what an Easy NPC plays on the Core step below Bronze. One per class, Core cards only,
// coherent but clearly weaker than the player's Core starter (LocalDecks.PlayerCoreStarterDecks): a worse curve, little
// removal, no legendaries. Admission "core" like NpcCoreDecks: never generated, matched or inferred on the ladder, served only to
// an Easy Core-step encounter. A class without a list here falls back to its Core starter. Strength is measured with the
// AiHarness `difficulty` verb (private/docs/roadmap/2026-11-15-npc-difficulty.md); tune the lists, never Hard.
public static class NpcBeginnerDecks
{
    public const string Origin = "beginner", IdPrefix = "npc-beginner-";

    public static string IdFor(string cardClass) => IdPrefix + (cardClass ?? "").ToLowerInvariant();

    // (class, hero, name, archetype, pairs, singles). Pairs are played twice; singles once. 30 cards.
    private static readonly (string Class, string Hero, string Name, DeckArchetype Archetype, string[] Pairs, string[] Singles)[] Lists =
    {
        ("WARRIOR", "HERO_01", "Recruit Warrior", DeckArchetype.Tempo,
            new[] { "CORE_DRG_024", "CORE_EX1_391", "CORE_EX1_604", "CORE_OG_149", "CORE_NEW1_018", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("SHAMAN", "HERO_02", "Spark Shaman", DeckArchetype.Midrange,
            new[] { "CS3_007", "CORE_WC_042", "CORE_LOOT_373", "CORE_CS2_042", "CORE_OG_031", "CORE_CS2_189", "CORE_EX1_011", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("ROGUE", "HERO_03", "Sneak Rogue", DeckArchetype.Tempo,
            new[] { "CORE_KAR_069", "CORE_EX1_131", "CORE_EX1_278", "CORE_EX1_134", "CORE_BOT_576", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("PALADIN", "HERO_04", "Squire Paladin", DeckArchetype.Midrange,
            new[] { "CORE_CFM_753", "CORE_EX1_362", "CORE_TRL_307", "CORE_KAR_077", "CORE_BAR_878", "CORE_CS2_189", "CORE_EX1_011", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("HUNTER", "HERO_05", "Rookie Hunter", DeckArchetype.Midrange,
            new[] { "CORE_UNG_912", "CORE_BAR_801", "CORE_DS1_184", "CORE_NEW1_031", "CORE_AT_062", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("DRUID", "HERO_06", "Sapling Druid", DeckArchetype.Midrange,
            new[] { "CORE_SW_439", "CORE_TRL_240", "CORE_ULD_133", "CORE_EX1_160", "Core_LOE_115", "CORE_CS2_189", "CORE_EX1_011", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("WARLOCK", "HERO_07", "Imp Warlock", DeckArchetype.Midrange,
            new[] { "CORE_YOD_026", "CORE_ICC_407", "CORE_WON_096", "CORE_LOOT_013", "CORE_EX1_302", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("MAGE", "HERO_08", "Apprentice Mage", DeckArchetype.Midrange,
            new[] { "CORE_DRG_107", "CORE_SW_108", "CORE_BAR_812", "CORE_EX1_289", "CORE_EDR_001", "CORE_CS2_189", "CORE_EX1_011", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("PRIEST", "HERO_09", "Novice Priest", DeckArchetype.Midrange,
            new[] { "CORE_EX1_193", "CORE_RLK_814", "CORE_AT_055", "CORE_CS1_130", "CORE_ICC_210", "CORE_CS2_189", "CORE_EX1_011", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("DEMONHUNTER", "HERO_10", "Fledgling Demon Hunter", DeckArchetype.Tempo,
            new[] { "CORE_BT_480", "CORE_BT_351", "CORE_BT_321", "CORE_WC_701", "CORE_TTN_843", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
        ("DEATHKNIGHT", "HERO_11", "Grave Recruit Death Knight", DeckArchetype.Midrange,
            new[] { "RLK_503", "RLK_061", "CORE_RLK_116", "CORE_RLK_121", "RLK_708", "CORE_CS2_189", "CORE_ULD_723", "CORE_ULD_191", "CORE_GVG_085", "CORE_LOOT_413", "CORE_KAR_062", "CORE_EX1_058", "CORE_EX1_103", "Core_UNG_072", "CORE_EX1_506" },
            new string[0]),
    };

    public static IReadOnlyList<NpcDeckEntry> Entries() => Lists.Select(list => new NpcDeckEntry(IdFor(list.Class), 1, list.Name,
        list.Class, list.Hero, Origin, NpcCoreDecks.Admission, null,
        new NpcDeckTags(list.Archetype, 5, 1, 0, 3, LadderBand.Bronze, LadderBand.Legend),
        new NpcDeckSource("solostone", null, null, null, "Beginner NPC deck"),
        list.Pairs.Select(id => new NpcDeckCard(id, 2)).Concat(list.Singles.Select(id => new NpcDeckCard(id, 1))),
        new NpcDeckSideboard[0])).ToArray();
}
