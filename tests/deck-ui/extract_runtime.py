"""Exercise production Handheld UI / save-picker code with isolated native boundaries."""
from pathlib import Path
import re
import sys

layout = (Path(__file__).parents[2] / "plugin/DeckLayout.cs").read_text()
placement_selection = re.search(r"^                DeckRect\[\] row = DeckMulliganGeometry.Cards.*?(?=^                Apply\(\);)", layout, re.M | re.S)
if placement_selection is None:
    raise ValueError("Missing production mulligan placement selection")
profiles = (Path(__file__).parents[2] / "plugin/NativeProfileSelector.cs").read_text()
profile_members = []
for name in ("Build", "ConfigureMeta", "QueueTooltipTrace", "SampleTooltipTrace", "ArrangePaceRow", "CenterPaceRows", "PlacePaceText", "TextHeight", "ArrangePaceNote", "StackPaceRows", "SamplePaceCheck", "Corners"):
    method = re.search(r"^    (?:private|internal) (?:static )?(?:void|float|IEnumerable<Vector3>) " + name + r"\([^)]*\)\n    \{.*?^    }", profiles, re.M | re.S)
    if method is None:
        raise ValueError("Missing production profile menu method: " + name)
    profile_members.append(method.group())
pace_row = re.search(r"^    private GameObject PaceRow\(.*?^    }", profiles, re.M | re.S).group()
pace_layout = re.search(r"        beforeLayout.Add\(\(\) => \{.*?        }\);", pace_row, re.S).group()
profile_members.append("private void AddPaceLayout(GameObject wrapper, Mesh mesh, float localUnit, float localWidth, "
    "Component name, Component stats, PegUIElement control, Component glyph, bool kit) {\n"
    "var nameScale = name.transform.localScale; var statsScale = stats.transform.localScale; "
    "var controlScale = control.transform.localScale;\n" + pace_layout + "\n}")
form_kit = (Path(__file__).parents[2] / "plugin/ProfileFormKit.cs").read_text()
rendered_bounds = re.search(r"^    internal static Bounds\? RenderedBounds\(.*?^    }", form_kit, re.M | re.S).group()
Path(sys.argv[1]).write_text(
    "static partial class DeckLayoutRuntimeHarness { private static void Bind() {\n" + placement_selection.group() + "\n}\n}\n"
    + "static partial class ProfileMenuRuntimeHarness {\nprivate sealed partial class NativeProfileMenu : ButtonListMenu {\n"
    + "\n".join(profile_members) + "\n}\nprivate static partial class ProfileFormKit {\n" + rendered_bounds + "\n}\n}\n")
