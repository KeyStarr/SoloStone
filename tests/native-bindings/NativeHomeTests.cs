// Test only the adapter's effects; pinned native signatures are checked in Program.cs.
internal static class NativeHomeTests
{
    private static void NativeHiddenCall(Box box) => NativeHomeUI.ApplyModePolicy(box, null);

    internal static void Run()
    {
        var box = new Box();
        foreach (var button in new[] { box.m_ArenaButton, box.m_BattleGroundsButton })
        {
            button.gameObject.SetActive(false);
            NativeHomeUI.DisableMode(box, button);
            Check(button.gameObject.activeSelf && button.DisabledVisual, "unsupported mode face must be visible and grey");
            Check(!button.IsEnabled(UIEventType.PRESS) && !button.IsEnabled(UIEventType.RELEASE), "unsupported mode must reject clicks");
            Check(button.IsEnabled(UIEventType.ROLLOVER) && button.IsEnabled(UIEventType.ROLLOUT), "disabled modes must keep hover");
            Check(!NativeHomeUI.ShowModeTooltip(box, button), "replace retail tooltip");
            Check(button.Tooltip.Body == "Not done yet! In the backlog", "show backlog tooltip");
            Check(!NativeHomeUI.HideModeTooltip(box, button) && !button.Tooltip.Visible, "hide tooltip on exit");
        }
        var campaignLogs = new List<string>();
        NativeHomeUI.ApplyModePolicy(box, campaignLogs.Add);
        Check(box.m_BattleGroundsButton.GetText() == "Fan-made modes", "disabled Battlegrounds face must read Fan-made modes");
        Check(box.m_ArenaButton.GetText() == "Native mode", "Arena face keeps its native label");
        box.m_BattleGroundsButton.SetText("Battlegrounds"); // Box.SetupButtonText on HUB load
        NativeHomeUI.BeginHub();
        NativeHomeUI.UpdateJournal(box, campaignLogs.Add);
        Check(box.m_BattleGroundsButton.GetText() == "Fan-made modes" && campaignLogs.Any(l => l.Contains("battlegrounds label -> Fan-made modes")),
            "periodic enforcement restores Fan-made modes after a native text reset");
        // One switch: an entry that stops applying (implemented mode) is neither relabelled nor swapped.
        var gated = new Box();
        var campaignSlot = new UnityEngine.Vector3(0, -2, 0);
        var arenaSlotPos = new UnityEngine.Vector3(0, -4, 0);
        gated.m_BattleGroundsButton.transform.localPosition = campaignSlot;
        gated.m_ArenaButton.transform.localPosition = arenaSlotPos;
        gated.m_BattleGroundsButton.SetText("Battlegrounds");
        NativeHomeUI.ApplyModePolicy(gated, null, id => false);
        Check(gated.m_BattleGroundsButton.GetText() == "Battlegrounds" && gated.m_ArenaButton.transform.localPosition == arenaSlotPos,
            "no entry policy: no relabel and no swap");
        NativeHomeUI.ApplyModePolicy(gated, null, id => id == "home.battlegrounds");
        Check(gated.m_BattleGroundsButton.GetText() == "Fan-made modes" && gated.m_ArenaButton.transform.localPosition == arenaSlotPos,
            "Arena enabled: Fan-made modes relabelled but the slots stay");
        NativeHomeUI.ApplyModePolicy(gated, null, id => id == "home.arena");
        Check(gated.m_ArenaButton.transform.localPosition == arenaSlotPos, "Fan-made modes enabled: no swap");
        NativeHomeUI.ApplyModePolicy(gated, null, id => true);
        Check(gated.m_ArenaButton.transform.localPosition == campaignSlot, "both entries active: swap");

        // Owner order Hearthstone / Arena / Fan-made modes: swap the two buttons' slots, keep identity, survive re-layout.
        UnityEngine.Mesh MeshOf(string name) => new UnityEngine.Mesh { name = name };
        var slotBoxA = new Box();
        slotBoxA.m_BattleGroundsButton.transform.localPosition = campaignSlot;
        slotBoxA.m_ArenaButton.transform.localPosition = arenaSlotPos;
        var slotLogs = new List<string>();
        // Plates that are not ready yet: the slots swap, the meshes wait and the retry is announced once.
        NativeHomeUI.ApplyModePolicy(slotBoxA, slotLogs.Add);
        NativeHomeUI.ApplyModePolicy(slotBoxA, slotLogs.Add);
        Check(slotBoxA.m_ArenaButton.transform.localPosition == campaignSlot && slotBoxA.m_BattleGroundsButton.transform.localPosition == arenaSlotPos,
            "Arena takes the upper slot and Fan-made modes the lower one");
        Check(slotLogs.Count(l => l.Contains("slots swapped")) == 1, "log the swap once per Box");
        Check(slotLogs.Count(l => l.Contains("not ready")) == 1 && !slotLogs.Any(l => l.Contains("traded")), "missing mesh filters: wait, no trade");
        var arenaMeshFilter = new UnityEngine.MeshFilter { sharedMesh = MeshOf("plate-third") };
        var campaignMeshFilter = new UnityEngine.MeshFilter { sharedMesh = MeshOf("plate-second") };
        slotBoxA.m_ArenaButton.m_ButtonMeshRenderer = new UnityEngine.Renderer { Extra = arenaMeshFilter };
        slotBoxA.m_BattleGroundsButton.m_ButtonMeshRenderer = new UnityEngine.Renderer { Extra = null }; // one filter still missing
        NativeHomeUI.ApplyModePolicy(slotBoxA, slotLogs.Add);
        Check(arenaMeshFilter.sharedMesh.name == "plate-third", "one missing filter: still no trade");
        slotBoxA.m_BattleGroundsButton.m_ButtonMeshRenderer = new UnityEngine.Renderer { Extra = campaignMeshFilter };
        NativeHomeUI.ApplyModePolicy(slotBoxA, slotLogs.Add);
        Check(arenaMeshFilter.sharedMesh.name == "plate-second" && campaignMeshFilter.sharedMesh.name == "plate-third",
            "plates trade meshes once both exist, so each fits the socket it now sits in");
        NativeHomeUI.ApplyModePolicy(slotBoxA, slotLogs.Add);
        Check(arenaMeshFilter.sharedMesh.name == "plate-second" && slotLogs.Count(l => l.Contains("traded")) == 1,
            "meshes are traded once, not flipped back by repeat calls");
        slotBoxA.m_BattleGroundsButton.transform.localPosition = campaignSlot; // native re-layout restores authored slots
        slotBoxA.m_ArenaButton.transform.localPosition = arenaSlotPos;
        NativeHiddenCall(slotBoxA);
        Check(slotBoxA.m_ArenaButton.transform.localPosition == campaignSlot && slotBoxA.m_BattleGroundsButton.transform.localPosition == arenaSlotPos,
            "swap survives native re-layout");
        Check(arenaMeshFilter.sharedMesh.name == "plate-second", "re-layout does not re-trade meshes");
        slotBoxA.m_BattleGroundsButton.transform.localPosition = campaignSlot;
        slotBoxA.m_ArenaButton.transform.localPosition = arenaSlotPos;
        NativeHomeUI.BeginHub();
        UnityEngine.Time.realtimeSinceStartup = 1000;
        NativeHomeUI.UpdateJournal(slotBoxA, slotLogs.Add);
        Check(slotBoxA.m_ArenaButton.transform.localPosition == campaignSlot, "swap survives native re-layout via the periodic scan");
        Check(slotBoxA.m_BattleGroundsButton.GetText() == "Fan-made modes" && slotBoxA.m_ArenaButton.GetText() == "Native mode",
            "labels stay with their own buttons after the swap");
        // A-B-A: another Box in between must not disturb A's state (no second mesh trade, no position flip).
        var slotBoxB = new Box(); // a reloaded HUB builds a new Box with the authored slots
        slotBoxB.m_BattleGroundsButton.transform.localPosition = campaignSlot;
        slotBoxB.m_ArenaButton.transform.localPosition = arenaSlotPos;
        var bArena = new UnityEngine.MeshFilter { sharedMesh = MeshOf("b-third") };
        var bCampaign = new UnityEngine.MeshFilter { sharedMesh = MeshOf("b-second") };
        slotBoxB.m_ArenaButton.m_ButtonMeshRenderer = new UnityEngine.Renderer { Extra = bArena };
        slotBoxB.m_BattleGroundsButton.m_ButtonMeshRenderer = new UnityEngine.Renderer { Extra = bCampaign };
        NativeHomeUI.ApplyModePolicy(slotBoxB, null);
        Check(slotBoxB.m_ArenaButton.transform.localPosition == campaignSlot && bArena.sharedMesh.name == "b-second",
            "a reloaded Box is swapped from its own current slots");
        NativeHomeUI.ApplyModePolicy(slotBoxA, null);
        NativeHomeUI.ApplyModePolicy(slotBoxB, null);
        NativeHomeUI.ApplyModePolicy(slotBoxA, null);
        Check(slotBoxA.m_ArenaButton.transform.localPosition == campaignSlot && slotBoxA.m_BattleGroundsButton.transform.localPosition == arenaSlotPos
            && arenaMeshFilter.sharedMesh.name == "plate-second" && bArena.sharedMesh.name == "b-second" && bCampaign.sharedMesh.name == "b-third",
            "A-B-A alternation keeps each Box's swap and mesh trade exactly once");
        UnityEngine.Time.realtimeSinceStartup = 0;
        Check(!box.m_PlayButton.DisabledVisual && box.m_PlayButton.IsEnabled(UIEventType.RELEASE), "Play must remain usable");
        Check(NativeHomeUI.ShowModeTooltip(box, box.m_PlayButton), "retain native Play tooltip");
        box.m_EmptyFourthButton.SetActive(true);
        box.m_GameModesButton.gameObject.SetActive(false);
        NativeHomeUI.DisableMode(box, box.m_GameModesButton);
        Check(!box.m_GameModesButton.gameObject.activeSelf && box.m_EmptyFourthButton.activeSelf, "keep native decorative plate");
        // After a match Box.ShowGameModesButton shows the Modes face and hides the plate: restore the boot look.
        box.m_EmptyFourthButton.SetActive(false);
        box.m_GameModesButton.gameObject.SetActive(true);
        NativeHomeUI.DisableMode(box, box.m_GameModesButton);
        Check(!box.m_GameModesButton.gameObject.activeSelf && box.m_EmptyFourthButton.activeSelf,
            "post-match fourth slot matches boot: decorative plate, no Modes face");
        var plate = box.m_EmptyFourthButton;
        box.m_EmptyFourthButton = null;
        box.m_GameModesButton.gameObject.SetActive(false);
        NativeHomeUI.DisableMode(box, box.m_GameModesButton);
        Check(box.m_GameModesButton.gameObject.activeSelf && box.m_GameModesButton.DisabledVisual, "layout without a plate keeps a disabled Modes face");
        box.m_EmptyFourthButton = plate;

        var journalLabel = new UberText { Text = "GLUE_JOURNAL" };
        journalLabel.transform.name = "JournalText";
        journalLabel.transform.parent.name = "DrawerLabels";
        var storeLabel = new UberText { Text = "Store" };
        var closedStoreLabel = new UberText { Text = "GLUE_JOURNAL" };
        box.m_StoreButton.m_storeClosedText = closedStoreLabel;
        var packsLabel = new UberText { Text = "Open Packs" };
        var collectionLabel = new UberText { Text = "My Collection" };
        box.Texts = new[] { journalLabel, storeLabel, closedStoreLabel, packsLabel, collectionLabel };
        box.m_StoreButton.m_storeText = storeLabel;
        var hiddenLabels = new List<string>();
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!journalLabel.gameObject.activeSelf, "hide detached Journal localisation label");
        Check(storeLabel.gameObject.activeSelf && packsLabel.gameObject.activeSelf && collectionLabel.gameObject.activeSelf,
            "keep Store, Packs and Collection labels visible");
        journalLabel.gameObject.SetActive(true); // native refresh
        journalLabel.Text = "Journal"; // localisation resolves key
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!journalLabel.gameObject.activeSelf, "hide Journal again after refresh and localisation");
        Check(hiddenLabels.Count(l => l.StartsWith("NATIVE_MENU journal label hidden ")) == 1
            && hiddenLabels.Any(l => l.Contains("DrawerLabels/JournalText")), "log hidden label path once");
        // Store label remains protected even if a native binding temporarily supplies Journal text.
        storeLabel.Text = "GLUE_JOURNAL";
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(storeLabel.gameObject.activeSelf && closedStoreLabel.gameObject.activeSelf, "never hide Store text meshes");
        var blankLabel = new UberText { Text = "" };
        box.Texts = new[] { journalLabel, blankLabel };
        GameStrings.JournalText = ""; // missing localisation key
        journalLabel.gameObject.SetActive(true);
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!journalLabel.gameObject.activeSelf && blankLabel.gameObject.activeSelf, "missing key must not hide unrelated blank labels");
        GameStrings.JournalText = "[xx] Journal";
        journalLabel.Text = "[xx] Journal";
        journalLabel.gameObject.SetActive(true);
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!journalLabel.gameObject.activeSelf, "hide localised Journal label");
        GameStrings.JournalText = "Journal";
        box.Texts = Array.Empty<UberText>();
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);

        // Journal templates can live outside Box and have late-bound non-Uber text.
        var spawned = new Hearthstone.UI.Widget();
        spawned.transform.name = "JournalTemplate";
        var widget = new Hearthstone.UI.WidgetInstance { Widget = spawned };
        widget.transform.name = "JournalWidget";
        box.m_journalButtonWidget = widget;
        var slotLabel = new TMPro.TMP_Text { text = "Unresolved binding" };
        slotLabel.transform.name = "ButtonName";
        widget.transform.parent.Children.Add(slotLabel.transform);
        slotLabel.transform.parent = widget.transform.parent;
        var lateText = new UnityEngine.TextMesh { text = "GLOBAL_JOURNAL" };
        lateText.transform.name = "LateLabel";
        var keepPacks = new TMPro.TMP_Text { text = "Open Packs" };
        box.Components = new UnityEngine.Component[] { lateText, keepPacks };
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(widget.Hidden && spawned.Hidden && !spawned.gameObject.activeSelf, "hide independent widget template through native Hide and its root");
        Check(!slotLabel.gameObject.activeSelf, "hide widget sibling label regardless of component type or binding value");
        Check(!lateText.gameObject.activeSelf && keepPacks.gameObject.activeSelf, "hide TextMesh Journal alias while keeping packs");
        spawned.gameObject.SetActive(true);
        lateText.gameObject.SetActive(true);
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!spawned.gameObject.activeSelf && !lateText.gameObject.activeSelf, "native refresh must not restore Journal template or label");
        var tmpLabel = new TMPro.TMP_Text { text = "GLUE_QUEST_LOG" };
        box.Components = new UnityEngine.Component[] { tmpLabel, keepPacks };
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(!tmpLabel.gameObject.activeSelf && keepPacks.gameObject.activeSelf, "hide TMP Journal alias");
        // A Journal-named parent can still be a shared bar. Protect retained controls and their label.
        var shared = widget.transform.parent;
        shared.name = "JournalHubButtons";
        box.m_StoreButton.transform.parent = shared;
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(shared.gameObject.activeSelf, "do not hide a Journal-named shared drawer");
        slotLabel.gameObject.SetActive(true);
        NativeHomeUI.HideJournalLabels(box, hiddenLabels.Add);
        Check(slotLabel.gameObject.activeSelf, "protect ButtonName when shared with Store");
        box.m_StoreButton.transform.parent = new UnityEngine.Transform();
        var logs = new List<string>();
        var diagnosticsBox = new Box();
        diagnosticsBox.Components = Enumerable.Range(0, 45).Select(i => {
            var text = new TMPro.TMP_Text { text = "Drawer " + i + "\nsecond line" };
            text.transform.name = "Label" + i;
            return (UnityEngine.Component)text;
        }).ToArray();
        NativeHomeUI.BeginHub();
        UnityEngine.Time.realtimeSinceStartup = 3;
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        Check(!logs.Any(l => l.StartsWith("NATIVE_MENU text ")), "no per-label text dump");
        Check(logs.Count(l => l.Contains("journal labels=0") && l.Contains("activeTexts=45")) == 1, "log a Journal miss explicitly in one summary line");
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        UnityEngine.Time.realtimeSinceStartup = 6;
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        Check(logs.Count(l => l.Contains("NATIVE_MENU journal labels=")) == 1, "summarise only once per HUB load");
        NativeHomeUI.BeginHub();
        UnityEngine.Time.realtimeSinceStartup = 9;
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        Check(logs.Count(l => l.Contains("NATIVE_MENU journal labels=")) == 2, "summarise again on next HUB load even with same Box");
        var lateBound = new TMPro.TMP_Text { text = "" };
        diagnosticsBox.Components = new UnityEngine.Component[] { lateBound };
        UnityEngine.Time.realtimeSinceStartup = 10;
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        Check(lateBound.gameObject.activeSelf, "keep unresolved unrelated text");
        lateBound.text = "Journal";
        UnityEngine.Time.realtimeSinceStartup = 11;
        NativeHomeUI.UpdateJournal(diagnosticsBox, logs.Add);
        Check(!lateBound.gameObject.activeSelf, "catch localization after initial HUB dump");
        box.Components = Array.Empty<UnityEngine.Component>();

        var store = box.m_StoreButton.transform.parent;
        var packs = box.m_OpenPacksButton.transform.parent;
        var collection = box.m_CollectionButton.transform.parent;
        store.position = new UnityEngine.Vector3(-20, 2, 3);
        packs.position = new UnityEngine.Vector3(4, 2, 3);
        collection.position = new UnityEngine.Vector3(8, 2, 3);
        NativeHomeUI.RefreshDrawer(box, 7);
        Check(store.position.x == 0 && store.position.y == 2 && store.position.z == 3, "move whole Store group to adjacent native spacing");
        Check(packs.position.x == 4 && collection.position.x == 8, "keep native Packs and Collection anchors");
        Check(box.m_OpenPacksButton.Count == 7 && box.m_OpenPacksButton.BadgeVisible, "show unopened pack total");
        NativeHomeUI.RefreshDrawer(box, 8); // purchase
        Check(box.m_OpenPacksButton.Count == 8 && store.position.x == 0, "refresh after purchase without layout drift");
        NativeHomeUI.RefreshDrawer(box, 0); // last pack opened
        Check(box.m_OpenPacksButton.Count == 0 && !box.m_OpenPacksButton.BadgeVisible, "clear badge after last pack");
        var centred = new List<string>();
        box.m_Drawer = new BoxDrawer { Mesh = new UnityEngine.Renderer {
            bounds = new UnityEngine.Bounds { center = new UnityEngine.Vector3(-6, 0, 0), size = new UnityEngine.Vector3(30, 1, 1) } } };
        NativeHomeUI.RefreshDrawer(box, 4, centred.Add);
        Check(store.position.x == -10 && packs.position.x == -6 && collection.position.x == -2 && store.position.y == 2,
            "centre the trio on the drawer mesh with native spacing");
        Check(centred.Count(l => l.StartsWith("NATIVE_MENU drawer centred")) == 1, "log the centring once");
        UnityEngine.Time.realtimeSinceStartup = 20;
        NativeHomeUI.UpdateJournal(box, centred.Add);
        NativeHomeUI.RefreshDrawer(box, 4, centred.Add);
        Check(centred.Count(l => l.StartsWith("NATIVE_MENU drawer centred")) == 1 && packs.position.x == -6 && store.position.x == -10, "re-centring is idempotent");
        store.position = new UnityEngine.Vector3(-20, 2, 3); // native re-layout restores prefab slots
        packs.position = new UnityEngine.Vector3(4, 2, 3);
        collection.position = new UnityEngine.Vector3(8, 2, 3);
        UnityEngine.Time.realtimeSinceStartup = 21;
        NativeHomeUI.UpdateJournal(box, centred.Add);
        Check(store.position.x == -10 && packs.position.x == -6 && collection.position.x == -2, "re-apply after native re-layout");
        box.m_Drawer.Mesh.bounds = new UnityEngine.Bounds(); // inactive drawer reports empty bounds
        packs.position = new UnityEngine.Vector3(4, 2, 3);
        collection.position = new UnityEngine.Vector3(8, 2, 3);
        NativeHomeUI.RefreshDrawer(box, 4);
        Check(store.position.x == 0 && packs.position.x == 4, "keep adjacent layout without drawer bounds");
        // Some native variants share a drawer parent; never move the whole drawer.
        box.m_StoreButton.transform.parent = packs;
        NativeHomeUI.RefreshDrawer(box, 3);
        Check(packs.position.x == 4, "do not move shared drawer parent");
        // Feats: the drawer Journal slot stays visible, says "Feats" and joins the row at native spacing.
        NativeAchievements.HomeJournalReady = true;
        var feats = new Box();
        var featsLabel = new UberText { Text = "Journal" };
        var featsStore = new UberText { Text = "Store" };
        feats.Texts = new[] { featsLabel, featsStore };
        feats.m_StoreButton.m_storeText = featsStore;
        var featsTemplate = new Hearthstone.UI.WidgetTemplate { IsDesiredHidden = true };
        var featsWidget = new Hearthstone.UI.WidgetInstance { Widget = featsTemplate };
        feats.m_journalButtonWidget = featsWidget;
        // Live hierarchy: BoxButtonParent/{Box_Button_Store, Box_Button_Journal/<widget nest>, ...}.
        var drawerParent = new UnityEngine.Transform();
        var journalSlot = new UnityEngine.Transform { name = "Box_Button_Journal", parent = drawerParent };
        drawerParent.Children.Add(journalSlot);
        foreach (var slot in new[] { feats.m_StoreButton.transform.parent, feats.m_OpenPacksButton.transform.parent, feats.m_CollectionButton.transform.parent })
            slot.parent = drawerParent;
        NativeHomeUI.HideJournalLabels(feats, null);
        Check(featsLabel.gameObject.activeSelf && featsLabel.Text == "Feats" && featsStore.Text == "Store" && !featsWidget.Hidden,
            "healthy Feats keeps the Journal slot visible and labels it Feats");
        try
        {
            GameStrings.Translations["GLUE_PROGRESSION_ACHIEVEMENTS_TITLE"] = "[xx] Feats";
            featsLabel.Text = "Journal";
            NativeHomeUI.HideJournalLabels(feats, null);
            Check(featsLabel.Text == "[xx] Feats", "Journal label uses localized Feats when strings become ready");
        }
        finally { GameStrings.Translations.Clear(); }
        Check(!featsTemplate.IsDesiredHidden && featsWidget.Events.Contains("ENABLE_INTERACTION"),
            "a natively hidden Journal widget is shown and enabled while the drawer is up");
        var fStore = feats.m_StoreButton.transform.parent; var fJournal = journalSlot;
        var fPacks = feats.m_OpenPacksButton.transform.parent; var fCollection = feats.m_CollectionButton.transform.parent;
        fStore.position = new UnityEngine.Vector3(-20, 2, 3); fJournal.position = new UnityEngine.Vector3(-30, 2, 3);
        fPacks.position = new UnityEngine.Vector3(4, 2, 3); fCollection.position = new UnityEngine.Vector3(8, 2, 3);
        feats.m_Drawer = new BoxDrawer { Mesh = new UnityEngine.Renderer {
            bounds = new UnityEngine.Bounds { center = new UnityEngine.Vector3(-6, 0, 0), size = new UnityEngine.Vector3(30, 1, 1) } } };
        NativeHomeUI.RefreshDrawer(feats, 0);
        Check(fStore.position.x == -12 && fJournal.position.x == -8 && fPacks.position.x == -4 && fCollection.position.x == 0
            && fJournal.position.y == 2, "four drawer slots (Store, Feats, Packs, Collection) centre at native spacing");
        var iconOffset = feats.m_StoreButton.transform.position - fStore.position;
        Check(featsWidget.transform.position == fJournal.position + iconOffset
            && featsWidget.transform.localScale == UnityEngine.Vector3.one * NativeHomeUI.FeatsIconScale,
            "the parked Journal widget sits in its slot at the Store icon offset and Feats icon scale");
        NativeHomeUI.RefreshDrawer(feats, 0);
        Check(fStore.position.x == -12 && fJournal.position.x == -8, "four-slot centring is idempotent");
        NativeAchievements.HomeJournalReady = false;
        Console.WriteLine("PASS native home disabled modes, tooltip, decorative plate, drawer labels, spacing and pack badge adapter");
    }
    private static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
}

