using System.Text.Json.Nodes;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

int checks = 0;
void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL: " + label);
    checks++;
    Console.WriteLine("PASS " + label);
}
void Reject(Action action, string label, string error = null)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException || ex is InvalidOperationException || ex is ArgumentException)
    { Check(error == null || ex.Message.Contains(error, StringComparison.Ordinal), label + (error == null ? "" : ": " + error)); return; }
    throw new Exception("FAIL: accepted " + label);
}
string Words(int count) => string.Join(" ", Enumerable.Repeat("word", count));
string path = Path.Combine(AppContext.BaseDirectory, "data/shop/recipes-v1.json");
string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/rules-v1.json"));

RecipeLibrary Fresh() => RecipeLibrary.LoadText(json);
var shipped = RecipeLibrary.Load(path);
Check(shipped.Validate().Count == 0, "shipped library validates");
Check(shipped.Recipes.Count + shipped.Withheld.Count == 72, "all 72 shipped recipes accounted for");
Check(shipped.Withheld.All(r => r.Pending != null && r.Pending.Waiting && r.Pending.Fallback == null
    && shipped.IsWithheld(r.Id) && !shipped.HasRecipe(r.Id)), "withheld recipes wait without a fallback and are not active");
Check(shipped.Recipes.All(r => !shipped.IsWithheld(r.Id)), "active recipes are not withheld");
var beRecipes = shipped.Recipes.Concat(shipped.Withheld).Where(r => r.Pending != null
    && r.Pending.Needs.Any(need => need.StartsWith("BE_"))).ToArray();
Check(beRecipes.Length == 11, "all eleven BE-dependent published recipes accounted for");
foreach (var recipe in beRecipes)
{
    bool admitted = recipe.Pending.Needs.All(need => LocalCardCatalog.InActiveMeta(Cards.FromId(need)));
    Check(recipe.Pending.Waiting != admitted && (admitted
        ? recipe.Pending.Needs.All(need => recipe.Cards.Any(card => card.Id == need))
        : recipe.Pending.Fallback == null ? shipped.IsWithheld(recipe.Id)
        : recipe.Cards.All(card => !recipe.Pending.Needs.Contains(card.Id))),
        recipe.Id + " resolves published cards or fallback against the installed catalog");
}
var casino = shipped.Recipes.Single(r => r.Id == "casino-mage");
bool arrivalAdmitted = LocalCardCatalog.InActiveMeta(Cards.FromId("BE_EVENT_101"));
Check(casino.Cards.Single(card => card.Id == (arrivalAdmitted ? "BE_EVENT_101" : "JAIL_986")).Count == 2
    && casino.Pending.Waiting != arrivalAdmitted && (arrivalAdmitted || casino.Source.Changes.Contains("JAIL_986")),
    "Casino Mage switches two Forger copies to Arrivals when admitted");
// A newly registered sideboard owner must not enter recipes before the native save adapter supports it.
var unsupported = Fresh();
var unsupportedRecipe = unsupported.Recipes[0];
var unsupportedCard = Cards.FromId(unsupportedRecipe.Cards[0].Id);
var sideboardTag = (GameTag)2931; // pinned client MAX_SIDEBOARD_CARDS
bool hadSideboardTag = unsupportedCard.Tags.TryGetValue(sideboardTag, out int oldSideboardTag);
try
{
    unsupportedCard.Tags[sideboardTag] = 1;
    Check(unsupported.Validate(unsupportedRecipe).Count != 0, "recipe rejects a native sideboard owner without a save adapter");
    Reject(() => LocalDecks.ValidateNativeSave(unsupportedRecipe.Cards.SelectMany(row => Enumerable.Repeat(row.Id, row.Count)),
        Array.Empty<LocalSideboard>()), "native save and recipe validation share unsupported-owner rejection");
}
finally
{
    if (hadSideboardTag) unsupportedCard.Tags[sideboardTag] = oldSideboardTag;
    else unsupportedCard.Tags.Remove(sideboardTag);
}

Check(shipped.Recipes.Select(recipe => recipe.Id).Distinct().Count() == shipped.Recipes.Count, "shipped ids unique");

