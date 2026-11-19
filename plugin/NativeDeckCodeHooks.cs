using System;
using System.Collections.Generic;
using System.Linq;
using Hearthstone.Core;
using PegasusShared;

// Deck-code import (#140), native side. The client's own flow stays: the clipboard prompt, the class pick and the new deck
// opening in the editor. SoloStone plans the code before the client creates the deck, fills the profile draft in one commit
// (CreateLocalDraft), then projects it onto the client's deck objects before the editor opens. Nothing is ever granted.
internal static partial class NativeUIProbe
{
    private const string DefaultImportName = "Imported deck";
    private const double PlanLifetimeSeconds = 10;

    private sealed class PendingImport { internal DeckCodePlan Plan; internal DateTime Created = DateTime.UtcNow; }
    private static PendingImport pendingImport;
    // The client fills a new deck from its cached code through the edit-mode paste right after it opens: skip that one call.
    private static readonly NativeDeckCodeImport.NativeFillSkip nativeFillSkip = new NativeDeckCodeImport.NativeFillSkip();
    // A planned import is in flight until CreateLocalDraft reports whether it made a deck (the postfix then tidies a refusal).
    private static bool importInFlight, importCreated;
    private static readonly Dictionary<long, DeckCodePlan> importProjections = new Dictionary<long, DeckCodePlan>();

    // What the last import did, for the status file and test control.
    internal static string LastImportHero, LastImportName, LastImportError = "", LastImportMessage = "";
    internal static long LastImportDeckId;
    internal static int LastImportCards, LastImportMissing, LastImportDropped, LastImportCodeCards;
    internal static string LastImportStage = "none";

    // Prefix of CollectionManagerDisplay.CreateDeckFromClipboard: decode the pasted code and plan it. A code that cannot be
    // imported is refused here, before the client creates anything.
    private static bool PlanDeckCodeImport(ShareableDeck __0)
    {
        if (!active) return true;
        // The edit-mode branch (overwrite prompt, paste while editing) is the blocked edit paste's: it speaks alone.
        if (CollectionManager.Get()?.IsInEditMode() == true) return true;
        pendingImport = null;
        nativeFillSkip.Reset();
        importInFlight = false; importCreated = false;
        try
        {
            var contents = __0?.DeckContents;
            if (__0 == null || contents == null) throw new DeckCodeRefusal(DeckCodeRefusalReason.Malformed, "This deck code is damaged.");
            string heroCard = CardIdOrNull(__0.HeroCardDbId);
            string hero = heroCard == null ? null : NativeUIProjection.CreateRequestHero(heroCard, LocalShop.Default.HeroSkinClass);
            var main = (contents.Cards ?? new List<DeckCardData>()).Select(card => (CardIdOrNull(card.Def.Asset), card.Qty)).ToList();
            var side = (contents.SideboardCards ?? new List<SideBoardCardData>())
                .Select(card => (CardIdOrNull(card.LinkedCardDbId), CardIdOrNull(card.Def.Asset), card.Qty)).ToList();
            var plan = NativeDeckCodeImport.Plan(hero, __0.DeckName, null, main, side);
            pendingImport = new PendingImport { Plan = plan };
            importInFlight = true;
            LastImportStage = "planned";
            LastImportError = "";
            log.LogInfo("NATIVE_DECK import planned hero=" + plan.Hero + " cards=" + plan.Cards.Count + " sideboards=" + plan.Sideboards.Count
                + " dropped=" + plan.Dropped + " code=" + plan.CodeCards);
            return true;
        }
        catch (DeckCodeRefusal refusal)
        {
            ReportImportRefused(refusal.Message, "refused:" + refusal.Reason);
            return false;
        }
        catch (Exception error)
        {
            log.LogError("NATIVE_DECK import decode failed: " + error);
            ReportImportRefused("This deck code could not be read.", "decode-failed");
            return false;
        }
    }

    private static string CardIdOrNull(int dbId)
    {
        try { return dbId > 0 ? GameUtils.TranslateDbIdToCardId(dbId, false) : null; }
        catch (Exception) { return null; }
    }

