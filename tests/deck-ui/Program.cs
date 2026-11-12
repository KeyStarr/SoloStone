int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
DeckLayoutRuntimeHarness.Run(Check);
ProfileMenuRuntimeHarness.Run(Check);
// Save picker paging after a delete (six a page): the page falls back to the last one that still has rows; Next only with rows beyond.
Check(new[] { (0, 0, 0), (0, 5, 0), (1, 6, 0), (1, 7, 1), (2, 13, 2), (3, 13, 2), (3, 12, 1), (4, 20, 3), (9, 1, 0), (-1, 4, 0) }
    .All(row => ProfileMenuGeometry.ClampPage(row.Item1, row.Item2) == row.Item3), "page clamps to the last page that has rows");
Check(!ProfileMenuGeometry.HasNext(0, 6) && ProfileMenuGeometry.HasNext(0, 7) && !ProfileMenuGeometry.HasNext(1, 12) && ProfileMenuGeometry.HasNext(2, 20) && !ProfileMenuGeometry.HasNext(3, 20),
    "Next appears only when rows remain past the page");
Check(NativeSaveDeleteText.RowId("a1") == "save.delete:a1" && !NativeSaveDeleteText.RowId("a1").StartsWith("save.slot:", StringComparison.Ordinal)
    && NativeSaveDeleteText.MarkedLabel("Save 1").EndsWith("Save 1", StringComparison.Ordinal) && NativeSaveDeleteText.MarkedLabel("Save 1").StartsWith(NativeSaveDeleteText.DeleteMark, StringComparison.Ordinal),
    "manage rows carry their own id and a delete mark, never the open-save id");
