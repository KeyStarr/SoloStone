using System.Xml.Serialization;

// Event XP settlement (rule 2, docs/design/pet-xp.md): counters recorded on the receipt, knobs frozen in the rule string,
// replay validation, the V14 to V15 upgrade, and the history of older rules alongside it.
internal static class PetEventXp
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    private static LocalPetXpCounts Counts(int small = 0, int medium = 0, int large = 0) => new LocalPetXpCounts { Small = small, Medium = medium, Large = large };

    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh, string root)
    {
        PetXpPolicy previous = PetXpPolicy.Current;
        try
        {
            // Production catalogue: Krush (native 3) has 7 likes, so a game can record at most 9 events = 18 counter ticks
            // (owner ruling 2026-10-28: 20 XP per like and per treat = two MEDIUM ticks each).
            string path = fresh("pet-event-xp", 100000);
            string rule = PetXpPolicy.PresetB.RuleString(LocalShop.Default.PetXpCap("PET_3_1"), PetXpPolicy.PresetB.MaxTicks(3));
            check(rule == "rule=3;like=medium;treat=medium;liketicks=2;treatticks=2;small=5;medium=10;large=10;gamecap=0;concede=0;short=0;turns=4;mult=100;ticks=18;cap=6750",
                "preset B rule string records the tick knobs and freezes the tick bound");
            check(PetXpPolicy.MaxEvents(3) == 9 && PetXpPolicy.MaxEvents(16) == 11 && PetXpPolicy.MaxEvents(77) == 2, "event count is the family's like count plus two treats");
            check(PetXpPolicy.PresetB.MaxTicks(3) == 18 && PetXpPolicy.PresetB.MaxTicks(16) == 22 && PetXpPolicy.PresetB.MaxTicks(77) == 4 && PetXpPolicy.PresetA.MaxTicks(3) == 9,
                "the tick bound is likes x ticks-per-like + 2 x ticks-per-treat");
            // Rule 2 receipts settled before the 20-XP ruling (what lane 3 holds) still parse, validate and pay by their own rules.
            const string legacy2 = "rule=2;like=medium;treat=medium;small=5;medium=10;large=10;gamecap=0;concede=0;short=0;turns=4;mult=100;events=9;cap=6750";
            check(legacy2 == PetXpPolicy.FormatEvents("medium", "medium", 5, 10, 10, 0, false, false, 4, 100, 9, 6750), "the rule=2 format is frozen");
            check(LocalShop.PetXpAfter(legacy2, 0, new LocalMatchRecord { Result = "Won", Turns = 6, PetMedium = 4, PetSmall = 0, PetLarge = 0 }) == 40
                && LocalShop.PetXpAfter(rule, 0, new LocalMatchRecord { Result = "Won", Turns = 6, PetMedium = 4, PetSmall = 0, PetLarge = 0 }) == 40, "rule 2 and rule 3 pay the same recorded counters the same way");
            using (var p = new LocalProfile(path))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "event-buy");
                // Rule 2 math: 4 MEDIUM events = 40 XP, from the recorded counters, not a flat award.
                var m = p.Begin("mage", "priest", 501);
                var done = p.Settle(m.Id, "Won", 6, false, petXp: Counts(medium: 4));
                check(done.PetXpBefore == 0 && done.PetXpAfter == 40 && done.PetRules == rule && done.PetSmall == 0 && done.PetMedium == 4 && done.PetLarge == 0,
                    "event settlement pays the recorded counters at 5/10/10 and records the counters and rules");
                byte[] settled = File.ReadAllBytes(path);
                check(p.Settle(m.Id, "Won", 6, false, petXp: Counts(medium: 4)).PetXpAfter == 40 && settled.SequenceEqual(File.ReadAllBytes(path)), "replay returns the original receipt byte-exact");
                reject(() => p.Settle(m.Id, "Won", 6, false, petXp: Counts(medium: 3)), "replay with different counters conflicts");
                check(settled.SequenceEqual(File.ReadAllBytes(path)), "conflicting replay changed nothing");
                p.AcknowledgeRewardPresentation(m.Id);
                // Ineligible games record the counters and pay nothing: concede, under the gate, abandoned.
                foreach (var outcome in new[] { ("Lost", 6, true, "conceded"), ("Won", 3, false, "short"), ("Tied", 4, false, "tied pays"), ("Lost", 9, false, "lost pays") })
                {
                    var g = p.Begin("mage", "priest", 502);
                    int before = p.PetXp("PET_3_1");
                    var r = p.Settle(g.Id, outcome.Item1, outcome.Item2, outcome.Item3, petXp: Counts(medium: 2));
                    int gain = outcome.Item4 == "conceded" || outcome.Item4 == "short" ? 0 : 20;
                    check(r.PetXpBefore == before && r.PetXpAfter == before + gain && r.PetMedium == 2, "eligibility under rule 2: " + outcome.Item4);
                    p.AcknowledgeRewardPresentation(g.Id);
                }
                p.Begin("mage", "priest", 503); p.RecoverInterrupted();
                var abandoned = p.History.Last();
                check(abandoned.Result == "Abandoned" && abandoned.PetXpAfter == abandoned.PetXpBefore && abandoned.PetSmall == 0 && abandoned.PetMedium == 0 && abandoned.PetLarge == 0,
                    "recovery records zero growth and zero counters");
                // No events: nothing.
                var quiet = p.Begin("mage", "priest", 504); int quietBefore = p.PetXp("PET_3_1");
                check(p.Settle(quiet.Id, "Won", 9, false).PetXpAfter == quietBefore, "a match with no pet events pays nothing");
                p.AcknowledgeRewardPresentation(quiet.Id);
                // Inputs beyond what one game can produce are refused before anything is written.
                var bad = p.Begin("mage", "priest", 505); byte[] pending = File.ReadAllBytes(path);
                reject(() => p.Settle(bad.Id, "Won", 9, false, petXp: Counts(medium: 19)), "more ticks than the family can bank refused");
                reject(() => p.Settle(bad.Id, "Won", 9, false, petXp: Counts(medium: -1)), "negative counters refused");
                check(pending.SequenceEqual(File.ReadAllBytes(path)), "refused counters wrote nothing");
                p.Settle(bad.Id, "Won", 9, false, petXp: Counts(medium: 18)); p.AcknowledgeRewardPresentation(bad.Id);
                check(p.PetXp("PET_3_1") == 40 + 20 + 20 + 180, "the nine-event ceiling is 18 ticks = 180 XP at 20 per event (preset B)");
            }
            using (var p = new LocalProfile(path)) check(p.PetXp("PET_3_1") == 260 && p.History.Count == 8, "event history reopens and revalidates");
            // Tampered receipts are rejected on load.
            foreach (var tamper in new (string, Action<LocalProfileData>)[] {
                ("raised counter", d => d.Matches[0].PetMedium++), ("missing counter", d => d.Matches[0].PetSmall = null),
                ("negative counter", d => { d.Matches[0].PetSmall = -2; d.Matches[0].PetMedium = 5; }), ("oversized counters", d => d.Matches[0].PetLarge = 50),
                ("paid after", d => d.Matches[0].PetXpAfter += 5), ("counters on an unstamped receipt", d => { d.Matches[0].PetRules = null; }),
                ("noncanonical rules", d => d.Matches[0].PetRules = d.Matches[0].PetRules.Replace("mult=100", "mult=0100")),
                ("rules with a knob dropped", d => d.Matches[0].PetRules = d.Matches[0].PetRules.Replace("gamecap=0;", "")) })
            {
                var data = Read(path); tamper.Item2(data);
                string forged = Path.Combine(root, "pet-event-forged-" + Guid.NewGuid() + ".xml"); Write(forged, data);
                reject(() => { using var p = new LocalProfile(forged); }, "forged event receipt rejected: " + tamper.Item1);
            }
            // The event bound is frozen in each receipt's rule string: editing today's like table never invalidates history.
            string Single(string name, PetXpPolicy policy) {
                string file = fresh(name, 100000);
                using var p = new LocalProfile(file);
                p.Purchase("pet-PET_3_1", 1, 5000, "frozen-buy");
                PetXpPolicy.Current = policy;
                var m = p.Begin("mage", "priest", 560); p.Settle(m.Id, "Won", 8, false, petXp: policy.Like == PetCounter.Small ? Counts(small: 1) : Counts(medium: 1));
                PetXpPolicy.Current = PetXpPolicy.PresetB;
                return file;
            }
            bool Opens(string file) { try { using var p = new LocalProfile(file); return true; } catch (InvalidOperationException) { return false; } }
            void Rewrite(string file, string events, int small, int medium, int large, string to) {
                var d = Read(file); var m = d.Matches[0];
                m.PetRules = System.Text.RegularExpressions.Regex.Replace(m.PetRules, ";(ticks|events)=\\d+;", ";$1=" + events + ";");
                m.PetSmall = small; m.PetMedium = medium; m.PetLarge = large;
                m.PetXpAfter = small * 5 + medium * 10 + large * 10; d.PetProgress[0].Xp = m.PetXpAfter.Value;
                Write(to, d);
            }
            string frozen = Single("pet-event-frozen", PetXpPolicy.PresetB);
            Rewrite(frozen, "3", 0, 3, 0, frozen + ".ok3");
            Rewrite(frozen, "2", 0, 3, 0, frozen + ".bad2");
            Rewrite(frozen, "20", 0, 15, 0, frozen + ".big");
            check(Opens(frozen + ".ok3") && !Opens(frozen + ".bad2"), "counts are validated against the receipt's own frozen bound");
            check(Opens(frozen + ".big"), "a receipt frozen with a larger bound than today's like table stays valid (a retuned list never invalidates history)");
            // Counters the rule does not map likes or treats to must be zero (here like=medium, treat=medium).
            Rewrite(frozen, "18", 1, 0, 0, frozen + ".small"); Rewrite(frozen, "18", 0, 0, 1, frozen + ".large");
            check(!Opens(frozen + ".small") && !Opens(frozen + ".large"), "a counter outside the rule's like/treat mapping is rejected");
            string small = Single("pet-event-small", PetXpPolicy.PresetA);
            Rewrite(small, "9", 0, 1, 0, small + ".medium"); Rewrite(small, "9", 1, 0, 0, small + ".ok");
            check(Opens(small + ".ok") && !Opens(small + ".medium"), "preset A receipts allow only the SMALL counter");
            // Handoff clamp: a bad count from the match can never abort settlement.
            var clamped = LocalProfile.ClampPetXp(Counts(small: -3, medium: 20, large: 4), "PET_3_1", out bool wasClamped);
            check(wasClamped && clamped.Small == 0 && clamped.Medium == 18 && clamped.Large == 0, "oversized and negative counts are clamped to the family's tick limit");
            var intact = LocalProfile.ClampPetXp(Counts(medium: 3), "PET_3_1", out wasClamped);
            check(!wasClamped && intact.Small == 0 && intact.Medium == 3 && intact.Large == 0, "valid counts pass the handoff unchanged");
            var unmapped = LocalProfile.ClampPetXp(Counts(small: 20, medium: 2, large: 3), "PET_3_1", out wasClamped);
            check(wasClamped && unmapped.Small == 0 && unmapped.Medium == 2 && unmapped.Large == 0, "counters the policy maps nothing to are dropped at the handoff");
            using (var p = new LocalProfile(fresh("pet-event-clamp-unmapped", 100000)))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "clamp-unmapped-buy"); var m = p.Begin("mage", "priest", 570);
                var safe = LocalProfile.ClampPetXp(Counts(small: 20), "PET_3_1", out wasClamped);
                var receipt = p.Settle(m.Id, "Won", 8, false, petXp: safe);
                check(wasClamped && receipt.Result == "Won" && receipt.PetXpAfter == 0 && p.Pending == null, "an unmapped-counter handoff still settles the whole receipt");
            }
            check(LocalProfile.ClampPetXp(Counts(medium: 1), null, out wasClamped) == null && LocalProfile.ClampPetXp(null, "PET_3_1", out wasClamped) == null, "no pet or no counts: nothing to hand over");
            using (var p = new LocalProfile(fresh("pet-event-clamp", 100000)))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "clamp-buy"); var m = p.Begin("mage", "priest", 570);
                var safe = LocalProfile.ClampPetXp(Counts(medium: 500), "PET_3_1", out wasClamped);
                var receipt = p.Settle(m.Id, "Won", 8, false, petXp: safe);
                check(wasClamped && receipt.Result == "Won" && receipt.PetXpAfter == 180 && p.History.Count == 1 && p.Pending == null, "a clamped handoff still settles the whole receipt");
            }

            // Rule 1 and rule 2 receipts live in one history.
            string mixed = fresh("pet-event-mixed", 100000);
            using (var p = new LocalProfile(mixed))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "mixed-buy");
                PetXpPolicy.Current = PetXpPolicy.PresetC;
                var flat = p.Begin("mage", "priest", 510); p.Settle(flat.Id, "Won", 8, false); p.AcknowledgeRewardPresentation(flat.Id);
                PetXpPolicy.Current = PetXpPolicy.PresetB;
                var events = p.Begin("mage", "priest", 511); p.Settle(events.Id, "Won", 8, false, petXp: Counts(medium: 4)); p.AcknowledgeRewardPresentation(events.Id);
            }
            // Turn the second receipt into the rule=2 form lane 3 holds (one tick per event pays the same 40 XP), then add a rule 3 receipt after it.
            var oldForm = Read(mixed);
            oldForm.Matches[1].PetRules = legacy2;
            Write(mixed, oldForm);
            using (var p = new LocalProfile(mixed))
            {
                var third = p.Begin("mage", "priest", 512); p.Settle(third.Id, "Won", 8, false, petXp: Counts(medium: 2)); p.AcknowledgeRewardPresentation(third.Id);
            }
            using (var p = new LocalProfile(mixed))
            {
                var history = p.History;
                check(p.PetXp("PET_3_1") == 160 && history[0].PetRules == "rule=1;xp=100;cap=6750" && !history[0].PetSmall.HasValue
                    && history[1].PetRules == legacy2 && history[1].PetMedium == 4 && history[2].PetRules == rule && history[2].PetMedium == 2,
                    "rule 1, rule 2 and rule 3 receipts validate in one history");
            }
            // Test-only pacing knob: one-shot, frozen at Begin, recorded as mult=, bounded, ignored without test control.
            string pacing = fresh("pet-event-pacing", 100000);
            using (var p = new LocalProfile(pacing))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "pace-buy");
                var plain = p.Begin("mage", "priest", 580);
                check(!plain.PetPacingPercent.HasValue, "no knob armed: nothing is frozen (pacing is the policy's 100)");
                p.Settle(plain.Id, "Won", 8, false, petXp: Counts(medium: 2));
                check(p.History.Single().PetRules.Contains(";mult=100;") && p.History.Single().PetXpAfter == 20, "absent knob settles at 100 percent");
                p.AcknowledgeRewardPresentation(plain.Id);
                reject(() => p.ArmPetPacing(99), "pacing below 100 refused"); reject(() => p.ArmPetPacing(10001), "pacing above 10000 refused");
                p.ArmPetPacing(5000);
                var boosted = p.Begin("mage", "priest", 581);
                check(boosted.PetPacingPercent == 5000 && p.Pending.PetPacingPercent == 5000, "the armed percent is frozen into the pending record at Begin");
                var settled = p.Settle(boosted.Id, "Won", 8, false, petXp: Counts(medium: 2));
                check(settled.PetXpBefore == 20 && settled.PetXpAfter == 20 + 20 * 50 && settled.PetRules.Contains(";mult=5000;") && !settled.PetPacingPercent.HasValue,
                    "the boosted match pays x50 and records mult=5000 in its rule string; the pending-only field is gone");
                check(p.PetLevel("PET_3_1") >= 2 && p.CanUsePetForm("PET_3_2"), "a boosted match reaches the next form in one game");
                p.AcknowledgeRewardPresentation(boosted.Id);
                var next = p.Begin("mage", "priest", 582);
                check(!next.PetPacingPercent.HasValue, "one-shot: the next match is back to 100");
                p.Settle(next.Id, "Won", 8, false, petXp: Counts(medium: 2));
                check(p.History.Last().PetRules.Contains(";mult=100;") && p.History.Last().PetXpAfter == p.History[1].PetXpAfter + 20, "the match after a boosted one pays normally");
                p.AcknowledgeRewardPresentation(next.Id);
                // Consumed even when Begin fails (a pending match already exists), so it can never leak onto a later match.
                var holder = p.Begin("mage", "priest", 583);
                p.ArmPetPacing(2500);
                reject(() => p.Begin("mage", "priest", 584), "Begin refused while a match is pending");
                p.Settle(holder.Id, "Won", 8, false, petXp: Counts(medium: 2)); p.AcknowledgeRewardPresentation(holder.Id);
                check(!p.Begin("mage", "priest", 585).PetPacingPercent.HasValue, "a failed Begin still consumes the one-shot");
                p.RecoverInterrupted();
            }
            using (var p = new LocalProfile(pacing)) check(p.PetXp("PET_3_1") >= 1000, "boosted history reopens and revalidates by its own mult=");
            // No pet recipient: nothing to freeze. Preset C (flat) ignores the knob. A stored percent outside the range, or on a receipt, rejects the save.
            using (var p = new LocalProfile(fresh("pet-event-pacing-none", 100000)))
            {
                p.ArmPetPacing(5000);
                check(!p.Begin("mage", "priest", 590).PetPacingPercent.HasValue, "a match with no pet XP recipient freezes no pacing");
            }
            using (var p = new LocalProfile(fresh("pet-event-pacing-flat", 100000)))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "flat-pace-buy"); PetXpPolicy.Current = PetXpPolicy.PresetC;
                p.ArmPetPacing(5000); var m = p.Begin("mage", "priest", 591);
                check(!m.PetPacingPercent.HasValue && p.Settle(m.Id, "Won", 8, false).PetXpAfter == 100, "preset C (rule 1) ignores the pacing knob");
                PetXpPolicy.Current = PetXpPolicy.PresetB;
            }
            string pend = fresh("pet-event-pacing-pending", 100000);
            using (var p = new LocalProfile(pend)) { p.Purchase("pet-PET_3_1", 1, 5000, "pend-buy"); p.ArmPetPacing(300); p.Begin("mage", "priest", 592); }
            foreach (var forged in new (string, Action<LocalProfileData>)[] { ("below range", d => d.Pending.PetPacingPercent = 99), ("above range", d => d.Pending.PetPacingPercent = 10001),
                ("no recipient", d => { d.Pending.PetXpBefore = null; d.Pending.Pet = null; }) })
            {
                var data = Read(pend); forged.Item2(data);
                string bad = Path.Combine(root, "pet-pace-forged-" + Guid.NewGuid() + ".xml"); Write(bad, data);
                reject(() => { using var p = new LocalProfile(bad); }, "forged pending pacing rejected: " + forged.Item1);
            }
            using (var p = new LocalProfile(pend)) check(p.Pending.PetPacingPercent == 300, "a valid pending pacing survives a restart");
            var onReceipt = Read(pacing); onReceipt.Matches[1].PetPacingPercent = 5000; string receiptPath = Path.Combine(root, "pet-pace-receipt.xml"); Write(receiptPath, onReceipt);
            reject(() => { using var p = new LocalProfile(receiptPath); }, "a settled receipt never carries the pending-only pacing field");
            // The file knob: bound to the launch's run id, parsed strictly, read only with test control live on a test lane, deleted whenever seen.
            const string run = "1a2b3c4d-2222-3333-4444-55555555555a";
            check(PetPacingKnob.Parse("runId=" + run + "\npercent=5000\n", run) == 5000 && PetPacingKnob.Parse("runId=" + run + "\r\npercent=100", run) == 100
                && PetPacingKnob.Parse("runId=" + run + "\npercent=10000\n\n", run) == 10000, "pacing file parses its percent for this run");
            foreach (string badText in new[] { "", "percent=5000", "percent=5000\n", "runId=" + run, "runId=" + run + "\npercent=99", "runId=" + run + "\npercent=10001", "runId=" + run + "\npercent=-5",
                "runId=" + run + "\npercent=abc", "runId=" + run + "\npercent=5000\nextra=1", "percent=5000\nrunId=" + run, "runId=other\npercent=5000", "runId=" + run.ToUpperInvariant() + "\npercent=5000", "mult=5000" })
                reject(() => PetPacingKnob.Parse(badText, run), "pacing file rejects: " + badText.Replace("\n", "|"));
            reject(() => PetPacingKnob.Parse("runId=\npercent=5000", ""), "an unknown run id accepts nothing");
            string reports = Path.Combine(root, "reports"); Directory.CreateDirectory(reports);
            // Lane 0 (the primary test client) and the Deck have no knob path at all; only lanes 1-3 do.
            check(PetPacingKnob.PathFor(0, false, reports) == null && PetPacingKnob.PathFor(0, true, reports) == null && PetPacingKnob.PathFor(2, true, reports) == null
                && PetPacingKnob.PathFor(4, false, reports) == null && PetPacingKnob.PathFor(-1, false, reports) == null && PetPacingKnob.PathFor(1, false, null) == null,
                "lane 0, the Deck and unknown lanes never get a pacing path");
            check(Enumerable.Range(1, 3).All(lane => PetPacingKnob.PathFor(lane, false, reports) == reports + @"\pet-xp-pacing"), "lanes 1-3 do");
            // Real launch contexts, resolved the way the plugin resolves them: a Deck host launch and lane 0 never arm the knob, even
            // with a valid knob file carrying the right run id sitting where a test lane would read it.
            string launchRun = "4d93167d-fc51-4ba0-a79f-64e524ce5a27", elsewhere = Path.Combine(root, "valid-knob-elsewhere");
            File.WriteAllText(elsewhere, "runId=" + launchRun + "\npercent=5000\n");
            var deckLaunch = OfflineGuard.ResolveLaunch(@"STEAMDECK\steamuser", "STEAMDECK", @"C:\hs-native-ui-"+@"prototype\client\Hearthstone.exe",
                @"C:\users\steamuser\AppData\Local", new[] { "-hs-native-ui-run", launchRun, "-hs-native-ui-host", "deck" }, false, true);
            check(deckLaunch.Deck && PetPacingKnob.PathFor(deckLaunch.LaneId, deckLaunch.Deck, deckLaunch.Reports) == null, "a -hs-native-ui-host deck launch gets no pacing path");
            check(PetPacingKnob.Take(PetPacingKnob.PathFor(deckLaunch.LaneId, deckLaunch.Deck, deckLaunch.Reports), true, launchRun) == null && File.Exists(elsewhere),
                "the Deck never arms the knob, even with test control live and a valid file with the right run id");
            foreach (int lane in new[] { 0, 1, 2, 3 })
            {
                var windows = OfflineGuard.ResolveLaunch(@"LAB\HSNativeUIProbe", "LAB", OfflineGuard.LaneRoot(lane) + @"\client\Hearthstone.exe",
                    @"C:\Us"+@"ers\HSNative"+@"UIProbe\AppData\Local", new[] { "-hs-native-ui-run", launchRun, "-hs-native-ui-lane", lane.ToString() }, false);
                string lanePath = PetPacingKnob.PathFor(windows.LaneId, windows.Deck, windows.Reports);
                check(!windows.Deck && (lane == 0 ? lanePath == null : lanePath == windows.Reports + @"\pet-xp-pacing"), "launch context lane " + lane + (lane == 0 ? " has no pacing path" : " reads its own reports directory"));
                if (lane == 0) check(PetPacingKnob.Take(lanePath, true, launchRun) == null && File.Exists(elsewhere), "lane 0 never arms the knob, even with test control live and a valid file with the right run id");
            }
            string knobFile = Path.Combine(root, "pet-xp-pacing"); var logs = new List<string>();
            string valid = "runId=" + run + "\npercent=5000\n";
            File.WriteAllText(knobFile, valid);
            check(PetPacingKnob.Take(null, true, run) == null && File.Exists(knobFile), "no knob path (lane 0, the Deck): nothing is read, nothing taken");
            check(PetPacingKnob.Take(knobFile, false, run, logs.Add) == null && !File.Exists(knobFile), "a knob file seen with test control off is deleted, never applied");
            File.WriteAllText(knobFile, valid);
            check(PetPacingKnob.Take(knobFile, true, run, logs.Add) == 5000 && !File.Exists(knobFile), "with test control live and this run's id the knob is read once and deleted");
            check(PetPacingKnob.Take(knobFile, true, run, logs.Add) == null, "one-shot: a second read finds nothing");
            logs.Clear(); File.WriteAllText(knobFile, "runId=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\npercent=5000\n");
            check(PetPacingKnob.Take(knobFile, true, run, logs.Add) == null && !File.Exists(knobFile) && logs.Count == 1, "a stale run id is dropped, logged and deleted");
            logs.Clear(); File.WriteAllText(knobFile, "percent=5000\n");
            check(PetPacingKnob.Take(knobFile, true, run, logs.Add) == null && !File.Exists(knobFile) && logs.Count == 1, "a file without a run id is dropped and deleted");
            logs.Clear(); File.WriteAllText(knobFile, "runId=" + run + "\npercent=99\n");
            check(PetPacingKnob.Take(knobFile, true, run, logs.Add) == null && !File.Exists(knobFile) && logs.Count == 1, "an out-of-range file is rejected, logged and deleted");
            File.WriteAllText(knobFile, valid); PetPacingKnob.ClearStale(knobFile);
            check(!File.Exists(knobFile), "plugin start clears any knob file left by an earlier launch");
            PetPacingKnob.ClearStale(knobFile); PetPacingKnob.ClearStale(null);

            // The knobs travel in the rule string, so a later retune never changes an old receipt.
            string knobs = fresh("pet-event-knobs", 100000);
            using (var p = new LocalProfile(knobs))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "knob-buy");
                int Gain(PetXpPolicy policy, string result, int turns, bool conceded, LocalPetXpCounts counts)
                {
                    PetXpPolicy.Current = policy;
                    var m = p.Begin("mage", "priest", 520); var r = p.Settle(m.Id, result, turns, conceded, petXp: counts); p.AcknowledgeRewardPresentation(m.Id);
                    return r.PetXpAfter.Value - r.PetXpBefore.Value;
                }
                var a = PetXpPolicy.PresetA;
                check(Gain(a, "Won", 6, false, Counts(small: 4)) == 20, "preset A: SMALL pays 5 (counters decide, preset only chooses the counter)");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GameCap = 25, GateTurns = 4, PacingPercent = 100 }, "Won", 6, false, Counts(medium: 4)) == 25,
                    "optional per-game cap clamps the counter XP");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 4, PacingPercent = 150 }, "Won", 6, false, Counts(medium: 3)) == 45,
                    "pacing multiplier hook scales the XP (percent, rounded down)");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 4, PacingPercent = 33 }, "Won", 6, false, Counts(medium: 1)) == 3, "pacing result is whole XP rounded down");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 4, PacingPercent = 100, PayConceded = true }, "Lost", 6, true, Counts(medium: 2)) == 20,
                    "a policy that pays conceded games pays them");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 4, PacingPercent = 100, PayShort = true }, "Won", 2, false, Counts(medium: 2)) == 20,
                    "a policy that pays short games pays them");
                check(Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 6, PacingPercent = 100 }, "Won", 5, false, Counts(medium: 2)) == 0
                    && Gain(new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, GateTurns = 6, PacingPercent = 100 }, "Won", 6, false, Counts(medium: 2)) == 20, "the turn threshold is a knob");
                check(Gain(PetXpPolicy.PresetB, "Won", 6, false, Counts(medium: 2)) == 20, "back on preset B");
            }
            // Every knob combination above was frozen into its receipt: reopening revalidates all of them under their own rules.
            PetXpPolicy.Current = PetXpPolicy.PresetB;
            using (var p = new LocalProfile(knobs))
            {
                var rules = p.History.Select(m => m.PetRules).Distinct().ToArray();
                check(rules.Length == 8 && rules.All(r => r.StartsWith("rule=3;")) && p.History.Count == 9, "each receipt carries its own knob values: " + rules.Length);
            }

            // V14 to V15: trivial in place, byte-exact .v14.bak; V14 cannot already hold event counters.
            string v14 = fresh("pet-v14-upgrade", 100000);
            using (var p = new LocalProfile(v14))
            {
                p.Purchase("pet-PET_3_1", 1, 5000, "v14-buy");
                PetXpPolicy.Current = PetXpPolicy.PresetC;
                var m = p.Begin("mage", "priest", 530); p.Settle(m.Id, "Won", 8, false); p.AcknowledgeRewardPresentation(m.Id);
                PetXpPolicy.Current = PetXpPolicy.PresetB;
                p.Begin("mage", "priest", 531);
            }
            var asV14 = Read(v14); asV14.Version = 14; Write(v14, asV14); byte[] v14Bytes = File.ReadAllBytes(v14);
            string v14settle = Path.Combine(root, "pet-v14-pending-settle.xml"); File.Copy(v14, v14settle);
            using (var p = new LocalProfile(v14))
                check(p.PetXp("PET_3_1") == 100 && p.Pending != null && p.History.Count == 1, "V14 profile with pet history and a pending match opens and upgrades");
            // A match pending across the upgrade settles normally under the event rules.
            using (var p = new LocalProfile(v14settle))
            {
                var done = p.Settle(p.Pending.Id, "Won", 8, false, petXp: Counts(medium: 2));
                check(done.PetXpBefore == 100 && done.PetXpAfter == 120 && done.PetRules.StartsWith("rule=3;") && p.PetXp("PET_3_1") == 120, "a V14 pending match settles under the event rules after the upgrade");
            }
            using (var p = new LocalProfile(v14settle)) check(p.PetXp("PET_3_1") == 120 && p.History.Count == 2, "mixed rule 1 (V14) and rule 2 history reopens");
            check(Read(v14).Version == 15 && v14Bytes.SequenceEqual(File.ReadAllBytes(v14 + ".v14.bak")), "V14 upgrade is in place with a byte-exact .v14.bak");
            var upgradedData = Read(v14); upgradedData.Version = 14;
            using (var before = new MemoryStream()) using (var after = new MemoryStream())
            {
                Serializer.Serialize(before, asV14); Serializer.Serialize(after, upgradedData);
                check(before.ToArray().SequenceEqual(after.ToArray()), "V14 upgrade changes only the version");
            }
            foreach (var invalid in new Action<LocalProfileData>[] { d => d.Matches[0].PetMedium = 1, d => d.Matches[0].PetRules = rule, d => d.Matches[0].PetRules = legacy2 })
            {
                string poisoned = fresh("pet-v14-counters-" + Guid.NewGuid().ToString("N"), 100000);
                using (var p = new LocalProfile(poisoned)) {
                    p.Purchase("pet-PET_3_1", 1, 5000, "p"); PetXpPolicy.Current = PetXpPolicy.PresetC;
                    var m = p.Begin("mage", "priest", 540); p.Settle(m.Id, "Won", 8, false); PetXpPolicy.Current = PetXpPolicy.PresetB;
                }
                var data = Read(poisoned); data.Version = 14; invalid(data); Write(poisoned, data);
                reject(() => { using var p = new LocalProfile(poisoned); }, "a V14 save cannot hold event counters");
                check(!File.Exists(poisoned + ".v14.bak"), "a rejected V14 file makes no backup");
            }
        }
        finally { PetXpPolicy.Current = previous; }
        Console.WriteLine("PASS pet event XP: rule 2 math, eligibility, bounds, replay, tamper, knobs, mixed history, V14 to V15 upgrade");
    }
}
