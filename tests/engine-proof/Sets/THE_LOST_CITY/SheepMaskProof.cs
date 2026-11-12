using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SheepMaskProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_429", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.Damage = 3; var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 1 && friend.Health == 1 && friend.Damage == 0 && friend.HasDeathrattle && friend.AppliedEnchantments.Any(e => e.Card.Id == "DINO_429e"), "sets damaged minion to 1/1 with pinned deathrattle");
        var ally = Summon(p, "CS2_182"); int hero = p.Opponent.Hero.Health; Kill(g, friend);
        Core2026Proof.Check(ally.Health == 3 && enemy.Health == 3 && p.Opponent.Hero.Health == hero, "deathrattle hits both boards but no heroes");
        Play(g, p, id, enemy); Kill(g, enemy); Core2026Proof.Check(ally.Health == 1, "enemy target gets deathrattle");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        p.Hero[GameTag.SPELLPOWER] = 1; p.Opponent.Hero[GameTag.SPELLPOWER] = 1;
        p.Hero.Damage = 15; p.Opponent.Hero.Damage = 15;
        enemy = Summon(p.Opponent, "CS2_182"); enemy.HasLifeSteal = true;
        ally = Summon(p, "CS2_182"); var other = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.HasLifeSteal, "mask preserves inherited Lifesteal"); Kill(g, enemy);
        Core2026Proof.Check(p.CurrentSpellPower == 1 && p.Opponent.CurrentSpellPower == 1 && ally.Health == 3 && other.Health == 3, "deathrattle stays two damage with Spell Damage +1 on both controllers");
        Core2026Proof.Check(p.Hero.Health == 15 && p.Opponent.Hero.Health == 19, "enemy masked minion Lifesteal heals its controller for four, not caster");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); friend = Summon(p, "CS2_182"); Play(g, p, id, friend); Play(g, p, "BE_228", friend); ally = p.BoardZone.Single(m => m.Card.Id == "BE_228");
        Kill(g, friend); Core2026Proof.Check(ally.Health == ally.Card.Health, "silence removes granted deathrattle");
        ClearBoard(g, p); friend = Summon(p, "CS2_182"); ally = Summon(p, "CS2_182");
        Play(g, p, id, friend); Play(g, p, id, friend); Kill(g, friend);
        Core2026Proof.Check(ally.Health == 1, "two masks stack two deathrattles");
    });
}