// Diagnostic windows expire and exhaust without reopening until the existing flag is disabled.
var trace = new DeckInputTraceWindow();
trace.Update(false, 0);
Check(!trace.Active && !trace.Take(0), "disabled input diagnostics emit nothing");
trace.Update(true, 1);
Check(trace.Active && trace.Take(1), "enabled diagnostics start a capture window");
Check(trace.Take(20.99) && !trace.Take(21), "diagnostics stop at twenty seconds");
trace.Update(true, 22);
Check(!trace.Active && !trace.Take(22), "expired diagnostics do not restart while flag stays enabled");
trace.Update(false, 23); trace.Update(true, 24);
Check(Enumerable.Range(0, 1024).All(_ => trace.Take(24)), "bounded trace admits its event budget");
Check(!trace.Active && !trace.Take(24), "diagnostics stop at event cap");
trace.Update(true, 25);
Check(!trace.Active, "event cap does not restart while flag stays enabled");
trace.Update(false, 26); trace.Update(true, 27);
Check(trace.Take(27), "flag off then on opens a fresh bounded capture");
// One counted native game dialog (the save picker) owns input; a concrete popup above it blocks it.
var pickerContext = new DeckModalContext(1, null, false, false, false);
var coveredContext = new DeckModalContext(1, new object(), false, true, false);
Check(pickerContext.OwnsSingleButtonList && !coveredContext.OwnsSingleButtonList, "single native button-list owns input but a nested alert blocks it");
Check(!coveredContext.Matches(new DeckModalContext(1, new object(), false, true, false)), "modal replacement changes context despite same flags");
Check(!new DeckModalContext(2, null, false, false, false).OwnsSingleButtonList, "another native menu blocks the picker");
Check(!new DeckModalContext(1, null, true, false, false).OwnsSingleButtonList, "loading modal blocks the picker");
Check(!new DeckModalContext(1, null, false, false, true).OwnsSingleButtonList, "notification modal blocks the picker");
foreach (int height in new[] { 800, 720 })
foreach (int count in new[] { 3, 4 }) {
    var row = DeckMulliganGeometry.Cards(1280, height, count);
    Check(row.Length == count, "card count");
    for (int i = 0; i < count; i++) {
        var card = row[i];
        Check(card.X >= 0 && card.Y >= 0 && card.X + card.Width <= 1280 && card.Y + card.Height <= height, "card inside screen");
        Check(card.Width >= 220 && card.Width <= 240 && card.Height >= 330 && card.Height <= 370, "card target size");
        if (i > 0) Check(card.X - (row[i - 1].X + row[i - 1].Width) >= 42, "enlarged cards have breathing room");
    }
    var confirm = DeckMulliganGeometry.Confirm(1280, height);
    Check(confirm.Y + confirm.Height < row[0].Y && confirm.Y >= 0, "confirm below row, clear hitboxes");
}
// Current-save check (measured on the 1280x800 native plank: row 522..758, face 557..722): a fixed inset inside the
// face's left edge, padded; a name that would touch it is nudged right; only an extreme name falls back to text.
foreach (float scale in new[] { .25f, 1f, 3f }) {
    float rowMin = 522 * scale, rowMax = 758 * scale, faceMin = rowMin + (rowMax - rowMin) * ProfileMenuGeometry.FaceLeft,
        faceMax = rowMin + (rowMax - rowMin) * ProfileMenuGeometry.FaceRight, w = 22 * scale, pad = w * ProfileMenuGeometry.CheckPadShare;
    // "me", "Save 1"/"UI test", a wider name, then the widest name that can still be nudged.
    foreach (var name in new (float Min, float Max)[] { (624 * scale, 656 * scale), (604 * scale, 676 * scale), (590 * scale, 690 * scale), (584 * scale, 692 * scale) }) {
        var plan = ProfileMenuGeometry.PlanCheck(rowMin, rowMax, name.Min, name.Max, w, 6);
        Check(plan.HasValue, "normal names always get a check");
        if (!plan.HasValue) continue;
        float centre = plan.Value.Center, shift = plan.Value.NameShift;
        Check(centre - w / 2 >= faceMin + pad - .001f && centre + w / 2 <= faceMax, "check sits fully inside the plank face with padding");
        Check(Math.Abs(centre - (faceMin + pad + w / 2)) < .001f, "check keeps its fixed inset whatever the name");
        Check(name.Min + shift >= centre + w / 2 + pad * ProfileMenuGeometry.NameGapShare - .001f, "name never touches the check");
        Check(name.Max + shift <= faceMax, "nudged name stays inside the face");
    }
    Check(ProfileMenuGeometry.PlanCheck(rowMin, rowMax, 624 * scale, 656 * scale, w, 2).Value.NameShift == 0, "short centred name is not moved");
    Check(ProfileMenuGeometry.PlanCheck(rowMin, rowMax, 560 * scale, 720 * scale, w, 14) == null, "a name filling the plank gets no check (text label instead)");
    Check(ProfileMenuGeometry.PlanCheck(rowMin, rowMax, 600 * scale, 680 * scale, 200 * scale, 6) == null, "oversized check refused");
    // Unmeasured name (glyph bounds are not laid out when the row is built): estimated from its length. Every normal
    // name (up to 11 characters) gets the check; the name is nudged only as far as it needs; extreme names fall back.
    foreach (int chars in new[] { 1, 2, 6, 7, 9, 11 }) {
        var guess = ProfileMenuGeometry.PlanCheck(rowMin, rowMax, null, null, w, chars);
        Check(guess.HasValue && Math.Abs(guess.Value.Center - (faceMin + pad + w / 2)) < .001f, "unmeasured name of " + chars + " chars gets the check at its fixed inset");
        if (!guess.HasValue) continue;
        float half = chars * ProfileMenuGeometry.EstimatedCharShare * (rowMax - rowMin) / 2, mid = (rowMin + rowMax) / 2;
        Check(mid - half + guess.Value.NameShift >= guess.Value.Center + w / 2 + pad * ProfileMenuGeometry.NameGapShare - .001f
            && mid + half + guess.Value.NameShift <= faceMax, "estimated name never touches the check and stays in the face");
    }
    Check(ProfileMenuGeometry.PlanCheck(rowMin, rowMax, null, null, w, 6).Value.NameShift == 0, "UI test / Save 1 sized names are not moved");
    Check(ProfileMenuGeometry.PlanCheck(rowMin, rowMax, null, null, w, 12) == null && ProfileMenuGeometry.PlanCheck(rowMin, rowMax, null, null, w, 32) == null,
        "extreme unmeasured names fall back to text");
}
foreach (var label in new[] { "me", "Save 1", "a really very long save name that goes on", "", null }) {
    string marker = ProfileMenuGeometry.FallbackLabel(label);
    Check(marker.Length <= ProfileMenuGeometry.FallbackChars && marker.StartsWith("* "), "text fallback fits the plank face and marks the row");
}
Check(ProfileMenuGeometry.FallbackLabel("Short") == "* Short" && ProfileMenuGeometry.FallbackLabel("0123456789ABCDEF").EndsWith(".."), "fallback keeps short names, truncates long ones");
foreach (var screen in new[] { (Width: 1280f, Height: 720f), (Width: 1280f, Height: 800f), (Width: 1920f, Height: 1080f), (Width: 3840f, Height: 2160f) }) {
    // Name input sits over the hidden label: frame/glyph rects in bottom-left screen pixels, result in top-left GUI fractions.
    float unit = screen.Height * ProfileMenuGeometry.UnitShare;
    var frame = new DeckRect(screen.Width * .4f, screen.Height * .6f, screen.Width * .2f, unit * .7f);
    var glyphs = new DeckRect(screen.Width * .46f, screen.Height * .6f + unit * .2f, screen.Width * .08f, unit * .3f);
    foreach (var known in new DeckRect?[] { glyphs, null }) {
        var input = ProfileMenuGeometry.NameInput(frame, known, screen.Width, screen.Height);
        var pixels = new DeckRect(input.X * screen.Width, input.Y * screen.Height, input.Width * screen.Width, input.Height * screen.Height);
        Check(pixels.X >= 0 && pixels.Y >= 0 && pixels.X + pixels.Width <= screen.Width + .01f && pixels.Y + pixels.Height <= screen.Height + .01f,
            "save name input remains inside native viewport");
        Check(Math.Abs(pixels.X - frame.X) < .01f && Math.Abs(pixels.Width - frame.Width) < .01f, "save name input spans the value box text rect");
        float centre = known.HasValue ? known.Value.Y + known.Value.Height / 2 : frame.Y + frame.Height / 2;
        Check(Math.Abs(screen.Height - (pixels.Y + pixels.Height / 2) - centre) < .5f, "save name input is centred on the label it replaces");
        Check(pixels.Height <= frame.Height * 1.2f + .01f && pixels.Height >= Math.Min(14f, screen.Height * .02f) - .01f,
            "save name input font follows the label, not the whole screen");
    }
    var offscreen = ProfileMenuGeometry.NameInput(new DeckRect(-50, -10, screen.Width + 100, 40), null, screen.Width, screen.Height);
    Check(offscreen.X >= 0 && offscreen.X + offscreen.Width <= 1.0001f && offscreen.Y >= 0 && offscreen.Y + offscreen.Height <= 1.0001f,
        "save name input clamps to the viewport");
    var picker = ProfileMenuGeometry.Place(screen.Width, screen.Height, 30, 300, 200);
    var subPixel = ProfileMenuGeometry.Place(screen.Width, screen.Height, .46f, 2.9f, 2.7f);
    Check(Math.Abs(.46f * subPixel.Scale - unit) < .01f, "sub-pixel native menu still scales to the unit height");
    Check(Math.Abs(30 * picker.Scale - unit) < .01f, "short save list scales one plank to the native unit height");
    Check(picker.CenterX == screen.Width / 2 && picker.CenterY == screen.Height / 2, "save menu is screen-centred");
    var form = ProfileMenuGeometry.Place(screen.Width, screen.Height, 30, 300, 900);
    Check(900 * form.Scale <= screen.Height * ProfileMenuGeometry.MaxHeightShare + .01f && 30 * form.Scale <= unit + .01f,
        "tall expanded form shrinks to fit the screen height");
    var wide = ProfileMenuGeometry.Place(screen.Width, screen.Height, 30, 2000, 200);
    Check(2000 * wide.Scale <= screen.Width * ProfileMenuGeometry.MaxWidthShare + .01f, "wide form shrinks to fit the screen width");
    var repeat = ProfileMenuGeometry.Place(screen.Width, screen.Height, 30 * form.Scale, 300 * form.Scale, 900 * form.Scale);
    Check(Math.Abs(repeat.Scale - 1) < .00001f, "save menu fitting is stable across repeated layout");
}
foreach (var size in new[] { (1280,800), (1280,720), (800,600), (640,480) })
{
    var confirm=DeckMulliganGeometry.Confirm(size.Item1,size.Item2);
    Check(confirm.Height>=size.Item2*.14f && confirm.Width>=size.Item1*.2f, "Confirm has readable aspect-fit budget");
    Check(confirm.Y>size.Item2*.1f && confirm.Y+confirm.Height<DeckMulliganGeometry.Cards(size.Item1,size.Item2,4)[0].Y,
        "Confirm stays above HUD and below enlarged cards at smaller resolutions");
}
// Bottom bar: currency scenes and a match show it, a transition never does.
Check(HandheldPolicy.BarVisible(true, false, false) && HandheldPolicy.BarVisible(false, true, false), "bar shows in currency scenes and in a match");
Check(!HandheldPolicy.BarVisible(false, false, false) && !HandheldPolicy.BarVisible(false, true, true) && !HandheldPolicy.BarVisible(true, false, true), "bar hidden elsewhere and during transitions");
// Help popup fit: never shrinks, bound by the tighter axis, capped, and modest on a desktop monitor.
Check(HandheldPolicy.FitScale(577, 348, 1280, 800, .88f, 3f) > 1.9f && HandheldPolicy.FitScale(577, 348, 1280, 800, .88f, 3f) < 2f, "Deck Help fit uses the width limit");
Check(HandheldPolicy.FitScale(300, 700, 1280, 800, .88f, 3f) < 1.05f && HandheldPolicy.FitScale(2000, 900, 1280, 800, .88f, 3f) == 1f, "fit never shrinks and obeys height");
Check(HandheldPolicy.FitScale(100, 100, 1280, 800, .88f, 3f) == 3f && HandheldPolicy.FitScale(0, 0, 1280, 800, .88f, 3f) == 1f, "fit is capped and ignores empty rects");
HandheldPolicy.HelpFit(true, out float handheldShare, out float handheldMax);
HandheldPolicy.HelpFit(false, out float desktopShare, out float desktopMax);
Check(handheldShare > desktopShare && handheldMax > desktopMax && desktopMax <= 1.5f, "desktop Help enlargement is modest");
Check(HandheldPolicy.FitScale(840, 470, 1920, 1080, desktopShare, desktopMax) <= 1.5f, "Help is not oversized at 1920x1080");
// Tooltip depth layering: in front of a surface it overlaps (never moved when already clear), Help sunk behind a tooltip plane.
Check(HandheldPolicy.LiftInFront(10f, 9f, .5f) == 1.5f && HandheldPolicy.LiftInFront(8f, 9f, .5f) == 0f, "tooltip lifts until wholly in front of the dialog, never when clear");
Check(HandheldPolicy.SinkBehind(9f, 10f, .5f) == 1.5f && HandheldPolicy.SinkBehind(11f, 10f, .5f) == 0f, "Help sinks behind the tooltip plane, never when already behind");
// Frame rate: a saved rate above 30 that start-up lowered is put back; a deliberate 30 or a first run is never touched.
Check(HandheldPolicy.RateToRestore(90, 30) == 90 && HandheldPolicy.RateToRestore(60, 30) == 60, "lowered rate restored");
Check(HandheldPolicy.RateToRestore(90, 90) == 0 && HandheldPolicy.RateToRestore(30, 30) == 0 && HandheldPolicy.RateToRestore(0, 60) == 0, "kept, deliberate 30 and first run untouched");
Check(HandheldPolicy.RateToRestore(90, 144) == 0, "a raised rate is not lowered");
// Game Menu rows: Achievements dropped, save selector relabelled, divider runs collapsed.
var rows = new List<string> { "SoloStoneHelpButton", "SoloStoneAchievementsButton", "SoloStoneSaveSelection", null, "OptionsButton" };
var relabelled = new List<string>();
int removed = HandheldPolicy.TidyRows(rows, row => row, "SoloStoneAchievementsButton", "SoloStoneSaveSelection", relabelled.Add);
Check(removed == 1 && rows.SequenceEqual(new[] { "SoloStoneHelpButton", "SoloStoneSaveSelection", null, "OptionsButton" }) && relabelled.SequenceEqual(new[] { "SoloStoneSaveSelection" }), "Tidy drops Achievements and relabels the save row");
var dividers = new List<string> { "A", null, null, "B", null };
Check(HandheldPolicy.TidyRows(dividers, row => row, "x", "y", _ => { }) == 1 && dividers.SequenceEqual(new[] { "A", null, "B", null }), "Tidy collapses doubled dividers");
var inMatch = new List<string> { "ConcedeButton", null, "OptionsButton", null, "QuitButton" };
Check(HandheldPolicy.TidyRows(inMatch, row => row, "SoloStoneAchievementsButton", "SoloStoneSaveSelection", _ => { }) == 0 && inMatch.Count == 5, "in-match menu is left alone");
// Save form retries: the Options kit retries a failure once per open; a refused text input retries a bounded number of times.
var kit = new ProfileFormRetry();
Check(kit.Start(false) && !kit.Start(true) && !kit.Loaded, "kit loads once at a time");
kit.Failed();
Check(!kit.Start(false), "a failed kit does not retry within the same open");
Check(kit.Start(true) && !kit.Start(true), "a new open retries the failed kit once");
kit.Succeeded();
Check(kit.Loaded && !kit.Start(true), "a loaded kit never reloads");
var nameInput = new TextInputRetry();
Check(Enumerable.Range(1, TextInputRetry.MaxAttempts - 1).All(_ => nameInput.Again()) && !nameInput.Again(), "text input retries are bounded");
nameInput.Reset();
Check(nameInput.Again(), "a successful edit resets the text input retries");
var lockout = new ClickLockout();
Check(!lockout.Blocks(0), "no lockout before the first rebuild");
lockout.Arm(5);
Check(lockout.Blocks(5) && lockout.Blocks(5.29f) && !lockout.Blocks(5.31f), "lockout lasts about 300 ms");
Check(ProfileInputBlock.Reason(true, false, false, true, true) == ProfileInputBlock.Lockout, "a picker plank in its click lockout reports blocked");
Check(ProfileInputBlock.Reason(true, false, false, true, false) == null, "a picker plank past its lockout is clickable");
Check(ProfileInputBlock.Reason(true, false, false, false, true) == "profileSelector", "a control the picker does not own stays covered");
Check(ProfileInputBlock.Reason(true, true, true, true, true) == null, "an alert's own buttons ignore the lockout of the picker beneath");
Check(ProfileInputBlock.Reason(true, true, false, true, false) == "dialog", "planks under an alert are covered");
Check(ProfileInputBlock.Reason(false, true, false, false, true) == null, "nothing is blocked once the picker is gone");

