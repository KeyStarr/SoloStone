using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CityChiefEshoProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_110", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var dragon = (Minion)Entity.FromCard(p, Cards.FromId("CORE_EX1_043")); p.DeckZone.Add(dragon);
        var elementalDragon = (Minion)Entity.FromCard(p, Cards.FromId("TLC_888")); p.DeckZone.Add(elementalDragon);
        var all = (Minion)Entity.FromCard(p, Cards.FromId("GIL_681")); p.DeckZone.Add(all);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        var friend = Summon(p, "CS2_182"); friend.Damage = 1;
        var held = (Minion)Add(p, "CS2_182"); var heldSpell = Add(p, "CS2_029");
        var enemy = Summon(p.Opponent, "CS2_182"); var enemyHeld = (Minion)Add(p.Opponent, "CS2_182");
        var dead = Summon(p, "CS2_182"); Kill(g, dead);
        var source = (Minion)Add(p, id);
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[held.Id]).AttackDamage == 6
            && held.AttackDamage == 4 && dragon.AttackDamage == 4, "clone wherever buffs isolated");
        PlayEntity(g, p, source);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 6 && friend.Damage == 1
            && held.AttackDamage == 6 && held.Health == 7 && dragon.AttackDamage == 6 && dragon.Health == 3
            && elementalDragon.AttackDamage == 5 && elementalDragon.Health == 5 && all.AttackDamage == all.Card.ATK + 2,
            "common secondary Dragon type and All permit buffs on board, hand and deck");
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 7 && enemy.AttackDamage == 4
            && enemyHeld.AttackDamage == 4 && dead.AttackDamage == 4 && heldSpell.Cost == 4 && spell.Cost == 4,
            "source, enemies, graveyard and spells excluded");
        Kill(g, source); End(g); End(g);
        Core2026Proof.Check(held.AttackDamage == 6 && friend.AttackDamage == 6, "buff survives source death and turn end");
        friend.Damage = 3; friend.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 4 && friend.Damage == 1,
            "Silence removes local +2 Health and only damage absorbed by that bonus");
        var typeless = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(typeless);
        Play(g, p, id);
        Core2026Proof.Check(held.AttackDamage == 6 && ((Minion)typeless).AttackDamage == 4, "typeless deck minion blocks condition");
        p.DeckZone.Remove(typeless); p.SetasideZone.Add(typeless);
        var beast = Entity.FromCard(p, Cards.FromId("CS2_171")); p.DeckZone.Add(beast);
        Play(g, p, id);
        Core2026Proof.Check(held.AttackDamage == 6, "Beast in Dragon deck breaks common type despite All minion");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        spell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(held.AttackDamage == 8 && held.Health == 9, "spells-only deck satisfies condition");
        p.DeckZone.Remove(spell); p.SetasideZone.Add(spell); Play(g, p, id);
        Core2026Proof.Check(held.AttackDamage == 10 && held.Health == 11, "empty deck satisfies condition; buffs stack");
        PlayEntity(g, p, held);
        Core2026Proof.Check(held.AttackDamage == 10 && held.Health == 11, "hand buffs survive playing recipient");
    });
}
