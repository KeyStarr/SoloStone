using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LastingLegacyProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_449", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); var enemyHeld = (Minion)Add(p.Opponent, "CS2_182");
        var board = Summon(p, "CS2_182"); var spell = Add(p, "CS2_008");
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.AttackDamage == 4 && held.AttackDamage == 4, "minion in deck prevents hand buff but gives hero attack");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.AttackDamage == 8 && held.AttackDamage == 8 && held.AppliedEnchantments.Single().Card.Id == "TIME_449e2", "empty deck qualifies, hero buffs stack");
        Core2026Proof.Check(board.AttackDamage == 4 && enemyHeld.AttackDamage == 4 && spell.Cost == 0, "only friendly hand minions buffed");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && p.Hero.AttackDamage == 8, "clone hero expiry isolated");
        End(g); End(g);
        Core2026Proof.Check(p.Hero.AttackDamage == 0 && held.AttackDamage == 8, "hero attack expires, hand buff persists");
        PlayEntity(g, p, held); Core2026Proof.Check(held.AttackDamage == 8, "hand buff carries into play");
        held.Silence(); Core2026Proof.Check(held.AttackDamage == 4, "Silence removes hand attack buff");
        var deckSpell = Add(p, "CS2_008"); p.HandZone.Remove(deckSpell); p.DeckZone.Add(deckSpell);
        var another = (Minion)Add(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(another.AttackDamage == 8, "nonempty all-spell deck qualifies");
    });
}
