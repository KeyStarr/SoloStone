#if !SOLOSTONE_RELEASE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hearthstone.UI;
using MiniJSON;
using UnityEngine;

internal static partial class NativeArena
{
    private static ArenaStubService testStub;
    internal static void TestControlEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (ReferenceEquals(ArenaServiceRegistry.Current, testStub)) ArenaServiceRegistry.Current = null;
            testStub = null;
            return;
        }
        if (ArenaServiceRegistry.Current != null) return;
        // Only neutral minions for the boot spike: all three test heroes can use these deterministic active-pool offers.
        testStub = new ArenaStubService(hero => LocalCardCatalog.ActivePoolIds().Where(id => {
            var card = SabberStoneCore.Model.Cards.FromId(id);
            return card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL && card.Type == SabberStoneCore.Enums.CardType.MINION;
        }));
        ArenaServiceRegistry.Current = testStub;
    }

    internal static JsonNode State()
    {
        try
        {
            var view = Read();
            return new JsonNode { { "scene", SceneMgr.IsInitialized() ? SceneMgr.Get().GetMode().ToString() : null },
                { "phase", view.Phase.ToString() }, { "slot", view.Slot }, { "nativeSlot", NativeSlot(view) }, { "wins", view.Wins }, { "losses", view.Losses },
                { "maxWins", view.MaxWins }, { "maxLosses", view.MaxLosses }, { "hero", view.HeroCardId }, { "runId", view.RunId },
                { "offers", TestControlProtocol.List((view.Phase == ArenaPhase.ChoosingHero ? view.HeroOffers : view.Offers).Cast<object>()) },
                { "reward", view.Reward == null ? null : new JsonNode { { "runId", view.Reward.RunId }, { "wins", view.Reward.Wins }, { "gold", view.Reward.Gold },
                    { "dust", view.Reward.Dust }, { "packTypes", TestControlProtocol.List(view.Reward.PackTypes.Cast<object>()) } } },
                { "deckCount", view.Deck.Count }, { "entryCost", view.EntryCost }, { "gold", view.GoldBalance },
                { "service", ArenaServiceRegistry.Current == null ? null : ArenaServiceRegistry.Current is ArenaStubService ? "stub" : "real" },
                { "serviceType", ArenaServiceRegistry.Current?.GetType().Name }, { "stub", ArenaServiceRegistry.Current is ArenaStubService }, { "available", !disabled },
                { "phoneUI", (bool)UniversalInputManager.UsePhoneUI }, { "matchActive", MatchActive },
                { "pendingReplies", TestControlProtocol.List(replies.Select(reply => (object)reply.Id)) },
                { "lastReplyIds", TestControlProtocol.List(lastReplies.Cast<object>()) }, { "error", lastError ?? view.Error }, { "errorSeq", errorSeq }, { "notice", lastNotice },
                { "loadSeconds", draftLoadBegan < 0 ? (object)null : Math.Round(Time.realtimeSinceStartup - draftLoadBegan, 1) },
                { "loadFailure", loadFailure }, { "firstWait", firstWait }, { "usable", ScreenUsable(out string unusable) ? null : unusable },
                // waits/changing are the last snapshot (taken every second while DRAFT is not usable, and once when it became
                // usable); waitsAge says how old it is. routines is live.
                { "clickBlockers", ClickBlockers() },
                { "waits", Waits() }, { "waitsAge", waitsReadAt < 0 ? (object)null : Math.Round(Time.realtimeSinceStartup - waitsReadAt, 1) },
                { "changing", TestControlProtocol.List(changingRows.Cast<object>()) }, { "routines", RoutineSummary() } };
        }
        catch (Exception error) { return new JsonNode { { "error", error.GetBaseException().Message } }; }
    }

    // Active StandardClickblocker elements under the draft screen (retail's own widget states raise them; one covering the tray Back refuses
    // arena.back): path and whether the element accepts input, so a live run can tie it to the draft phase.
    private static JsonNode ClickBlockers()
    {
        var node = new JsonNode();
        try
        {
            var display = DraftDisplay.Get();
            int index = 0;
            if (display != null)
                foreach (var element in display.GetComponentsInChildren<PegUIElement>(false))
                    if (element != null && element.name.IndexOf("Clickblocker", StringComparison.OrdinalIgnoreCase) >= 0 && index < 8)
                        node["b" + index++] = element.transform.name + " enabled=" + element.IsEnabled() + " path=" + PathOf(element.transform, null, 10);
        }
        catch (Exception error) { node["error"] = error.GetType().Name; }
        return node;
    }

    // The last DRAFT load's wait conditions (NativeArenaLoad.ReadWaits), in the client's order.
    private static JsonNode Waits()
    {
        var node = new JsonNode();
        foreach (var wait in waits) node[wait.Key] = wait.Value;
        return node;
    }

    private static bool IsBackLabel(string label) => label != null
        && (label.Trim().Equals(NativeStrings.Get("GLOBAL_BACK", "Back"), StringComparison.OrdinalIgnoreCase) || label.Trim().Equals(GameStrings.Get("GLUE_BACK"), StringComparison.OrdinalIgnoreCase));

    internal static bool HasAncestorNamed(Transform node, string name)
    {
        for (var at = node; at != null; at = at.parent) if (at.name == name) return true;
        return false;
    }

    internal static void NameControls(Action<PegUIElement, string> name, List<PegUIElement> elements, List<PegUIElementProxy> proxies)
    {
        if (!InDraft) return;
        var display = DraftDisplay.Get();
        var landing = ArenaLandingPageManager.Get();
        var roots = new List<Component> { display, landing };
        foreach (var root in roots.Where(root => root != null))
        {
            elements.AddRange(root.GetComponentsInChildren<PegUIElement>(true));
            proxies.AddRange(root.GetComponentsInChildren<PegUIElementProxy>(true));
        }
        void NameRoot(object value, string id)
        {
            GameObject go = value as GameObject ?? (value as Component)?.gameObject;
            if (go == null) return;
            foreach (var element in go.GetComponentsInChildren<PegUIElement>(true))
                if ((element.GetComponent<Clickable>() != null || element is UIBButton || element is NormalButton || element is PlayButton)
                    && element.name.IndexOf("Clickblocker", StringComparison.OrdinalIgnoreCase) < 0) name(element, id);  // a blocker is never a control
        }
        NameRoot(Field(display, "m_confirmButton"), "draft.confirm");
        NameRoot(Field(display, "m_playButton"), "arena.play");
        NameRoot(Field(display, "m_retireButtonWidget"), "arena.retire");
        NameRoot(Field(display, "m_doneButton"), "arena.back");
        NameRoot(Field(display, "m_doneButtonWidget"), "arena.back");
        NameRoot(Field(display, "m_viewDeckButtonWidget"), "arena.view-deck");
        NameRoot(Field(display, "m_PhoneLargeViewDeckButton"), "arena.view-deck");
        NameRoot(Field(display, "m_PhoneDraftBackButton"), "arena.back");
        NameRoot(Field(display, "m_midrunPhoneBackButton"), "arena.back");
        NameRoot(Field(landing, "m_backButtonWidget"), "arena.back");
        NameRoot(Field(landing, "m_modeToggleButtonWidget"), "arena.underground");
        NameRoot(Field(landing, "m_infoButtonWidget"), "arena.info");
        var page = landing?.GetArenaLandingPage();
        NameRoot(Field(page, "m_playButtonNoActiveRun"), "arena.enter");
        NameRoot(Field(page, "m_playButtonActiveRun"), "arena.continue");
        // Landing page controls that no client field points at: found by their place in the page.
        foreach (var element in elements.Where(element => element != null).Distinct().ToList())
        {
            if (element is UIBButton && HasAncestorNamed(element.transform, "ArenaLandingPage_BottomBar")) name(element, "arena.back");
            else if (element.name == "rewardsChest") name(element, "arena.rewards.preview");
            // The "Run Complete!" chest (ArenaRewardsClaimPopup(Clone)/ArenaRewardChestsClaimable/ArenaRewardChestsClaimable/Clickable): no client field holds it.
            else if (HasAncestorNamed(element.transform, "ArenaRewardChestsClaimable") && HasAncestorNamed(element.transform, "ArenaRewardsClaimPopup(Clone)")
                && !(element is RewardPackage)) name(element, "arena.rewards.chest");
            // The draft screen's tray Back (retail lets a player leave mid-draft; DraftDisplay's navigate-back exits to Home).
            else if ((element is UIBButton || element.GetComponent<Clickable>() != null) && HasAncestorNamed(element.transform, "DraftTrayContainer")
                && HasAncestorNamed(element.transform, "Button_Framed_Simple")) name(element, "arena.back");
        }
        // Ready Up's bottom-right Back (no client field is known to hold it on every layout): while Ready Up is showing, an active
        // button of the draft screen labelled Back is the screen's Back. AssignIds keeps the hittable instance as the bare id.
        if (display != null && Read().Phase == ArenaPhase.Ready)
            foreach (var element in display.GetComponentsInChildren<PegUIElement>(false))
                if (element != null && (element is UIBButton || element.GetComponent<Clickable>() != null)
                    && element.name.IndexOf("Clickblocker", StringComparison.OrdinalIgnoreCase) < 0
                    && IsBackLabel(NativeUiTree.Label(element)))
                    name(element, "arena.back");
        if (Field(display, "m_choices") is IEnumerable choices)
        {
            string kind = Read().Phase == ArenaPhase.ChoosingHero ? "hero" : "card";
            int index = 0;
            foreach (var choice in choices)
            {
                foreach (string field in new[] { "m_actor", "m_subActor" })
                    if (Field(choice, field) is Actor actor && actor.gameObject.activeInHierarchy)
                        name(actor.GetCollider()?.GetComponent<PegUIElement>(), "draft." + kind + ":" + index);
                index++;
            }
        }
        if (RewardBoxesDisplay.Get() is RewardBoxesDisplay boxes)
        {
            elements.AddRange(boxes.GetComponentsInChildren<PegUIElement>(true));
            name(boxes.m_DoneButton, "arena.rewards.claim");
            // Each chest needs a real hit-tested press before the native Done button becomes available.
            int index = 0;
            foreach (var element in boxes.GetComponentsInChildren<PegUIElement>(true))
                if (element is RewardPackage)
                    name(element, "arena.rewards.box:" + index++);
        }
        foreach (var tray in UnityEngine.Object.FindObjectsByType<ArenaTrayDisplay>(FindObjectsSortMode.None))
            name(tray.m_TheKeyMesh?.GetComponent<PegUIElement>(), "arena.rewards.open");
    }
}
#endif
