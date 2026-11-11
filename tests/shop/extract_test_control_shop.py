"""Compile the production ledger handler without Unity/client dependencies."""
from pathlib import Path
import sys

source = (Path(__file__).parents[2] / "plugin/NativeTestControl.cs").read_text()
start = source.index("internal static class TestControlShop\n")
end = source.index("{", start) + 1
depth = 1
while depth:
    depth += (source[end] == "{") - (source[end] == "}")
    end += 1
Path(sys.argv[1]).write_text("using System;\nusing System.Linq;\nusing MiniJSON;\n" + source[start:end])
