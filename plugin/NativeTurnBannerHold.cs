using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

/// <summary>#146: on the local player's turn start, a Sigil, Objective, Sidequest or Secret trigger waits for the "Your Turn"
/// banner. The client's generic trigger controller already shows that banner and waits for it; the controllers for those four do
/// not. This asks for the banner as the generic controller does (TurnStartManager.NotifyOfTriggerVisual) and keeps the power queue
/// closed until the banner is gone, with a timeout so it cannot freeze a match. Presentation only: nothing the plugin sends changes.</summary>
internal static class NativeTurnBannerHold
{
    private static readonly TurnBannerHoldRules.Hold hold = new TurnBannerHoldRules.Hold();
    private static ManualLogSource log;

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        try
        {
            var method = AccessTools.Method(typeof(GameState), "CanProcessPowerQueue", Type.EmptyTypes);
            if (method == null || method.IsStatic || method.ReturnType != typeof(bool)) return;
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(NativeTurnBannerHold), nameof(AfterCanProcessPowerQueue)));
        }
        catch (Exception error) { log?.LogWarning("TURN_BANNER_HOLD_INSTALL_FAILED " + error.Message); }
    }

    /// <summary>For the stall readout: whether the last check held the queue, and since when (Time.realtimeSinceStartup).</summary>
    internal static MiniJSON.JsonNode State() => new MiniJSON.JsonNode { { "holding", hold.Holding }, { "released", hold.Released },
        { "since", hold.Since }, { "lastRelease", hold.LastRelease } };

    private static void AfterCanProcessPowerQueue(GameState __instance, ref bool __result)
    {
        // Hot path: the client asks every frame. Only the turn-start window (the manager is listening) gets past the first checks.
        if (!__result) return;
        try
        {
            var manager = TurnStartManager.Get();
            if (manager == null || !manager.IsListeningForTurnEvents()) return;
            if (__instance.IsGameOverNowOrPending() || __instance.WasRestartRequested() || !__instance.IsLocalSidePlayerTurn()) return;
            var queue = __instance.GetPowerProcessor()?.GetPowerQueue();
            if (queue == null || queue.Count == 0) return;
            var head = queue.Peek();
            var start = head?.GetBlockStart();
            if (start == null || !head.IsStartOfBlock()) return;
            var source = head.GetSourceEntity(false);
            if (!TurnBannerHoldRules.IgnoresBanner((int)start.BlockType == (int)PegasusGame.HistoryBlock.Type.TRIGGER, start.TriggerKeyword,
                source != null && source.IsSecret())) return;
            manager.NotifyOfTriggerVisual(); // Shows the banner once; later calls do nothing.
            double now = Time.realtimeSinceStartup;
            bool wait = hold.ShouldHold(head, manager.IsTurnStartIndicatorShowing(), now);
            if (wait) __result = false;
            else if (hold.LastRelease != null)
            {
                log?.LogInfo("TURN_BANNER_HOLD released=" + hold.LastRelease + " waited=" + hold.LastWaited.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " keyword=" + start.TriggerKeyword + " card=" + source?.GetCardId());
                // Report each release once: the list stays released, so clear the note.
                hold.AcknowledgeRelease();
            }
        }
        catch (Exception) { /* A failed check never holds the queue. */ }
    }
}
