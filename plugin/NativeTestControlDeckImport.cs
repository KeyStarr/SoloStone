#if !SOLOSTONE_RELEASE
using System;
using HarmonyLib;
using MiniJSON;
using UnityEngine;

// Test control for the deck-code import live check (#140). clipboard-set puts text on the client's clipboard (what the
// native prompt reads); collection-paste-check runs the Collection's own paste entry ("prompt": the clipboard prompt,
// "paste": the paste button) or answers the prompt that entry showed. The import itself is the player's path, unchanged.
internal sealed partial class NativeTestControl
{
    private void HandleDeckImportTest(ControlEnvelope envelope)
    {
        AssertMainThread();
        var request = envelope.Request;
        var operation = ledger.Accept(request, out bool added);
        if (!added) { OperationReply(envelope.Connection, request, operation); return; }
        if (fault != null) Reject(operation, "Mutations frozen: " + fault);
        else if (ControlBusy().Count != 0) Reject(operation, "Another mutation unresolved");
        else if ((DateTime.UtcNow - request.ReceivedUtc).TotalSeconds > 10) Reject(operation, "Expired before execution");
        else if (!Enabled(out string enabledToken) || token != enabledToken)
        { Reject(operation, "Control flag changed"); Stop("Control flag changed"); }
        else
        {
            try
            {
                if (request.Command == "clipboard-set")
                {
                    GUIUtility.systemCopyBuffer = request.Text("text", 4096);
                    operation.Detail["clipboardLength"] = request.Text("text", 4096).Length;
                    operation.Detail["inputKind"] = "clipboard";
                }
                else
                {
                    var profile = LocalLadder.Profile ?? throw new InvalidOperationException("Local profile unavailable.");
                    if (LocalLadder.Screen == "Playing" || profile.HasPendingMatch) throw new InvalidOperationException("Finish the match first.");
                    var display = SceneMgr.Get()?.GetMode() == SceneMgr.Mode.COLLECTIONMANAGER
                        ? CollectionManager.Get()?.GetCollectibleDisplay() as CollectionManagerDisplay : null;
                    if (display == null) throw new InvalidOperationException("Open the Collection first.");
                    if (request.Has("answer"))
                    {
                        var popup = AccessTools.Field(typeof(DialogManager), "m_currentDialog")?.GetValue(DialogManager.Get()) as AlertPopup
                            ?? throw new InvalidOperationException("No prompt is shown.");
                        var button = request.Text("answer") == "yes" ? popup.m_confirmButton : popup.m_cancelButton;
                        if (button == null || !button.gameObject.activeInHierarchy)
                            throw new InvalidOperationException("The prompt's " + request.Text("answer") + " button is not visible.");
                        operation.Detail["buttonVisible"] = true;
                        var press = AccessTools.Method(typeof(AlertPopup), "ButtonPress") ?? throw new MissingMethodException("AlertPopup", "ButtonPress");
                        press.Invoke(popup, new object[] { request.Text("answer") == "yes" ? AlertPopup.Response.CONFIRM : AlertPopup.Response.CANCEL });
                        operation.Detail["answer"] = request.Text("answer");
                        operation.Detail["inputKind"] = "semantic-popup-answer";
                    }
                    else
                    {
                        // A prompt needs the deck list; the paste button is also the edit-mode check (blocked, with a message).
                        if (CollectionManager.Get().IsInEditMode() && !request.Has("mode") ) throw new InvalidOperationException("Close the open deck first.");
                        string mode = request.Has("mode") ? request.Text("mode") : "prompt";
                        var method = AccessTools.Method(typeof(CollectionManagerDisplay),
                            mode == "paste" ? "PasteFromClipboardIfValidOrShowStatusMessage" : "CheckClipboardAndPromptPlayerToPaste")
                            ?? throw new MissingMethodException("CollectionManagerDisplay", mode);
                        method.Invoke(display, null);
                        operation.Detail["mode"] = mode;
                        operation.Detail["inputKind"] = "semantic-paste-entry";
                    }
                    uiSampleFrame = -1;
                }
                operation.Phase = "settled";
            }
            catch (Exception error) { Reject(operation, error.GetBaseException().Message); }
        }
        OperationReply(envelope.Connection, request, operation);
    }

    private static JsonNode DeckImportState()
    {
        var popup = AccessTools.Field(typeof(DialogManager), "m_currentDialog")?.GetValue(DialogManager.Get()) as AlertPopup;
        var info = popup?.GetInfo();
        return new JsonNode {
            { "stage", NativeUIProbe.LastImportStage }, { "deckId", NativeUIProbe.LastImportDeckId }, { "hero", NativeUIProbe.LastImportHero },
            { "name", NativeUIProbe.LastImportName }, { "cards", NativeUIProbe.LastImportCards }, { "codeCards", NativeUIProbe.LastImportCodeCards },
            { "missing", NativeUIProbe.LastImportMissing }, { "dropped", NativeUIProbe.LastImportDropped },
            { "message", NativeUIProbe.LastImportMessage }, { "error", NativeUIProbe.LastImportError },
            { "popupHeader", info?.m_headerText }, { "popupText", info?.m_text }
        };
    }
}
#endif