var paceInner = new DeckRect(0, 0, 100, 100);
List<string> PaceIssues(params ProfilePaceGeometry.Element[] elements) => ProfilePaceGeometry.Check(paceInner, elements);
ProfilePaceGeometry.Element PaceElement(string id, float x, float y, float width, float height) => new(id, new DeckRect(x, y, width, height));
Check(PaceIssues(PaceElement("name", 10, 10, 20, 10)).Count == 0, "paceCheck accepts inner glyphs");
Check(PaceIssues(PaceElement("stats", -1, 10, 20, 10)).SequenceEqual(new[] { "stats:outside" }), "paceCheck reports outside element by id");
Check(PaceIssues(PaceElement("check", 10, 10, 20, 20), PaceElement("name", 20, 10, 20, 20))
    .Contains("check/name:overlap"), "paceCheck detects overlapping checkbox and text");
Check(PaceIssues(PaceElement("check", 10, 10, 20, 20), PaceElement("name", 30, 10, 20, 20)).Count == 0,
    "paceCheck allows touching edges");
Check(PaceIssues().Count == 0, "paceCheck accepts empty list");
Check(PaceIssues(PaceElement("note", 10, 10, 0, 10)).Contains("note:empty-or-invalid"), "paceCheck rejects empty measurement");
Check(PaceIssues(PaceElement("note", float.NaN, 10, 10, 10)).Contains("note:empty-or-invalid"), "paceCheck rejects non-finite measurement");
Check(ProfilePaceGeometry.Check(default, Array.Empty<ProfilePaceGeometry.Element>()).Contains("inner:empty-or-invalid"),
    "paceCheck rejects empty inner rect");

