using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CrimsonSigilRunnerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_480", (g, p, id) => {
        Core2026Proof.Check(SabberStoneCore.Model.Cards.FromId(id).AssetId == 69683, "Crimson dbfId");
        OutcastProof.DrawCases(g, p, id, 0);
        OutcastProof.EmptyHand(p);
        var brann = Summon(p, "LOE_077"); g.AuraUpdate();
        Core2026Proof.Check(p.ExtraBattlecry, "Brann aura active");
        int deck = p.DeckZone.Count;
        var runner = (Minion)Play(g, p, id);
        // R4 tier C: https://outof.games/realms/hearthstone/keywords/battlecry (pseudo-Battlecries).
        Core2026Proof.Check(!runner.HasBattleCry && p.DeckZone.Count == deck - 1, "R4 Brann does not double Outcast; exactly one draw");
        Generic.ReturnToHandBlock(p, runner);
        var filler = Add(p, "CS2_182");
        Core2026Proof.Check(runner.ZonePosition > 0 && runner.ZonePosition < p.HandZone.Count - 1, "bounced Runner now in middle");
        deck = p.DeckZone.Count; PlayEntity(g, p, runner);
        Core2026Proof.Check(p.DeckZone.Count == deck && !OutcastRules.PlayedFromEdge(runner), "bounced middle replay has no stale Outcast");
        OutcastProof.EmptyHand(p);
        deck = p.DeckZone.Count; var summoned = Summon(p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck && !OutcastRules.PlayedFromEdge(summoned), "R3 effect summon has no Outcast");
        StandardSetsProof.ClearBoard(g, p);
        OutcastProof.DrawBurn(g, p, id, 1, fillOnPlay: true);
    });
}
