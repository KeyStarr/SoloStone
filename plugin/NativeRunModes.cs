using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using MiniJSON;

// Run modes in the client's own adventure screen (Dungeon Run first: adventure 414, mode DUNGEON_CRAWL; plugin/Runs holds the
// rules, RunCatalog the shipped definitions). The Modes button opens it while run modes are enabled. The client's Dungeon
// Crawl display reads the run from the save data served here.
internal static class NativeRunModes
{
    private static ManualLogSource log;

    // The public build shows run modes once the shipped one is complete against this engine and this client (a boss for
    // every round, loot for every class, both treasure pools); developer and test-hook builds always show them (an
    // incomplete run plays there). Decided once the client's cards have loaded.
    internal static bool Enabled
    {
        get
        {
            if (RunCatalog.All.Count == 0) return false;
#if SOLOSTONE_RELEASE
            if (ready == null)
            {
                if (!(DefLoader.Get()?.GetAllEntityDefs()?.Count > 0)) return false;
                RunAdmission admission = EngineFor(Current).Admission;
                ready = admission.IsReady;
                if (!admission.IsReady) log?.LogWarning("RUNMODE " + Current.Id + " hidden, not complete: " + string.Join("; ", admission.Problems()));
            }
            return ready.Value;
#else
            return true;
#endif
        }
    }
#if SOLOSTONE_RELEASE
    private static bool? ready;
#endif