    private static void ReportImportRefused(string message, string stage)
    {
        LastImportStage = stage; LastImportError = message; LastImportMessage = "";
        lastDeckOperation = "import-" + stage; lastDeckError = message;
        log.LogWarning("NATIVE_DECK import refused: " + message);
        try
        {
            DialogManager.Get()?.ShowPopup(new AlertPopup.PopupInfo {
                m_headerText = "Deck not imported", m_text = message + "\n\nNothing was added to your decks.",
                m_showAlertIcon = true, m_responseDisplay = AlertPopup.ResponseDisplay.OK
            });
        }
        catch (Exception popupError) { log.LogError("NATIVE_DECK popup failed: " + popupError); }
    }

    // The planned import this creation request belongs to, once: the plan lives only for the synchronous create the client
    // starts from CreateDeckFromClipboard, never for a later request.
    private static DeckCodePlan TakePendingImport()
    {
        var pending = pendingImport;
        pendingImport = null;
        return pending != null && (DateTime.UtcNow - pending.Created).TotalSeconds <= PlanLifetimeSeconds ? pending.Plan : null;
    }


    // The client swaps its deck-created callback to the clipboard one after SendCreateDeck returns, even when no deck was made.
    // Put the ordinary one back (the recipe page, no paste on the next New Deck).
    private static void RestoreDeckCreatedCallbacks(CollectionManagerDisplay display)
    {
        var manager = CollectionManager.Get();
        manager.RemoveDeckCreatedListener((CollectionManager.DelOnDeckCreated)Delegate.CreateDelegate(
            typeof(CollectionManager.DelOnDeckCreated), display, Required(typeof(CollectionManagerDisplay), "OnDeckCreatedFromClipboard")));
        manager.RegisterDeckCreatedListener((CollectionManager.DelOnDeckCreated)Delegate.CreateDelegate(
            typeof(CollectionManager.DelOnDeckCreated), display, Required(typeof(CollectionManagerDisplay), "OnDeckCreatedByPlayer")));
    }

    // Postfix of CreateDeckFromClipboard: a planned import that made no deck (gate refusal, failed commit) restores the callbacks
    // and clears the cached code, as No on the prompt does.
    private static void TidyRefusedImport(CollectionManagerDisplay __instance)
    {
        if (!active || !importInFlight) return;
        importInFlight = false;
        if (importCreated) return;
        NativeRecipeSafety.OrDefault(() => { RestoreDeckCreatedCallbacks(__instance); Invoke(__instance, "RejectDeckFromClipboard"); return true; }, false, LogRecipeFailure);
    }

    // Prefix of CollectionManagerDisplay.Exit: nothing armed outlives the Collection.
    private static void LeaveCollectionImportState() { nativeFillSkip.Reset(); }

    private static string ImportRefusalText(int personalCount) =>
        NativeUIProjection.PersonalDeckBlocker(personalCount, SessionLoanerKeys().Count()) ?? "This deck code cannot be imported here.";

    private static string ImportDeckName(string requested, string hero, DeckCodePlan plan) =>
        plan.Name ?? NativeDeckCodeImport.Name(hero, requested, DefaultImportName);

    private static void RecordImport(LocalCustomDeck saved, DeckCodePlan plan)
    {
        LastImportDeckId = saved.Id; LastImportHero = saved.Hero; LastImportName = saved.Name;
        LastImportCards = saved.Cards.Count; LastImportCodeCards = plan.CodeCards; LastImportDropped = plan.Dropped;
        LastImportMissing = Profile.MissingDeckSlotCount(saved.Id);
        LastImportStage = "committed"; LastImportError = ""; LastImportMessage = NativeDeckCodeImport.StatusMessage(plan, LastImportMissing) ?? "";
        log.LogInfo("NATIVE_DECK import deck=" + saved.Id + " cards=" + saved.Cards.Count + " missing=" + LastImportMissing
            + " dropped=" + plan.Dropped + " sideboards=" + saved.Sideboards.Count);
    }

