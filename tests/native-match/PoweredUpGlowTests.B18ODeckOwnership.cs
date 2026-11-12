using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using SabberStoneCore.Model;

// Only the headless assembly supplies this context; production uses NativeUIProbe's real launch/profile state.
internal static partial class NativeUIProbe
{
    internal static bool active;
    internal static LocalProfile Profile;
}

internal static partial class PoweredUpGlowTests
{
    private static readonly Dictionary<string, EntityDef> B18NativeDefinitions = new Dictionary<string, EntityDef>();
    private static bool B18Definition(CollectionDeckSlot __instance, ref EntityDef __result)
    { __result = B18NativeDefinitions[__instance.CardID]; return false; }
    private static bool B18Insert(CollectionDeck __instance, string __0, TAG_PREMIUM __1, bool __2, int __3, ref CollectionDeckSlot __result)
    {
        // Unity's sorter references a stripped Logger type. Preserve the native ownership argument and real InsertSlot.
        var slot = new CollectionDeckSlot { CardID = __0, Owned = __2 };
        slot.SetCount(__3, __1);
        bool inserted = (bool)AccessTools.Method(typeof(CollectionDeck), "InsertSlot").Invoke(__instance,
            new object[] { __instance.GetSlots().Count, slot });
        __result = inserted ? slot : null;
        return false;
    }
    private static bool B18Collectible(string __0, ref bool __result)
    { __result = Cards.FromId(__0).Collectible; return false; }
    private static bool B18NoBans(ref bool __result) { __result = false; return false; }
    // Offline the client has no set-rotation timings, so no card reads as Wild-only in the Standard local deck.
    private static bool B18NotWild(ref bool __result) { __result = false; return false; }