// Guide copy (owner 2026-11-05): two short plain pages. Strategy and Mulligan stay within their word limits, use no em or en dash, and name
// only cards the deck holds (main deck and sideboards). A pending list that ships a fallback must hold the named cards in BOTH lists, since
// either may be the one in play. The data carries nothing else (no dead guide fields).
{
    var rawRecipes = JsonNode.Parse(File.ReadAllText(path))!["recipes"]!.AsArray();
    Check(rawRecipes.Count == 72, "guide check reads all 72 recipes");
    var everyName = Cards.All.Where(card => card.Collectible && !string.IsNullOrWhiteSpace(card.Name)).Select(card => card.Name).Distinct(StringComparer.Ordinal).ToArray();
    IEnumerable<string> Aliases(string name) => name.Contains(", ") ? new[] { name, name[..name.IndexOf(", ", StringComparison.Ordinal)] } : new[] { name };
    HashSet<string> DeckNames(JsonNode list)
    {
        var ids = list["cards"]!.AsArray().Select(row => (string)row!["id"]!)
            .Concat((list["sideboards"]?.AsArray() ?? new JsonArray()).SelectMany(board => board!["cards"]!.AsArray().Select(row => (string)row!["id"]!)));
        return ids.SelectMany(id => Aliases(Cards.FromId(id).Name)).ToHashSet(StringComparer.Ordinal);
    }
    System.Text.RegularExpressions.MatchCollection Mentions(string text, string name) => System.Text.RegularExpressions.Regex.Matches(text,
        "(?<![\\w'])" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![\\w'])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    var foreignMentions = new List<string>();
    // Game keywords that are also the name of one collectible card; the guide uses them as rules words.
    var GameKeywords = new HashSet<string>(StringComparer.Ordinal) { "Charge" };
    foreach (var raw in rawRecipes)
    {
        string id = (string)raw!["id"]!;
        var guide = raw["guide"]!.AsObject();
        Check(guide.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(new[] { "keepCards", "mulligan", "strategy", "strategyCards" }),
            id + " guide holds exactly strategy, mulligan and their card lists");
        var deck = DeckNames(raw);
        if (raw["pending"]?["fallback"] is JsonNode fallback) deck.IntersectWith(DeckNames(fallback));
        foreach (var (field, limit) in new[] { ("strategy", RecipeLibrary.MaxStrategyWords), ("mulligan", RecipeLibrary.MaxMulliganWords) })
        {
            string text = (string)guide[field]!;
            int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            Check(words > 0 && words <= limit, id + " " + field + " is " + words + " words (limit " + limit + ")");
            Check(!text.Contains('\u2014') && !text.Contains('\u2013') && !text.Contains(" - "), id + " " + field + " has no dash punctuation");
            // Mentions of the deck's own names first; a longer own name swallows a shorter foreign name inside it.
            var own = deck.SelectMany(name => Mentions(text, name).Select(match => (match.Index, End: match.Index + match.Length))).ToArray();
            var foreign = everyName.Where(name => !deck.Contains(name) && !GameKeywords.Contains(name))
                .SelectMany(name => Mentions(text, name).Select(match => (Name: name, match.Index, End: match.Index + match.Length)))
                .Where(hit => !own.Any(span => span.Index <= hit.Index && hit.End <= span.End)).Select(hit => hit.Name).Distinct().ToArray();
            // The card pictures come from the same text: each page's list is the deck's cards that page names, in order, at most five.
            var listed = guide[field == "strategy" ? "strategyCards" : "keepCards"]!.AsArray().Select(node => Cards.FromId((string)node!).Name).ToArray();
            string scanned = field == "strategy" ? text : string.Join(" ", System.Text.RegularExpressions.Regex.Split(text, @"(?<=\.)\s+").Where(sentence => sentence.StartsWith("Keep") || sentence.StartsWith("Always keep")));
            var mentioned = listed.Where(name => !Aliases(name).Any(form => Mentions(scanned, form).Count > 0)).ToArray();
            // A card the page tells you to return must never be shown as a keep, whatever else the Keep sentences say about it.
            if (field == "mulligan")
            {
                string returned = string.Join(" ", System.Text.RegularExpressions.Regex.Split(text, @"(?<=\.)\s+").Where(sentence => sentence.StartsWith("Return")));
                var contradicted = listed.Where(name => Aliases(name).Any(form => Mentions(returned, form).Count > 0)).ToArray();
                if (contradicted.Length != 0) foreignMentions.Add(id + " keepCards lists cards the text returns: " + string.Join(", ", contradicted));
            }
            if (mentioned.Length != 0) foreignMentions.Add(id + " " + field + " card list names cards the page does not mention: " + string.Join(", ", mentioned));
            if (foreign.Length != 0) foreignMentions.Add(id + " " + field + ": " + string.Join(", ", foreign));
        }
    }
    Check(foreignMentions.Count == 0, "every guide names only cards in its deck" + (foreignMentions.Count == 0 ? "" : "\n  " + string.Join("\n  ", foreignMentions)));
}
var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data/shop/catalog-v1.json")))!["Products"]!.AsArray();
foreach (var recipe in shipped.Recipes.Concat(shipped.Withheld))
    foreach (string kind in new[] { "Recipe", "Deck" })
        Check(catalog.Count(product => (string)product!["RecipeId"]! == recipe.Id && (string)product["Kind"]! == kind) == 1,
            recipe.Id + " has one " + kind + " product");
var library = Fresh();
Check(library.Recipes.Count == 2 && library.Validate().Count == 0, "rule fixtures validate");
Check(library.Recipes.Select(recipe => recipe.Class).Distinct().Count() == 2, "fixtures use different classes");
Check(library.Recipes.All(recipe => recipe.Preview.Contains("Fixture")), "fixture content honest");
void BadJson(Action<JsonObject> mutate, string label)
{
    var root = JsonNode.Parse(json)!.AsObject(); mutate(root);
    Reject(() => RecipeLibrary.LoadText(root.ToJsonString()), label);
}
void Bad(Action<DeckRecipe> mutate, string label)
{
    var l = Fresh(); var r = l.Recipes[0]; mutate(r);
    Check(l.Validate().Count != 0, label);
    Reject(() => l.BuildDeck(r, _ => 99), label + " cannot build");
}
BadJson(root => root["schema"] = 2, "unknown schema");
BadJson(root => root["libraryVersion"] = 0, "invalid libraryVersion");
BadJson(root => root["clientBuild"] = "wrong", "client build mismatch");
BadJson(root => root["recipes"] = new JsonArray(), "empty library");
BadJson(root => root["recipes"]!.AsArray().Add(root["recipes"]![0]!.DeepClone()), "duplicate recipe id");
BadJson(root => root["recipes"]![0]!["cards"]![0]!["count"] = 1.5, "fractional count");
BadJson(root => root["recipes"]![0]!["cards"] = "wrong", "cards must be array");
Reject(() => RecipeLibrary.LoadText("{"), "malformed JSON");
Reject(() => RecipeLibrary.LoadText("null"), "null JSON");
foreach (string malformed in new[] { json + "[]", json.Replace("1,", "1"),
    json.Insert(json.LastIndexOf('}'), ","), json.Replace("\"schema\": 1", "\"schema\": 1,\"schema\": 1"),
    json.Replace("\"schema\": 1", "\"schema\": 01"), json.TrimEnd()[..^1] })
    Reject(() => RecipeLibrary.LoadText(malformed), "strict recipe JSON syntax");
foreach (string field in new[] { "id", "name", "class", "hero", "archetype", "role", "difficulty", "tag", "style", "preview", "guide", "source" })
    BadJson(root => root["recipes"]![0]!.AsObject().Remove(field), "missing " + field);
foreach (string field in new[] { "strategy", "mulligan", "strategyCards", "keepCards" })
    BadJson(root => root["recipes"]![0]!["guide"]!.AsObject().Remove(field), "missing guide " + field);
BadJson(root => root["recipes"]![0]!["guide"]!["keepCards"] = new JsonArray(), "empty keep list");
BadJson(root => root["recipes"]![0]!["guide"]!["keepCards"] = new JsonArray("CORE_CS2_023", "CORE_CS2_023"), "duplicate guide card");
BadJson(root => root["recipes"]![0]!["guide"]!["strategyCards"] = new JsonArray("CORE_CS2_093"), "guide card outside the deck");
BadJson(root => root["recipes"]![0]!["guide"]!["strategy"] = Words(RecipeLibrary.MaxStrategyWords + 1), "strategy over the word limit");
BadJson(root => root["recipes"]![0]!["guide"]!["mulligan"] = Words(RecipeLibrary.MaxMulliganWords + 1), "mulligan over the word limit");
foreach (string field in new[] { "site", "url", "accessed", "patch", "deckCode", "changes" })
    BadJson(root => root["recipes"]![0]!["source"]!.AsObject().Remove(field), "missing source " + field);
Bad(r => r.Name = new string('x', 33), "33-character deck name");
foreach (string name in new[] { "bad\nname", "bad\tname", "bad\0name", "bad\u2028name", "bad\u2029name", "\u200B" })
    Bad(r => r.Name = name, "non-saveable deck name");
foreach (string name in new[] { "x", new string('x', 32), string.Concat(Enumerable.Repeat("e\u0301", 32)), string.Concat(Enumerable.Repeat("\U0001F600", 32)) })
{
    var named = Fresh(); named.Recipes[0].Name = name;
    Check(named.Validate().Count == 0, "deck name grapheme boundary accepted");
}
Bad(r => r.Preview = new string('x', 201), "long preview");
Bad(r => r.Guide.Strategy = new string('x', 401), "long strategy");
Bad(r => r.Guide.Mulligan = new string('x', 401), "long mulligan");
Bad(r => r.Guide.Strategy = Words(RecipeLibrary.MaxStrategyWords + 1), "strategy over the word limit");
Bad(r => r.Guide.Mulligan = Words(RecipeLibrary.MaxMulliganWords + 1), "mulligan over the word limit");
Bad(r => r.Guide.KeepCards = r.Cards.Take(RecipeLibrary.MaxGuideCards + 1).Select(row => row.Id).ToList(), "too many guide cards");
Bad(r => r.Preview = " ", "blank preview");
Bad(r => r.Source.Url = "not a url", "invalid source URL");
Bad(r => r.Source.Accessed = "2026-02-30", "invalid accessed date");
Bad(r => r.Role = "meta", "invalid role");
Bad(r => r.Difficulty = "impossible", "invalid difficulty");
Bad(r => r.Tag = "popular", "invalid tag");
Bad(r => r.Style = "defensive", "invalid style");
BadJson(root => root["recipes"]![0]!["tag"] = "popular", "invalid tag JSON");
BadJson(root => root["recipes"]![0]!["style"] = "defensive", "invalid style JSON");
Bad(r => r.Hero = "HERO_01", "class hero mismatch");
Bad(r => r.Hero = "HERO_99", "unknown hero");
Bad(r => r.Cards[0].Id = "NO_SUCH_CARD", "unregistered card");
Bad(r => r.Cards[0].Id = "EX1_298", "registered card outside Standard");
Bad(r => r.Cards[0].Id = "CORE_CS2_093", "wrong class");
Bad(r => r.Cards[0].Count = 0, "zero count");
Bad(r => r.Cards[0].Count = -1, "negative count");
Bad(r => r.Cards[0].Count = int.MaxValue, "oversized count");
Bad(r => r.Cards.RemoveAt(0), "short deck");
Bad(r => r.Cards[0].Count = 3, "copy overflow");
Bad(r => { r.Cards[0].Id = "CORE_EX1_110"; r.Cards[0].Count = 2; }, "Legendary limit");
Bad(r => r.Cards.Add(new RecipeCard { Id = r.Cards[0].Id, Count = 1 }), "duplicate card rows");
Bad(r => r.Cards = null, "null card records");
Bad(r => r.Cards[0] = null, "null card row");
Bad(r => r.Sideboards = null, "null sideboard records");
var boundary = Fresh();
boundary.Recipes[0].Preview = new string('x', 200);
boundary.Recipes[0].Guide.Strategy = Words(RecipeLibrary.MaxStrategyWords);
boundary.Recipes[0].Guide.Mulligan = Words(RecipeLibrary.MaxMulliganWords);
Check(boundary.Validate().Count == 0, "text and word limits inclusive");
boundary.Recipes[0].Source.Changes = "";
Check(boundary.Validate().Count == 0, "empty source changes allowed");

// Keep pending cases independent of which production dependencies have landed.
const string need = "EX1_298";
const string fallbackChanges = "Fixture: replace the unavailable card with the original valid list.";
JsonObject PendingJson(bool fallback = true, bool unavailable = true)
{
    var root = JsonNode.Parse(json)!.AsObject();
    var recipe = root["recipes"]![0]!;
    var pending = new JsonObject { ["needs"] = new JsonArray(need), ["branch"] = "fixture/pending-card" };
    if (fallback)
        pending["fallback"] = new JsonObject { ["changes"] = fallbackChanges,
            ["cards"] = recipe["cards"]!.DeepClone(), ["sideboards"] = recipe["sideboards"]!.DeepClone() };
    // This row has one copy, so a Legendary dependency creates no unrelated copy-limit error.
    if (unavailable) recipe["cards"]![8]!["id"] = need;
    recipe["pending"] = pending;
    return root;
}
void RejectPending(Action<JsonObject> mutate, string label, string error)
{
    var root = PendingJson(); mutate(root);
    Reject(() => RecipeLibrary.LoadText(root.ToJsonString()), label, error);
}
var pendingRoot = PendingJson();
var fallbackLibrary = RecipeLibrary.LoadText(pendingRoot.ToJsonString());
var fallbackRecipe = fallbackLibrary.Recipes.Single(r => r.Id == library.Recipes[0].Id);
Check(fallbackLibrary.Recipes.Count == library.Recipes.Count && fallbackLibrary.Withheld.Count == 0
    && fallbackRecipe.Pending.Waiting && fallbackRecipe.Pending.Fallback != null, "pending list ships its fallback as active");
Check(fallbackRecipe.Cards.Select(c => (c.Id, c.Count)).SequenceEqual(library.Recipes[0].Cards.Select(c => (c.Id, c.Count)))
    && fallbackRecipe.Source.Changes == fallbackChanges && fallbackLibrary.Validate().Count == 0,
    "fallback cards and source changes replace the published list");
var withheldLibrary = RecipeLibrary.LoadText(PendingJson(fallback: false).ToJsonString());
string withheldId = library.Recipes[0].Id;
Check(withheldLibrary.Withheld.Count == 1 && withheldLibrary.Withheld.Single().Id == withheldId
    && withheldLibrary.Withheld.Single().Pending.Waiting && withheldLibrary.Withheld.Single().Pending.Fallback == null
    && withheldLibrary.Recipes.All(r => r.Id != withheldId) && withheldLibrary.IsWithheld(withheldId)
    && !withheldLibrary.HasRecipe(withheldId), "pending list without fallback is withheld");
Check(!withheldLibrary.IsWithheld(null) && !withheldLibrary.IsWithheld("unknown-recipe"), "unknown ids are not withheld");
var satisfiedLibrary = RecipeLibrary.LoadText(PendingJson(unavailable: false).ToJsonString());
var satisfiedRecipe = satisfiedLibrary.Recipes.Single(r => r.Id == withheldId);
Check(!satisfiedRecipe.Pending.Waiting && satisfiedLibrary.Withheld.Count == 0
    && satisfiedRecipe.Cards.Select(c => (c.Id, c.Count)).SequenceEqual(library.Recipes[0].Cards.Select(c => (c.Id, c.Count)))
    && satisfiedRecipe.Source.Changes == library.Recipes[0].Source.Changes,
    "valid published list wins over fallback despite pending marker");
RejectPending(root => root["recipes"]![0]!["cards"]![0]!["id"] = "CORE_CS2_093",
    "pending cannot hide wrong-class defect", "not explained by its needs");
RejectPending(root => root["recipes"]![0]!["pending"]!["fallback"]!["cards"]![0]!["count"] = 1,
    "invalid fallback fails strict validation", "Invalid deck size.");
RejectPending(root => root["recipes"]![0]!["pending"]!["needs"] = new JsonArray(),
    "empty pending needs rejected", "Invalid pending dependency.");
RejectPending(root => root["recipes"]![0]!["pending"]!.AsObject().Remove("branch"),
    "missing pending branch rejected", "Invalid pending dependency.");
var sizeRoot = PendingJson(fallback: false, unavailable: false);
sizeRoot["recipes"]![0]!["cards"]![0]!["count"] = 1;
sizeRoot["recipes"]![0]!["pending"]!["needs"] = new JsonArray("rule:azalina-20-card");
Reject(() => RecipeLibrary.LoadText(sizeRoot.ToJsonString()), "Azalina rule never explains a non-Azalina deck size", "not explained by its needs");
// The rule explains deck-size errors only for an exactly 20-card list holding Azalina.
var azalinaRule = new RecipePending { Needs = new() { RecipePending.AzalinaRule } };
var azalinaList = new DeckRecipe { Cards = new() { new RecipeCard { Id = RecipePending.Azalina, Count = 1 }, new RecipeCard { Id = "CORE_CS1_130", Count = 19 } } };
Check(azalinaRule.Explains("a: Invalid deck size.", azalinaList) && azalinaRule.Explains("a: Local draft must fit its 30-card limit.", azalinaList)
    && !azalinaRule.Explains("a: Card does not belong to selected class.", azalinaList), "Azalina rule explains only deck size of a 20-card Azalina list");
azalinaList.Cards[1].Count = 20;
Check(!azalinaRule.Explains("a: Invalid deck size.", azalinaList), "Azalina rule refuses a 21-card list");
var duplicateRoot = PendingJson(fallback: false);
duplicateRoot["recipes"]![0]!["id"] = duplicateRoot["recipes"]![1]!["id"]!.GetValue<string>();
Reject(() => RecipeLibrary.LoadText(duplicateRoot.ToJsonString()), "withheld id cannot duplicate an active id", "Duplicate recipe id:");

// Removing exemptions or using raw counts instead of current grants breaks these checks.
var emptyOwned = RecipeLibrary.OwnedCount(new Dictionary<string, int>());
Check(emptyOwned("CS2_024") == 2, "cards that need no copy count as owned");
Check(emptyOwned("CORE_CS2_024") == 0, "locked Core is not silently granted");
Check(emptyOwned("JAIL_397") == 0 && emptyOwned("TIME_005") == 0, "sideboard owners and Fabled primaries need real copies");
var temp = Path.Combine(Path.GetTempPath(), "recipe-profile-" + Guid.NewGuid() + ".xml");
try
{
    using var profile = new LocalProfile(temp);
    var owned = RecipeLibrary.OwnedCount(profile.Collection);
    Check(owned("CORE_DRG_107") == 2, "current profile Core grants count as owned");
    Check(owned("CORE_CS2_024") == 0, "current profile locked Core stays missing");
    Check(owned("CS2_024") == 2, "profile free exemption");
}
finally
{
    foreach (string file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(temp) + "*")) File.Delete(file);
}
foreach (var r in library.Recipes)
{
    var empty = library.BuildDeck(r, emptyOwned);
    Check(empty.CardIds.Count == 30 && empty.CardIds.GroupBy(id => id).All(g => g.Count() == r.Cards.Single(c => c.Id == g.Key).Count), r.Id + " full list with missing cards");
    Check(empty.Copies.All(c => c.Missing == c.Needed - c.Owned), r.Id + " empty collection missing counts");
    Check(empty.Incomplete, r.Id + " incomplete deck flagged");
    var full = library.BuildDeck(r, _ => 2);
    Check(!full.Incomplete && full.Copies.All(c => c.Missing == 0), r.Id + " full collection");
    var extras = library.BuildDeck(r, _ => 20);
    Check(extras.Copies.All(c => c.Owned == 20 && c.Missing == 0) && extras.CardIds.Count == 30, r.Id + " extras never add deck cards or negative missing");
    var partial = library.BuildDeck(r, _ => 1);
    Check(partial.Copies.Sum(c => c.Missing) == r.Cards.Count(c => c.Count == 2), r.Id + " partial collection");
    var legend = empty.Copies.First(c => c.Rarity == Rarity.LEGENDARY);
    Check(legend.Needed == 1 && legend.Missing == 1, r.Id + " missing Legendary needs one");
    foreach (var count in new Func<string, int>[] { emptyOwned, _ => 1, _ => 20 })
    {
        var missing = library.MissingCopies(r, count);
        var build = library.BuildDeck(r, count);
        Check(missing.Total == build.Copies.Sum(c => c.Missing) && missing.Total == missing.Purchasable.Values.Sum() + missing.LockedCore.Sum(c => c.LockedCore), r.Id + " missing total agrees");
        Check(build.Copies.All(c => missing.Purchasable[c.Rarity] == build.Copies.Where(other => other.Rarity == c.Rarity).Sum(other => other.Purchasable)), r.Id + " rarity breakdown agrees");
    }
    var before = r.Cards.Select(c => (c.Id, c.Count)).ToArray();
    empty.Deck.CardIds.Clear();
    Check(r.Cards.Select(c => (c.Id, c.Count)).SequenceEqual(before) && library.BuildDeck(r, _ => 0).CardIds.Count == 30, "build does not mutate recipe");
}
Reject(() => library.BuildDeck(library.Recipes[0], null), "null ownership delegate");
Reject(() => RecipeLibrary.OwnedCount(null), "null collection");

DeckRecipe Legal(string hero, params string[] extra)
{
    var r = Fresh().Recipes[0]; r.Hero = hero; r.Class = LocalDecks.HeroClass(hero).ToString();
    var neutral = StandardSets.CollectibleIds.Select(Cards.FromId).Where(c => c.Implemented && LocalCardCatalog.InActiveMeta(c) && c.Class == CardClass.NEUTRAL
        && !FabledRules.IsFabled(c.Id) && !DeckbuildingRules.HasSideboard(c.Id) && !extra.Contains(c.Id)).Select(c => c.Id);
    r.Cards = extra.Concat(neutral).Take(30).Select(id => new RecipeCard { Id = id, Count = 1 }).ToList();
    r.Guide.StrategyCards = r.Guide.KeepCards = new List<string> { r.Cards[0].Id };   // the guide's card lists must name deck cards
    return r;
}
void Invalid(DeckRecipe r, string label) => Check(library.Validate(r).Count != 0, label);
var blood = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => LocalCardCatalog.InActiveMeta(c) && c.Class == CardClass.DEATHKNIGHT && c[(GameTag)2196] >= 2);
var frost = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => LocalCardCatalog.InActiveMeta(c) && c.Class == CardClass.DEATHKNIGHT && c[(GameTag)2197] >= 2);
var runeBad = Legal("HERO_11", blood.Id, frost.Id);
Invalid(runeBad, "Death Knight conflicting runes");
Check(library.Validate(Legal("HERO_11", blood.Id)).Count == 0, "Death Knight legal runes");
var fabled = Legal("HERO_08", "TIME_852");
Invalid(fabled, "Fabled missing companions");
fabled.Cards = fabled.Cards.Take(28).Concat(FabledRules.Bundle("TIME_852").Select(id => new RecipeCard { Id = id, Count = 1 })).ToList();
Check(library.Validate(fabled).Count == 0, "Fabled bundle accepted");
// Retail Dragon Hunter's deck code lists Sylvanas (TIME_609) and both bundled Rangers; any other token is not a deck card.
var bundleCode = shipped.Recipes.Single(r => r.Id == "dragon-hunter").Cards.Select(row => row.Id).Where(id => !Cards.FromId(id).Collectible);
Check(bundleCode.OrderBy(id => id).SequenceEqual(FabledRules.Bundle("TIME_609")), "dragon-hunter non-collectible rows are exactly Sylvanas's companions");
var token = Legal("HERO_08");
token.Cards[29] = new RecipeCard { Id = "CS2_101t", Count = 1 };
Check(library.Validate(token).Any(e => e.Contains("Not a collectible deck card: CS2_101t")), "token rejected as a non-collectible deck card");