    // Prefix of CollectionManagerDisplay.OnDeckCreatedFromClipboard: the client now holds an empty deck for the committed
    // draft; fill it from the profile (missing cards as ghosts, sideboards, versions) before its editor opens.
    private static bool ProjectImportedDeck(CollectionManagerDisplay __instance, long __0)
    {
        if (!active || !importProjections.TryGetValue(__0, out DeckCodePlan plan)) return true;
        importProjections.Remove(__0);
        string stage = "prepare";
        try
        {
            var saved = Profile.GetDraft(__0) ?? throw new InvalidOperationException("Imported deck is not in the profile: " + __0);
            var manager = CollectionManager.Get() ?? throw new InvalidOperationException("Collection manager unavailable.");
            var working = manager.GetDeck(__0) ?? throw new InvalidOperationException("Imported deck is missing natively: " + __0);
            var prior = manager.GetBaseDeck(__0) ?? working;
            var header = DeckHeaders().Decks.Single(row => row.ID == __0);
            stage = "fill-native";
            working.ClearSideboards();
            ProjectMissingDraftCards(working, saved, true);
            ProjectSideboards(working, saved.Sideboards);
            working.SourceType = DeckSourceType.DECK_SOURCE_TYPE_NORMAL;
            stage = "project-header-and-base";
            ProjectSavedDraft(saved, working, prior, header, ExpectedRanks()[__0]);
            stage = "assert-working";
            NativeRecipeSafety.AssertContents(saved, working.Name, NativeBaseHero(working), working.GetCardsWithCardID(), ReadSideboards(working));
            stage = "assert-base";
            NativeRecipeSafety.AssertContents(saved, prior.Name, NativeBaseHero(prior), prior.GetCardsWithCardID(), ReadSideboards(prior));
            stage = "notify";
            working.OnContentChangesComplete();
            working.OnNameChangeComplete();
            lastDeckOperation = "import:" + __0;
            lastDeckError = "";
            LastImportStage = "projected";
            nativeFillSkip.Arm(__0);
            string message = LastImportMessage;
            if (message.Length > 0)
                Processor.ScheduleCallback(0.8f, false, _ => { try { UIStatus.Get()?.AddInfo(message); } catch (Exception) { } });
            return true;
        }
        catch (Exception error)
        {
            LastImportStage = "projection-failed:" + stage; LastImportError = error.GetBaseException().Message;
            log.LogError("NATIVE_DECK import projection failed deck=" + __0 + " stage=" + stage + ": " + error);
            // Clear the cached code and the clipboard, as the client does when the player answers No, so the code is not offered
            // again. The empty native deck left beside the saved one cannot be saved over it: SaveLocalDraft refuses a base deck that differs from the profile draft.
            NativeRecipeSafety.OrDefault(() => { Invoke(__instance, "RejectDeckFromClipboard"); return true; }, false, LogRecipeFailure);
            NativeRecipeSafety.OrDefault(() => { RestoreDeckCreatedCallbacks(__instance); return true; }, false, LogRecipeFailure);
            NativeRecipeSafety.OrDefault(() => {
                NativeRecipeGuide.ShowError("DECK SAVED", "Your deck is saved locally. Restart the game to reload it.");
                return true;
            }, false, LogRecipeFailure);
            return false;
        }
    }

    // Prefix of CollectionInputMgr.PasteDeckInEditModeFromShareableDeck. Pasting over an open deck stays unavailable: the
    // client's routine does not respect what SoloStone saves. Say so, instead of doing nothing.
    private static bool BlockDeckEditPaste()
    {
        if (!active) return true;
        long edited = CollectionManager.Get()?.GetEditedDeck()?.ID ?? 0;
        if (nativeFillSkip.TryConsume(CurrentCollectionScene(), edited))
        {
            log.LogInfo("NATIVE_DECK import native fill skipped deck=" + edited);
            return false;
        }
        log.LogWarning("NATIVE_DECK blocked edit paste");
        LastImportStage = "edit-paste-blocked"; LastImportError = NativeDeckCodeImport.EditPasteMessage;
        try { UIStatus.Get()?.AddInfo(NativeDeckCodeImport.EditPasteMessage); }
        catch (Exception error) { log.LogError("NATIVE_DECK status message failed: " + error); }
        return false;
    }
}
