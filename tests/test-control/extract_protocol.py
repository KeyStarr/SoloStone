"""Compile production request validation without the socket/UI implementation."""
from pathlib import Path
import sys

source = (Path(__file__).resolve().parents[2] / "plugin/TestControlProtocol.cs").read_text(encoding='utf-8')
Path(sys.argv[1]).write_text(source[:source.index("internal sealed class ControlLines")], encoding='utf-8')
