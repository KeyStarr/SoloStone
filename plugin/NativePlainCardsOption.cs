using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MiniJSON;
using UnityEngine;

// Options > SoloStone Options > "Show Plain Cards" (Discord/Reddit request: premium art distracts, Signature cards are hard to read).
// NativeOptionsPanels builds the row and hands it to Attach. On, every match prepared afterwards sends every card of both sides Normal
// (NativeMatch.PlainCards): hand, board, hero, weapon, location, history, big cards, Discover and choices. A running match keeps what it
// began with. Matches only: the collection, deck builder and pack opening keep premium, so players still see what they own.
internal static class NativePlainCardsOption
{
    private static PlainCardsSettings settings;
    private static ManualLogSource log;
    internal static CheckBox Checkbox { get; private set; }
    internal static bool Enabled => settings?.Enabled == true;

    internal static void Initialize(ManualLogSource logger, ConfigFile file) { settings = new PlainCardsSettings(file); log = logger; }

    // The panel's row, built once; it stays for the session.
    internal static void Attach(CheckBox box)
    {
        if (settings == null) throw new InvalidOperationException("Show Plain Cards not initialized");
        box.AddEventListener(UIEventType.RELEASE, _ => {
            try { settings.Set(box.IsChecked()); log.LogInfo("PLAIN_CARDS_OPTION enabled=" + Enabled + " applies=next-match"); }
            catch (Exception error) { log.LogWarning("PLAIN_CARDS_OPTION not saved: " + error.Message); }
            box.SetChecked(Enabled);
        });
        Checkbox = box;
        box.SetChecked(Enabled);
    }

    internal static void Sync() { try { if (Checkbox != null) Checkbox.SetChecked(Enabled); } catch (Exception error) { log?.LogWarning("PLAIN_CARDS_OPTION sync: " + error.Message); } }

#if !SOLOSTONE_RELEASE
    // Test control (state.plainCards): the setting, what the current match runs with, the checkbox.
    internal static JsonNode State() => new JsonNode { { "enabledNextMatch", Enabled }, { "thisMatch", ClientBridge.PlainCardsThisMatch },
        { "checkbox", NativeOptionsPanels.RowState(Checkbox) } };

    // Test control `plain-cards-render` (read-only, on request only: it walks the scene): in a match, what the client's actors render.
    // Every active Actor in the scene by premium (`actors`), the GameState cards by side:zone (`zones`: the entity's PREMIUM tag and the
    // bound actor), the history tiles and big card (`history`), and `nonNormal`, the first premium actors found.
    internal static JsonNode Render()
    {
        if (GameState.Get() == null) return new JsonNode { { "inMatch", false } };
        try { return Walk(); }
        catch (Exception error) { return new JsonNode { { "inMatch", true }, { "error", error.GetType().Name + ": " + error.Message } }; }
    }

    private static readonly System.Reflection.FieldInfo HistoryTiles = AccessTools.Field(typeof(HistoryManager), "m_historyTiles");
    private static readonly System.Reflection.FieldInfo CurrentBigCard = AccessTools.Field(typeof(HistoryManager), "m_currentBigCard");
    // In-match actors carry an Entity, not an EntityDef (that is collection and pack actors), so neither filters what is counted.
    private static readonly System.Reflection.MethodInfo ActorEntity = AccessTools.Method(typeof(Actor), "GetEntity", Type.EmptyTypes);

    private static string CardId(Actor actor)
    {
        try { return (ActorEntity?.Invoke(actor, null) as Entity)?.GetCardId() ?? actor.GetEntityDef()?.GetCardId(); }
        catch (Exception) { return null; }
    }

    private static JsonNode Walk()
    {
        var nonNormal = new List<object>();
        void Note(string where, Actor actor)
        {
            if (actor == null || actor.GetPremium() == TAG_PREMIUM.NORMAL || nonNormal.Count >= 20) return;
            nonNormal.Add(new JsonNode { { "where", where }, { "cardId", CardId(actor) }, { "premium", actor.GetPremium().ToString() },
                { "object", actor.gameObject.name } });
        }
        JsonNode Count(IEnumerable<TAG_PREMIUM> premiums)
        {
            var counts = new JsonNode();
            foreach (var group in premiums.GroupBy(premium => premium).OrderBy(group => group.Key)) counts[group.Key.ToString()] = group.Count();
            return counts;
        }

        var actors = UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsSortMode.None)
            .Where(actor => actor != null && actor.gameObject.activeInHierarchy).ToArray();
        foreach (var actor in actors) Note("scene", actor);

        var zones = new JsonNode();
        var front = GameState.Get();
        int friendly = front.GetFriendlySidePlayer()?.GetPlayerId() ?? 0;
        foreach (var group in front.GetEntityMap().Values.Where(entity => entity.GetCard() != null)
            .GroupBy(entity => (entity.GetControllerId() == friendly ? "friendly" : "opposing") + ":" + ZoneName(entity.GetTag(GAME_TAG.ZONE))))
        {
            var cards = group.ToArray();
            foreach (var entity in cards) Note(group.Key, entity.GetCard().GetActor());
            zones[group.Key] = new JsonNode {
                { "cards", cards.Length },
                { "entityPremiumTag", Count(cards.Select(entity => (TAG_PREMIUM)entity.GetTag(GAME_TAG.PREMIUM))) },
                { "actors", Count(cards.Select(entity => entity.GetCard().GetActor()).Where(actor => actor != null).Select(actor => actor.GetPremium())) }
            };
        }

        var historyActors = new List<Actor>();
        var manager = HistoryManager.Get();
        if (manager != null)
        {
            if (HistoryTiles?.GetValue(manager) is IEnumerable tiles)
                foreach (var tile in tiles.OfType<Component>().Where(tile => tile != null))
                    historyActors.AddRange(tile.GetComponentsInChildren<Actor>(true));
            if (CurrentBigCard?.GetValue(manager) is Component big && big != null)
                historyActors.AddRange(big.GetComponentsInChildren<Actor>(true));
        }
        foreach (var actor in historyActors) Note("history", actor);

        return new JsonNode {
            { "inMatch", true },
            { "actors", Count(actors.Select(actor => actor.GetPremium())) },
            { "zones", zones },
            { "history", new JsonNode { { "tiles", manager == null || !(HistoryTiles?.GetValue(manager) is ICollection list) ? 0 : list.Count },
                { "actors", Count(historyActors.Select(actor => actor.GetPremium())) } } },
            { "nonNormal", nonNormal }
        };
    }

    private static string ZoneName(int zone) => zone == 1 ? "play" : zone == 2 ? "deck" : zone == 3 ? "hand" : zone == 4 ? "graveyard"
        : zone == 5 ? "removed" : zone == 6 ? "setaside" : zone == 7 ? "secret" : "zone" + zone;
#endif
}
