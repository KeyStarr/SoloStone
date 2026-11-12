// The record of one admission fault: native-match checks packets with it, and the controller admission invariant (shared with
// native-match-logic) reports engine faults in the same shape. NativeAdmission is partial; the rest of it is in native-match.
internal static partial class NativeAdmission
{
    internal sealed record Fault(string Rule, int Row, int Source, int Entity, string Detail, string SourceCard = "");
}