// Options > Pace plank: rects read off a live 1280x720 Options capture (top-down px), scaled about the screen centre for the Deck.
foreach (var (screenHeight, label) in new[] { (720f, "1280x720"), (800f, "1280x800") })
{
    float s = screenHeight / 720;
    float Y(float top) => screenHeight / 2 + (top - 360) * s;
    float X(float left) => 640 + (left - 640) * s;
    DeckRect Row(float left, float top, float width, float height) => new DeckRect(X(left), screenHeight - Y(top + height), width * s, height * s);
    DeckRect signature = Row(377, 492.7f, 235.7f, 39);
    var plank = OptionsPaceLayout.Place(signature, 205 * s, 37 * s);
    float gapAbove = signature.Y - (plank.Y + plank.Height), gapBelow = plank.Y - (screenHeight - Y(600));
    Check(gapAbove > 12 * s && gapBelow > 12 * s, "Pace plank clears the Signature dropdown and the frame: " + label);
    Check(Math.Abs(plank.X + plank.Width / 2 - (signature.X + signature.Width / 2)) < .01f && plank.X + plank.Width < X(640) - 40 * s, "Pace plank is centred in the left column, clear of the divider: " + label);
    foreach (var (name, row) in new[] { ("signature", signature), ("frameRate", Row(377, 351, 235.7f, 39)), ("fullscreen", Row(388, 398, 100, 26)),
        ("soundOptions", Row(684, 338, 205, 37)), ("turnTimer", Row(691, 431, 200, 26)), ("screenShake", Row(691, 464, 200, 26)), ("handheld", Row(691, 496, 200, 26)),
        ("restoreStarters", Row(684, 532, 205, 37)) })
        Check(!OptionsPaceLayout.Overlap(plank, row), "Pace plank overlaps no Options row: " + name + " " + label);
}
Check(OptionsPaceLayout.Overlap(new DeckRect(0, 0, 10, 10), new DeckRect(5, 5, 10, 10)) && !OptionsPaceLayout.Overlap(new DeckRect(0, 0, 10, 10), new DeckRect(10, 0, 10, 10)), "overlap test itself");