internal sealed class BoxDrawer : UnityEngine.Component
{
    public UnityEngine.Renderer Mesh;
    public UnityEngine.Renderer GetDrawerMesh() => Mesh;
}
internal enum UIEventType { PRESS, RELEASE, ROLLOVER, ROLLOUT }
// Feats needs the Unity client; this double carries the drawer flag and label NativeHomeUI reads.
internal static class NativeAchievements
{
    public static bool HomeJournalReady;
    public static string ButtonLabel => NativeStrings.Get("GLUE_PROGRESSION_ACHIEVEMENTS_TITLE", "Feats");
}
internal class PegUIElement : UnityEngine.Component
{
    private readonly HashSet<UIEventType> disabled = new();
    public readonly TooltipZone Tooltip = new();
    public bool DisabledVisual;
    public void SetEnabled(UIEventType type, bool enabled) { if (enabled) disabled.Remove(type); else disabled.Add(type); }
    public bool IsEnabled(UIEventType type) => !disabled.Contains(type);
    public new T GetComponent<T>() where T : class => Tooltip as T;
}
internal class BoxMenuButton : PegUIElement { public UnityEngine.Renderer m_ButtonMeshRenderer; public UnityEngine.Component m_TextMesh = null; public string Text = "Native mode"; public string GetText() => Text; public void SetText(string text) => Text = text; }
internal sealed class StoreButton : PegUIElement { public UberText m_storeText, m_storeClosedText; }
internal sealed class UberText : UnityEngine.Component { public string Text { get; set; } }
internal static class GameStrings
{
    public static string JournalText = "Journal";
    internal static readonly Dictionary<string, string> Translations = new();
    public static string Get(string key) => Translations.TryGetValue(key, out string value) ? value : key == "GLUE_JOURNAL" ? JournalText : key;
}
internal sealed class PackOpeningButton : BoxMenuButton
{
    public int Count;
    public bool BadgeVisible;
    public void SetPackCount(int count) { Count = count; BadgeVisible = count > 0; }
}
internal sealed class TooltipZone
{
    public string Body;
    public bool Visible;
    public bool IsShowingTooltip() => Visible;
    public void ShowBoxTooltip(string headline, string body, int index = 0) { Body = body; Visible = true; }
    public void HideTooltip() => Visible = false;
}
internal sealed class Box : UnityEngine.Component
{
    public BoxMenuButton m_PlayButton = new(), m_ArenaButton = new(), m_BattleGroundsButton = new(), m_GameModesButton = new(),
        m_CollectionButton = new();
    public StoreButton m_StoreButton = new();
    public PackOpeningButton m_OpenPacksButton = new();
    public UnityEngine.GameObject m_EmptyFourthButton = new();
    public UberText[] Texts = Array.Empty<UberText>();
    public BoxDrawer m_Drawer = null;
    public PegUIElement m_QuestLogButton = new();
    public Hearthstone.UI.Widget m_journalButtonWidget;
    public UnityEngine.Component JournalButton = null;
    public UnityEngine.Component GetOldJournalButton() => JournalButton;
    public override UnityEngine.Component[] GetComponentsInChildren(Type type, bool includeInactive) =>
        Texts.Where(t => type.IsAssignableFrom(t.GetType())).Cast<UnityEngine.Component>().Concat(base.GetComponentsInChildren(type, includeInactive)).ToArray();
    public void SetBoxButtonInteractions(bool enabled, BoxMenuButton button, bool updateText = false) => button.DisabledVisual = !enabled;
    public bool IsInStateWithButtons() => true;
}
namespace UnityEngine
{
    internal struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector3 operator /(Vector3 a, float k) => a * (1 / k);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x * k, a.y * k, a.z * k);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => 0;
    }
    internal sealed class GameObject
    {
        public bool activeSelf = true;
        public Transform transform;
        public bool activeInHierarchy => activeSelf && (transform?.parent == null || transform.parent.gameObject.activeInHierarchy);
        public void SetActive(bool active) => activeSelf = active;
    }
    internal class Component
    {
        public Transform transform = new();
        public GameObject gameObject => transform.gameObject;
        public Component[] Components = Array.Empty<Component>();
        public object Extra;
        public T GetComponent<T>() where T : class => Extra as T;
        public virtual Component[] GetComponentsInChildren(Type type, bool includeInactive) =>
            Components.Where(c => type.IsAssignableFrom(c.GetType()) && (includeInactive || c.gameObject.activeInHierarchy)).ToArray();
    }
    internal sealed class Transform
    {
        public string name = "";
        public Transform parent;
        public readonly GameObject gameObject;
        public Vector3 position, localPosition, localScale, localEulerAngles;
        public Vector3 lossyScale = Vector3.one;
        public readonly List<Transform> Children = new();
        public Transform() : this(true) { }
        private Transform(bool makeParent)
        {
            gameObject = new GameObject { transform = this };
            if (makeParent) parent = new Transform(false);
        }
        public Transform Find(string name) => Children.FirstOrDefault(c => c.name == name);
        public Vector3 InverseTransformVector(Vector3 value) => new Vector3(
            value.x / lossyScale.x, value.y / lossyScale.y, value.z / lossyScale.z);
        public IEnumerator<Transform> GetEnumerator() => Children.GetEnumerator();
        public bool IsChildOf(Transform other) => this == other || parent != null && parent.IsChildOf(other);
    }
    internal static class Time { public static float realtimeSinceStartup; }
    internal static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Round(float value) => MathF.Round(value);
    }
    internal sealed class Mesh { public string name; }
    internal sealed class MeshFilter { public Mesh sharedMesh; }
    internal struct Bounds { public Vector3 center, size; }
    internal sealed class Renderer : Component { public Bounds bounds; }
    internal sealed class TextMesh : Component { public string text { get; set; } }
}
namespace TMPro
{
    internal class TMP_Text : UnityEngine.Component { public string text { get; set; } }
}
namespace Hearthstone.UI
{
    internal class Widget : UnityEngine.Component
    {
        public bool IsInitialized = true;
        public bool Hidden;
        public readonly List<string> Events = new();
        public virtual void Hide() => Hidden = true;
        public virtual void Show() => Hidden = false;
        public void TriggerEvent(string name) => Events.Add(name);
    }
    internal sealed class WidgetTemplate : Widget
    {
        public bool IsDesiredHidden;
        public override void Show() { base.Show(); IsDesiredHidden = false; }
        public override void Hide() { base.Hide(); IsDesiredHidden = true; }
    }
    internal sealed class WidgetInstance : Widget
    {
        public Widget Widget { get; set; }
        public override void Hide() { base.Hide(); Widget?.Hide(); }
        public override void Show() { base.Show(); Widget?.Show(); }
    }
}
