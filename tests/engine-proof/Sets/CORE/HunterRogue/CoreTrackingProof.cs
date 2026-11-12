using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreTrackingProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DS1_184", (g, p, id) => {
        Summon(p, "BRM_031"); // Chromaggus distinguishes a draw from generated copy.
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone); Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        int deck = p.DeckZone.Count; int drawn = p.NumCardsDrawnThisTurn;
        Play(g, p, id); Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(key => g.IdEntityDic[key].Card.Id).Distinct().Count() == 3
            && p.Choice.Choices.All(key => g.IdEntityDic[key].Zone == p.DeckZone) && p.DeckZone.Count == deck, "three distinct deck identities offered without discarding");
        int selected = p.Choice.Choices[0]; var clone = g.CloneExact(history: true);
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, selected)) && clone.Process(ChooseTask.Pick(clone.Player1, selected)), "choice accepted in both games");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Any(c => c.Id == selected) && p.NumCardsDrawnThisTurn == drawn + 1 && p.DeckZone.Count == deck - 1
            && Canonical.State(g) == Canonical.State(clone), "selected real entity drawn once, Chromaggus reacts, clone preserves pending choice");
        ParkBoard(p); foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        int fatigue = p.Hero.Damage; Play(g, p, id); Core2026Proof.Check(p.Choice == null && p.Hero.Damage == fatigue, "empty deck offers nothing without fatigue");
        var only = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone); only[GameTag.ATK] = 7;
        Play(g, p, id); Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { only.Id }), "one-card deck offers exactly that entity");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, only.Id)) && p.HandZone.Single().Id == only.Id && only[GameTag.ATK] == 7, "selected draw preserves deck entity buffs");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        var shred = Entity.FromCard(p, Cards.FromId("TIME_025t"), zone: p.DeckZone);
        var replacement = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone); int damage = p.Hero.Damage;
        Play(g, p, id); Core2026Proof.Check(p.Choice.Choices.Contains(shred.Id), "offers cast-when-drawn Shred");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, shred.Id)) && p.Choice == null && shred.Zone == p.GraveyardZone
            && p.HandZone.Single().Id == replacement.Id && p.Hero.Damage == damage + 3, "selected Shred casts then draws replacement without losing outer choice");
        var last = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone); var fullSource = Add(p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_182"); var kept = p.HandZone.Where(c => c != fullSource).Select(c => c.Id).ToHashSet();
        PlayEntity(g, p, fullSource); Core2026Proof.Check(g.Process(ChooseTask.Pick(p, last.Id)) && p.HandZone.Count == 10
            && p.HandZone.Contains(last) && kept.IsSubsetOf(p.HandZone.Select(c => c.Id)), "full-hand cast frees one slot for selected deck entity");
    }, legacyPoolMember: true);
}