var inactive = Legal("HERO_08", "BE_258");
Invalid(inactive, "BE expansion outside active meta");
Reject(() => library.BuildDeck(inactive, _ => 99), "inactive expansion cannot build");
var twenty = Legal("HERO_09", "JAIL_430");
twenty.Cards = LocalDecks.Get("test-standard-azalina-priest").CardIds.GroupBy(id => id)
    .Select(group => new RecipeCard { Id = group.Key, Count = group.Count() }).ToList();
Check(library.Validate(twenty).Count == 0 && library.BuildDeck(twenty, _ => 0).Deck.Build().Count == 20,
    "Azalina recipe validates and builds twenty including Fabled companions");
twenty.Cards.First().Count--;
Invalid(twenty, "Azalina recipe requires all twenty cards");
Reject(() => library.BuildDeck(twenty, _ => 99), "nineteen-card Azalina recipe cannot build");
var forty = Legal("HERO_07", "TIME_005");
forty.Cards.AddRange(FabledRules.Bundle("TIME_005").Select(id => new RecipeCard { Id = id, Count = 1 }));
var extraNeutral = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => LocalCardCatalog.InActiveMeta(c) && c.Class == CardClass.NEUTRAL
    && !FabledRules.IsFabled(c.Id) && forty.Cards.All(row => row.Id != c.Id));
