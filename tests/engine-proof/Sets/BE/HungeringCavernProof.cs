using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HungeringCavernProof
{
    internal static void Run() => StandardSetsProof.Case("BE_877", (g, p, id) => {
        var spell = Add(p, id); Core2026Proof.Check(spell.Cost == 2 && spell.HasLifeSteal, "base cost and Lifesteal");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == friend.Id) && options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == p.Hero.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id) && options.All(t => t.Target is ICharacter), "required character target on either side");
        Kill(g, enemy); Core2026Proof.Check(spell.Cost == 2, "opponent death excluded");
        Kill(g, friend); Core2026Proof.Check(p.NumFriendlyMinionsThatDiedThisTurn == 1 && spell.Cost == 0, "core death fact changes existing hand cost");
        var later = Add(p, id); Core2026Proof.Check(later.Cost == 0, "newly drawn spell sees prior friendly death");
        var clone = g.Clone(); End(clone); Core2026Proof.Check(clone.IdEntityDic[spell.Id].Cost == 2 && spell.Cost == 0, "cost reset clone isolated");
        p.Hero.Damage = 10; PlayEntity(g, p, spell, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == 28 && p.Hero.Health == 22, "two damage and Lifesteal at zero cost");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        PlayEntity(g, p, later, shield); Core2026Proof.Check(shield.Health == 1 && !shield.HasDivineShield && p.Hero.Health == 22, "no healing for shielded damage");
        End(g); End(g); spell = Add(p, id); Core2026Proof.Check(spell.Cost == 2, "death condition resets next turn");
        Summon(p, "CS2_052"); PlayEntity(g, p, spell, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == 25 && p.Hero.Health == 25, "spell damage also increases Lifesteal");
    });
}
