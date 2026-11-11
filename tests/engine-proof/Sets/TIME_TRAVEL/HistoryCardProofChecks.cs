using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using static Core2026Proof;

internal static class HistoryCardProofChecks
{
    internal static void HiddenDisplay(Game original, string id)
    {
        foreach (bool hand in new[] { false, true })
        {
            var a = original.CloneExact(history: true); var b = original.CloneExact(history: true);
            int hiddenId = a.Player2.DeckZone[0].Id;
            foreach (var g in new[] { a, b })
            {
                var p = g.Player2; var hidden = p.DeckZone[0];
                if (g == a) { Generic.ChangeEntityBlock(p, hidden, Cards.FromId(id), false); hidden = g.IdEntityDic[hidden.Id]; }
                if (hand) { p.DeckZone.Remove(hidden); p.HandZone.Add(hidden); }
            }
            int ca = a.PowerHistory.Full.Count, cb = b.PowerHistory.Full.Count;
            foreach (var g in new[] { a, b })
            {
                g.Player1.Hero.TakeDamage(g.Player2.Hero, 1);
                g.Player1.Hero.TakeHeal(g.Player1.Hero, 1);
                g.Player2.Hero.TakeDamage(g.Player1.Hero, 1);
                var treant = Summon(g.Player2, "END_009t"); Kill(g, treant);
                End(g); End(g);
            }
            // HistoryDisplay caches private numeric tags; all other raw history must stay identical.
            bool PublicEntry(IPowerHistoryEntry entry) => !(entry is PowerHistoryTagChange tag
                && tag.EntityId == hiddenId && tag.Tag == GameTag.TAG_SCRIPT_DATA_NUM_1);
            Core2026Proof.Check(a.PowerHistory.Full.Skip(ca).Where(PublicEntry).Select(e => Canonical.Value(e))
                .SequenceEqual(b.PowerHistory.Full.Skip(cb).Where(PublicEntry).Select(e => Canonical.Value(e))),
                id + " hidden deck/hand display adds only private N1 changes");
#if CLIENT_TRANSPORT
            var packetsA = StarsurgeProof.Packets(a, ca); var packetsB = StarsurgeProof.Packets(b, cb);
            Core2026Proof.Check(packetsA.Count == packetsB.Count && packetsA.Zip(packetsB).All(pair => pair.First.SequenceEqual(pair.Second)),
                id + " hidden deck/hand identity emits identical native packets");
#endif
            Core2026Proof.Check(Canonical.Value(a.Player1.NativeTags) == Canonical.Value(b.Player1.NativeTags)
                && Canonical.Value(a.Player2.NativeTags) == Canonical.Value(b.Player2.NativeTags), "no public controller display leak");
            a.ProjectHistoryForFairView(a.Player1.PlayerId); b.ProjectHistoryForFairView(b.Player1.PlayerId);
            Core2026Proof.Check(a.HistoryFacts.CanonicalText() == b.HistoryFacts.CanonicalText(), "hidden permutations yield identical projected history");
        }
    }
}
