using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TitanSizedProof
{
    internal static void Run() => StandardSetsProof.Case("BE_208", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var deckSpell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(deckSpell);
        var drawn = (Minion)Entity.FromCard(p, Cards.FromId("CS2_120")); p.DeckZone.Add(drawn);
        var beast = (Minion)Add(p, "CS2_120"); var dual = (Minion)Add(p, "BE_730t");
        var all = (Minion)Add(p, "GIL_681"); var ordinary = (Minion)Add(p, "CS2_182");
        var spell = Add(p, "CS2_029"); var board = Summon(p, "CS2_120");
        var enemy = (Minion)Add(p.Opponent, "CS2_120");
        var source = Play(g, p, id);
        Core2026Proof.Check(drawn.Zone == p.HandZone && p.DeckZone.Count == 1 && deckSpell.Zone == p.DeckZone,
            "tutors minion past spell before buffing");
        Core2026Proof.Check(drawn.AttackDamage == 4 && drawn.BaseHealth == 5 && beast.AttackDamage == 4 && beast.BaseHealth == 5,
            "drawn and held Beasts gain +2/+2");
        Core2026Proof.Check(dual.AttackDamage == 3 && dual.BaseHealth == 3 && all.AttackDamage == 5 && all.BaseHealth == 6,
            "secondary Beast and Amalgam qualify");
        Core2026Proof.Check(ordinary.AttackDamage == 4 && ordinary.BaseHealth == 5 && spell.Cost == 4
            && board.AttackDamage == 2 && board.BaseHealth == 3 && enemy.AttackDamage == 2,
            "non-Beasts, spells, board and opponent hand excluded");
        Core2026Proof.Check(beast.AppliedEnchantments.Single().Card.Id == "BE_208e", "exact Titanic enchantment");
        int damage = p.Hero.Damage;
        Play(g, p, id);
        Core2026Proof.Check(beast.AttackDamage == 6 && beast.BaseHealth == 7 && p.DeckZone.Count == 1 && p.Hero.Damage == damage,
            "no matching minion still buffs without drawing spell or fatigue");
        var later = (Minion)Add(p, "CS2_120");
        Core2026Proof.Check(later.AttackDamage == 2 && later.BaseHealth == 3, "later hand arrivals not buffed");
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, id);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[beast.Id]).AttackDamage == 8 && beast.AttackDamage == 6,
            "hand buffs isolated in clone");
        PlayEntity(g, p, beast); beast.Silence();
        Core2026Proof.Check(beast.AttackDamage == 2 && beast.BaseHealth == 3, "hand buff survives play and Silence removes it");
        while (!p.HandZone.IsFull) Add(p, "CS2_120");
        var burned = Entity.FromCard(p, Cards.FromId("CS2_120")); p.DeckZone.Add(burned);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && burned.Zone == p.GraveyardZone && ((Minion)burned).AttackDamage == 2
            && later.AttackDamage == 4, "full hand burns draw, buffs existing Beasts only");
    });
}
