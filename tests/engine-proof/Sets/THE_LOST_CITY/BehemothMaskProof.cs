using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BehemothMaskProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_428", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.Damage = 3;
        var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); p.Hero.Damage = 15;
        enemy.IsExhausted = true; enemy.IsFrozen = true;
        Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 8 && friend.Health == 6 && friend.HasLifeSteal && enemy.Zone.Type == Zone.GRAVEYARD && p.Hero.Health == 23, "set damaged target to 8/10 Lifesteal then force frozen exhausted enemy attack and heal");
        Core2026Proof.Check(friend.AppliedEnchantments.Any(e => e.Card.Id == "DINO_428e"), "pinned mask enchantment");
        Play(g, p, id, friend); Core2026Proof.Check(friend.Health == 10, "no enemy still sets stats and heals target to max");
        Play(g, p, "BE_228", friend); Core2026Proof.Check(friend.AttackDamage == 4 && !friend.HasLifeSteal, "silence removes stats and Lifesteal");
        enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 10 && enemy.HasLifeSteal, "enemy target excluded from attacking itself");
        p.Hero.Damage = 15; p.Opponent.Hero.Damage = 15;
        var other = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 6 && other.Zone.Type == Zone.GRAVEYARD, "another enemy may be forced to attack enemy target");
        Core2026Proof.Check(p.Opponent.Hero.Health == 23 && p.Hero.Health == 15, "enemy target Lifesteal heals enemy controller for eight, caster receives nothing");
        Kill(g, enemy); enemy = Summon(p.Opponent, "CS2_182"); other = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id); var clone = g.CloneExact(history: true);
        PlayEntity(g, p, spell, friend); PlayEntity(clone, clone.Player1, clone.IdEntityDic[spell.Id], (ICharacter)clone.IdEntityDic[friend.Id]);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "multiple attackers use deterministic game RNG");
    });
}
