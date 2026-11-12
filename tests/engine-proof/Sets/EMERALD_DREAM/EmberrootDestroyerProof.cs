using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EmberrootDestroyerProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_955", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, "CS2_008", p.Opponent.Hero); Core2026Proof.Check(enemy.Damage == 0, "enemy hero damage excluded");
        Play(g, p, "CS2_008", source); Core2026Proof.Check(enemy.Damage == 0, "friendly minion damage excluded");
        p.Hero.HasDivineShield = true; Play(g, p, "CS2_008", p.Hero); Core2026Proof.Check(enemy.Damage == 0, "absorbed hero damage excluded");
        p.Hero.IsImmune = true; Play(g, p, "CS2_008", p.Hero); p.Hero.IsImmune = false;
        Core2026Proof.Check(enemy.Damage == 0, "immune hero prevents damage and trigger");
        var armored = g.Clone(); var armoredPlayer = armored.Player1;
        int healthDamage = armoredPlayer.Hero.Damage;
        armoredPlayer.Hero.Armor = 10; Play(armored, armoredPlayer, "CS2_029", armoredPlayer.Hero);
        Core2026Proof.Check(armoredPlayer.Hero.Armor == 4 && armoredPlayer.Hero.Damage == healthDamage
            && ((Minion)armored.IdEntityDic[enemy.Id]).Damage == 3, "fully Armor-absorbed hero damage triggers");
        armoredPlayer.Hero.Armor = 1; Play(armored, armoredPlayer, "CS2_029", armoredPlayer.Hero);
        Core2026Proof.Check(armoredPlayer.Hero.Armor == 0 && armoredPlayer.Hero.Damage == healthDamage + 5
            && ((Minion)armored.IdEntityDic[enemy.Id]).Damage == 6 && enemy.Damage == 0, "partially Armor-absorbed damage triggers once; clone isolated");
        var replaced = g.Clone(); var replacedPlayer = replaced.Player1; var oldHero = replacedPlayer.Hero;
        Play(replaced, replacedPlayer, "CORE_EX1_323");
        Core2026Proof.Check(replacedPlayer.Hero.Id != oldHero.Id && replacedPlayer.Hero.Card.Id == "CORE_EX1_323", "Jaraxxus replaces hero entity");
        replacedPlayer.Hero.Armor = 0; Play(replaced, replacedPlayer, "CS2_008", replacedPlayer.Hero);
        Core2026Proof.Check(((Minion)replaced.IdEntityDic[enemy.Id]).Damage == 3 && enemy.Damage == 0, "current friendly hero damage after replacement triggers; clone isolated");
        var clone = g.Clone(); Play(clone, clone.Player1, "CS2_008", clone.Player1.Hero);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[enemy.Id]).Damage == 3 && enemy.Damage == 0, "hero damage trigger clone isolated");
        Play(g, p, "CS2_008", p.Hero); Core2026Proof.Check(enemy.Damage == 3, "hero takes damage on own turn triggers three damage");
        End(g); Play(g, p.Opponent, "CS2_008", p.Hero); End(g); Core2026Proof.Check(enemy.Damage == 3, "opponent turn excluded");
        Play(g, p, "BE_228", source); Play(g, p, "CS2_008", p.Hero);
        Core2026Proof.Check(enemy.Damage == 3 && source.ActivatedTrigger == null, "Silence removes trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); Play(g, p, "CS2_008", p.Hero);
        Core2026Proof.Check(enemy.Damage == 3, "dead source no trigger");
        source = (Minion)Play(g, p, id); ClearBoard(g, p.Opponent); int damage = p.Opponent.Hero.Damage;
        Play(g, p, "CS2_008", p.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "no minions means no target; enemy hero excluded");
    });
}