forty.Cards.Add(new RecipeCard { Id = extraNeutral.Id, Count = 1 });
Check(library.Validate(forty).Count == 0 && library.BuildDeck(forty, _ => 0).CardIds.Count == 40, "Fabled plus uses engine forty-card size");
forty.Cards.RemoveAt(forty.Cards.Count - 1);
Invalid(forty, "Fabled plus requires full forty-card list");

var side = Legal("HERO_04", DeckbuildingRules.Beatrix);
var selection = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => LocalCardCatalog.InActiveMeta(c)
    && DeckbuildingRules.Eligible(DeckbuildingRules.Beatrix, c, FormatType.FT_STANDARD) && side.Cards.All(row => row.Id != c.Id));
side.Sideboards.Add(new RecipeSideboard { Owner = DeckbuildingRules.Beatrix, Cards = new() { new RecipeCard { Id = selection.Id, Count = 1 } } });
Check(library.Validate(side).Count == 0, "legal sideboard");
var sideBuild = library.BuildDeck(side, _ => 0);
Check(sideBuild.CardIds.Count == 30 && sideBuild.Sideboards[DeckbuildingRules.Beatrix].Single() == selection.Id, "build preserves sideboards separately");
Check(sideBuild.Copies.Single(c => c.Id == selection.Id).Needed == 1 && sideBuild.Copies.Single(c => c.Id == selection.Id).Missing == 1, "sideboard missing included");
Check(library.MissingCopies(side, _ => 0).Total == 31 && library.MissingCopies(side, _ => 1).Total == 0, "sideboard included in missing total");
var inactiveSelection = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => !LocalCardCatalog.InActiveMeta(c)
    && DeckbuildingRules.Eligible(DeckbuildingRules.Beatrix, c, FormatType.FT_STANDARD));