// Deck picker "Deck Guide" button: the picker's hit rects as `hslive ui` reports them live on the 1280x720 frame (lane 0, 2026-11-05: Back 1028,647
// 63x32; Play 868,529 134x130; My Collection 386,639 190x34), bottom-left origin, scaled to the screens the owner checks. It sits right-aligned over Back at the first step clear of Play, My Collection and REMATCH, never over any of them.
foreach (var (label, scale) in new[] { ("1280x720", 1f), ("1280x800", 800f / 720), ("1920x1080", 1.5f) })
{
    DeckRect Box(float left, float top, float width, float height) => new DeckRect(left * scale, (720 - top - height) * scale, width * scale, height * scale);
    var back = Box(1028, 647, 63, 32);
    var play = Box(868, 529, 134, 130);
    var collection = Box(386, 639, 190, 34);
    var rematch = Box(1028, 607, 63, 32);   // one Back height plus the REMATCH gap above Back
    // Back's frame art past its hit body, from the same live dump (1920x1080 px: art 1504.5..1670.6 x 55.4..116.9, body 1541.7..1635.6 x 61.4..109.1).
    var frame = new FrameMargin(24.8f * scale, 23.3f * scale, 4.6f * scale);
    float cx = play.X + play.Width / 2, cy = play.Y + play.Height / 2, radius = Math.Min(play.Width, play.Height) / 2;
    foreach (var (name, obstacles) in new[] { ("alone", new[] { collection }), ("with rematch", new[] { collection, rematch }) })
    {
        var plate = DeckGuideLayout.Place(back, frame, play, obstacles, out int step);
        Check(plate.HasValue && step >= 1 && step <= DeckGuideLayout.MaxSlots, "the Guide button finds a free step " + name + " " + label);
        var p = plate!.Value;
        Check(Math.Abs(p.X - back.X) < .01f && Math.Abs(p.Width - back.Width) < .01f && Math.Abs(p.Height - back.Height) < .001f,
            "the Guide button is exactly Back's size and right-aligned with it " + name + " " + label);
        Check(p.X + p.Width <= 1280 * scale && p.Y + p.Height <= 720 * scale && p.X >= 0 && p.Y >= back.Y + back.Height, "the Guide button stays on screen above Back " + name + " " + label);
        foreach (var (other, rect) in new[] { ("play", play), ("collection", collection), ("back", back) }.Concat(obstacles.Contains(rematch) ? new[] { ("rematch", rematch) } : new (string, DeckRect)[0]))
            Check(!OptionsPaceLayout.Overlap(p, rect) || other == "play", "the Guide body overlaps no " + other + " " + name + " " + label);
        // The ART, not just the hit body, clears Play's round button: nearest point of the art rect to Play's centre is outside its radius.
        float artX0 = p.X - frame.Left, artX1 = p.X + p.Width + frame.Right, artY0 = p.Y - frame.Vertical, artY1 = p.Y + p.Height + frame.Vertical;
        float nearX = Math.Clamp(cx, artX0, artX1), nearY = Math.Clamp(cy, artY0, artY1);
        float distance = MathF.Sqrt((nearX - cx) * (nearX - cx) + (nearY - cy) * (nearY - cy));
        Check(distance >= radius, "the Guide art clears Play's round button " + name + " " + label + " distance=" + distance + " radius=" + radius);
        // What the first layout did (body 1.2x Back, art 15 px over Play): the art of such a plate would NOT clear the ring at this step.
        if (step <= 2)
        {
            float oldX0 = back.X + back.Width - back.Width * 1.2f - frame.Left, oldY0 = p.Y - frame.Vertical, oldY1 = p.Y + p.Height + frame.Vertical;
            float oy = Math.Clamp(cy, oldY0, oldY1), ox = Math.Clamp(cx, oldX0, back.X + back.Width + frame.Right);
            Check(MathF.Sqrt((ox - cx) * (ox - cx) + (oy - cy) * (oy - cy)) < radius, "a 1.2x plate would have sat on Play's ring at step " + step + " " + label);
        }
        Check(step == (obstacles.Contains(rematch) ? 2 : 1) || step == (obstacles.Contains(rematch) ? 3 : 2), "the Guide button stays near Back " + name + " " + label + " step=" + step);
    }
    // A taller Play collider than the screenshot's pushes the button up, never onto Play; a Play that covers everything means no button.
    var tall = new DeckRect(play.X, play.Y, play.Width, play.Height * 1.4f);
    var tallPlate = DeckGuideLayout.Place(back, frame, tall, new[] { collection }, out int tallStep);
    Check(tallPlate.HasValue ? !OptionsPaceLayout.Overlap(tallPlate.Value, tall) : tallStep == 0, "a taller Play collider is still never overlapped " + label);
    Check(DeckGuideLayout.Place(back, frame, new DeckRect(back.X - 2500, back.Y - 2500, 5000, 5000), new DeckRect[0], out int none) == null && none == 0, "no free step means no button " + label);
    Check(DeckGuideLayout.Place(back, frame, play, new[] { new DeckRect(0, 0, 0, 0) }, out int degenerate) != null && degenerate >= 1, "empty optional rects are ignored " + label);
    // Frame sequence of a picker sliding in (the button is decided per frame, so nothing carries over): Play unknown, then Play over every
    // step, then Play clear. The button exists only in the last frame; Play without a rect never yields a position.
    Check(DeckGuideLayout.Place(back, frame, null, new[] { collection }, out int unknown) == null && unknown == 0, "Play without a rect: no button " + label);
    Check(DeckGuideLayout.Place(back, frame, new DeckRect(0, 0, 0, 0), new[] { collection }, out int empty) == null && empty == 0, "Play with an empty rect: no button " + label);
    var covering = new DeckRect(back.X - 600 * scale, back.Y, 1000 * scale, 700 * scale);
    Check(DeckGuideLayout.Place(back, frame, covering, new[] { collection }, out int covered) == null && covered == 0, "Play over every step: no button " + label);
    var settled = DeckGuideLayout.Place(back, frame, play, new[] { collection }, out int later);
    Check(settled.HasValue && later >= 1 && later <= 2, "Play clear: the button appears near Back " + label);
    Check(DeckGuideLayout.Place(new DeckRect(0, 0, 0, 0), frame, play, new DeckRect[0], out int noBack) == null && noBack == 0, "Back without a rect: no button " + label);
}

