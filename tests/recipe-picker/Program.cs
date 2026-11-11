using System.Reflection;
using System.Xml.Serialization;
using PegasusShared;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
void Reject(Action action, string label)
{
    try { action(); } catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is IOException)
    { Check(true, label); return; }
    throw new Exception("FAIL accepted " + label);
}
var failures = new List<Exception>();
var unavailable = NativeRecipeSafety.OrDefault<List<CollectionManager.TemplateDeck>>(
    () => throw new InvalidOperationException("source engine unavailable"),
    new List<CollectionManager.TemplateDeck>(), failures.Add);
Check(unavailable.Count == 0 && failures.Count == 1, "loader exception logs and returns empty templates for Custom flow");
Check(NativeRecipeSafety.OrDefault(() => 7, 0, failures.Add) == 7 && failures.Count == 1,
    "successful patch result preserved");
var preview = new CollectionDeck();
preview.GetAllSideboards().Add("JAIL_397", null!);
NativeRecipeSafety.ResetPreview(preview);
Check(preview.GetAllSideboards().Count == 0, "preview A sideboards cleared before filling B");
preview.GetAllSideboards().Add("JAIL_831", null!);
NativeRecipeSafety.ResetPreview(preview);
Check(preview.GetAllSideboards().Count == 0, "repeated preview and alternate owner reset");
NativeRecipeSafety.ResetPreview(preview);
Check(preview.GetAllSideboards().Count == 0, "empty preview reset safe");
var projection = new LocalCustomDeck { Name = "B", Hero = "HERO_08", Cards = new() { "JAIL_397" },
    Sideboards = new() { new LocalSideboard { Source = "JAIL_397", Cards = new() { "CORE_CS2_024" } } } };
NativeRecipeSafety.AssertContents(projection, "B", "HERO_08", new[] { "JAIL_397" },
    new[] { new LocalSideboard { Source = "JAIL_397", Cards = new() { "CORE_CS2_024" } } });
Check(true, "matching committed main and sideboard contents accepted");
Reject(() => NativeRecipeSafety.AssertContents(projection, "B", "HERO_08", new[] { "JAIL_397" },
    new[] { new LocalSideboard { Source = "JAIL_397", Cards = new() { "CORE_CS2_023" } } }), "stale A sideboard rejected after B commit");
Reject(() => NativeRecipeSafety.AssertContents(projection, "B", "HERO_08", Array.Empty<string>(), projection.Sideboards),
    "missing projected main cards rejected");
Reject(() => NativeRecipeSafety.AssertContents(projection, "B", "HERO_08", new[] { "JAIL_397", "JAIL_397" }, projection.Sideboards),
    "extra projected copies rejected");
Reject(() => NativeRecipeSafety.AssertContents(projection, "B", "HERO_08", new[] { "JAIL_397" }, Array.Empty<LocalSideboard>()),
    "missing projected sideboard rejected");
foreach (int count in new[] { 0, 1, 2, 3 })
    Check(NativeRecipeTemplates.ButtonVisibility(count, 3).SequenceEqual(
        count == 0 ? new[] { false, false, false } : count == 1 ? new[] { true, false, false }
        : count == 2 ? new[] { true, true, false } : new[] { true, true, true }), "picker visible slots at " + count);
Reject(() => NativeRecipeTemplates.ButtonVisibility(4, 3), "picker capacity overflow fails closed");
Check((int)GAME_TAG.MAX_SIDEBOARD_CARDS == 2931, "shared native-save rule uses pinned client sideboard tag");
var gelbin = new LocalCustomDeck { Name = "Gelbin", Hero = "HERO_04",
    Cards = new() { "TIME_009", "TIME_009t1", "TIME_009t2" } };
NativeRecipeSafety.AssertContents(gelbin, gelbin.Name, gelbin.Hero, gelbin.Cards, Array.Empty<LocalSideboard>());
Check(true, "native Fabled main slots match committed bundle without a sideboard");
Reject(() => NativeRecipeSafety.AssertContents(gelbin, gelbin.Name, gelbin.Hero, new[] { "TIME_009" },
    new[] { new LocalSideboard { Source = "TIME_009", Cards = new() { "TIME_009t1", "TIME_009t2" } } }),
    "Fabled companions cannot be moved out of main slots");
