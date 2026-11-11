using System;

// The one static helper of plugin/NativeMatch.cs that plugin/LocalNativeText.cs calls (TransportCardId: the client keeps a few
// card ids under other names). The suites that build without the client have no NativeMatch (it needs the client's protocol
// types), so they link this mirror under the same name. Do not change one without the other: the client-tier suite
// engine-proof-client proves, over every card id the engine has, that NativeMatch.TransportCardId and this mirror agree.
internal static class TransportCardIdMirror
{
    internal static string Of(string cardId)
    {
        // The pinned client keeps four Jade art tiers; legacy sizes retain their exact entity stats.
        if (cardId != null && cardId.Length == 11 && cardId.StartsWith("CFM_712_t", StringComparison.Ordinal)
            && int.TryParse(cardId.Substring(9), out int size) && size >= 1 && size <= 30)
            return size >= 20 ? "CFM_712_t20" : size >= 7 ? "CFM_712_t07" : size >= 4 ? "CFM_712_t04" : "CFM_712_t01";
        switch (cardId)
        {
            case "CS2_034": return "HERO_08bp";
            case "CS2_102": return "HERO_01bp";
            case "CS1h_001": return "HERO_09bp";
            case "CS2_101": return "HERO_04bp";
            case "CS2_017": return "HERO_06bp";
            case "DS1h_292": return "HERO_05bp";
            case "CS2_083b": return "HERO_03bp";
            case "CS2_056": return "HERO_07bp";
            case "CS2_049": return "HERO_02bp";
            case "AT_132_WARRIOR": return "HERO_01bp2";
            case "AT_132_SHAMAN": return "HERO_02bp2";
            case "AT_132_ROGUE": return "HERO_03bp2";
            case "AT_132_PALADIN": return "HERO_04bp2";
            case "AT_132_HUNTER": return "HERO_05bp2";
            case "AT_132_DRUID": return "HERO_06bp2";
            case "AT_132_WARLOCK": return "HERO_07bp2";
            case "AT_132_MAGE": return "HERO_08bp2";
            case "AT_132_PRIEST": return "HERO_09bp2";
            default: return cardId ?? "";
        }
    }
}
#if !CLIENT_TRANSPORT
internal static class NativeMatch
{
    internal static string TransportCardId(string cardId) => TransportCardIdMirror.Of(cardId);
}
#endif
