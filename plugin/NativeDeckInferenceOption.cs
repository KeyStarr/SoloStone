using System;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MiniJSON;
using UnityEngine;

// Options > SoloStone Options > "NPC Deck Guess (Experimental)" for the experimental fair deck inference (#63; NativeOptionsPanels
// builds the row and hands it to Attach). On, each world encounter begun afterwards searches with BrainOptions.ReplyPrior = Inferred
// (NpcWorldRuntime.Begin); a running match keeps what it began with. Node budgets and clocks do not change, and the inference reads
// public plays only, never a saved decklist. Every match logs NPC_REPLY_PRIOR and its journal header carries encounter.replyPrior, so a
// report says which mode ran.
internal static class NativeDeckInferenceOption
{
    private static DeckInferenceSettings settings;
    private static ManualLogSource log;
    private static long loggedReads;
    internal static CheckBox Checkbox { get; private set; }
    internal static CheckBox Shown => NativeOptionsPanels.OnScreen(Checkbox) ? Checkbox : null;
    internal static bool Enabled => settings?.Enabled == true;
    internal static bool Checked => Checkbox != null && Checkbox.IsChecked();
    // What the active encounter's NPC searches with: "inferred" or "class" (also a legacy opponent, which has no encounter).
    internal static string ThisMatch => NpcWorldRuntime.Active?.Profile?.Budget.Brain?.ReplyPrior == ReplyPrior.Inferred ? "inferred" : "class";

    internal static void Initialize(ManualLogSource logger, ConfigFile file)
    {
        settings = new DeckInferenceSettings(file); log = logger;
        NpcWorldRuntime.InferredReplyPrior = () => Enabled;
    }

    // The panel's row, built once; it stays for the session.
    internal static void Attach(CheckBox box)
    {
        if (settings == null) throw new InvalidOperationException("NPC Deck Guess not initialized");
        box.AddEventListener(UIEventType.RELEASE, _ => {
            try { settings.Set(box.IsChecked()); log.LogInfo("NPC_DECK_INFERENCE_OPTION enabled=" + Enabled + " applies=next-match"); }
            catch (Exception error) { log.LogWarning("NPC_DECK_INFERENCE_OPTION not saved: " + error.Message); }
            box.SetChecked(Enabled);
        });
        Checkbox = box;
        box.SetChecked(Enabled);
    }

    internal static void Sync() { try { if (Checkbox != null) Checkbox.SetChecked(Enabled); } catch (Exception error) { log?.LogWarning("NPC_DECK_INFERENCE_OPTION sync: " + error.Message); } }

    // Called from OfflineProbe.Update, once per frame: logs each new read on the main thread.
    internal static void Tick()
    {
        long reads = DeckInference.Reads;
        if (reads != loggedReads) { loggedReads = reads; log?.LogInfo("NPC_DECK_READ " + DeckInference.LastRead); }
    }

#if !SOLOSTONE_RELEASE
    // Test control (state.deckInference): the setting, what this match runs, the inference's own counters (`queries` moves only
    // while an inferred search asks for its table) and the checkbox in its panel (NativeOptionsPanels.RowState).
    internal static JsonNode State() => new JsonNode {
        { "enabledNextMatch", Enabled }, { "thisMatch", ThisMatch }, { "queries", DeckInference.Queries }, { "reads", DeckInference.Reads },
        { "lastRead", DeckInference.LastRead }, { "checkbox", NativeOptionsPanels.RowState(Checkbox) }
    };
#endif
}
