"""Compile the complete production gameplay-rank hooks against isolated native boundaries."""
from pathlib import Path
import re
import sys

source = Path(__file__).parents[2] / "plugin/NativeGameplayRank.cs"
body = source.read_text()
body = re.sub(r"^using (BepInEx.Logging|HarmonyLib);\n", "", body, flags=re.M)
Path(sys.argv[1]).write_text("namespace NativeGameplayRankBoundary {\n" + body + "\n}\n")
