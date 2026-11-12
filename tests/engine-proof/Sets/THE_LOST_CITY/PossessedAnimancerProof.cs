using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PossessedAnimancerProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_131", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var beast = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(beast);
        var nonBeast = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(nonBeast);
        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.IdEntityDic[beast.Id].Zone.Type == Zone.PLAY && beast.Zone == p.DeckZone && p.DeckZone.Count == 2, "clone recruits only its own deck entity");
        Kill(g, source);
        Core2026Proof.Check(beast.Zone == p.BoardZone && beast.HasLifeSteal && beast.AttackDamage == 1 && beast.Health == 1 && p.DeckZone.Count == 1, $"summons actual Beast from deck and grants Lifesteal: zone={beast.Zone.Type}, life={beast.HasLifeSteal}, stats={beast.AttackDamage}/{beast.Health}, deck={p.DeckZone.Count}");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { beast.Id, left.Id, right.Id }), "removed leftmost source's slot takes Beast");
        Play(g, p, "BE_228", beast); Core2026Proof.Check(!beast.HasLifeSteal, "Silence removes granted Lifesteal");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == 1 && p.BoardZone.Count == 4, "no Beast leaves deck untouched");
        var eligible = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(eligible);
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == 2 && eligible.Zone == p.DeckZone && !eligible.HasLifeSteal, "silenced source cannot recruit eligible Beast");
        p.DeckZone.Remove(eligible); p.SetasideZone.Add(eligible);
        ClearBoard(g, p);
        beast = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(beast);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 7 && beast.Zone == p.BoardZone, "death frees last slot");
        var blocked = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(blocked);
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(blocked.Zone == p.DeckZone && p.BoardZone.Count == 7, "full board preserves Beast in deck");
        ClearBoard(g, p); left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1;
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, blocked.Id, right.Id }), "live source uses actual position including Dormant neighbour");
        left[GameTag.DORMANT] = 0; g.DeathProcessingAndAuraUpdate();
        var recruit = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(recruit);
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        CheckDeathrattleSlot(p, board, new[] { recruit }, "recruited Beast fills the dying source's physical slot");
        ParkBoard(p);
    });
}