side.Sideboards[0].Cards[0].Id = inactiveSelection.Id;
Invalid(side, "inactive sideboard selection");
Reject(() => library.BuildDeck(side, _ => 99), "inactive sideboard cannot build");
side.Sideboards[0].Cards[0].Id = selection.Id;
side.Sideboards[0].Cards.Clear(); Invalid(side, "incomplete sideboard");
side.Sideboards[0].Cards.Add(new RecipeCard { Id = "CORE_CS2_093", Count = 1 }); Invalid(side, "ineligible sideboard selection");
side.Sideboards[0].Cards[0].Id = "NO_SUCH_CARD"; Invalid(side, "unknown sideboard card");
side.Sideboards[0].Cards[0].Id = selection.Id;
side.Sideboards.Add(side.Sideboards[0]); Invalid(side, "duplicate sideboard owner");
side.Sideboards.RemoveAt(1);
side.Sideboards[0].Owner = "CORE_CS2_093"; Invalid(side, "card without sideboard");
side.Sideboards[0].Owner = DeckbuildingRules.Beatrix;
side.Cards[0].Id = "CORE_EX1_362"; Invalid(side, "sideboard owner absent");
var hunter = Legal("HERO_05", DeckbuildingRules.Underbelly);
var beasts = StandardSets.CollectibleIds.Select(Cards.FromId).Where(c => LocalCardCatalog.InActiveMeta(c)
    && DeckbuildingRules.Eligible(DeckbuildingRules.Underbelly, c, FormatType.FT_STANDARD)).Take(3).ToArray();
