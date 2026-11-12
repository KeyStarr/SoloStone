using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StoryOfCarnassaProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_826", (g, p, id) => {
        var before = p.DeckZone.Select(c => c.Id).ToArray(); var enemy = p.Opponent.DeckZone.Select(c => c.Id).ToArray();
        Play(g, p, id); var raptors = p.DeckZone.Where(c => c.Card.Id == "UNG_920t2").Cast<Minion>().ToArray();
        Core2026Proof.Check(raptors.Length == 10 && raptors.All(m => m.Cost == 1 && m.AttackDamage == 3 && m.Health == 2 && m.IsRace(Race.BEAST) && m.Card.Implemented), "ten pinned 1-Cost 3/2 Beast Raptors");
        Core2026Proof.Check(p.DeckZone.Count == before.Length + 10 && before.All(n => p.DeckZone.Any(c => c.Id == n)) && p.Opponent.DeckZone.Select(c => c.Id).SequenceEqual(enemy), "preserves existing cards and opponent deck");
        var raptor = raptors[0]; p.DeckZone.Remove(raptor); p.HandZone.Add(raptor); int deck = p.DeckZone.Count;
        PlayEntity(g, p, raptor); Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1, "Raptor Battlecry draws one");
        foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        var spell = Add(p, id); var clone = g.CloneExact(history: true); PlayEntity(g, p, spell); PlayEntity(clone, clone.Player1, clone.IdEntityDic[spell.Id]);
        Core2026Proof.Check(p.DeckZone.Count == 10 && Canonical.State(g) == Canonical.State(clone), "empty deck shuffle deterministic through game RNG");
    });
}
