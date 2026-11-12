#if !SOLOSTONE_RELEASE
using System;

// Test control for the craft-token live check (#74). craft-token-grant adds tokens to the disposable save the way a
// ranked milestone does; collection-jump flips the native Collection to one card's page. Neither clicks anything: the
// card view opens with a right-click press on collection.card.<id>, and Create/close are crafting.create/crafting.close.
internal sealed partial class NativeTestControl
{
    private void HandleCraftingTest(ControlEnvelope envelope)
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
                var profile = LocalLadder.Profile ?? throw new InvalidOperationException("Local profile unavailable.");
                if (LocalLadder.Screen == "Playing" || profile.HasPendingMatch) throw new InvalidOperationException("Finish the match first.");
                if (NativeCrafting.PresentationBusy || CraftingManager.GetIsInCraftingMode())
                    throw new InvalidOperationException("Close the crafting view first.");
                if (request.Command == "craft-token-grant")
                {
                    var rarity = (LocalTokenRarity)Enum.Parse(typeof(LocalTokenRarity), request.Text("rarity"));
                    int count = request.Integer("count");
                    profile.GrantCardTokensForTest(rarity, count);
                    operation.Detail["rarity"] = rarity.ToString();
                    operation.Detail["granted"] = count;
                    operation.Detail["tokens"] = profile.CardTokenCount(rarity);
                    operation.Detail["inputKind"] = "local-ledger";
                }
                else
                {
                    string cardId = request.Text("cardId");
                    var display = SceneMgr.Get()?.GetMode() == SceneMgr.Mode.COLLECTIONMANAGER
                        ? CollectionManager.Get()?.GetCollectibleDisplay() as CollectionManagerDisplay : null;
                    var pages = display?.GetPageManager() as CollectionPageManager;
                    if (pages == null || display.GetViewMode() != CollectionUtils.ViewMode.CARDS)
                        throw new InvalidOperationException("Open the Collection's card pages first.");
                    // Native resets the filters when they hide the card (the crafting tray lists missing cards).
                    bool found = pages.JumpToPageWithCard(cardId, TAG_PREMIUM.NORMAL, null, null);
                    operation.Detail["cardId"] = cardId;
                    operation.Detail["found"] = found;
                    operation.Detail["craftingTray"] = display.InCraftingMode();
                    operation.Detail["inputKind"] = "semantic-page-flip";
                    if (!found) throw new InvalidOperationException("Card not on any Collection page (press collection.craftingmode to list missing cards).");
                    uiSampleFrame = -1;
                }
                // Settled means the change was made; wait-ui observes the page flip.
                operation.Phase = "settled";
            }
            catch (Exception error) { Reject(operation, error.GetBaseException().Message); }
        }
        OperationReply(envelope.Connection, request, operation);
    }
}
#endif
