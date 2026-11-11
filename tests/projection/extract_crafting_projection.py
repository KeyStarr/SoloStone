"""Run native crafting projection helpers without loading Unity; use native DTO boundaries only."""
from pathlib import Path
import re
import sys
root = Path(__file__).parents[2]
source = (root / 'plugin/NativeUIProbe.cs').read_text()
editor = (root / 'plugin/NativeDeckEditor.cs').read_text()
save = (root / 'plugin/NativeDeckEditorSave.cs').read_text()

def block(source, pattern):
    match = re.search(pattern, source)
    if not match:
        return None
    start = match.start()
    opening = source.index('{', start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

parts = []
for name, text in [('AddNativePremiumGrants', source), ('ProjectMedal', source), ('CompleteGameplayMedal', source), ('OwnsGameplayRank', source), ('ProjectOpponentLadder', source), ('CollectionPacket', source), ('CraftingRecipe', source), ('ReprojectCraftingDecks', source), ('ProjectCraftingRecipe', source),
                   ('RememberReducedCraftingRecipe', source), ('CraftingRecipeSignature', source),
                   ('ApplyCraftingChanges', source), ('CopyCraftingDeck', source), ('RefreshCraftingOwnership', save),
                   ('ProjectMissingDraftCards', save), ('RestoreSavedPremiums', save), ('RestoreDraftVersions', save),
                   ('IsFabledCompanionSlot', save), ('AlignFabledCompanions', save), ('RefreshUpgradedDecks', save), ('IsUnrestoredCraftingRecipe', editor),
                   ('ProjectSideboards', save), ('DeckEntries', save), ('ReadSideboards', save), ('NativeBaseHero', save)]:
    method = block(text, r'    (?:internal|private) static [^\n]+ ' + name + r'\(')
    if method:
        parts.append(method)
        if name == 'CollectionPacket':
            # Overloads: the Normal-count adapter and the per-version packet writer.
            rest = source[source.index(method) + len(method):]
            while True:
                other = block(rest, r'    internal static [^\n]+ CollectionPacket\(')
                if not other:
                    break
                parts.append(other)
                rest = rest[rest.index(other) + len(other):]
    elif name == 'CollectionPacket':
        # Characterize the old Bootstrap packet writer until it is extracted into a shared helper.
        start = source.index('        var packet = new PegasusUtil.Collection();')
        end = source.index('        Publish(CollectionManager.Get().OnInitialCollectionReceived(packet));', start)
        parts.append('internal static PegasusUtil.Collection CollectionPacket(IEnumerable<KeyValuePair<string,int>> own) {' +
            source[start:end].replace('own.Concat(Heroes.Select(id => new KeyValuePair<string, int>(id, 1)))', 'own') + 'return packet; }')
    elif name == 'ProjectCraftingRecipe':
        parts.append('internal static void ProjectCraftingRecipe(CollectionDeck native, CollectionDeck baseline, LocalCustomDeck saved) {}')
    elif name == 'IsUnrestoredCraftingRecipe':
        parts.append('internal static bool IsUnrestoredCraftingRecipe(CollectionDeck native, LocalCustomDeck saved) => false;')
# ApplyCraftingChanges raises this flag around native reconciliation (SaveLocalDraft ignores those reconcile saves).
if 'private static bool craftingSynchronize;' in source:
    parts.append('    internal static bool craftingSynchronize;')
# AddNativePremiumGrants re-projects saved slots once per ledger commit.
if 'private static object premiumDeckLedger;' in source:
    parts.append('    private static object premiumDeckLedger;')

def expression(source, name):
    # Expression-bodied members end at the first ';' that closes the statement.
    match = re.search(r'    (?:internal|private) static [^\n]+ ' + name + r'\([^\n]*\) =>', source)
    if not match:
        raise ValueError('Missing native projection member ' + name)
    return source[match.start():source.index(';\n', match.end()) + 1]

# ProjectMissingDraftCards compares per-version entries before refilling a draft.
parts.extend(expression(save, name) for name in ('EntryKey', 'SameDeckIds', 'SameDeckEntries'))
crafting = (root / 'plugin/NativeCrafting.cs').read_text()
synchronize = block(crafting, r'    private static void Synchronize\(')
rematch = (root / 'plugin/LocalRematch.cs').read_text()
profile = rematch[rematch.index('public sealed partial class LocalProfile'):]
Path(sys.argv[1]).write_text('using System;\nusing System.Collections.Generic;\nusing System.Linq;\ninternal static partial class NativeUIProbe {\n' + '\n'.join(parts) + '\n}\ninternal static partial class NativeCrafting {\n' + synchronize + '\n}\n' + profile)

cosmetics = (root / 'plugin/NativeCosmetics.cs').read_text()
coin_methods = [block(cosmetics, r'    private static [^\n]+ ' + name + r'\(')
                for name in ('CosmeticCoins', 'RefreshCoins', 'SelectLocalCoin')]
if any(method is None for method in coin_methods):
    raise ValueError('Missing native coin projection method')
with Path(sys.argv[1]).open('a') as output:
    output.write('\ninternal static partial class NativeCoinsHarness {\n' + '\n'.join(coin_methods) + '\n}\n')