var library = NativeRecipeLibrary.Current;
// Use a shipped Paladin recipe for the persisted-draft integration checks.
var recipe = library.Recipes.First(recipe => recipe.Class == "PALADIN");
var deckClass = (TAG_CLASS)Enum.Parse(typeof(TAG_CLASS), recipe.Class);
var templates = new NativeRecipeTemplates(library.Recipes);
var owned = new[] { recipe.Id, "removed-recipe" };
Check(templates.ForClass(Array.Empty<string>(), FormatType.FT_WILD, deckClass).Count == 0, "no entitlement has no recipes");
var list = templates.ForClass(owned, FormatType.FT_WILD, deckClass);
Check(list.Count == 1, "only owned matching class appears");
Check(templates.ForClass(owned, FormatType.FT_STANDARD, deckClass).Single() == list.Single(), "Standard and Wild share stable template identity");
Check(templates.ForClass(owned, FormatType.FT_UNKNOWN, deckClass).Count == 0, "unsupported format has no recipes");
Check(templates.ForClass(owned, FormatType.FT_WILD, TAG_CLASS.WARRIOR).Count == 0, "other class excluded");
list.Clear();
var template = templates.ForClass(owned, FormatType.FT_WILD, deckClass).Single();
Check(template.m_id < 0 && template.m_id == template.m_deckTemplateId && !template.m_isStarterDeck, "reserved synthetic identities and nonstarter mapping");
Check(new NativeRecipeTemplates(library.Recipes.Reverse()).ForClass(owned, FormatType.FT_WILD, deckClass).Single().m_id == template.m_id,
    "synthetic ID stable across library order");
Check(template.m_title == recipe.Name && template.m_description == recipe.Preview && template.m_class == deckClass
    && template.m_formatType == FormatType.FT_STANDARD, "native title preview class format mapped");
Check(template.m_cardIds.OrderBy(p => p.Key).SequenceEqual(recipe.Cards.ToDictionary(c => c.Id, c => c.Count).OrderBy(p => p.Key)), "all main cards mapped including missing");
Check(templates.Recipe(template) == recipe && templates.Recipe(new CollectionManager.TemplateDeck { m_id = template.m_id }) == null,
    "only exact cached template resolves recipe");
var sideRecipe = new DeckRecipe { Id = "sideboard-map", Name = "Sideboard", Class = "MAGE", Hero = "HERO_08", Preview = "Preview",
    Cards = new() { new RecipeCard { Id = "JAIL_397", Count = 1 } },
    Sideboards = new() { new RecipeSideboard { Owner = "JAIL_397", Cards = new() { new RecipeCard { Id = "CORE_CS2_024", Count = 2 } } } } };
var sideTemplate = NativeRecipeTemplates.Map(sideRecipe);
Check(sideTemplate.m_sideboardCardIds["JAIL_397"].SequenceEqual(new[] { "CORE_CS2_024", "CORE_CS2_024" }), "owner keyed sideboard copies mapped");
var dkRecipe = new DeckRecipe { Id = "dk-map", Class = "DEATHKNIGHT", Hero = "HERO_11", Cards = new() { new RecipeCard { Id = "CORE_RLK_063", Count = 2 } } };
var dk = NativeRecipeTemplates.Map(dkRecipe);
Check(new[] { dk.m_rune1, dk.m_rune2, dk.m_rune3 }.All(rune => rune == RuneType.RT_FROST), "three Frost runes mapped from DK requirement");

var draft = new LocalCustomDeck { Id = 910000010, SortOrder = 7, Hero = recipe.Hero, Name = "New deck" };
var choice = NativeRecipeTemplates.PrepareChoice(library, recipe, draft, owned, _ => 0);
Check(choice.Id == draft.Id && choice.SortOrder == draft.SortOrder && choice.Name == recipe.Name && choice.Hero == draft.Hero
    && choice.SourceRecipeId == recipe.Id && choice.Cards.Count == recipe.Cards.Sum(row => row.Count), "choose supplies existing draft identity and full recipe to SaveDeckFromRecipe");
Check(draft.Cards.Count == 0 && draft.SourceRecipeId == null, "prepare does not mutate draft");
var premiumChoice = NativeRecipeTemplates.PrepareChoice(library, recipe, draft, owned, _ => 0,
    (id, premium) => premium == 1 ? 2 : 0);
Check(premiumChoice.CardEntries.All(card => card.Premium == 1)
    && premiumChoice.Sideboards.SelectMany(board => board.CardEntries).All(card => card.Premium == 1),
    "native recipe picker assigns golden-only owned copies including sideboards");
var bothChoice = NativeRecipeTemplates.PrepareChoice(library, recipe, draft, owned, _ => 0,
    (id, premium) => premium <= 1 ? 2 : 0);
Check(bothChoice.CardEntries.All(card => card.Premium == 1)
    && bothChoice.Sideboards.SelectMany(board => board.CardEntries).All(card => card.Premium == 1),
    "native recipe picker prefers Golden over Normal when both are owned");

