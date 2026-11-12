# Headless recipe library

Run `dotnet run --project tests/recipes/RecipeTests.csproj` (or `python3 tests/run.py --suite recipes`).
The project always compiles the source engine; it needs no `EngineSource` flag.

```csharp
var library = RecipeLibrary.Load("data/shop/recipes-v1.json");
// In-memory loading uses the installed engine pool policy.
// var library = RecipeLibrary.LoadText(recipeJson);
var recipe = library.Recipes[0];
var ownedCount = RecipeLibrary.OwnedCount(profile.Collection);
var build = library.BuildDeck(recipe, ownedCount);
var missing = library.MissingCopies(recipe, ownedCount);
// build.Deck is the complete LocalDeck, with sideboards; build.Incomplete marks missing cards.
// build.Copies contains Id, Rarity, Needed, Owned, Missing.
// missing.Total includes all missing main and sideboard copies.
// missing.Purchasable groups paid copies by rarity. missing.LockedCore lists
// missing Core copies, with UnlockHero and UnlockLevel from progression.
// Empty UnlockHero means combined class level; null UnlockLevel means unknown.
// The shop owns purchase/save transactions; this library never writes a profile.
```

`Load` validates immediately and throws `InvalidDataException` for invalid library data.
`Validate()` and `Validate(recipe)` return errors; `BuildDeck` rejects invalid recipes.
Ownership delegates supply effective counts, including exemptions. The provided helper uses
current `profile.Collection`, which already includes progressively earned Core grants and
offline unlocks, plus free-card/offline-deckbuilding exemptions. Locked Core cards remain
missing; Core is not an unconditional grant. Other saved decks reserve no copies.

Modern pool membership uses the installed pool policy and current Standard registration,
including Fabled companions. A legacy vendor-engine plugin builds successfully but rejects
recipe loading: runtime recipe validation requires an `EngineSource=true` plugin build.

Rule tests load `fixtures/rules-v1.json`, independent of shipped content.
The shipped library is checked for validation, unique ids and one Recipe/Deck catalogue pair per recipe.
The two fixture lists adapt `LocalDecks.All` Mage Frost and Paladin Control to current Core
printings, with retired slots replaced by current cards. Provenance and changes are marked
fixture; `local:` source URLs and placeholder deck codes do not claim a public imported list.
No `All` deck has sideboards. Sideboard tests construct valid engine-backed selections.

Integration still required (existing files deliberately untouched):

- `plugin/OfflineProbe.csproj`: no compile edit needed; its default glob includes `Recipe*.cs`.
- Shop embeds `data/shop/recipes-v1.json`; recipe validation reads only `OfflinePoolPolicy.Load()`
  from the source engine. Native picker and Windows smoke belong to separate integration.
