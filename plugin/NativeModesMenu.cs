using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Hearthstone.DataModels;
using Hearthstone.UI;

// The home screen's Modes button opens the client's own Modes screen (scene GAME_MODE, GameModeDisplay) with SoloStone's
// modes as its entries instead of the retail ones (Arena, Duels, Mercenaries, ...). Each mode adds one entry; an entry
// borrows the look of the retail row with the same button state (its art and frame) and brings its own name, description
// and start. The screen shows while at least one entry is available.
internal static class NativeModesMenu
{
    internal sealed class Entry
    {
        internal readonly string ButtonState, Name, Description;
        internal readonly Func<bool> Available;
        internal readonly Action Open;
        internal Entry(string buttonState, string name, string description, Func<bool> available, Action open)
        { ButtonState = buttonState; Name = name; Description = description; Available = available; Open = open; }
    }

    private static ManualLogSource log;
    private static readonly List<Entry> entries = new List<Entry>();

    // One entry per button state: the client keys a button's art, and the record behind it, by its state.
    internal static void Add(Entry entry)
    {
        entries.RemoveAll(other => other.ButtonState == entry.ButtonState);
        entries.Add(entry);
    }

    internal static IReadOnlyList<Entry> Shown => entries.Where(entry => entry.Available()).ToList();
    internal static bool Enabled => entries.Any(entry => entry.Available());

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        NativeHomeUI.ModesAvailable = () => Enabled;
        harmony.Patch(AccessTools.Method(typeof(Box), "OnModesButtonPressed"), prefix: new HarmonyMethod(typeof(NativeModesMenu), nameof(ModesPressed)));
        harmony.Patch(AccessTools.Method(typeof(GameModeDisplay), "InitializeGameModeSceneData"),
            postfix: new HarmonyMethod(typeof(NativeModesMenu), nameof(SceneDataInitialized)));
        harmony.Patch(AccessTools.Method(typeof(GameModeDisplay), "CanEnterMode"), prefix: new HarmonyMethod(typeof(NativeModesMenu), nameof(CanEnter)));
        harmony.Patch(AccessTools.Method(typeof(GameModeDisplay), "NavigateToSelectedMode"), prefix: new HarmonyMethod(typeof(NativeModesMenu), nameof(Choose)));
        harmony.Patch(AccessTools.Method(typeof(GameModeDisplay), "GoToHub"), prefix: new HarmonyMethod(typeof(NativeModesMenu), nameof(Home)));
        harmony.Patch(AccessTools.Method(typeof(GameModeDisplay), "InitializeSlidingTray"), postfix: new HarmonyMethod(typeof(NativeModesMenu), nameof(ShowTray)));
    }

    // The box's fourth slot shows the Modes face, pressable, instead of the empty plate (home screen setup, while enabled).
    internal static void ShowModesButton(Box box)
    {
        if (!Enabled || box == null || box.m_GameModesButton == null) return;
        AccessTools.Method(typeof(Box), "ShowGameModesButton").Invoke(box, null);
        BoxMenuButton button = box.m_GameModesButton;
        box.SetBoxButtonInteractions(true, button, true);
        button.SetEnabled(UIEventType.PRESS, true);
        button.SetEnabled(UIEventType.RELEASE, true);
        button.SetEnabled(UIEventType.ROLLOVER, true);
        button.SetEnabled(UIEventType.ROLLOUT, true);
    }

    // The client's Modes handler asks for a Battle.net login first. While the menu is enabled the press goes straight to the
    // hub's button route (NativeUIProbe.OnLocalHubButton), which opens Open().
    private static bool ModesPressed(Box __instance)
    {
        if (!Enabled) return true;
        __instance.FireButtonPressEvent(Box.ButtonType.GAME_MODES, false);
        return false;
    }

    internal static void Open()
    {
        log?.LogInfo("MODES open: " + string.Join(", ", Shown.Select(entry => entry.Name)));
        NativeUIProbe.Navigate(SceneMgr.Mode.GAME_MODE);
    }

    // The client lists the retail rows whose event is active. The screen lists SoloStone's entries instead, each on the
    // record of its button state (the record id is what the screen selects and remembers).
    private static void SceneDataInitialized(GameModeDisplay __instance)
    {
        if (!Enabled) return;
        try
        {
            var records = (List<GameModeDbfRecord>)AccessTools.Field(typeof(GameModeDisplay), "m_activeGameModeRecords").GetValue(__instance);
            GameModeSceneDataModel scene = __instance.GetGameModeSceneDataModel();
            if (records == null || scene == null) return;
            records.Clear();
            var buttons = new DataModelList<GameModeButtonDataModel>();
            foreach (Entry entry in Shown)
            {
                GameModeDbfRecord record = GameDbf.GameMode?.GetRecords()?.FirstOrDefault(row => row.GameModeButtonState == entry.ButtonState);
                if (record == null) { log?.LogWarning("MODES no client row for button state " + entry.ButtonState + ": " + entry.Name + " not shown"); continue; }
                records.Add(record);
                buttons.Add(new GameModeButtonDataModel {
                    GameModeRecordId = record.ID, Name = entry.Name, Description = entry.Description, ButtonState = entry.ButtonState,
                    IsNew = false, IsEarlyAccess = false, IsBeta = false, IsDownloadRequired = false, IsDownloading = false, ModuleTag = "",
                });
            }
            scene.GameModeButtons = buttons;
            if (!records.Any(record => record.ID == scene.LastSelectedGameModeRecordId))
                scene.LastSelectedGameModeRecordId = records.FirstOrDefault()?.ID ?? 0;
            log?.LogInfo("MODES screen lists " + string.Join(", ", buttons.Select(button => button.Name + "#" + button.GameModeRecordId)));
        }
        catch (Exception error) { log?.LogError("MODES screen entries not set: " + error); }
    }

    // The screen slides its tray in only when it opens from the home screen; from anywhere else (Back from Dungeon Run) the
    // retail scene that was left runs a scene-driven transition, which SoloStone's navigation does not. The tray shows from
    // every scene, as it does from home.
    private static void ShowTray(GameModeDisplay __instance)
    {
        if (!Enabled || !SceneMgr.IsInitialized() || SceneMgr.Get().GetPrevMode() == SceneMgr.Mode.HUB) return;
        __instance.m_slidingTray?.ToggleTraySlider(true, null, false);
    }

    private static Entry Selected(GameModeDisplay display)
    {
        var selected = AccessTools.Field(typeof(GameModeDisplay), "m_selectedGameModeButtonDataModel").GetValue(display) as GameModeButtonDataModel;
        if (selected == null) return null;
        return Shown.FirstOrDefault(entry => entry.ButtonState == selected.ButtonState);
    }

    // The client's lock checks (server feature flags, Tavern Brawl season, downloads) do not apply to SoloStone's entries.
    private static bool CanEnter(GameModeDisplay __instance, ref string __0, ref GameModeDisplay.ModeLockedStatus __1, ref bool __result)
    {
        if (!Enabled || Selected(__instance) == null) return true;
        __0 = "";
        __1 = GameModeDisplay.ModeLockedStatus.Unlocked;
        __result = true;
        return false;
    }

    private static bool Choose(GameModeDisplay __instance)
    {
        if (!Enabled) return true;
        Entry entry = Selected(__instance);
        if (entry == null) { log?.LogWarning("MODES play pressed with no SoloStone entry selected"); return false; }
        log?.LogInfo("MODES choose " + entry.Name);
        try { entry.Open(); }
        catch (Exception error) { log?.LogError("MODES " + entry.Name + " did not open: " + error); }
        return false;
    }

