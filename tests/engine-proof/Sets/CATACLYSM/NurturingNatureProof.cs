using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NurturingNatureProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_305", (g, p, id) => {
        var beast = Summon(p, "CS2_171"); var other = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_171"); var hand = (Minion)Add(p, "CORE_LOE_039");
        var handOther = (Minion)Add(p, "CS2_182"); var opponentHand = (Minion)Add(p.Opponent, "CS2_171");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length == 1 && options[0].Target?.Id == beast.Id, "only friendly Beast target; enemy, non-Beast and heroes excluded");
        PlayEntity(g, p, spell, beast);
        Core2026Proof.Check(beast.AttackDamage == 3 && beast.BaseHealth == 3 && hand.AttackDamage == 5 && hand.BaseHealth == 6 && hand.IsRace(Race.BEAST) && hand.IsRace(Race.MECHANICAL), "target and dual-type Beast in hand gain +2/+2");
        Core2026Proof.Check(other.AttackDamage == 4 && enemy.AttackDamage == 1 && handOther.AttackDamage == 4 && opponentHand.AttackDamage == 1, "non-Beasts and opponents unchanged");
        beast.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(beast.AttackDamage == 1 && beast.BaseHealth == 1, "Silence removes board buff");
        PlayEntity(g, p, hand); Core2026Proof.Check(hand.AttackDamage == 5 && hand.BaseHealth == 6, "hand buff survives playing dual-type minion");
        var second = (Minion)Add(p, "CS2_171");
        var third = (Minion)Add(p, "CS2_171");
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, id, (Minion)clone.IdEntityDic[beast.Id]);
        var candidates = new[] { second.Id, third.Id }.Select(i => (Minion)clone.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(candidates.Count(m => m.AttackDamage == 3 && m.BaseHealth == 3) == 1 && second.AttackDamage == 1 && third.AttackDamage == 1 && beast.AttackDamage == 1, "one random hand Beast buffed only in clone");
        foreach (var item in p.HandZone.ToArray()) { p.HandZone.Remove(item); p.SetasideZone.Add(item); }
        Play(g, p, id, beast); Core2026Proof.Check(beast.AttackDamage == 3, "no Beast in hand still buffs board target");
        ClearBoard(g, p); spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "no friendly Beast makes spell unplayable");
    });
}
