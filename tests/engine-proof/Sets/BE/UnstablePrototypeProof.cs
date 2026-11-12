using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UnstablePrototypeProof
{
    internal static void Run() => StandardSetsProof.Case("BE_207", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && source.IsRace(Race.MECHANICAL), "printed Reborn and Mech");
        var left = Summon(p, "CS2_182"); var held = (Minion)Add(p, "CS2_171"); var spell = Add(p, "CS2_008");
        var enemy = (Minion)Add(p.Opponent, "CS2_171");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[held.Id]).Health == 2 && held.Health == 1 && p.BoardZone.Contains(source), "clone deathrattle and Reborn isolated");
        Kill(g, source);
        var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(held.AttackDamage == 2 && held.Health == 2 && held.AppliedEnchantments.Single().Card.Id == "BE_207e", "deathrattle buffs held minion +1/+1");
        Core2026Proof.Check(enemy.Health == 1 && spell.Cost == 0 && left.Health == 5, "excludes opposing hand, spells and board");
        Core2026Proof.Check(reborn.Id != source.Id && reborn.Health == 1 && reborn.AttackDamage == 0 && reborn[GameTag.REBORN] == 0 && reborn.ZonePosition == 0, "fresh one-Health copy at original position without Reborn");
        Kill(g, reborn); Core2026Proof.Check(held.AttackDamage == 3 && held.Health == 3 && p.BoardZone.All(m => m.Card.Id != id), "reborn copy retains deathrattle but cannot reborn twice");
        PlayEntity(g, p, held); held.Silence(); Core2026Proof.Check(held.Health == 1 && held.AttackDamage == 1, "Silence removes hand buffs");
        var silenced = Summon(p, id); silenced.Silence(); var untouched = (Minion)Add(p, "CS2_171");
        Kill(g, silenced); Core2026Proof.Check(untouched.Health == 1 && p.BoardZone.All(m => m.Card.Id != id), "Silence removes both deathrattle and Reborn");
        ClearBoard(g, p); source = Summon(p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "Reborn uses vacated slot on full board");
        Play(g, p, "CS2_008", p.Opponent.Hero);
    });
}
