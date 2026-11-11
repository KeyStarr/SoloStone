using SabberStoneCore.Model;
using static Core2026Proof;

internal static class B18TPrimalfinCardsProof
{
    [GroupProof("b18-t-primalfin-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("TLC_251", (g, p, id) => {
        var token = Cards.FromId("TLC_251e");
        Core2026Proof.Check(token.Implemented && token.AssetId == 121022 && token.Text == "Your next <b>Kindred</b> triggers twice.", "pinned Challenger enchantment identity");
        Play(g, p, id); Core2026Proof.Check(B18TPrimalfinProof.Pending(p) == 1, "Battlecry installs pending modifier");
        B18TPrimalfinProof.Match(p, "TLC_440"); int deck = p.DeckZone.Count;
        Play(g, p, "TLC_440", p.Opponent.Hero);
        Core2026Proof.Check(B18TPrimalfinProof.Pending(p) == 0 && p.DeckZone.Count == deck - 3, "Unpinned_Primalfin repeats next clause twice total");
    });
}
