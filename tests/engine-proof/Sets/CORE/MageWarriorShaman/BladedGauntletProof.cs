using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier C: https://hearthstone.wiki.gg/wiki/Bladed_Gauntlet#Notes (weapon modifiers overwritten by Armor).
// Tier C: https://hearthstone.wiki.gg/wiki/Attack (weapon Attack adds only on owner turn).
// Tier C: https://liquipedia.net/hearthstone/Weapon (independent owner-turn rule).
// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class BladedGauntletProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_044", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); p.Opponent.Hero.Armor = 11;
        Play(g, p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 0 && p.Hero.AttackDamage == 0 && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK), "zero Armor gives zero Attack");
        p.Hero.Armor = 5; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 5 && p.Hero.AttackDamage == 5 && p.Hero.Weapon.Durability == 2 && p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target is Hero) && p.Opponent.Hero.AttackDamage == 0, "five Armor attacks minions, not heroes; enemy unaffected");
        Play(g, p, "CS2_105");
        Core2026Proof.Check(p.Hero.AttackDamage == 9 && p.Hero.Weapon.AttackDamage == 5, "Heroic Strike adds four hero Attack independently of weapon Armor Attack");
        Play(g, p, "CS2_074");
        Core2026Proof.Check(p.Hero.AttackDamage == 9 && p.Hero.Weapon.AttackDamage == 5, "weapon Attack stays equal to Armor after Deadly Poison");
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; owner.Hero.Armor = 8; next.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(owner.Hero.Weapon.AttackDamage == 8 && owner.Hero.AttackDamage == 12, "Armor increase preserves separate hero buff across clone/snapshot");
            var attack = owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == enemy.Id);
            Core2026Proof.Check(next.Process(attack) && next.IdEntityDic[enemy.Id].Zone == next.Player2.GraveyardZone && owner.Hero.Weapon.Durability == 1 && owner.Hero.Armor == 4 && owner.Hero.Weapon.AttackDamage == 4 && owner.Hero.AttackDamage == 8, "combat uses Armor Attack, spends durability and follows Armor lost to retaliation");
            End(next);
            Core2026Proof.Check(owner.Hero.AttackDamage == 0 && owner.Hero.Weapon.AttackDamage == 4, "opponent turn sheathes weapon and expires Heroic Strike");
            var attacker = Summon(next.Player2, "CS2_182"); Attack(next, attacker, owner.Hero);
            Core2026Proof.Check(attacker.Damage == 0 && owner.Hero.Armor == 0 && owner.Hero.Weapon.AttackDamage == 0 && owner.Hero.AttackDamage == 0,
                "opponent attack gets no weapon retaliation and lost Armor updates weapon");
            End(next);
            owner.Hero.Armor = 2; next.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(owner.Hero.Weapon.AttackDamage == 2 && owner.Hero.AttackDamage == 2 && next.Player2.Hero.Armor == 11, "owner turn restores weapon Attack; opponent Armor stays independent");
            Play(next, owner, "CS2_106"); Core2026Proof.Check(owner.Hero.AttackDamage == 3 && owner.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target is Hero), "replacement removes adaptive Attack and hero restriction");
            Play(next, owner, id); End(next); Play(next, next.Player2, "EX1_066");
            Core2026Proof.Check(owner.Hero.Weapon == null && !next.Auras.Any(a => a.Owner?.Controller == owner), "weapon destruction removes both Armor effect and hero restriction");
            owner.Hero.Armor = 10; End(next);
            Core2026Proof.Check(owner.Hero.AttackDamage == 0 && owner.Hero.CantAttackHeroes == false, "destroyed Gauntlet cannot grant Attack or retain targeting restriction");
        });
    }, legacyPoolMember: true);
}
