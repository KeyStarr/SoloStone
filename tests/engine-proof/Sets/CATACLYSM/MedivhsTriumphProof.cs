using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MedivhsTriumphProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_308", (g, p, id) => {
        var spell = Add(p, id); var foe = Summon(p.Opponent, "EX1_563"); g.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 5 && spell[GameTag.COST] == 5, "opponent Legendary does not discount");
        var friend = Summon(p, "CS2_182"); Core2026Proof.Check(spell.Cost == 5, "ordinary friendly does not discount");
        var legend = Summon(p, "EX1_399"); Core2026Proof.Check(spell.Cost == 5, "non-Legendary friendly excluded");
        Kill(g, legend); legend = Summon(p, "EX1_563");
        Core2026Proof.Check(spell.Cost == 1 && spell[GameTag.COST] == 1 && Add(p, id).Cost == 1, "Legendary discounts existing and newly drawn spells with visible COST");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[legend.Id]);
        Core2026Proof.Check(clone.IdEntityDic[spell.Id].Cost == 5 && spell.Cost == 1, "cost condition clone isolated");
        legend.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(spell.Cost == 1, "Silence does not remove Legendary rarity");
        Kill(g, legend); Core2026Proof.Check(spell.Cost == 5 && spell[GameTag.COST] == 5, "source leaving restores visible base cost");
        Play(g, p, "LOOT_542"); Core2026Proof.Check(spell.Cost == 1 && spell[GameTag.COST] == 1, "Legendary weapon also qualifies");
        p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(spell.Cost == 5, "weapon removal restores cost");
        var quest = (Spell)Entity.FromCard(p, Cards.FromId("UNG_940")); p.SecretZone.Add(quest); g.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 1 && spell[GameTag.COST] == 1, "controlled Legendary Quest also qualifies");
        p.SecretZone.Quest = null; p.SetasideZone.Add(quest); g.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 5, "Quest removal restores base cost");
        p.AddHeroAndPower(Cards.FromId("ICC_827"), Cards.FromId("DS1h_292")); g.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 1 && spell[GameTag.COST] == 1, "Legendary hero also qualifies");
        int own = p.Hero.Health, enemy = p.Opponent.Hero.Health; PlayEntity(g, p, spell);
        Core2026Proof.Check(friend.Health == 1 && foe.Health == 8 && p.Hero.Health == own && p.Opponent.Hero.Health == enemy, "four damage to both boards, excludes heroes");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Summon(p, "CS2_052"); friend = Summon(p, "CS2_182"); foe = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD && foe.Zone.Type == Zone.GRAVEYARD, "board damage gains spell damage");
    });
}
