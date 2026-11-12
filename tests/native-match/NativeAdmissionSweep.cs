using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;

internal static partial class Program
{
    private sealed record AdmissionResult(string Family, string Card, int Seat, List<NativeAdmission.Fault> Faults);
    private static readonly List<AdmissionResult> admissionResults = new();
    private static void AdmitFixture(string family, string card, int seat, IEnumerable<PegasusGame.PowerHistory> packets, SabberStoneCore.Model.Game game)
    {
        var sent = packets.ToArray();
        InventoryEmptyTriggers(family, card, seat, sent, game);
        var faults = NativeAdmission.ValidatePackets(sent.Select(p => (IReadOnlyList<PowerHistoryData>)p.List));
        faults.AddRange(ControllerAdmissionInvariant.Faults(game));
        faults.AddRange(TransportInvariants.BoardActorInvariant.Validate(sent)
            .Select(detail => new NativeAdmission.Fault("R6", -1, 0, 0, detail)));
        admissionResults.Add(new(family, card, seat, faults));
        foreach (var fault in faults) Console.WriteLine($"ADMISSION FAIL {family}/{card}/seat{seat}: {fault}");
        if (faults.Count > 0 && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HS_DRAW_HISTORY_OUTPUT")))
        {
            var capture = new Capture(); foreach (var packet in sent) capture.Send(19, packet);
            DumpDrawHistory($"{family}-{card}-seat{seat}", capture, 0);
        }
    }

    private static void NativeAdmissionSweep(string onlyCard = null)
    {
        StandardBatchLoanerTransport(admissionOnly: true, admissionCard: onlyCard);
        AutomaticAdmissionFixtures(onlyCard);
        FinishAdmissionSweep();
        if (onlyCard == null) ChaosStrikeHistory();
    }

    private static void AutomaticAdmissionFixtures(string onlyCard = null)
        => TransportInvariants.AutoCastProof.Coverage((id, owner, rows, game) => AdmitFixture("automatic", id, owner, rows, game), onlyCard);

    private static void FinishAdmissionSweep()
    {
        NativeAdmissionControls();
        Check(admissionResults.Count > 0, "native admission captured no fixtures");
        var lines = new List<string> { "# Native admission sweep — 2026-10-30", "",
            "Outgoing NativeMatch PowerHistory (R1-R4, R6) and engine zone admission (R5). R1: logical type at native segment entry for pinned DH/Frost impacts; unknown FX may consume untyped-only inferred or authored targets, missing explicit IDs and mixed typed/untyped inference remain violations; complete-list metadata resolution, TAG_CHANGE fallback and explicit subspell IDs. Blocks, subspell boundaries, FLUSH and packets split lists; parent/previous-list metadata never protects a continuation. R2: hidden opposing PLAY must be SPELL + SECRET. R3: hidden public PLAY / own HAND or SECRET transitions require identity first. R4: Core Jaraxxus POWER owns HERO_POWER + WEAPON FULL_ENTITY. R5: every engine HAND/DECK/PLAY admission must have CONTROLLER equal to Controller.PlayerId; historyless search clones do not maintain transport tags and are excluded. R6: after every packet, public PLAY entities have identity and minion/location actors have no hidden null-zone transit; both viewer orientations.", "",
            $"Fixtures: {admissionResults.Count}; Standard={admissionResults.Count(r => r.Family == "standard")}, automatic={admissionResults.Count(r => r.Family == "automatic")}. Both seats. Source-card counts identify the card whose block violates admission, not every fixture containing that card (automatic fixtures cast common school spells first). Fixture-card counts and repeated block/target faults are separate. When a block source is anonymous, its fixture card ID is the diagnostic fallback; this never supplies identity to the client.", "" };
        foreach (string rule in new[] { "R1", "R2", "R3", "R4", "R5", "R6" })
        {
            var affected = admissionResults.Where(r => r.Faults.Any(f => f.Rule == rule)).ToArray();
            string Card(AdmissionResult result, NativeAdmission.Fault fault) => string.IsNullOrEmpty(fault.SourceCard) ? result.Card : fault.SourceCard;
            var violations = affected.SelectMany(r => r.Faults.Where(f => f.Rule == rule).Select(f => (Result: r, Fault: f, Card: Card(r, f)))).ToArray();
            string summary = $"{rule}: {violations.Select(v => v.Card).Distinct().Count()} source cards, {affected.Select(r => r.Card).Distinct().Count()} fixture cards, {violations.Length} faults";
            Console.WriteLine("ADMISSION " + summary);
            lines.Add("## " + summary); lines.Add("");
            foreach (int seat in new[] { 1, 2 })
            {
                string[] cards = violations.Where(v => v.Result.Seat == seat).Select(v => v.Card).Distinct().Order(StringComparer.Ordinal).ToArray();
                lines.Add($"Seat {seat}: {cards.Length} cards. " + (cards.Length == 0 ? "None." : string.Join(", ", cards.Select(c => "`" + c + "`")))); lines.Add("");
            }
            foreach (var group in violations.GroupBy(v => (v.Card, v.Result.Family, v.Result.Seat)).OrderBy(g => g.Key.Card, StringComparer.Ordinal).ThenBy(g => g.Key.Family).ThenBy(g => g.Key.Seat))
            {
                var sample = group.First();
                lines.Add($"- `{group.Key.Card}` / {group.Key.Family} / seat {group.Key.Seat}: {group.Count()} faults across {group.Select(v => v.Result.Card).Distinct().Count()} fixture cards; first in `{sample.Result.Card}` row {sample.Fault.Row}, entity {sample.Fault.Entity}.");
            }
            lines.Add("");
        }
        string output = Environment.GetEnvironmentVariable("HS_NATIVE_ADMISSION_REPORT");
        if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))); File.WriteAllLines(output, lines); }
        Check(admissionResults.All(r => r.Faults.Count == 0), "native admission violations; set HS_NATIVE_ADMISSION_REPORT for full card/seat list");
        NativeAdmissionTransportControls();
        Console.WriteLine($"PASS native admission: {admissionResults.Count} fixtures, R1/R2/R3/R4/R5/R6; {ControllerAdmissionInvariant.Admissions} engine HAND/DECK/PLAY admissions observed");
    }
}
