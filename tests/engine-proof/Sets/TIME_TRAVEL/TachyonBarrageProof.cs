using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TachyonBarrageProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_027", (g, p, id) => {
        int count = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && p.Hero.Damage == 0 && p.DeckZone.Count == count + 2 && p.DeckZone.Count(c => c.Card.Id == "TIME_025t") == 2, "six enemy-only missiles, two exact Shreds");
        var history = g.PowerHistory.Full.OfType<PowerHistoryTagChange>().ToList();
        int damageIndex = history.FindIndex(e => e.EntityId == p.Opponent.Hero.Id && e.Tag == GameTag.DAMAGE && e.Value == 6);
        var shredIds = p.DeckZone.Where(c => c.Card.Id == "TIME_025t").Select(c => c.Id).ToHashSet();
        int shuffleIndex = history.FindIndex(e => shredIds.Contains(e.EntityId) && e.Tag == GameTag.ZONE && e.Value == (int)Zone.DECK);
        Core2026Proof.Check(damageIndex >= 0 && shuffleIndex > damageIndex, "damage resolves before Shreds are shuffled");
        Summon(p, "TIME_856"); p.Opponent.Hero.Damage = 0; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 8, "spell power adds missiles rather than damage per missile");
        ClearBoard(g, p); p.Opponent.Hero.Damage = 0; var enemy = Summon(p.Opponent, "CS2_171"); var clone = g.CloneExact(history: true);
        Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "seeded missile selection identical after clone");
        Core2026Proof.Check(enemy.Damage + p.Opponent.Hero.Damage == 6 && enemy.Damage <= 1, "dead one-health target excluded from later missiles");
        var shred = p.DeckZone.First(c => c.Card.Id == "TIME_025t"); Generic.Draw(p, shred); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(shred.Zone == p.GraveyardZone && p.Hero.Damage == 3, "Barrage Shred casts on draw");
    });
}
