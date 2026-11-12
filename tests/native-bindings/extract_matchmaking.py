"""Compile the complete production controller against isolated Unity boundaries."""
from pathlib import Path
import re
import sys

source = Path(__file__).parents[2] / "plugin/NativeMatchmaking.cs"
body = source.read_text()
body = re.sub(r"^using (BepInEx.Logging|HarmonyLib|UnityEngine);\n", "", body, flags=re.M)
Path(sys.argv[1]).write_text("namespace NativeMatchmakingBoundary {\n" + body + "\n}\n")