Reject(() => NativeRecipeTemplates.PrepareChoice(library, recipe, draft, Array.Empty<string>(), _ => 0), "unowned choose rejected");
Reject(() => NativeRecipeTemplates.PrepareChoice(library, recipe, new LocalCustomDeck { Id = draft.Id, Hero = "HERO_01" }, owned, _ => 0), "wrong class draft rejected");
var text = string.Join("\n", NativeRecipeGuideText.Sections(recipe));
Check(new[] { recipe.Guide.Strategy, recipe.Guide.Mulligan }.All(text.Contains) && NativeRecipeGuideText.Sections(recipe).Length == 2,
    "guide is exactly the strategy and mulligan pages");
Check(NativeRecipeGuideText.ForSavedDeck(library.Recipes, choice) == recipe
    && NativeRecipeGuideText.ForSavedDeck(library.Recipes, draft) == null
    && NativeRecipeGuideText.ForSavedDeck(library.Recipes, new LocalCustomDeck { SourceRecipeId = "removed" }) == null, "saved guide resolves provenance and handles absent recipes");

// Deck Guide coverage (deck picker button): every recipe the library offers has both guide pages, and every shop recipe and
// ready-made deck product that is offered (not withheld) resolves to one of them.
Check(library.Recipes.Count > 0 && library.Recipes.All(row => NativeRecipeGuideText.Sections(row).Length == NativeRecipeGuideText.Headings.Length
    && NativeRecipeGuideText.Sections(row).All(section => !string.IsNullOrWhiteSpace(section))),
    "every offered recipe has a non-empty two-page guide: " + library.Recipes.Count);
var offeredProducts = LocalShop.Default.Products.Where(row => (row.Kind == "Recipe" || row.Kind == "Deck") && !LocalShop.Default.Withheld(row)).ToList();
Check(offeredProducts.Count > 0 && offeredProducts.All(row => library.Recipes.Any(r => r.Id == row.RecipeId)),
    "every offered shop recipe and ready-made deck product has a guide: " + offeredProducts.Count);
// The card page lays the description out as the header line plus at most MaxWrappedLines wrapped lines (the cards sit below that block).
Check(library.Recipes.All(row => Enumerable.Range(0, NativeRecipeGuideText.Headings.Length)
        .All(page => NativeRecipeGuideText.Wrap(NativeRecipeGuideText.Sections(row)[page]).Count <= NativeRecipeGuideText.MaxWrappedLines)),
    "every guide page wraps to at most " + NativeRecipeGuideText.MaxWrappedLines + " lines: " + library.Recipes.Max(row => Enumerable.Range(0, 2)
        .Max(page => NativeRecipeGuideText.Wrap(NativeRecipeGuideText.Sections(row)[page]).Count)));
foreach (var row in library.Recipes.Take(3))
    for (int page = 0; page < NativeRecipeGuideText.Headings.Length; page++)
        Check(NativeRecipeGuideText.PageHeader(row, page) == row.Name + ": " + NativeRecipeGuideText.Headings[page]
            && NativeRecipeGuideText.PageBody(row, page).StartsWith("(" + (page + 1) + "/2)\n\n")
            && NativeRecipeGuideText.PageBody(row, page).EndsWith(NativeRecipeGuideText.Sections(row)[page]), "page text " + row.Id + " " + page);
Check(NativeDeckGuideText.DialogButtonId(NativeDeckGuideText.PopupId(recipe.Id, 2), "confirm") == "guide.next"
    && NativeDeckGuideText.DialogButtonId(NativeDeckGuideText.PopupId(recipe.Id, 1), "cancel") == "guide.close"
    && NativeDeckGuideText.DialogButtonId("LOCAL_SOLOSTONE_HELP", "cancel") == null && NativeDeckGuideText.DialogButtonId(null, "cancel") == null
    && NativeDeckGuideText.DialogButtonId(NativeDeckGuideText.PopupId(recipe.Id, 0), "okay") == null, "guide popup buttons have semantic ids only on guide popups");