// Native button chrome (ButtonChromeRules): the numbers are the retail Back button and the old Deck Guide clone as `hslive buttons` measured them at
// 1920x1080 on 2026-11-05 (Back: plate 1541.2..1636.7, caps 20.5 and 20.8 px, "Back" 58.7 px wide; Deck Guide: root scaled 1.08 in x, label 90.6 px).
{
    var backArt = new DeckRect(1504.5f, 55.4f, 166.1f, 61.5f);
    var backBody = new DeckRect(1541.7f, 61.4f, 93.9f, 47.7f);
    var backPlate = new DeckRect(1541.2f, 61.7f, 95.5f, 46.6f);
    ButtonChromeRules.Facts Facts(DeckRect label, float capAspect = 1f, DeckRect? plate = null, float capLeft = 20.5f, float capRight = 20.8f) =>
        new ButtonChromeRules.Facts { Art = backArt, Body = backBody, Label = label, Plate = plate ?? backPlate, CapLeft = capLeft, CapRight = capRight, CapAspect = capAspect };
    DeckRect Label(float width) => new DeckRect(1588.6f - width / 2, 70.6f, width, 30f);
    var cream = ButtonChromeRules.Cream(backPlate, 20.5f, 20.8f);
    Check(Math.Abs(cream.Width - 72.4f) < .5f && Math.Abs((cream.X + cream.Width / 2) - (backPlate.X + backPlate.Width / 2)) < .01f, "cream window of the retail Back plate is about 72 px, centred");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f)), true).Count == 0, "retail Back is right: " + string.Join(",", ButtonChromeRules.Judge(Facts(Label(58.7f)), true)));
    Check(ButtonChromeRules.Judge(Facts(Label(90.6f)), true).Contains(ButtonChromeRules.LabelOutsideCream), "a label wider than the cream window is flagged (old Deck Guide, 90.6 px)");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), 1.08f), true).Contains(ButtonChromeRules.CapStretched), "a root-scaled 3-slice button has stretched caps");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), 1.02f), true).Count == 0, "a 2 percent cap aspect is within tolerance");
    Check(ButtonChromeRules.Judge(Facts(Label(40f)), true).Count == 0 && ButtonChromeRules.Judge(Facts(Label(72f)), true).Count == 0, "labels up to the cream width are right");
    var wide = ButtonChromeRules.Judge(new ButtonChromeRules.Facts { Art = backArt, Body = new DeckRect(1400, 61.4f, 400, 47.7f), Label = Label(58.7f), Plate = backPlate, CapLeft = 20.5f, CapRight = 20.8f }, true);
    Check(wide.Contains(ButtonChromeRules.BodyOutsideArt), "a hit body wider than the art is flagged");
    Check(ButtonChromeRules.Judge(new ButtonChromeRules.Facts { Art = backArt, Body = backBody, Label = null }, true).Contains(ButtonChromeRules.NoLabel), "an expected label that is not drawn is flagged");
    Check(ButtonChromeRules.Judge(new ButtonChromeRules.Facts { Art = backArt, Body = backBody, Label = null }, false).Count == 0, "no label expected, none judged");
    var plain = new ButtonChromeRules.Facts { Art = new DeckRect(0, 0, 200, 60), Body = new DeckRect(10, 5, 180, 50), Label = new DeckRect(20, 20, 160, 20) };
    Check(ButtonChromeRules.Judge(plain, true).Count == 0, "an unsliced button: label inside the body is right");
    plain.Label = new DeckRect(-30, 20, 260, 20);
    var plainIssues = ButtonChromeRules.Judge(plain, true);
    Check(plainIssues.Contains(ButtonChromeRules.LabelOutsideBody) && plainIssues.Contains(ButtonChromeRules.LabelOutsideArt), "an unsliced button: label past the body and art is flagged");
    // Cap aspect is read along the axes that run along the screen, not a hard-coded x:y (a cap whose width runs along local z).
    var uniform = new[] { 7.75f, 7.75f, 7.75f };
    Check(Math.Abs(ButtonChromeRules.CapAspect(uniform, new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }) - 1f) < .001f, "an untouched native cap reads 1");
    Check(Math.Abs(ButtonChromeRules.CapAspect(new[] { 8.37f, 7.75f, 7.75f }, new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }) - 1.08f) < .001f, "the old Deck Guide cap (x stretched 1.08) reads 1.08");
    // Non-synthetic: a prefab turned a quarter about y reads along local z (the retail mesh's z carries 7.49 vs 6.64 on the deck tray Done plate).
    var turned = new[] { 6.64f, 6.64f, 9.3f };
    Check(Math.Abs(ButtonChromeRules.CapAspect(turned, new[] { 0f, 0f, -1f }, new[] { 0f, 1f, 0f }) - 9.3f / 6.64f) < .001f
        && Math.Abs(ButtonChromeRules.CapAspect(turned, new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }) - 1f) < .001f,
        "a stretched cap whose width runs along local z is read on z (a hard-coded x:y would call it 1.0)");
    var stretchedZ = ButtonChromeRules.Judge(Facts(Label(58.7f), 9.3f / 6.64f), true);
    Check(stretchedZ.Contains(ButtonChromeRules.CapStretched), "that stretch is flagged");
    Check(Math.Abs(ButtonChromeRules.CapAspect(uniform, new[] { 1f, 0f, 0f }, new[] { 1f, 0f, 0f }) - 1f) < .001f, "both directions on one axis fall back to another axis");
    // Native art is not uniform: retail Back and My Collection read a raw 0.757 live (2026-11-05) and must pass; a root-stretched clone must not.
    const float nativeRaw = .757f;
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), ButtonChromeRules.RelativeAspect(nativeRaw, nativeRaw)), true).Count == 0, "a native cap aspect of 0.757 is not a stretch");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), ButtonChromeRules.RelativeAspect(nativeRaw, 0f)), true).Count == 0, "no native baseline: the raw aspect is not judged");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), ButtonChromeRules.RelativeAspect(nativeRaw * 1.08f, nativeRaw)), true).Contains(ButtonChromeRules.CapStretched),
        "a clone of that source with its root stretched 1.08 in x is flagged");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), ButtonChromeRules.RelativeAspect(nativeRaw * 1.4f, nativeRaw)), true).Contains(ButtonChromeRules.CapStretched),
        "the Recipe guide's old 1.4x root stretch is flagged against its source");
    Check(ButtonChromeRules.Judge(Facts(Label(58.7f), ButtonChromeRules.RelativeAspect(nativeRaw / 1.1f, nativeRaw)), true).Contains(ButtonChromeRules.CapStretched), "a squeezed cap is flagged too");
    // Hidden hover glow strip: narrower than the plate it lights is flagged; none or wider is fine (native glow is about 1.07x the plate).
    var glowFacts = Facts(Label(58.7f));
    Check(ButtonChromeRules.Judge(glowFacts, true).Count == 0, "no glow strip: nothing to judge");
    glowFacts.GlowToPlate = 1.07f;
    Check(ButtonChromeRules.Judge(glowFacts, true).Count == 0, "a glow wider than the plate is right");
    glowFacts.GlowToPlate = .8f;
    Check(ButtonChromeRules.Judge(glowFacts, true).Contains(ButtonChromeRules.GlowNarrow), "a glow left narrow by a fit that skipped it is flagged");
}

DeckRecordLayoutChecks.Run((value, label) => Check(value, label));

Console.WriteLine($"Handheld UI: {checks} checks passed");
