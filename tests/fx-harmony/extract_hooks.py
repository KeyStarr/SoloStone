"""Compile the production hook registration/finalizers without Unity dependencies."""
from pathlib import Path
import sys

source = (Path(__file__).parents[2] / "plugin/NativeFxTelemetry.cs").read_text()
start = source.index("    private static void Hook(")
end = source.index("    private static void Observe(", start)
Path(sys.argv[1]).write_text("using System;\nusing System.Linq;\nusing System.Reflection;\nusing HarmonyLib;\npartial class NativeFxTelemetry\n{\n" + source[start:end] + "}\n")