string root = Path.Combine(Path.GetTempPath(), "recipe-picker-" + Guid.NewGuid());
Directory.CreateDirectory(root);
string path = Path.Combine(root, "profile.xml");
try
{
    using (var profile = new LocalProfile(path)) profile.CreateDraft("Native draft", recipe.Hero);
    // Seed a legitimate milestone recipe entitlement; purchases are owned by the concurrent shop work.
    var serializer = new XmlSerializer(typeof(LocalProfileData));
    LocalProfileData data;
    using (var stream = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(stream)!;
    data.OwnedRecipes.Add(recipe.Id);
    data.MilestoneCatchUp = new LocalMilestoneCatchUp { Id = "picker-fixture", TierRewards = new() {
        new LocalTierReward { MilestoneId = "picker-fixture", Grants = new() { new LocalTierGrant { Kind = LocalTierGrantKind.Recipe, Ref = recipe.Id, Count = 1 } } } } };
    using (var stream = File.Create(path)) serializer.Serialize(stream, data);
    long id;
    using (var profile = new LocalProfile(path))
    {
        var original = profile.CustomDecks.Single(); id = original.Id;
        var pending = NativeRecipeTemplates.PrepareChoice(library, recipe, original, profile.OwnedRecipes, _ => 0);
        byte[] before = File.ReadAllBytes(path); long next = profile.NextCustomDeckId;
        foreach (string stage in new[] { "write", "flush", "replace" })
        {
            bool reached = false;
            profile.CommitFailure = at => { if (at == stage) { reached = true; throw new IOException("injected " + stage); } };
            Reject(() => profile.SaveDeckFromRecipe(recipe.Id, pending, id), stage + " failure surfaced");
            Check(reached && File.ReadAllBytes(path).SequenceEqual(before) && profile.GetDraft(id).Cards.Count == 0
                && profile.NextCustomDeckId == next, stage + " failure preserves draft bytes and allocator");
        }
        profile.CommitFailure = null;
        var saved = profile.SaveDeckFromRecipe(recipe.Id, pending, id);
        Check(saved.Id == id && saved.SourceRecipeId == recipe.Id && saved.Cards.SequenceEqual(pending.Cards)
            && profile.CustomDecks.Count == 1 && profile.NextCustomDeckId == next, "recipe replaces draft atomically without extra slot or ID");
        Check(!profile.IsDeckComplete(id), "missing recipe cards remain saved and block play");
        profile.SaveDeckFromRecipe(recipe.Id, pending, id);
        Check(profile.CustomDecks.Count == 1 && profile.NextCustomDeckId == next, "retry cannot duplicate deck");
        profile.SaveDraft(id, saved.Name, saved.Hero, saved.Cards.Skip(1), saved.SortOrder, saved.Sideboards);
        Check(profile.GetDraft(id).SourceRecipeId == recipe.Id, "ordinary editor save retains recipe provenance");
        Reject(() => profile.SaveDeckFromRecipe(recipe.Id, pending, id), "choosing again cannot overwrite an edited deck");
        // Deck Guide on the picker: edits and renames keep the guide (provenance, not a card-list match); a deck hand-built from the same cards has none.
        profile.SaveDraft(id, "Renamed guide deck", saved.Hero, saved.Cards.Skip(1), saved.SortOrder, saved.Sideboards);
        Check(profile.GetDraft(id).Name == "Renamed guide deck" && NativeRecipeGuideText.ForSavedDeck(library.Recipes, profile.GetDraft(id)) == recipe,
            "renamed and edited recipe deck keeps its guide");
        var other = profile.CreateDraft("Other", recipe.Hero);
        Check(NativeRecipeGuideText.ForSavedDeck(library.Recipes, profile.GetDraft(other.Id)) == null, "fresh player-built deck has no guide");
        profile.SaveDraft(other.Id, "Hand built copy", recipe.Hero, pending.Cards, other.SortOrder, pending.Sideboards);
        Check(profile.GetDraft(other.Id).Cards.SequenceEqual(pending.Cards) && NativeRecipeGuideText.ForSavedDeck(library.Recipes, profile.GetDraft(other.Id)) == null,
            "deck hand-built from the same cards has no guide (no card-list matching)");
        Reject(() => profile.SaveDeckFromRecipe(recipe.Id, pending, other.Id), "mismatched target rejected");
        Reject(() => profile.SaveDeckFromRecipe("not-owned", pending, id), "profile enforces entitlement");
        while (profile.CustomDecks.Count < LocalProfile.MaximumCustomDecks) profile.CreateDraft("Capacity", recipe.Hero);
        var last = profile.CustomDecks.Last(); long atCapacity = profile.NextCustomDeckId;
        var lastChoice = NativeRecipeTemplates.PrepareChoice(library, recipe, last, profile.OwnedRecipes, _ => 0);
        profile.SaveDeckFromRecipe(recipe.Id, lastChoice, last.Id);
        Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks && profile.NextCustomDeckId == atCapacity,
            "last draft below the cap can choose recipe without allocating a slot past it");
    }
    using var reopened = new LocalProfile(path);
    Check(reopened.GetDraft(id).SourceRecipeId == recipe.Id && reopened.GetDraft(id).Cards.Count == choice.Cards.Count - 1,
        "incomplete recipe edit and guide provenance survive restart");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{checks} checks passed");
