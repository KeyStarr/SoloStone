using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

// Nefarian's Creation (CATA_470t1) takes the art of the first Dragon picked for it (issue #119). The engine publishes the card as
// CATA_470t1 with the dragon's dbf id in MODULAR_ENTITY_PART_1 (tag 471); the client draws a card's portrait from the CardDef of the
// entity's own card id, so this postfix swaps in the portrait texture of the part's CardDef after the actor has set its own.
// Name, text, sounds and effects stay the Creation's. The patched methods (Actor.UpdatePortraitTexture, HistoryCard.LoadTile) are optional:
// a miss logs one warning and leaves the stock art. The client members this file names directly (see its rows in client-bindings.txt)
// are hard rows: a retail rename of one refuses the launch.
internal static class NativeCreationPortrait
{
    internal const string CreationId = "CATA_470t1";
    internal const int FirstPartTag = 471;
    private static ManualLogSource log;
    private static readonly Dictionary<(int, TAG_PREMIUM), Texture> Textures = new Dictionary<(int, TAG_PREMIUM), Texture>();
    // Kept alive for the session: the texture lease is released with its CardDef.
    private static readonly List<DefLoader.DisposableCardDef> Defs = new List<DefLoader.DisposableCardDef>();
    private static readonly List<object> Handles = new List<object>();
    private static MethodInfo getPortraitTexture, getPortraitMaterial;
    // A golden, diamond or signature actor shows its CardDef's premium portrait MATERIAL, which SetPortraitTexture leaves alone
    // (Actor.UpdatePortraitMaterials loads it and clears m_usingInitialPortraitMaterial), so the part's material is swapped in too.
    private static FieldInfo usingInitialMaterial;
    private static readonly Dictionary<(int, TAG_PREMIUM), Material> Materials = new Dictionary<(int, TAG_PREMIUM), Material>();
    private static readonly HashSet<(int, TAG_PREMIUM)> FailedMaterials = new HashSet<(int, TAG_PREMIUM)>();
    private static readonly HashSet<(int, TAG_PREMIUM)> Failed = new HashSet<(int, TAG_PREMIUM)>();
    private static readonly HashSet<int> Warned = new HashSet<int>();

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        try
        {
            var original = AccessTools.Method(typeof(Actor), "UpdatePortraitTexture") ?? throw new MissingMethodException("Actor", "UpdatePortraitTexture");
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(NativeCreationPortrait), nameof(UseFirstDragonPortrait)));
            // Cosmetic: a client rename must never refuse a launch over a portrait (the stock art stays).
            ClientChecks.NoteSelfAdapting(original);
        }
        catch (Exception error) { log?.LogWarning("creation portrait hook unavailable: " + error.Message); }
        // Optional like the hooks: without the field a golden Creation keeps the template's golden art and logs this once.
        try { usingInitialMaterial = AccessTools.Field(typeof(Actor), "m_usingInitialPortraitMaterial") ?? throw new MissingFieldException("Actor", "m_usingInitialPortraitMaterial"); }
        catch (Exception error) { log?.LogWarning("creation premium portrait hook unavailable: " + error.Message); }
        InstallTile(harmony);
    }

    // The history bar tile copies the entity's own portrait into its material without going through UpdatePortraitTexture, so its
    // init info is patched before HistoryCard.LoadTile reads it. Found by name: a client without it keeps the stock tile.
    private static FieldInfo tileEntity, tilePortrait;
    private static void InstallTile(Harmony harmony)
    {
        try
        {
            var card = AccessTools.TypeByName("HistoryCard") ?? throw new MissingMemberException("HistoryCard");
            var load = AccessTools.Method(card, "LoadTile") ?? throw new MissingMethodException("HistoryCard", "LoadTile");
            var info = load.GetParameters().Single().ParameterType;
            tileEntity = AccessTools.Field(info, "m_entity") ?? throw new MissingFieldException(info.Name, "m_entity");
            tilePortrait = AccessTools.Field(info, "m_portraitTexture") ?? throw new MissingFieldException(info.Name, "m_portraitTexture");
            harmony.Patch(load, prefix: new HarmonyMethod(typeof(NativeCreationPortrait), nameof(UseFirstDragonTile)));
            ClientChecks.NoteSelfAdapting(load);
        }
        catch (Exception error) { log?.LogWarning("creation history tile hook unavailable: " + error.Message); }
    }

    private static void UseFirstDragonTile(object __0)
    {
        try
        {
            if (__0 == null || !(tileEntity.GetValue(__0) is Entity entity)) return;
            var texture = FirstDragonPortrait(entity, (TAG_PREMIUM)entity.GetTag(GAME_TAG.PREMIUM));
            if (texture != null) tilePortrait.SetValue(__0, texture);
        }
        catch (Exception error) { Fail(0, TAG_PREMIUM.NORMAL, error); }
    }

    private static Texture FirstDragonPortrait(Entity entity, TAG_PREMIUM premium)
    {
        int part = PartDbfId(entity.GetCardId(), id => entity.GetTag((GAME_TAG)id));
        return part == 0 ? null : PortraitOf(part, premium);
    }

    // The dragon the Creation borrows from: its part tag when the entity is the Creation, else 0.
    internal static int PartDbfId(string cardId, Func<int, int> tag) => cardId == CreationId ? tag(FirstPartTag) : 0;

    private static void UseFirstDragonPortrait(Actor __instance)
    {
        try
        {
            var entity = __instance?.GetEntity();
            if (entity == null) return;
            var texture = FirstDragonPortrait(entity, __instance.GetPremium());
            if (texture != null) __instance.SetPortraitTexture(texture);
            UseFirstDragonPremiumMaterial(__instance, entity, texture);
        }
        catch (Exception error) { Fail(0, TAG_PREMIUM.NORMAL, error); }
    }

    // A premium actor drawing the CardDef's golden material (not the initial one) gets the first part's golden material of the same
    // premium. Diamond asks for the golden material, as Actor.UpdatePortraitMaterials does.
    private static void UseFirstDragonPremiumMaterial(Actor actor, Entity entity, Texture partTexture)
    {
        var premium = actor.GetPremium();
        if (premium == TAG_PREMIUM.NORMAL || usingInitialMaterial == null || (bool)usingInitialMaterial.GetValue(actor)) return;
        int part = PartDbfId(entity.GetCardId(), id => entity.GetTag((GAME_TAG)id));
        if (part == 0) return;
        var material = MaterialOf(part, premium == TAG_PREMIUM.DIAMOND ? TAG_PREMIUM.GOLDEN : premium);
        if (material == null) return;
        // As Actor.TryApplyPremiumPortraitTexture: a material authored without a texture takes the CardDef's premium portrait texture
        // (the actor's premium). With neither, the template's golden art stays rather than a blank portrait.
        if (material.mainTexture == null)
        {
            if (partTexture == null)
            {
                if (Warned.Add(part * 8 + (int)premium + 4096)) log?.LogWarning("creation premium portrait: part " + part + " has no golden texture, keeping the template art");
                return;
            }
            material.mainTexture = partTexture;
        }
        actor.SetPortraitMaterial(material, -1);
    }

    private static Material MaterialOf(int dbfId, TAG_PREMIUM premium)
    {
        var key = (dbfId, premium);
        if (Materials.TryGetValue(key, out var known)) { if (known != null) return known; Materials.Remove(key); }
        if (FailedMaterials.Contains(key)) return null;
        try
        {
            var def = DefLoader.Get()?.GetCardDef(dbfId);
            if (def?.CardDef == null) { def?.Dispose(); throw new InvalidOperationException("part CardDef unavailable"); }
            Defs.Add(def);
            object handle = (getPortraitMaterial ?? (getPortraitMaterial = typeof(CardDef).GetMethod("GetPortraitMaterial", new[] { typeof(TAG_PREMIUM) }))).Invoke(def.CardDef, new object[] { premium });
            Handles.Add(handle);
            var material = handle?.GetType().GetProperty("Asset", BindingFlags.Public | BindingFlags.Instance)?.GetValue(handle, null) as Material;
            if (material == null) throw new InvalidOperationException("part has no " + premium + " portrait material");
            Materials[key] = material;
            log?.LogInfo("creation premium portrait: part " + dbfId + " " + premium);
            return material;
        }
        catch (Exception error)
        {
            FailedMaterials.Add(key);
            log?.LogWarning("creation premium portrait unavailable for part " + dbfId + " " + premium + ": " + error.Message);
            return null;
        }
    }

    private static Texture PortraitOf(int dbfId, TAG_PREMIUM premium)
    {
        var key = (dbfId, premium);
        // A texture Unity has destroyed (Unity null check) is dropped and loaded again.
        if (Textures.TryGetValue(key, out var known)) { if (known != null) return known; Textures.Remove(key); }
        if (Failed.Contains(key)) return null;
        try
        {
            var def = DefLoader.Get()?.GetCardDef(dbfId);
            if (def?.CardDef == null) { def?.Dispose(); Fail(dbfId, premium, new InvalidOperationException("part CardDef unavailable")); return null; }
            Defs.Add(def);
            // AssetHandle<Texture> lives in an assembly the plugin does not reference, so it is read by reflection.
            object handle = (getPortraitTexture ?? (getPortraitTexture = typeof(CardDef).GetMethod("GetPortraitTexture",
                new[] { typeof(TAG_PREMIUM), typeof(bool), typeof(bool) }))).Invoke(def.CardDef, new object[] { premium, true, false });
            Handles.Add(handle);
            // Not op_Implicit: the handle has two (to the asset and to bool), so selecting one by its parameter is ambiguous.
            var texture = handle?.GetType().GetProperty("Asset", BindingFlags.Public | BindingFlags.Instance)?.GetValue(handle, null) as Texture;
            if (texture == null) { Fail(dbfId, premium, new InvalidOperationException("part has no portrait texture")); return null; }
            Textures[(dbfId, premium)] = texture;
            log?.LogInfo("creation portrait: part " + dbfId + " " + premium);
            return texture;
        }
        catch (Exception error) { Fail(dbfId, premium, error); return null; }
    }

    private static void Fail(int dbfId, TAG_PREMIUM premium, Exception error)
    {
        if (dbfId != 0) Failed.Add((dbfId, premium));
        if (!Warned.Add(dbfId * 8 + (int)premium)) return;
        log?.LogWarning("creation portrait unavailable" + (dbfId == 0 ? "" : " for part " + dbfId) + ": " + error.Message);
    }
}
