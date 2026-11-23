using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hearthstone.DataModels;
using UnityEngine;

internal static partial class NativeArena
{
    private static float nextPresentation;
    private static void UpdatePresentation()
    {
        if (!InDraft || Time.realtimeSinceStartup < nextPresentation) return;
        nextPresentation = Time.realtimeSinceStartup + .5f;
        var landing = ArenaLandingPageManager.Get();
        var page = landing?.GetArenaLandingPage();
        if (page == null) return;
        var view = Read();
        var root = AccessTools.Field(typeof(ArenaLandingPage), "m_playButtonNoActiveRun").GetValue(page) as GameObject;
        var button = root?.GetComponentInChildren<PlayButton>(true);
        if (button != null)
        {
            button.SetText("Enter Arena");
            button.SetSecondaryText(GoldCostText(view.EntryCost));
            button.SetTertiaryText(view.GoldBalance + " gold available");
        }
        // Ticket UI has no offline meaning. Hide only leaf visuals of its own: never an object that holds the Arena screen itself.
        // Never during loading either: SceneMgr waits for the first states to settle (DraftDisplay.NotifySceneLoadedWhenReady).
        if (SceneMgr.Get().IsSceneLoaded() && !SceneMgr.Get().IsTransitioning())
        {
            var screen = new List<Component> { landing, page, DraftDisplay.Get() };
            var activeRun = AccessTools.Field(typeof(ArenaLandingPage), "m_playButtonActiveRun").GetValue(page) as GameObject;
            foreach (var play in new[] { root, activeRun }) if (play != null) screen.Add(play.transform);
            var tickets = AccessTools.Field(typeof(ArenaLandingPageManager), "m_tavernTicketDisplay").GetValue(landing) as TavernTicketDisplay;
            HideOwnPart(tickets == null ? null : tickets.m_widget != null ? tickets.m_widget as Component : tickets, "tickets", screen);
            HideOwnPart(AccessTools.Field(typeof(ArenaLandingPage), "m_getMoreTicketsButtonWidget").GetValue(page) as Component, "moreTickets", screen);
            SweepTexts(landing, root, activeRun, view, screen);
        }
        if (AccessTools.Field(typeof(ArenaLandingPageManager), "m_arenaLandingPageDataModel").GetValue(landing) is ArenaLandingPageDataModel model)
        {
            model.isUndergroundUnlocked = true; // Presentation only: its click always explains "coming later".
        }
        NoRating(landing);
    }
    private static readonly HashSet<string> partsLogged = new HashSet<string>();
    private static readonly HashSet<int> hiddenOnce = new HashSet<int>();
    // True when target is, or is an ancestor of, a part of the screen: SetActive(false) on it stops DraftDisplay's coroutines
    // (the DRAFT load never finishes) and blanks the scene. A part the screen sits inside (target inside a play button) counts too.
    private static bool HoldsScreen(Transform target, List<Component> screen) =>
        screen.Any(part => part != null && (part.transform == target || part.transform.IsChildOf(target) || target.IsChildOf(part.transform) && part is Transform));
    private static void HideOwnPart(Component part, string name, List<Component> screen)
    {
        if (part == null || !part.gameObject.activeSelf) return;
        var target = part.gameObject.transform;
        bool shared = HoldsScreen(target, screen);
        if (partsLogged.Add(name))
            log.LogInfo("NATIVE_ARENA_PRESENTATION " + name + " path=" + PathOf(target, null, 12) + " children=" + target.childCount
                + (shared ? " holds the Arena screen: hiding leaf visuals only" : " hidden"));
        if (!shared) { part.gameObject.SetActive(false); return; }
        // The part contains the screen: blank only its own leaf visuals (text and renderers of objects named for tickets).
        foreach (var text in Texts(target))
            if (!HoldsScreen(text.transform, screen) && Mentions(TextOf(text), "ticket")) SetTextOf(text, "");
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(false))
            if (renderer.enabled && NamedFor(renderer.transform, target, "ticket") && !HoldsScreen(renderer.transform, screen)) renderer.enabled = false;
    }
    // UberText lives in a client assembly the plugin does not reference: reached by name, like the other native text.
    private static readonly Type UberTextType = GameTypes.ByName("UberText");
    private static Component[] Texts(Transform root, bool inactive = false) =>
        UberTextType == null ? new Component[0] : root.GetComponentsInChildren(UberTextType, inactive);
    private static string TextOf(Component text) => AccessTools.Property(UberTextType, "Text")?.GetValue(text, null) as string;
    private static void SetTextOf(Component text, string value) => AccessTools.Property(UberTextType, "Text")?.SetValue(text, value, null);
    private static bool Mentions(string text, string word) => !string.IsNullOrEmpty(text) && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool NamedFor(Transform node, Transform root, string word)
    {
        for (var at = node; at != null && at != root.parent; at = at.parent) if (Mentions(at.name, word)) return true;
        return false;
    }
    // Landing page text the offline game has no meaning for: ticket counts and the rating. Leaf objects only, never clickable
    // controls and never a container of the screen; every object is hidden at most once and its path logged for live review.
    private static void SweepTexts(ArenaLandingPageManager landing, GameObject playNoRun, GameObject playActive, ArenaView view, List<Component> screen)
    {
        foreach (var text in Texts(landing.transform))
        {
            string value = TextOf(text);
            if (string.IsNullOrEmpty(value)) continue;
            bool inPlay = (playNoRun != null && text.transform.IsChildOf(playNoRun.transform)) || (playActive != null && text.transform.IsChildOf(playActive.transform));
            if (Mentions(value, "ticket"))
            {
                // On the play button the label becomes the gold cost; anywhere else the ticket count is blanked.
                string replacement = inPlay ? GoldCostText(view.EntryCost) : "";
                if (value != replacement)
                {
                    if (hiddenOnce.Add(text.GetInstanceID()))
                        log.LogInfo("NATIVE_ARENA_PRESENTATION text path=" + PathOf(text.transform, null, 12) + " '" + value + "' -> '" + replacement + "'");
                    SetTextOf(text, replacement);
                }
            }
            else if (Mentions(value, "rating") && !inPlay && !HoldsScreen(text.transform, screen))
            {
                if (hiddenOnce.Add(text.GetInstanceID()))
                    log.LogInfo("NATIVE_ARENA_PRESENTATION rating text path=" + PathOf(text.transform, null, 12) + " '" + value + "'");
                SetTextOf(text, "");
            }
        }
        // Rating plates: leaf objects named for a rating that hold no control and no text other than rating text.
        foreach (var node in landing.GetComponentsInChildren<Transform>(false))
        {
            if (!Mentions(node.name, "rating") || HoldsScreen(node, screen) || node.GetComponentInChildren<PegUIElement>(true) != null) continue;
            if (Texts(node, true).Any(text => !string.IsNullOrEmpty(TextOf(text)) && !Mentions(TextOf(text), "rating") && !int.TryParse(TextOf(text), out _))) continue;
            if (hiddenOnce.Add(node.GetInstanceID()))
                log.LogInfo("NATIVE_ARENA_PRESENTATION rating plate path=" + PathOf(node, null, 12) + " hidden");
            node.gameObject.SetActive(false);
        }
    }
    // Retail's first-visit slides ("How to Play Arena", five wins, other players) do not describe this Arena. Mark them seen the way
    // the client does and run nothing; the "?" button shows our own rules (Disclaimer).
    private static bool NoFtue(ArenaLandingPageManager __instance, ref System.Collections.IEnumerator __result)
    {
        if (!Offline) return true;
        __result = SkipFtue(__instance);
        return false;
    }
    private static System.Collections.IEnumerator SkipFtue(ArenaLandingPageManager landing)
    {
        var options = Options.Get();
        options.SetBool(Option.HAS_SEEN_FORGE, true);
        options.SetBool(Option.HAS_SEEN_FORGE_REVAMP, true);
        options.SetBool(Option.HAS_SEEN_FORGE_REVAMP_UNDERGROUND, true);
        yield return null;
        AccessTools.Field(typeof(ArenaLandingPageManager), "m_FTUECoroutine").SetValue(landing, null);
    }
    // Set the landing models as Awake builds them, before any widget binds: no data change while the first states settle.
    private static void LandingModels(ArenaLandingPageManager __instance)
    {
        if (!Offline || __instance == null) return;
        if (AccessTools.Field(typeof(ArenaLandingPageManager), "m_arenaLandingPageDataModel").GetValue(__instance) is ArenaLandingPageDataModel model)
            model.isUndergroundUnlocked = true; // Presentation only: its click always explains "coming later".
        NoRating(__instance);
    }
    private static bool NoRating(ArenaLandingPageManager __instance)
    {
        if (!Offline) return true;
        if (AccessTools.Field(typeof(ArenaLandingPageManager), "m_arenaLandingPageDataModel").GetValue(__instance) is ArenaLandingPageDataModel model)
            model.rating = model.undergroundRating = string.Empty;
        foreach (string field in new[] { "m_arenaNormalLandingPageDataModel", "m_arenaUndergroundLandingPageDataModel" })
            if (AccessTools.Field(typeof(ArenaLandingPageManager), field).GetValue(__instance) is ArenaLandingPageModeDataModel mode)
                mode.rating = string.Empty;
        return false;
    }
}