#if !SOLOSTONE_RELEASE
    // Test control: the screen's entries are Widget Clickables, whose PegUIElements (DontSave) a global search misses. Ids:
    // modes.<button state> (modes.adventure, ...), modes.play, modes.back.
    internal static IEnumerable<KeyValuePair<PegUIElement, string>> Controls()
    {
        GameModeDisplay display = UnityEngine.Object.FindFirstObjectByType<GameModeDisplay>();
        if (!Enabled || display == null) yield break;
        foreach (string field in new[] { "m_playButton", "m_backButton" })
            if (AccessTools.Field(typeof(GameModeDisplay), field)?.GetValue(display) is PegUIElement button && button != null)
                yield return new KeyValuePair<PegUIElement, string>(button, field == "m_playButton" ? "modes.play" : "modes.back");
        var model = AccessTools.Method(typeof(GameModeButton), "GetGameModeButtonDataModel");
        foreach (GameModeButton button in display.GetComponentsInChildren<GameModeButton>(true))
        {
            string state = (model?.Invoke(button, null) as GameModeButtonDataModel)?.ButtonState;
            if (string.IsNullOrEmpty(state)) continue;
            foreach (PegUIElement element in button.GetComponentsInChildren<PegUIElement>(true))
                yield return new KeyValuePair<PegUIElement, string>(element, "modes." + state.ToLowerInvariant());
        }
    }
#endif

    // Back (and Escape) go home through SoloStone's navigation, which owns every scene change offline.
    private static bool Home()
    {
        if (!Enabled) return true;
        NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
        return false;
    }
}
