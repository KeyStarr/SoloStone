// Deck picker record line (NativeDeckRecord): pure placement under the hero name. Synthetic picker at 1280x720, scaled to the screens the owner
// checks (the layout is relative, so the choice must be the same at every size); live geometry is verified by hslive deck-record.
internal static class DeckRecordLayoutChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (label, scale) in new[] { ("1280x720", 1f), ("1280x800", 800f / 720), ("1920x1080", 1.5f) })
        {
            DeckRect Box(float left, float top, float width, float height) => new DeckRect(left * scale, (720 - top - height) * scale, width * scale, height * scale);
            float w = 1280 * scale, h = 720 * scale;
            var name = Box(150, 330, 200, 34);
            var text = (w: 130 * scale, h: 20 * scale);
            var power = Box(120, 420, 120, 120);   // hero power below the name
            var play = Box(875, 530, 120, 120);
            var rects = new[] { play, Box(1008, 643, 104, 40), Box(365, 633, 233, 47) };

            // Free space on both sides: centred directly under the name, one gap below.
            var spot = DeckRecordLayout.Place(name, text.w, text.h, w, h, rects, out int slot);
            check(spot.HasValue && slot == DeckRecordLayout.Below, "record goes under the hero name when free " + label);
            var s = spot!.Value;
            check(Math.Abs(s.X + s.Width / 2 - (name.X + name.Width / 2)) < .01f && s.Y + s.Height < name.Y && Math.Abs(s.Width - text.w) < .001f,
                "record is centred under the name and does not touch it " + label);
            check(name.Y - (s.Y + s.Height) > 0 && name.Y - (s.Y + s.Height) <= text.h, "record is within one text height of the name " + label);
            foreach (var other in rects) check(!OptionsPaceLayout.Overlap(s, other), "record overlaps no picker control " + label);

            // Something sits right under the name (hero power): the same spot above the name is used.
            var covered = DeckRecordLayout.Place(name, text.w, text.h, w, h, rects.Concat(new[] { power, Box(120, 366, 160, 30) }), out int above);
            check(covered.HasValue && above == DeckRecordLayout.Above && covered.Value.Y >= name.Y + name.Height, "blocked below: record goes above the name " + label);
            check(!OptionsPaceLayout.Overlap(covered!.Value, power), "record does not overlap the hero power " + label);

            // Padding: an obstacle inside the pad of the nearest spot moves the record (never onto it); one clear of every pad does not.
            var close = new DeckRect(s.X, s.Y - text.h * DeckRecordLayout.PadShare * .5f - 1, s.Width, 1);
            var moved = DeckRecordLayout.Place(name, text.w, text.h, w, h, new[] { close }, out int closeSlot);
            check(moved.HasValue && closeSlot != 0 && Math.Abs(moved.Value.Y - s.Y) > .01f && !OptionsPaceLayout.Overlap(moved.Value, close),
                "an obstacle inside the pad moves the record, clear of it " + label);
            var far = new DeckRect(s.X, s.Y - text.h * DeckRecordLayout.PadShare * 2, s.Width, 1);
            var stay = DeckRecordLayout.Place(name, text.w, text.h, w, h, new[] { far }, out int farSlot);
            check(stay.HasValue && farSlot == DeckRecordLayout.Below && Math.Abs(stay.Value.Y - s.Y) < .01f, "an obstacle outside the pad does not " + label);
            // Loose art right under the name (the hero actor's box) is stepped over when there is room below it, else the line goes above.
            var loose = new DeckRect(name.X - 40 * scale, name.Y - 4 * scale, name.Width + 80 * scale, 6 * scale);
            var below = DeckRecordLayout.Place(name, text.w, text.h, w, h, new[] { loose }, out int looseSlot);
            check(below.HasValue && looseSlot == DeckRecordLayout.Below && below.Value.Y + below.Value.Height < name.Y && !OptionsPaceLayout.Overlap(below.Value, loose),
                "loose art under the name: the line steps further down " + label);

            // Both spots blocked, or off screen: no line, slot 0.
            var wall = new DeckRect(0, 0, w, h);
            check(DeckRecordLayout.Place(name, text.w, text.h, w, h, new[] { wall }, out int none) == null && none == 0, "no free spot: no record " + label);
            var top = new DeckRect(100 * scale, h - 40 * scale, 200 * scale, 34 * scale);
            var edge = DeckRecordLayout.Place(top, text.w, text.h, w, h, new DeckRect[0], out int edgeSlot);
            check(edge.HasValue && edgeSlot == DeckRecordLayout.Below, "an anchor near the top still has room below " + label);
            var bottom = new DeckRect(100 * scale, 4 * scale, 200 * scale, 34 * scale);
            var floor = DeckRecordLayout.Place(bottom, text.w, text.h, w, h, new DeckRect[0], out int floorSlot);
            check(floor.HasValue && floorSlot == DeckRecordLayout.Above && floor.Value.Y >= 0, "an anchor at the bottom falls back above, on screen " + label);
            var corner = new DeckRect(w - 20 * scale, h / 2, 20 * scale, 34 * scale);
            var side = DeckRecordLayout.Place(corner, text.w, text.h, w, h, new DeckRect[0], out _);
            check(side == null || side.Value.X + side.Value.Width <= w, "a line that would leave the screen is never returned " + label);

            // Degenerate input fails closed.
            check(DeckRecordLayout.Place(new DeckRect(0, 0, 0, 0), text.w, text.h, w, h, rects, out int a0) == null && a0 == 0, "no anchor rect: no record " + label);
            check(DeckRecordLayout.Place(name, 0, text.h, w, h, rects, out _) == null && DeckRecordLayout.Place(name, text.w, 0, w, h, rects, out _) == null, "no text size: no record " + label);
            check(DeckRecordLayout.Place(name, text.w, text.h, 0, 0, rects, out _) == null, "no screen: no record " + label);
            check(DeckRecordLayout.Place(name, text.w, text.h, w, h, null, out int nullSlot) != null && nullSlot == 1
                && DeckRecordLayout.Place(name, text.w, text.h, w, h, new[] { new DeckRect(0, 0, 0, 0) }, out int emptySlot) != null && emptySlot == 1, "missing and empty obstacles are ignored " + label);
        }
    }
}