    [GroupProof("native-b18-o-deck-ownership", ProofKind.Native)]
    private static void B18ODeckOwnership()
    {
        string dir = Path.Combine(Path.GetTempPath(), "b18-contraband-ownership-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var harmony = new Harmony("offline.tests.b18.contraband");
        var rulesField = AccessTools.Field(typeof(DeckRuleset), "s_FormatRulesets");
        object originalRules = rulesField.GetValue(null);
        try
        {
            using var profile = new LocalProfile(Path.Combine(dir, "profile.xml"));
            NativeUIProbe.Profile = profile;
            var saved = profile.CreateDraft("Unowned contraband", "HERO_05");
            string[] ids = Cards.All.Where(card => SabberStoneCore.Actions.DeckbuildingRules.Eligible("JAIL_831",card,SabberStoneCore.Enums.FormatType.FT_STANDARD)
                && LocalCardCatalog.InActiveMeta(card) && (!profile.Collection.TryGetValue(card.Id,out int count) || count==0))
                .OrderBy(card=>card.Id,StringComparer.Ordinal).Take(3).Select(card=>card.Id).ToArray();
            Check(ids.Length==3,"three active unowned contraband choices");
            Check(ids.All(id => !profile.Collection.TryGetValue(id, out int count) || count == 0), "fresh profile owns none of the contraband fixture");
            var main = B18Deck(saved, new[] { "JAIL_831" });
            B18SideboardFixture(main, new[] { new LocalSideboard { Source = "JAIL_831", Cards = new List<string>() } });
            var side = main.GetSideboard("JAIL_831");
            var data = new Hearthstone.DataModels.DeckSideboardDataModel { OwnerCardId = "JAIL_831", MaxCards = 3, MinCards = 3 };
            AccessTools.Field(typeof(SideboardDeck), "<DataModel>k__BackingField").SetValue(side, data);
            foreach (string id in ids)
            {
                var definition = new EntityDef();
                definition.SetCardId(id);
                foreach (var tag in Cards.FromId(id).Tags) definition.SetTag((int)tag.Key, tag.Value);
                B18NativeDefinitions.Add(id, definition);
                Check(!side.CanCardBeAddedAsOwned(id, TAG_PREMIUM.NORMAL), "native collection has no owned/trial copy: " + id);
            }
            // Replace Unity definition/localization and DBF services, keeping native add/slot/ownership/validation code real.
            void Patch(Type type, string target, string prefix) => harmony.Patch(AccessTools.Method(type, target),
                prefix: new HarmonyMethod(typeof(PoweredUpGlowTests), prefix));
            Patch(typeof(CollectionDeckSlot), "GetEntityDef", nameof(B18Definition));
            Patch(typeof(CollectionDeck), "InsertSlotWithCard", nameof(B18Insert));
            Patch(typeof(GameUtils), "IsCardCollectible", nameof(B18Collectible));
            Patch(typeof(GameUtils), "IsBanned", nameof(B18NoBans));
            harmony.Patch(AccessTools.Method(typeof(GameUtils), "IsWildCard", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(PoweredUpGlowTests), nameof(B18NotWild)));
            var rules = new DeckRuleset();
            AccessTools.Field(typeof(DeckRuleset), "m_rules").SetValue(rules, new List<DeckRule>());
            object formats = Activator.CreateInstance(rulesField.FieldType);
            formats.GetType().GetMethod("Add").Invoke(formats, new object[] { NativeUIProbe.LocalDeckFormat, rules });
            rulesField.SetValue(null, formats);
            // Owner 2026-10-30 (retail CanCardBeAddedAsOwned): no contraband ownership prefix exists, so an active local
            // session keeps native ownership and unowned contraband stays unowned and invalid.
            Check(AccessTools.Method(typeof(NativeUIProbe), "InitializeContrabandOwnership") == null
                && AccessTools.Method(typeof(NativeUIProbe), "LocalContrabandOwnership") == null, "no virtual contraband ownership hook");
            // Live 2026-10-30: offline subset 19499 is empty, so the plugin supplies membership through Harmony prefixes on
            // DeckRule_SideboardSubset; their argument names must keep binding to the pinned client's parameters.
            foreach (var pair in new[] { ("IsInAllowedSubset", "LocalSideboardSubsetMember"), ("IsDeckValid", "LocalSideboardSubsetDeckValid") })
            {
                var target = AccessTools.Method(typeof(DeckRule_SideboardSubset), pair.Item1);
                var prefix = AccessTools.Method(typeof(NativeUIProbe), pair.Item2);
                var names = target.GetParameters().Select(parameter => parameter.Name).ToArray();
                Check(prefix != null && prefix.GetParameters().All(parameter => parameter.Name.StartsWith("__") || names.Contains(parameter.Name)),
                    "sideboard subset prefix binds to pinned parameters: " + pair.Item1 + "(" + string.Join(",", names) + ")");
            }
            NativeUIProbe.active = true;
            foreach (string id in ids)
            {
                Check(!side.CanCardBeAddedAsOwned(id, TAG_PREMIUM.NORMAL), "active context keeps native contraband ownership: " + id);
                Check(side.AddCard(id, TAG_PREMIUM.NORMAL, true, B18NativeDefinitions[id]), "native unowned add fixture");
            }
            Check(side.GetSlots().All(slot => !slot.Owned) && main.GetInvalidSideboardCardCount(null) == 3,
                "real native add constructs three unowned invalid contraband slots");
            Check(!side.CanCardBeAddedAsOwned(ids[0], TAG_PREMIUM.GOLDEN), "no virtual premium ownership");
            var persisted = NativeUIProbe.CommitLocalDraft(profile, main, B18Deck(saved, Array.Empty<string>()));
            Check(persisted.Sideboards.Single().Cards.OrderBy(id => id).SequenceEqual(ids)
                && ids.All(id => !profile.Collection.TryGetValue(id, out int count) || count == 0), "draft save keeps unowned choices without granting collection copies");
            NativeUIProbe.active = false; NativeUIProbe.Profile = null;
            profile.Dispose();
            using var reloaded = new LocalProfile(Path.Combine(dir, "profile.xml"));
            Check(reloaded.GetDraft(saved.Id).Sideboards.Single().Cards.SequenceEqual(ids)
                && ids.All(id => !reloaded.Collection.TryGetValue(id, out int count) || count == 0), "restart preserves choices without collection grants");
            Console.WriteLine("PASS Underbelly contraband keeps native ownership: unowned invalid slots, draft save without grants");
        }
        finally
        {
            NativeUIProbe.active = false; NativeUIProbe.Profile = null;
            harmony.UnpatchAll(harmony.Id);
            rulesField.SetValue(null, originalRules);
            B18NativeDefinitions.Clear();
            Directory.Delete(dir, true);
        }
    }
}
