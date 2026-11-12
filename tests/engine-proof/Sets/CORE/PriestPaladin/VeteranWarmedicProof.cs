using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VeteranWarmedicProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_878", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "play between neighbours");
        Play(g, p, "GAME_005"); Core2026Proof.Check(p.BoardZone.Count == 3, "schoolless spell excluded");
        Play(g, p, "CORE_BT_292", source);
        var medic = p.BoardZone.Single(m => m.Card.Id == "BAR_878t");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, medic.Id, right.Id }), "Medic summoned right of actual source position");
        Core2026Proof.Check(medic.AttackDamage == 2 && medic.Health == 2 && medic.HasLifeSteal, "exact 2/2 Lifesteal token");
        p.Hero.Damage = 5; Attack(g, medic, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 2, "Medic Lifesteal heals actual combat damage");
        End(g); Play(g, p.Opponent, "CS2_004", right); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 4, "opponent Holy spell excluded; trigger persists across turns");
        var clone = g.Clone(); Play(clone, clone.Player1, "CS2_004", (ICharacter)clone.IdEntityDic[left.Id]);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 5 && p.BoardZone.Count == 4, "legacy Holy trigger works in isolated clone");
        Play(g, p, "CS2_004", left);
        Core2026Proof.Check(p.BoardZone.Count == 5 && p.BoardZone[2].Card.Id == "BAR_878t", "legacy school fallback triggers beside source");
        Play(g, p, "CS2_061", p.Opponent.Hero); Core2026Proof.Check(p.BoardZone.Count == 5, "Shadow spell excluded");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "CS2_004", right); Core2026Proof.Check(p.BoardZone.Count == 7, "full board blocks token");
        Kill(g, p.BoardZone.Last());
        source.Silence(); Play(g, p, "CS2_004", right);
        Core2026Proof.Check(p.BoardZone.Count == 6 && source.ActivatedTrigger == null, "Silence removes trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        int count = p.BoardZone.Count; Play(g, p, "CS2_004", right);
        Core2026Proof.Check(p.BoardZone.Count == count, "removed source cannot trigger");
        source = (Minion)Add(p, id); Play(g, p, "CS2_004", right);
        Core2026Proof.Check(source.Zone == p.HandZone && p.BoardZone.Count == count, "source in hand cannot trigger");
        PlayEntity(g, p, source); source.Damage = source.BaseHealth - 1;
        Play(g, p, "CS1_130", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.BoardZone.Count == count, "Holy spell killing source prevents after-cast summon");
    });
}
