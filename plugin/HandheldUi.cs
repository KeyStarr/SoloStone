using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

// "Handheld UI" (owner 2026-10-28): the Steam Deck layout work (mulligan row, larger native menu text) behind one checkbox in
// Options > SoloStone Options (NativeOptionsPanels builds the row and hands it to Attach). Default ON when running as the Deck host,
// OFF elsewhere; persisted in the BepInEx config file ([Handheld UI] Enabled). Toggling takes effect immediately: the layout restores
// or applies on the next frame.
internal static class HandheldUi
{
    private static ManualLogSource log;
    private static ConfigFile config;
    private static ConfigEntry<bool> enabled;
    private static CheckBox checkbox;
    private static bool failed;
    internal static bool Enabled => !failed && enabled != null && enabled.Value;
    internal const string Label = "Handheld UI";
    // The panel's checkbox; null when unavailable.
    internal static CheckBox Checkbox => checkbox;

    internal static void Initialize(Harmony harmony, ManualLogSource logger, ConfigFile file, OfflineLaunchContext launch)
    {
        log = logger;
        config = file;
        // The default only applies when the key is absent, so a player's own choice survives every restart.
        enabled = file.Bind("Handheld UI", "Enabled", launch.Deck,
            "Steam Deck layout: larger mulligan cards and menu text. Default on for the Steam Deck host, off elsewhere. Also in Options > SoloStone Options.");
        DeckLayout.Initialize(harmony, logger);
    }

    internal static void Tick()
    {
        if (failed) return;
        DeckLayout.Tick(Enabled);
        DeckLayout.TickMenus(Enabled);
    }

    internal static void Fail(Exception error)
    {
        failed = true;
        try { DeckLayout.Restore(); DeckLayout.RestoreMenus(); } catch { }
        log?.LogWarning("HANDHELD_UI disabled for the session: " + error.Message);
    }

    internal static void Shutdown()
    {
        DeckLayout.Restore();
        DeckLayout.RestoreMenus();
        if (checkbox != null) UnityEngine.Object.Destroy(checkbox.gameObject);
        checkbox = null;
    }

    internal static void Set(bool value)
    {
        if (enabled == null || enabled.Value == value) return;
        // Save explicitly so a read-only config shows in the log instead of throwing out of the ConfigEntry setter.
        bool automatic = config.SaveOnConfigSet;
        try { config.SaveOnConfigSet = false; enabled.Value = value; }
        finally { config.SaveOnConfigSet = automatic; }
        try { config.Save(); log.LogInfo("HANDHELD_UI " + (value ? "on" : "off")); }
        catch (Exception error) { log.LogWarning("HANDHELD_UI setting NOT SAVED: " + error.Message); }
        if (!value) { DeckLayout.Restore(); DeckLayout.RestoreMenus(); }
    }

    // The panel's row, built once; it stays for the session. Toggling applies at once.
    internal static void Attach(CheckBox box)
    {
        if (enabled == null) throw new InvalidOperationException("Handheld UI not initialized");
        box.SetChecked(Enabled);
        box.AddEventListener(UIEventType.RELEASE, _ => Set(box.IsChecked()));
        checkbox = box;
    }

    internal static void Sync()
    {
        try { if (checkbox != null) checkbox.SetChecked(Enabled); }
        catch (Exception error) { log?.LogWarning("HANDHELD_UI checkbox sync: " + error.Message); }
    }
}