    // The run mode the Modes button opens (the first shipped one until there is a chooser).
    internal static RunDefinition Current => RunCatalog.All.FirstOrDefault();

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        NativeHomeUI.RunModesAvailable = () => Enabled;
        harmony.Patch(AccessTools.Method(typeof(Box), "OnModesButtonPressed"), prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(ModesPressed)));
        harmony.Patch(AccessTools.Method(typeof(GameMgr), "FindGameWithHero"), prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(FindGameWithHero)));
        harmony.Patch(AccessTools.Method(typeof(AbsDeckPickerTrayDisplay), "OnPlayGameButtonReleased"),
            prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(PickerPlayReleased)));
        foreach (string handler in new[] { "ContinueButtonPress_PrevMode", "ContinueButtonPress_Common" })
            harmony.Patch(AccessTools.Method(typeof(EndGameScreen), handler), prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(EndScreenContinue)));
        harmony.Patch(AccessTools.Method(typeof(EndGameScreen), "Show"), postfix: new HarmonyMethod(typeof(NativeRunModes), nameof(EndScreenShown)));
        harmony.Patch(AccessTools.Method(typeof(AdventureConfig), "OnAdventureSceneAwake"), postfix: new HarmonyMethod(typeof(NativeRunModes), nameof(AdventureSceneAwake)));
        harmony.Patch(AccessTools.Method(typeof(AdventureDungeonCrawlDisplay), "OnBackButtonPress"), prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(DisplayBack)));
        harmony.Patch(AccessTools.Method(typeof(RewardPopups), "ShowRewardsForAdventureUnlocks"), prefix: new HarmonyMethod(typeof(NativeRunModes), nameof(AdventureUnlocks)));
        harmony.Patch(AccessTools.Method(typeof(AdventureDungeonCrawlDisplay), "OnRewardOptionSelected"), postfix: new HarmonyMethod(typeof(NativeRunModes), nameof(RewardChosen)));
    }

    // The display's Back goes to the client's adventure chooser, which offers only Practice (not playable offline). With one
    // run mode there is nothing to choose: Back goes home. A chooser of run modes can take this over when there are several.
    private static bool DisplayBack()
    {
        if (!InRunMode) return true;
        Trace("display back: home");
        NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
        return false;
    }

    // A won run's end sequence hands its last step (the run marked seen, Back enabled) to the client's reward popups for the
    // adventure's unlocks, whose "all shown" never fires offline. A run mode has no such unlocks: with none to show, that
    // step runs at once.
    private static bool AdventureUnlocks(List<AdventureHeroPowerDbfRecord> unlockedHeroPowers, List<AdventureDeckDbfRecord> unlockedDecks,
        List<AdventureLoadoutTreasuresDbfRecord> unlockedLoadoutTreasures, List<AdventureLoadoutTreasuresDbfRecord> upgradedLoadoutTreasures, Action callback)
    {
        if (!InRunMode || unlockedHeroPowers?.Count > 0 || unlockedDecks?.Count > 0 || unlockedLoadoutTreasures?.Count > 0 || upgradedLoadoutTreasures?.Count > 0)
            return true;
        Trace("run end: no adventure unlocks to show");
        callback?.Invoke();
        return false;
    }

    // A chosen bundle or treasure goes into the deck tray card by card (BasePhoneDeckTray.AddCard), which leaves the tray's
    // card count as it was; the client sets the count only when it loads a whole deck. The choice is already saved by now
    // (the client's save request is answered in place), so the count is set from the run: its cards and passive treasures.
    private static readonly System.Reflection.FieldInfo TrayDeck = AccessTools.Field(typeof(AdventureDungeonCrawlDeckTray), "m_deck");
    private static readonly System.Reflection.MethodInfo TrayCountUpdated = AccessTools.Method(typeof(BasePhoneDeckTray), "OnCardCountUpdated");

    private static void RewardChosen(AdventureDungeonCrawlDisplay __instance)
    {
        if (!InRunMode) return;
        try
        {
            RunState run = LocalLadder.Profile?.RunOf(Current);
            var tray = __instance.m_dungeonCrawlDeckTray;
            if (run == null || run.Status != RunStatus.Active || tray == null) return;
            int count = run.Deck.Count + run.Passives.Count;
            var deck = (CollectionDeck)TrayDeck.GetValue(tray);
            TrayCountUpdated.Invoke(tray, new object[] { count, deck?.GetMaxCardCount() ?? count });
            Trace("deck tray count " + count);
        }
        catch (Exception error) { log?.LogWarning("RUNMODE deck tray count not updated: " + error.Message); }
    }

    // After a run fight the client's victory or defeat screen waits on services that do not exist offline (achievements,
    // rewards) and never registers its own continue or leaves the game scene. A click on it goes back to the run mode's
    // adventure screen, which shows the run as it now stands.
    private static void EndScreenShown(EndGameScreen __instance)
    {
        if (phase != FightPhase.Ended || __instance?.m_hitbox == null) return;
        __instance.m_hitbox.AddEventListener(UIEventType.RELEASE, _ => EndScreenContinue());
        Trace("fight " + fightId + " end screen shown");
    }

    private static bool EndScreenContinue()
    {
        if (phase != FightPhase.Ended) return true;
        if (!leaving)
        {
            leaving = true;
            Trace("fight " + fightId + " end screen continued: back to the adventure screen");
            Open();
        }
        return false;
    }
    private static bool leaving;

    // In a run mode's hero picker, Play skips the client's Battle.net login check (offline it opens a reconnect dialog and
    // stops) and does the rest of its own handler: lock the buttons and play.
    private static bool PickerPlayReleased(AbsDeckPickerTrayDisplay __instance)
    {
        if (!InRunMode) return true;
        AccessTools.Method(typeof(AbsDeckPickerTrayDisplay), "SetPlayButtonEnabled").Invoke(__instance, new object[] { false });
        AccessTools.Method(typeof(AbsDeckPickerTrayDisplay), "SetHeroButtonsEnabled").Invoke(__instance, new object[] { false });
        AccessTools.Method(typeof(AbsDeckPickerTrayDisplay), "PlayGame").Invoke(__instance, null);
        return false;
    }

    // The adventure screen is showing a shipped run mode.
    private static bool InRunMode => Enabled && SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.ADVENTURE
        && AdventureConfig.Get() != null && RunCatalog.All.Any(d => (int)AdventureConfig.Get().GetSelectedAdventure() == d.Client.Adventure);

    // ---- Boss fights ----
    private enum FightPhase { None, Staged, Ended }
    private static FightPhase phase;
    private static string fightId;
    private static RunDefinition fightDefinition;
    private static NativeMatch fightMatch;

    // A run fight owns the current match from staging until the player has left its game scene.
    internal static bool OwnsMatch => phase != FightPhase.None;

    // Every collectible card of a class, one id per name (a reprint shares its original's name), for class-pool buckets.
    private static IEnumerable<string> ClassCards(string cardClass) =>
        SabberStoneCore.Model.Cards.All.Where(c => c.Collectible && c.Class.ToString() == cardClass)
            .GroupBy(c => c.Name).Select(g => g.Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).First())
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

    // Content is admitted against the engine and the client together: a card plays only if both know it.
    private static RunEngine EngineFor(RunDefinition definition)
    {
        bool Labelled(string id) => ClientBridge.ClientKnowsCard(id);
        bool Playable(string id)
        {
            try { return SabberStoneCore.Model.Cards.FromId(id).Implemented && Labelled(id); }
            catch (KeyNotFoundException) { return false; }
        }
#if SOLOSTONE_RELEASE
        const bool developer = false;
#else
        const bool developer = true;
#endif
        return new RunEngine(RunAdmission.Evaluate(definition, Playable, Labelled, ClassCards), allowIncomplete: developer);
    }

    // The client's Dungeon Run play button (and every later adventure's) asks the server for a game with the chosen hero on
    // the adventure's mission. A run mode's mission is played here: the run starts (or continues), its fight is marked
    // under way and staged as an encounter. Any other game goes on to the client's own code.
    private static bool FindGameWithHero(int missionId, int heroCardDbId)
    {
        RunDefinition definition = Enabled ? RunCatalog.ForScenario(missionId) : null;
        if (definition == null) return true;
        try { StartFight(definition, heroCardDbId); }
        catch (Exception error)
        {
            log?.LogError("RUNMODE fight not started: " + error);
            // A marker left by a fight that never staged would block every start until a restart: clear it.
            try { if (LocalLadder.Profile?.RunFightUnderway(definition) == true) LocalLadder.Profile.RecoverInterrupted(); }
            catch (Exception cleanup) { log?.LogError("RUNMODE fight marker not cleared: " + cleanup.Message); }
            phase = FightPhase.None;
        }
        return false;
    }

    private static void StartFight(RunDefinition definition, int heroCardDbId)
    {
        LocalProfile profile = LocalLadder.Profile ?? throw new InvalidOperationException("No save is loaded.");
        RunEngine engine = EngineFor(definition);
        RunState run = profile.RunOf(definition);
        if (run == null || run.Status != RunStatus.Active)
        {
            // A new run with the class of the hero picked in the client (its base hero plays; skins stay cosmetic).
            var hero = SabberStoneCore.Model.Cards.FromAssetId(heroCardDbId) ?? throw new ArgumentException("Unknown hero " + heroCardDbId);
            string cls = hero.Class.ToString();
            string baseHero = SabberStoneCore.Model.Cards.HeroCard(hero.Class).Id;
            run = profile.StartRun(engine, cls, baseHero, Environment.TickCount & 0x7fffffff);
            Trace("run started " + definition.Id + " class=" + cls + " hero=" + baseHero + " seed=" + run.Seed);
        }
        LocalRunFight fight = profile.BeginRunFight(engine);
        EncounterSpec encounter = fight.Encounter.ToSpec(definition);
        Trace("fight " + fight.Id + " round=" + fight.Encounter.Round + " boss=" + fight.Encounter.Boss.Id + " health=" + fight.Encounter.PlayerHealth
            + " deck=" + encounter.PlayerDeck.Count + " passives=" + (encounter.PlayerLoadout?.Count ?? 0));
        NativeMatch prepared = ClientBridge.PrepareRunMatch(encounter, Environment.TickCount & 0x7fffffff);
        fightId = fight.Id; fightDefinition = definition; fightMatch = prepared; phase = FightPhase.Staged;
        DebugScenario scenario = null;
#if !SOLOSTONE_RELEASE
        scenario = DebugScenario.Read(null, message => log?.LogWarning(message), ClientBridge.ClientKnowsCard, DebugScenario.RunId, NativeTestControl.DiagnosticsEnabled);
#endif
        ClientBridge.StartRunMatch(prepared, "run:" + fight.Id, scenario);
    }

    // ClientBridge.Update, every frame: a finished fight settles into its run once (Won, Lost, or a tie).
    internal static void Observe(NativeMatch match)
    {
        if (phase != FightPhase.Staged || match == null || !ReferenceEquals(match, fightMatch) || match.Game?.State != SabberStoneCore.Enums.State.COMPLETE) return;
        phase = FightPhase.Ended;
        try
        {
            var outcome = MatchOutcome.From(match.Game, match.HumanConceded);
            RunFightResult result = outcome.Result == "Won" ? RunFightResult.Won : outcome.Result == "Lost" ? RunFightResult.Lost : RunFightResult.Tied;
            // Cards the fight added to the run deck ("Add it to your Dungeon Deck"); engine builds only (ENGINE_SOURCE).
            IReadOnlyList<string> additions = null;
#if ENGINE_SOURCE
            additions = SabberStoneCore.Actions.RunDeckRules.Of(match.Game.Player1);
#endif
            RunState run = LocalLadder.Profile.ResolveRunFight(EngineFor(fightDefinition), fightId, result, additions);
            Trace("fight " + fightId + " " + result + " -> run " + run.Status + " round=" + run.Round + " next=" + (run.NextBoss ?? "-")
                + " offer=" + (run.Offer?.Kind.ToString() ?? "-"));
        }
        catch (Exception error) { log?.LogError("RUNMODE fight result not saved: " + error); }
        // The ladder's screen state follows every staged match; a run fight releases it here (it has no ladder receipt).
        LocalLadder.RewardPresentationComplete();
    }

    // NativeUIProbe.Update: once the fight's game scene is gone, the fight no longer owns anything.
    internal static void Update()
    {
        PresentWaitingCardBacks();
        if (phase == FightPhase.Ended && SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() != SceneMgr.Mode.GAMEPLAY && !SceneMgr.Get().IsTransitioning())
        {
            Trace("fight " + fightId + " left its game scene for " + SceneMgr.Get().GetMode());
            phase = FightPhase.None; fightId = null; fightMatch = null; leaving = false;
        }
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

    // The client's Modes handler asks for a Battle.net login first and then opens its online modes screen. While run modes
    // are enabled the press goes straight to the hub's button route (NativeUIProbe.OnLocalHubButton), which opens Open().
    private static bool ModesPressed(Box __instance)
    {
        if (!Enabled) return true;
        __instance.FireButtonPressEvent(Box.ButtonType.GAME_MODES, false);
        return false;
    }

    internal static void Open()
    {
        RunDefinition definition = Current;
        if (definition == null) return;
        AdventureConfig.Get().SetSelectedAdventureMode((AdventureDbId)definition.Client.Adventure, (AdventureModeDbId)definition.Client.Mode);
        log?.LogInfo("RUNMODE open " + definition.Id + " adventure=" + definition.Client.Adventure + " mode=" + definition.Client.Mode);
        openPending = true;
        NativeUIProbe.Navigate(SceneMgr.Mode.ADVENTURE);
    }

    // Set from Open until the adventure scene has loaded: the scene being loaded is the run mode's.
    private static bool openPending;

    // The adventure scene, waking up, restores the last adventure from the options and falls back to Practice for one the
    // account does not own; coming back from a run fight it then waits for Practice's save data forever. While a run mode
    // is being opened, its own adventure is selected right after.
    private static void AdventureSceneAwake(AdventureConfig __instance)
    {
        if (!Enabled || !openPending || Current == null) return;
        __instance.SetSelectedAdventureMode((AdventureDbId)Current.Client.Adventure, (AdventureModeDbId)Current.Client.Mode);
        Trace("adventure scene awake: selected " + Current.Id);
    }

    // The adventure scene opens on its chooser; a run mode goes straight to its own screen (as the chooser's Choose would).
    internal static void SceneLoaded()
    {
        if (!Enabled || Current == null) return;
        var config = AdventureConfig.Get();
        Trace("adventure scene loaded: selected=" + config.GetSelectedAdventure() + "/" + config.GetSelectedMode() + " subscene=" + config.CurrentSubScene);
        // The scene restores the last adventure the player chose on load; select the run mode's again.
        openPending = false;
        if (config.GetSelectedAdventure() != (AdventureDbId)Current.Client.Adventure || config.GetSelectedMode() != (AdventureModeDbId)Current.Client.Mode)
            config.SetSelectedAdventureMode((AdventureDbId)Current.Client.Adventure, (AdventureModeDbId)Current.Client.Mode);
        // From the hub the scene opens on its chooser; back from a fight it already shows the run mode's screen.
        if (config.CurrentSubScene != Assets.AdventureData.Adventuresubscene.DUNGEON_CRAWL) config.ChangeSubSceneToSelectedAdventure();
        Trace("subscene -> " + config.CurrentSubScene);
        // A card back still unshown (the game closed before) shows now, unless a run-end sequence is about to show it.
        RunState run = LocalLadder.Profile?.RunOf(Current);
        if (run == null || run.Status == RunStatus.Active || run.RunEndSeen) QueueCardBacks(Current);
    }

    // ---- Rewards ----
    // A card back the mode granted (Dungeon Run: Candle King) shows in the client's own reward display; the grant itself was
    // committed with the fight's result (LocalProfile.ResolveRunFight). It waits until the adventure screen is settled and
    // the run-end banner ("Dungeon cleared with ...") is closed. Dismissed, it counts as shown.
    private static bool presentingCardBacks;
    private static RunDefinition cardBacksWaiting;

    private static void QueueCardBacks(RunDefinition definition)
    {
        if (definition != null) cardBacksWaiting = definition;
    }

    private static void PresentWaitingCardBacks()
    {
        if (cardBacksWaiting == null || presentingCardBacks || !SceneMgr.IsInitialized() || SceneMgr.Get().IsTransitioning()
            || SceneMgr.Get().GetMode() != SceneMgr.Mode.ADVENTURE || BannerManager.Get()?.IsShowing == true) return;
        RunDefinition definition = cardBacksWaiting;
        cardBacksWaiting = null;
        LocalProfile profile = LocalLadder.Profile;
        if (profile == null) return;
        var backs = profile.UnshownRunCardBacks(definition).Where(id => GameDbf.CardBack?.GetRecord(id) != null).ToList();
        if (backs.Count == 0) return;
        presentingCardBacks = true;
        Trace("card backs shown: " + string.Join(",", backs));
        try
        {
            RewardUtils.LoadAndDisplayRewards(backs.Select(id => (RewardData)new CardBackRewardData(id)).ToList(), () => {
                presentingCardBacks = false;
                try { profile.MarkRunCardBacksShown(definition); Trace("card backs dismissed"); }
                catch (Exception error) { log?.LogError("RUNMODE card backs not marked shown: " + error.Message); }
            });
        }
        catch (Exception error)
        {
            presentingCardBacks = false;
            log?.LogError("RUNMODE card backs not shown: " + error);
        }
    }

    // ---- Save data ----
    // The save keys a shipped run mode owns (Dungeon Run: server 24, client 42).
    internal static bool OwnsKey(GameSaveKeyId key) => Enabled && DefinitionFor(key) != null;

    // Returning from a run fight, the adventure scene briefly has its default adventure (Practice) selected and asks for that
    // adventure's save key, which is none (0). Answered empty; nothing to serve.
    internal static bool IgnoresKey(GameSaveKeyId key) => Enabled && (int)key == 0;

    private static RunDefinition DefinitionFor(GameSaveKeyId key) =>
        RunCatalog.All.FirstOrDefault(definition => definition.Client.ServerKey == (int)key || definition.Client.ClientKey == (int)key);

    private static int Dbf(string id) => SabberStoneCore.Model.Cards.FromId(id).AssetId;
    private static int ClassId(string cls) => (int)Enum.Parse(typeof(SabberStoneCore.Enums.CardClass), cls);
    private static int Health(string id) => SabberStoneCore.Model.Cards.FromId(id)[SabberStoneCore.Enums.GameTag.HEALTH];

    // Puts the run mode's values for this key into the client's save-data cache: the server key gets the run as the Dungeon
    // Crawl display reads it (RunSaveData), the client key the flags the client wrote before (voice lines, tutorials).
    internal static void Serve(GameSaveKeyId key, Action<PegasusShared.GameSaveDataUpdate> apply)
    {
        RunDefinition definition = DefinitionFor(key);
        LocalProfile profile = LocalLadder.Profile;
        if (definition == null || profile == null) return;
        IEnumerable<KeyValuePair<int, long[]>> values;
        if ((int)key == definition.Client.ServerKey)
        {
            RunState run = profile.RunOf(definition);
            int health = run == null ? 0 : EngineFor(definition).FightStartHealth(run);
            var wins = profile.RunWins(definition).Select(w => new RunClassWinCount { Class = w.Class, Bosses = w.Bosses, Runs = w.Runs });
            values = RunSaveData.Project(definition, run, wins, health, Dbf, ClassId, Health);
            Trace("serve " + key + ": run=" + (run == null ? "none" : run.Status + " round " + run.Round + " next " + (run.NextBoss ?? "-")
                + " offer " + (run.Offer?.Kind.ToString() ?? "-")));
        }
        else values = profile.RunClientFlags(definition).Select(f => new KeyValuePair<int, long[]>(f.Subkey, f.Values.ToArray()));
        apply(new PegasusShared.GameSaveDataUpdate { Tuple = new List<PegasusShared.GameSaveKey> { new PegasusShared.GameSaveKey { Id = (long)key } } });
        foreach (var value in values)
            apply(new PegasusShared.GameSaveDataUpdate {
                Tuple = new List<PegasusShared.GameSaveKey> { new PegasusShared.GameSaveKey { Id = (long)key }, new PegasusShared.GameSaveKey { Id = value.Key } },
                Value = new PegasusShared.GameSaveDataValue { IntValue = value.Value.ToList() },
            });
    }

    // A value the client wrote to the run mode's keys: a choice, the run-end screen seen, or retire (server key) becomes a run
    // operation; anything on the client key is kept for the next launch.
    internal static void Write(GameSaveDataManager.SubkeySaveRequest request)
    {
        RunDefinition definition = DefinitionFor(request.Key);
        LocalProfile profile = LocalLadder.Profile;
        if (definition == null || profile == null) return;
        long[] values = request.Long_Values ?? new long[0];
        if ((int)request.Key == definition.Client.ClientKey)
        {
            profile.SetRunClientFlag(definition, (int)request.Subkey, values);
            return;
        }
        RunWrite write = RunSaveData.Interpret((int)request.Subkey, values);
        Trace("write " + request.Subkey + "=" + string.Join(",", values) + " -> " + write.Kind + (write.Kind == RunWriteKind.ChooseLoot || write.Kind == RunWriteKind.ChooseTreasure ? " " + write.Index : ""));
        RunEngine engine = EngineFor(definition);
        RunState run = profile.RunOf(definition);
        switch (write.Kind)
        {
            case RunWriteKind.ChooseLoot:
            case RunWriteKind.ChooseTreasure:
                bool loot = write.Kind == RunWriteKind.ChooseLoot;
                // A choice for the offer the client showed: loot for a loot offer, a treasure for a treasure offer.
                // The client always shows three slots; with fewer options a slot is an empty crate, and choosing it does nothing.
                if (run?.Offer != null && (run.Offer.Kind == RunRewardKind.Loot) == loot && write.Index < run.Offer.Options.Count)
                    profile.ChooseRunReward(engine, write.Index);
                break;
            case RunWriteKind.RunEndSeen:
                if (run != null && run.Status != RunStatus.Active && !run.RunEndSeen) profile.MarkRunEndSeen(engine);
                // The client writes this at the end of its run-end sequence (the banner may still be up; the card back waits).
                QueueCardBacks(definition);
                break;
            case RunWriteKind.Retire:
                if (run != null && run.Status == RunStatus.Active) profile.RetireRun(engine);
                break;
        }
    }

    internal static void Trace(string message) => log?.LogInfo("RUNMODE " + message);

    // ---- Test tools ----
    // The run as a tester reads it (test control's state, hook builds): status, Health of the next fight, next boss, the
    // pending offer and the deck, with card names. Null without a run mode or a save.
    private static RunEngine testEngine;
    internal static JsonNode TestView()
    {
        RunDefinition definition = Current;
        LocalProfile profile = LocalLadder.Profile;
        if (!Enabled || definition == null || profile == null) return null;
        string Name(string id)
        {
            try { return SabberStoneCore.Model.Cards.FromId(id).Name; }
            catch (KeyNotFoundException) { return id; }
        }
        JsonList Names(IEnumerable<string> ids) { var list = new JsonList(); foreach (string id in ids) list.Add(Name(id)); return list; }
        var node = new JsonNode { { "mode", definition.Id } };
        RunState run = profile.RunOf(definition);
        if (run == null) { node["status"] = "none"; return node; }
        if (testEngine == null || testEngine.Definition != definition) testEngine = EngineFor(definition);
        node["status"] = run.Status.ToString();
        node["class"] = run.Class;
        node["round"] = run.Round;
        node["bossesDefeated"] = run.BossesDefeated;
        node["health"] = testEngine.FightStartHealth(run);
        node["nextBoss"] = run.NextBoss == null ? null : Name(definition.Boss(run.NextBoss).Hero) + " (" + run.NextBoss + ")";
        node["lostTo"] = run.LostTo;
        node["runEndSeen"] = run.RunEndSeen;
        node["deckCount"] = run.Deck.Count;
        node["deck"] = Names(run.Deck);
        node["passives"] = Names(run.Passives);
        if (run.Offer != null)
        {
            var options = new JsonList();
            foreach (RunOption option in run.Offer.Options)
                options.Add(new JsonNode { { "title", option.Title == null ? null : Name(option.Title) }, { "cards", Names(option.Cards) } });
            node["offer"] = new JsonNode { { "kind", run.Offer.Kind.ToString() }, { "options", options } };
        }
        return node;
    }
}
