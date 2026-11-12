using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class PowerWordBarrierProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_447", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = (Minion)Add(p, "CS2_182"); var otherSpell = Add(p, "CS2_029");
        var enemyHand = (Minion)Add(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(targets.Length == 4 && targets.All(t => t.Target is ICharacter)
            && targets.Select(t => t.Target.Id).ToHashSet().SetEquals(new[] { p.Hero.Id, p.Opponent.Hero.Id, friendly.Id, enemy.Id }),
            "required character target includes both heroes and both sides' minions");
        var clone = g.CloneExact(history: true);
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[spell.Id], clone.Player1.Hero);
        Core2026Proof.Check(clone.Player1.Hero.HasDivineShield && ((Minion)clone.IdEntityDic[held.Id]).Health == 7
            && !p.Hero.HasDivineShield && held.Health == 5, "shield and hand buff isolated in clone");
        PlayEntity(g, p, spell, p.Hero);
        Core2026Proof.Check(p.Hero.HasDivineShield && held.Health == 7 && held.AttackDamage == 4
            && held.AppliedEnchantments.Any(e => e.Card.Id == "TIME_447e2"), "hero shield and pinned +2 Health hand enchantment");
        Core2026Proof.Check(friendly.Health == 5 && enemy.Health == 5 && enemyHand.Health == 5 && otherSpell.Cost == 4
            && p.DeckZone.All(c => c[GameTag.HEALTH] == 5), "board, enemy hand, spells and deck excluded from hand buff");
        p.Hero.TakeDamage(p.Opponent.Hero, 7); p.Hero.TakeDamage(p.Opponent.Hero, 2);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 2, "card-granted hero shield absorbs next hit then drops");
        Play(g, p, id, p.Opponent.Hero);
        p.Opponent.Hero.TakeDamage(p.Hero, 7); p.Opponent.Hero.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(!p.Opponent.Hero.HasDivineShield && p.Opponent.Hero.Damage == 2 && held.Health == 9,
            "opponent hero is valid; hand Health stacks on caster's minions");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.HasDivineShield && held.Health == 11, "enemy minion shield still buffs friendly hand");
        enemy.TakeDamage(p.Hero, 9);
        Core2026Proof.Check(enemy.Health == 5 && !enemy.HasDivineShield, "enemy minion shield prevents damage");
        Play(g, p, id, friendly); friendly.Silence();
        Core2026Proof.Check(!friendly.HasDivineShield && held.Health == 13, "Silence removes granted minion shield");
        PlayEntity(g, p, held); End(g); End(g);
        Core2026Proof.Check(held.Health == 13, "hand buff survives play and turn boundary");
        held.Silence();
        Core2026Proof.Check(held.Health == 5 && held.AttackDamage == 4, "Silence removes persistent hand Health enchantments");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.HasDivineShield && p.HandZone.Count == 0, "empty hand still grants shield");
    });
}
