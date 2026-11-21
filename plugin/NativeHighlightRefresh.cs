using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

/// <summary>#239: a Taunt minion's green glow keeps a stale, oversized outline. The client renders the glow's silhouette from the
/// actor's current meshes and caches it; a Taunt frame that is still animating when the glow first renders is cached as it was.
/// Two presentation-only patches refresh it: every glow that turns on re-renders its silhouette, and a Taunt frame that reaches
/// its resting state asks its actor to re-render. Nothing the plugin sends changes.</summary>
internal static class NativeHighlightRefresh
{
    private static AccessTools.FieldRef<HighlightState, bool> forceRerender;
    private static MethodInfo spellType, activeState;
    private static ManualLogSource log;
    private static int logged;

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        try
        {
            var change = AccessTools.Method(typeof(HighlightState), "ChangeState", new[] { typeof(ActorStateType) });
            var field = AccessTools.Field(typeof(HighlightState), "m_forceRerender");
            if (change == null || change.IsStatic || change.ReturnType != typeof(bool) || field == null || field.FieldType != typeof(bool)) return;
            forceRerender = AccessTools.FieldRefAccess<HighlightState, bool>("m_forceRerender");
            harmony.Patch(change, prefix: new HarmonyMethod(typeof(NativeHighlightRefresh), nameof(BeforeChangeState)));
        }
        catch (Exception error) { log?.LogWarning("HIGHLIGHT_REFRESH_INSTALL_FAILED ChangeState " + error.Message); }
        try
        {
            // SpellType and SpellStateType live in Blizzard.T5.Game.dll, absent from the local reference subset: read them by reflection.
            spellType = typeof(Spell).GetMethod("GetSpellType", Type.EmptyTypes);
            activeState = typeof(Spell).GetMethod("GetActiveState", Type.EmptyTypes);
            var started = AccessTools.DeclaredMethod(typeof(Actor), "OnSpellStateStarted");
            if (spellType == null || activeState == null || started == null || started.ReturnType != typeof(void)) return;
            harmony.Patch(started, postfix: new HarmonyMethod(typeof(NativeHighlightRefresh), nameof(AfterSpellStateStarted)));
        }
        catch (Exception error) { log?.LogWarning("HIGHLIGHT_REFRESH_INSTALL_FAILED OnSpellStateStarted " + error.Message); }
    }

    private static bool Off(ActorStateType state)
        => state == ActorStateType.NONE || state == ActorStateType.CARD_IDLE || state == ActorStateType.HIGHLIGHT_OFF;

    // Runs on every highlight state change; two enum compares unless the glow goes from off to on.
    private static void BeforeChangeState(HighlightState __instance, ActorStateType __0)
    {
        try
        {
            if (HighlightRefreshRules.GlowTurnsOn(Off(__instance.CurrentState), Off(__0))) forceRerender(__instance) = true;
        }
        catch (Exception) { /* A failed refresh leaves the client's own cached silhouette. */ }
    }

    // Runs on every actor spell state start; the Taunt check comes first so other spells stop at one field read.
    private static void AfterSpellStateStarted(Actor __instance, object __0)
    {
        try
        {
            if (__0 == null || __instance == null || !(__instance.GetEntity()?.HasTaunt() ?? false)) return;
            if (!HighlightRefreshRules.IsTauntFrame(Convert.ToInt32(spellType.Invoke(__0, null)))
                || Convert.ToInt32(activeState.Invoke(__0, null)) != HighlightRefreshRules.SpellIdle) return;
            __instance.UpdateHighlights(); // Marks each glow dirty and forces its next silhouette render; a glow that is off waits for its turn-on.
            if (logged++ < 20)
                log?.LogInfo("HIGHLIGHT_REFRESH taunt-frame-idle card=" + __instance.GetEntity()?.GetCardId() + " entity=" + __instance.GetEntity()?.GetEntityId());
        }
        catch (Exception) { /* Never disturb the client's spell callback. */ }
    }
}
