using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using HarmonyLib;

internal static partial class PoweredUpGlowTests
{
    private static void RecipeGhostFailure()
    {
        // Load the built plugin: the headless test assembly does not compile NativeDeckEditor.cs.
        Assembly ResolvePluginReference(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            foreach (string directory in new[] { "references/bepinex", "references/game" })
            {
                string path = Path.GetFullPath(Path.Combine(directory, name + ".dll"));
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }
        AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginReference;
        FieldInfo activeField = null;
        object priorActive = null;
        try
        {
            string artifactPlugin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../OfflineProbe/release/HSNativeUIProbe.dll"));
            var probe = Assembly.LoadFrom(File.Exists(artifactPlugin) ? artifactPlugin : Path.GetFullPath("plugin/bin/Release/net48/HSNativeUIProbe.dll"))
                .GetType("NativeUIProbe", throwOnError: true);
            activeField = AccessTools.Field(probe, "active");
            priorActive = activeField.GetValue(null);
            activeField.SetValue(null, true);
            // Headless references cannot initialize SceneMgr; prove the native failure is present first.
            Exception sceneFailure = null;
            try { AccessTools.Method(probe, "CurrentCollectionScene").Invoke(null, null); }
            catch (TargetInvocationException error) { sceneFailure = error.InnerException; }
            Check(sceneFailure != null, "headless ghost fixture fails native scene lookup");
            AccessTools.Method(probe, "RestorePersonalDeckGhost").Invoke(null, new object[] { null });
        }
        finally
        {
            if (activeField != null && priorActive != null) activeField.SetValue(null, priorActive);
            AppDomain.CurrentDomain.AssemblyResolve -= ResolvePluginReference;
        }
    }

    [GroupProof("native-recipe-editor-presentation", ProofKind.Native)]
    private static void RecipeEditorPresentation()
    {
        string dir = Path.Combine(Path.GetTempPath(), "recipe-editor-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            RecipeGhostFailure();
            var saved = new LocalCustomDeck { Id = 910000010, Hero = "HERO_08", Name = "Missing copies",
                Cards = new List<string> { "CS2_182", "CS2_182", "CS2_179" } };
            var deck = B18Deck(saved, Array.Empty<string>());
            foreach (var row in new[] { ("CS2_182", true), ("CS2_182", false), ("CS2_179", false) })
            {
                var slot = new CollectionDeckSlot { CardID = row.Item1, Owned = row.Item2 };
                slot.SetCount(1, TAG_PREMIUM.NORMAL); deck.GetSlots().Add(slot);
            }
            // Loading already preserves all copies; projection must retain the ownership split.
            NativeUIProbe.ProjectMissingDraftCards(deck, saved);
            Check(NativeUIProbe.IsProjectedDeckSlot(deck, deck.GetSlots()[1]),
                "reopened missing tile belongs to projected local deck");
            Check(deck.GetSlots().Select(slot => slot.Owned).SequenceEqual(new[] { true, false, false })
                && deck.GetCardsWithCardID().Count == 3, "projection preserves one owned and two missing copies");
            Check(!NativeUIProbe.IsProjectedDeckSlot(deck, new CollectionDeckSlot { CardID = "CS2_179", Owned = false })
                && !NativeUIProbe.IsProjectedDeckSlot(null, deck.GetSlots()[1])
                && !NativeUIProbe.IsProjectedDeckSlot(deck, null), "unrelated, absent and null slots excluded");
            var missing = deck.GetSlots()[1];
            missing.m_entityDefOverride = new EntityDef();
            Check(ReferenceEquals(NativeUIProbe.LocalDeckSuggestionSlot(deck, missing), missing),
                "ordinary missing slot keeps native replacement target");
            Check(NativeUIProbe.LocalDeckSuggestionSlot(deck, null) == null, "empty deck suggestion target retained");
            B18SideboardFixture(deck, new[] { new LocalSideboard { Source = "JAIL_397", Cards = new List<string> { "CS2_179" } } });
            var sideSlot = deck.GetSideboard("JAIL_397").GetSlots()[0]; sideSlot.Owned = false;
            Check(NativeUIProbe.IsProjectedDeckSlot(deck, sideSlot) && !sideSlot.Owned,
                "attached missing sideboard tile belongs to same local projection");

            // Legitimate entitlement fixture, then exercise the exact native save boundary.
            string path = Path.Combine(dir, "profile.xml");
            using (var profile = new LocalProfile(path)) profile.CreateDraft("Seed", "HERO_08");
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var stream = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(stream);
            string recipeId = LocalShop.Default.Library.Recipes.First(recipe => recipe.Class == "MAGE").Id;
            data.OwnedRecipes.Add(recipeId);
            data.MilestoneCatchUp = new LocalMilestoneCatchUp { Id = "editor-fixture", TierRewards = new List<LocalTierReward> {
                new LocalTierReward { MilestoneId = "editor-fixture", Grants = new List<LocalTierGrant> {
                    new LocalTierGrant { Kind = LocalTierGrantKind.Recipe, Ref = recipeId, Count = 1 } } } } };
            using (var stream = File.Create(path)) serializer.Serialize(stream, data);
            long id;
            using (var profile = new LocalProfile(path))
            {
                var recipeDeck = profile.SaveDeckFromRecipe(recipeId, saved); id = recipeDeck.Id;
                var crafted = B18Deck(recipeDeck, recipeDeck.Cards);
                var ghostBaseline = B18Deck(recipeDeck, recipeDeck.Cards);
                foreach (var slot in ghostBaseline.GetSlots()) slot.Owned = false;
                foreach (var slot in crafted.GetSlots()) slot.Owned = true;
                NativeUIProbe.CommitLocalDraft(profile, crafted, ghostBaseline);
                Check(profile.GetDraft(id).SourceRecipeId == recipeId
                    && profile.GetDraft(id).Cards.SequenceEqual(recipeDeck.Cards),
                    "native save after ghost becomes owned preserves every card and recipe provenance");
                var replacement = new[] { "CS2_182", "CS2_182", "CS2_171" };
                var working = B18Deck(recipeDeck, replacement);
                var prior = B18Deck(recipeDeck, recipeDeck.Cards);
                byte[] before = File.ReadAllBytes(path);
                profile.CommitFailure = stage => { if (stage == "replace") throw new IOException("replacement write failed"); };
                bool refused = false;
                try { NativeUIProbe.CommitLocalDraft(profile, working, prior); } catch (IOException) { refused = true; }
                Check(refused && File.ReadAllBytes(path).SequenceEqual(before)
                    && profile.GetDraft(id).SourceRecipeId == recipeId && profile.GetDraft(id).Cards.SequenceEqual(recipeDeck.Cards),
                    "failed native replacement preserves saved bytes, cards and recipe provenance");
                profile.CommitFailure = null;
                NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(profile.GetDraft(id).SourceRecipeId == recipeId && profile.GetDraft(id).Cards.SequenceEqual(replacement),
                    "native replacement save retains SourceRecipeId");
            }
            using (var profile = new LocalProfile(path))
                Check(profile.GetDraft(id).SourceRecipeId == recipeId
                    && profile.GetDraft(id).Cards.SequenceEqual(new[] { "CS2_182", "CS2_182", "CS2_171" }),
                    "native replacement edit and recipe provenance survive restart");
            Console.WriteLine("PASS recipe editor ownership mapping, replacement targets and native save provenance");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
