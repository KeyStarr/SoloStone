using System;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using MiniJSON;
using UnityEngine;

// Options > SoloStone Options > "Show Opponents' Pets" (owner 2026-11-15; NativeOptionsPanels builds the row and hands it to Attach).
// On by default. Off, ClientBridge starts each match without the NPC's pet (Gate): NativeMatch then sends no pet entity, no player link
// tags and no NPC pet reactions, so the client builds no pet actor, corner or hit area and plays no pet sound or emote. The player's own
// pet, its reactions, likes, treats and XP do not depend on the NPC pet (tests/native-match NpcPetTests). A running match keeps what it
// began with.
internal static class NativeOpponentPetsOption
{
    internal const string Id = "options.opponent-pets", ObjectName = "SoloStoneOpponentPetsCheckbox", Label = "Show Opponents' Pets";
    private static OpponentPetsSettings settings;
    private static ManualLogSource log;
    internal static CheckBox Checkbox { get; private set; }
    // Unset (a test or a stand-in without Initialize) shows pets, as the default does.
    internal static bool Enabled => settings?.Enabled != false;
    // The pet the last match's NPC owns, whether or not it was shown (test control and logs only).
    internal static string LastOwned { get; private set; }

    internal static void Initialize(ManualLogSource logger, ConfigFile file) { settings = new OpponentPetsSettings(file); log = logger; }

    // The NPC pet card a match starts with: the owned one, or null while the option is off.
    internal static string Gate(string owned)
    {
        LastOwned = owned;
        if (owned == null) return null;
        bool show = Enabled;
        // The option's answer only: NativeMatch.Start still drops a pet the shop cannot map (state.opponentPets.shown is the result).
        log?.LogInfo("NPC_PET card=" + owned + " option=" + (show ? "on" : "off"));
        return show ? owned : null;
    }

    // The panel's row, built once; it stays for the session.
    internal static void Attach(CheckBox box)
    {
        if (settings == null) throw new InvalidOperationException("Show Opponents' Pets not initialized");
        box.AddEventListener(UIEventType.RELEASE, _ => {
            try { settings.Set(box.IsChecked()); log.LogInfo("OPPONENT_PETS_OPTION enabled=" + Enabled + " applies=next-match"); }
            catch (Exception error) { log.LogWarning("OPPONENT_PETS_OPTION not saved: " + error.Message); }
            box.SetChecked(Enabled);
        });
        Checkbox = box;
        box.SetChecked(Enabled);
    }

    internal static void Sync() { try { if (Checkbox != null) Checkbox.SetChecked(Enabled); } catch (Exception error) { log?.LogWarning("OPPONENT_PETS_OPTION sync: " + error.Message); } }

#if !SOLOSTONE_RELEASE
    // Test control (state.opponentPets): the setting, what the running match's NPC owns and was started with, and the client's own pet
    // actors per side (entity, actor, PetControllerGame model, rendered), plus every PetControllerGame in the scene and the pet
    // manager's seats, so "no opponent pet" is a semantic query, not a screenshot.
    internal static JsonNode State(NativeMatch match)
    {
        bool gameplay = SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.GAMEPLAY;
        var node = new JsonNode {
            { "enabledNextMatch", Enabled }, { "checkbox", NativeOptionsPanels.RowState(Checkbox) }, { "gameplay", gameplay },
            { "owned", gameplay ? LastOwned : null }, { "shown", gameplay ? match?.OpponentPetCard : null }
        };
        if (!gameplay) return node;
        var state = GameState.Get();
        node["friendly"] = Side(state?.GetFriendlySidePlayer());
        node["opposing"] = Side(state?.GetOpposingSidePlayer());
        var controllers = UnityEngine.Object.FindObjectsByType<PetControllerGame>(FindObjectsSortMode.None);
        node["controllers"] = TestControlProtocol.List(controllers.Where(controller => controller != null).OrderBy(controller => controller.name, StringComparer.Ordinal).ThenBy(controller => controller.PetVariantId).Select(controller => (object)new JsonNode {
            { "name", controller.name }, { "variantId", controller.PetVariantId }, { "model", controller.PetObject != null ? controller.PetObject.name : null },
            { "active", controller.gameObject.activeInHierarchy } }));
        var seats = new JsonList();
        var manager = PetGameplayManager.Get();
        for (int player = 1; manager != null && player <= 2; player++)
            if (manager.TryGetPet(player, out var pet) && pet != null)
                seats.Add(new JsonNode { { "playerId", player }, { "friendly", pet.IsFriendly(false) }, { "variantId", pet.PetVariantId }, { "visible", pet.PetObject != null } });
        node["seats"] = seats;
        return node;
    }

    private static JsonNode Side(Player player)
    {
        // Explicit != null on Unity objects: `?.` skips Unity's destroyed-object check during scene teardown.
        var entity = player == null ? null : player.GetPet();
        var card = entity == null ? null : entity.GetCard();
        var actor = card != null ? card.GetActor() : null;
        var controller = actor != null ? actor.GetComponentInChildren<PetControllerGame>() : null;
        var model = controller != null ? controller.PetObject : null;
        var colliders = actor == null ? 0 : actor.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled && collider.gameObject.activeInHierarchy);
        bool rendered = model != null && model.activeInHierarchy && model.GetComponentsInChildren<Renderer>(true)
            .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
        return new JsonNode {
            { "present", entity != null }, { "entityId", entity?.GetEntityId() ?? 0 }, { "cardId", entity?.GetCardId() }, { "actor", actor != null },
            { "variantId", controller != null ? controller.PetVariantId : 0 }, { "model", model != null ? model.name : null }, { "rendered", rendered }, { "colliders", colliders }
        };
    }
#endif
}
