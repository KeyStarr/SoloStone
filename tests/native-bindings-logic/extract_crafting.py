"""Exercise production transaction hooks with native UI boundaries substituted; never load Unity."""
from pathlib import Path
import re
import sys

source = (Path(__file__).parents[2] / "plugin/NativeCrafting.cs").read_text()

def block(pattern, source=source):
    start = re.search(pattern, source).start()
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]

names = ["TryReadBatch", "CanCraft", "Price", "CraftingEvent", "Permission", "BeforeClose", "CommitBatch", "ClearPending", "AfterClose",
         "AllowAction", "AllowUpgrade", "BeforeCreate", "BeforeDisenchant", "BeforeUpgradeEvent", "PendingDisenchants", "ProtectPremiumButton",
         "CloseFailed", "CommitMass", "MassResponse", "MassUnblocked", "SafeStep", "TryMassTotals",
         "PreviewMass", "MassCompleted", "Synchronize", "RefreshDust", "RefreshPage", "ShowError",
         "FinishClose", "HasMassExtras", "LedgerPreviewCard", "WarnMassRefresh", "ShowRefreshError", "SettlementDust",
         "FeaturedCardSet", "CoreCardSet", "ShownCardIsCore", "AllowTokenCraft", "ShowTokenCraft", "TokenCraftLabel", "UiLabel", "UiText", "SetUiText"]
ledger = (Path(__file__).parents[2] / "plugin/LocalCraftingLedger.cs").read_text()
parts = [block(r"internal interface ICraftingLedger", ledger), block(r"internal static class CraftingLedgerRegistry", ledger)]
body = [block(r"    private sealed class BatchCompletion")]
for name in ["batch", "closing", "massCompleting, massListenersFailed", "massResponse", "massRefreshErrorShown"]:
    body.append(re.search(r"    private static [^\n]+ " + name + r";", source).group())
body.append(re.search(r"    private static bool Enabled => [^;]+;", source).group())
body.append(re.search(r"    private static bool Eligible\([^;]+;", source).group())
body.append(re.search(r"    private static bool Disenchantable\([^;]+;", source).group())
body.append(re.search(r"    private static bool PresentationEligible\([^;]+;", source).group())
body.append(re.search(r"    private static bool PremiumSaleAvailable\([^;]+;", source).group())
for name in ["TokenOneCopy"]:
    body.append(re.search(r"    internal const string " + name + r" = [^;]+;", source).group())
plans = (Path(__file__).parents[2] / "plugin/LocalCrafting.cs").read_text()
for name in ["CraftPlan", "CraftCardDelta", "CraftLedgerView"]:
    match = re.search(r"public sealed class " + name + r"\s*\{[^}]+\}", plans)
    parts.append(match.group())
parts.append("internal static partial class LocalCrafting {" + block(r"    public static CraftPlan PlanUpgrade\(", plans) + "}")
for name in names:
    pattern = r"    (?:private|internal) static [^\n]+ " + name + r"\("
    if re.search(pattern, source):
        body.append(block(pattern))
Path(sys.argv[1]).write_text("using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Reflection;\nusing UnityEngine;\n" +
    "\n".join(parts) + "\ninternal static partial class NativeCrafting\n{\n" + "\n".join(body) + "\n}\n")
