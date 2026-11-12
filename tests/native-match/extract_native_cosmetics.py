"""Compile real cosmetics hooks/refresh; native popup dependencies are absent headlessly."""
from pathlib import Path
import re
import sys

source = (Path(__file__).parents[2] / "plugin/NativeCosmetics.cs").read_text()
end = source.index("    private static void CosmeticSelectionFailed(Exception error, bool saved)")
Path(sys.argv[1]).write_text(source[:end] + "}\n")

# Run the audit's actual identity helper against real NativeMatch, not the scheduler's fake.
source = (Path(__file__).parents[2] / "plugin/NativeUIProbe.cs").read_text()
method = re.search(r"^    private static string TestControlPresentationCardId\([^\n]*\)(?:\s*=>[^;]+;|\n    \{.*?^    })", source, re.M | re.S)
if method is None:
    raise ValueError("Missing visible audit method: TestControlPresentationCardId")
with Path(sys.argv[1]).open("a") as output:
    output.write("internal static partial class Program {\n" + method.group() + "\n}\n")

# Test the real late hand-choice refresh against the packet/animated-zone boundary.
source = (Path(__file__).parents[2] / "plugin/ClientBridge.cs").read_text()
method = re.search(r"^    private static void RefreshHandChoiceTargeting\([^\n]*\)\n    \{.*?^    }", source, re.M | re.S)
if method is None:
    raise ValueError("Missing hand choice refresh")
with Path(sys.argv[1]).open("a") as output:
    output.write("internal static partial class Program { private partial class HandChoiceClient {\n" + method.group() + "\n} }\n")