hunter.Sideboards.Add(new RecipeSideboard { Owner = DeckbuildingRules.Underbelly, Cards = beasts.Select(c => new RecipeCard { Id = c.Id, Count = 1 }).ToList() });
Check(library.Validate(hunter).Count == 0, "owner-specific off-class sideboard valid");
hunter.Sideboards[0].Cards = new() { new RecipeCard { Id = beasts[0].Id, Count = 3 } };
Invalid(hunter, "sideboard copy limit");
var shared = Legal("HERO_05", DeckbuildingRules.Underbelly);
var sharedBeast = StandardSets.CollectibleIds.Select(Cards.FromId).First(c => LocalCardCatalog.InActiveMeta(c) && c.Class != CardClass.HUNTER
    && c.Class != CardClass.NEUTRAL && c.Rarity != Rarity.LEGENDARY && DeckbuildingRules.Eligible(DeckbuildingRules.Underbelly, c, FormatType.FT_STANDARD));
// An independent sideboard can contain two copies and a third slot of another card.
shared.Sideboards.Add(new RecipeSideboard { Owner = DeckbuildingRules.Underbelly,
    Cards = new() { new RecipeCard { Id = sharedBeast.Id, Count = 2 },
        new RecipeCard { Id = beasts.First(c => c.Id != sharedBeast.Id).Id, Count = 1 } } });
