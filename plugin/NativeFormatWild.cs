using System;
using HarmonyLib;
using PegasusShared;

// #87 Classic is Wild. Offline the client has no set-rotation timings (GameUtils.IsSetRotated reads server events), so it
// classes every set Standard and lets Basic/Classic cards into Standard decks. These hooks give the client the engine's
// Standard rule (NativeUIProjection.NativeCardIsWild / NativeSetIsRotated): the native collection filters, deck rules
// (DeckRule_IsInFormat, DeckRule_IsNotRotated), CollectionDeck.IsEntityDefValid and the crafting Wild notice then behave as
// on retail. Every client format query for one card or set funnels into the five methods patched here (37.0.0.254477).
internal static partial class NativeUIProbe
{
    internal static void InitializeWildFormat(Harmony harmony)
    {
        Hook(harmony, typeof(GameUtils), "IsSetRotated", nameof(LocalSetRotated), new[] { typeof(TAG_CARD_SET), typeof(DateTime) });
        Hook(harmony, typeof(GameUtils), "IsCardRotated", nameof(LocalCardRotated), new[] { typeof(EntityDef), typeof(DateTime) });
        Hook(harmony, typeof(GameUtils), "IsWildCard", nameof(LocalWildCard), new[] { typeof(EntityDef) });
        Hook(harmony, typeof(GameUtils), "IsStandardCard", nameof(LocalStandardCard), new[] { typeof(EntityDef) });
        Hook(harmony, typeof(GameUtils), "IsCardValidForFormat", nameof(LocalCardValidForFormat), new[] { typeof(FormatType), typeof(EntityDef) });
        harmony.Patch(Required(typeof(EntityBase), "GetCardSet", Type.EmptyTypes),
            postfix: new HarmonyMethod(typeof(NativeUIProbe), nameof(LocalFallbackCardSet)));
    }

    private static bool LocalSetRotated(TAG_CARD_SET __0, ref bool __result)
    {
        bool? rotated = NativeUIProjection.NativeSetIsRotated(active, (int)__0);
        if (!rotated.HasValue) return true;
        __result = rotated.Value; return false;
    }

    private static bool LocalCardRotated(EntityDef __0, ref bool __result) => AnswerWild(__0, wild => wild, ref __result);
    private static bool LocalWildCard(EntityDef __0, ref bool __result) => AnswerWild(__0, wild => wild, ref __result);
    private static bool LocalStandardCard(EntityDef __0, ref bool __result) => AnswerWild(__0, wild => !wild, ref __result);

    private static bool LocalCardValidForFormat(FormatType __0, EntityDef __1, ref bool __result)
    {
        if (__0 == FormatType.FT_STANDARD) return AnswerWild(__1, wild => !wild, ref __result);
        if (__0 == FormatType.FT_WILD) return AnswerWild(__1, wild => true, ref __result);
        return true;
    }

    private static bool AnswerWild(EntityDef def, Func<bool, bool> answer, ref bool result)
    {
        bool? wild = NativeUIProjection.NativeCardIsWild(active, def?.GetCardId());
        if (!wild.HasValue) return true;
        result = answer(wild.Value); return false;
    }

    // Postfix: only an INVALID answer is replaced, so a card with a real set (and every non-SoloStone card) is untouched.
    private static void LocalFallbackCardSet(EntityBase __instance, ref TAG_CARD_SET __result)
    {
        if (__result != TAG_CARD_SET.INVALID || !active) return;
        int? set = NativeUIProjection.NativeFallbackSet(active, __instance?.GetCardId());
        if (set.HasValue) __result = (TAG_CARD_SET)set.Value;
    }
}
