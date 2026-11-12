using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class TwilightMenderProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_814", (g, p, id) => {
        Batch9PoolProof.HandDeathrattle(g, p, id,
            c => c.Type == CardType.SPELL && c[GameTag.SPELL_SCHOOL] == 5,
            c => c.Type == CardType.SPELL && c[GameTag.SPELL_SCHOOL] == 6);
    });
}