Check(library.Validate(shared).Count == 0, "sideboard non-Legendary pair allowed");
var contraband = library.BuildDeck(shared, _ => 0).Copies.Single(c => c.Id == sharedBeast.Id);
Check(contraband.Needed == 2 && contraband.Owned == 0 && contraband.Missing == 2,
    "Underbelly contraband needs owned copies like any sideboard (retail CanCardBeAddedAsOwned, owner 2026-10-30)");
// Pin the recipe ownership rule to the actual profile rule without changing its private API.
var validateSideboardOwnership = typeof(LocalProfile).GetMethod("ValidateSideboardOwnership",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
foreach (var recipe in new[] { shared, Legal("HERO_04", DeckbuildingRules.Beatrix) })
{
    if (recipe.Sideboards.Count == 0)
        recipe.Sideboards.Add(new RecipeSideboard { Owner = DeckbuildingRules.Beatrix,
            Cards = new() { new RecipeCard { Id = selection.Id, Count = 1 } } });
    foreach (int ownedCopies in new[] { 0, 1 })
    {
        var owned = recipe.Cards.ToDictionary(row => row.Id, row => row.Count);
        foreach (var row in recipe.Sideboards.SelectMany(board => board.Cards)) owned[row.Id] = ownedCopies;
        bool accepted = true;
        try
        {
            validateSideboardOwnership.Invoke(null, new object[] {
                recipe.Sideboards.Select(board => new LocalSideboard { Source = board.Owner,
                    Cards = board.Cards.SelectMany(row => Enumerable.Repeat(row.Id, row.Count)).ToList() }), owned });
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        { accepted = false; }
        var build = library.BuildDeck(recipe, id => owned.TryGetValue(id, out int count) ? count : 0);
        Check(build.Incomplete == !accepted, recipe.Sideboards[0].Owner + " missing agrees with profile ownership at " + ownedCopies);
    }
}
Check(library.MissingCopies(library.Recipes[0], _ => -10).Total == 30, "negative ownership cannot inflate missing");
Console.WriteLine($"RecipeTests: {checks} checks passed");
