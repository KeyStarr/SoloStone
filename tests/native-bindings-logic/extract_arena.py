"""Compile production Arena transport/DTO code against isolated client boundaries."""
from pathlib import Path
import re
import sys

source = (Path(__file__).resolve().parents[2] / "plugin/arena/NativeArena.cs").read_text()
# Harmony installation, scene bootstrap and home callbacks are pinned against the real client separately.
start = source.index("    internal static void Initialize(")
end = source.index("    private static ArenaView Read()", start)
source = source[:start] + source[end:]
start = source.index("    private static bool HomeButton(")
end = source.index("    internal static void Update()", start)
source = source[:start] + source[end:]
source = source.replace("    private static DraftManager loggedIn;\n", "")
source = re.sub(r"^using (BepInEx.Logging|HarmonyLib|PegasusShared|PegasusUtil|UnityEngine);\n", "", source, flags=re.M)
source = source.replace("Hearthstone.UI.Widget", "Widget")
Path(sys.argv[1]).write_text("namespace NativeArenaBoundary {\nusing PegasusShared;\nusing PegasusUtil;\n" + source + "\n}\n")
