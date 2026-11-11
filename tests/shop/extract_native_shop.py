from pathlib import Path
import sys
import re
source = (Path(__file__).parents[2] / "plugin/NativeShop.cs").read_text()
builders = source.split("// BEGIN catalogue builders", 1)[1].split("\n", 1)[1].split("    // END catalogue builders", 1)[0]
for name in ("CollectionCardBackProduct", "CollectionHeroSkinProduct", "ActiveCollectionController", "CollectionPurchaseContext", "RefuseCollectionPurchase",
             "LocalTabs", "LocalPrice", "ValidProduct", "LocalModel", "UpdateAvailability", "RefreshPrices", "RefreshNeeded", "ModelPricesMatch", "DeckDetails", "NativeEntryValid", "Buy", "TickPurchase", "Acknowledge",
             "GuardPurchase", "ClickFailed", "LocalPurchase", "ShowNotEnoughGold", "ShowPurchaseFailure", "ClearFinishedPurchase", "BuyPackTile",
             "NoteDragRelease", "NoteTilePress", "ClearTilePress", "ExpireTilePress", "PackTileRelease", "SuccessShown",
             "DeckSectionDividers", "ShouldFetchDivider", "HideEmptySlot", "SyncSubTabs", "SubTabList", "SyncSubTabsFrame", "Equip", "Quote", "FullPrice", "DeckVariants", "DeckFirstVariant", "RecipeTileText", "Priced"):
    # RecipeTileText is internal (NativeShopTilePrices calls it); Priced is an expression body.
    access = "(?:private|internal)" if name in ("RecipeTileText", "Priced") else "private"
    method = re.search(r"^    " + access + r" static [^\n]+\b" + name + r"\([^\n]*\)(?:\n    \{.*?^    \}| =>\n[^\n]+;)", source, re.S | re.M)
    if method is None:
        raise ValueError("Missing native shop method " + name)
    # The harness doubles are private nested types, so extracted methods stay private.
    builders += "\n" + method.group().replace("    internal static ", "    private static ", 1) + "\n"
# Production deck text cache state (ledger/shop keyed memo) so DeckDetails runs as shipped.
cache = re.search(r"    private static readonly Dictionary<string, string> deckDetails = [^\n]+\n    private static object deckDetailsLedger, deckDetailsShop;\n", source)
if cache is None:
    raise ValueError("Missing deck details cache state")
builders += "\n" + cache.group()
projection = (Path(__file__).parents[2] / "plugin/NativeShopProjection.cs").read_text().replace("internal static partial class NativeUIProbe", "partial class NativeShopProjectionHarness")
Path(sys.argv[1]).write_text("using System.Collections;\n" + projection + "\npartial class NativeBundleHarness {\n" + builders + "\n}\n")

# Run native pet projection/mapping against deterministic DBF/NetCache doubles.
source = (Path(__file__).parents[2] / "plugin/NativeCosmetics.cs").read_text()
methods = []
for name in ("PetVariant", "PetEntryValid", "CosmeticPets", "RefreshPets", "SelectLocalPet", "SelectLocalPetVariant"):
    method = re.search(r"^    (?:private|internal) static [^\n]+\b" + name + r"\([^\n]*\) \{.*?^    }", source, re.M | re.S)
    if method is None:
        raise ValueError("Missing native pet method " + name)
    methods.append(method.group())
with Path(sys.argv[1]).open("a") as output:
    output.write("partial class NativePetsHarness {\n" + "\n".join(methods) + "\n}\n")
