using SabberStoneCore.Enums;

internal static class HiveMapProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_900", (g, p, id) =>
        CryptMapProof.Prove(g, p, id, CardClass.DEMONHUNTER, c => c.Type == CardType.SPELL && c[GameTag.SPELL_SCHOOL] == 7,
            new[] { "CORE_BT_801", "FIR_902", "TLC_901" }));
}
