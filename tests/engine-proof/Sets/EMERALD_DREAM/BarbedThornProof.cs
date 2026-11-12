using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static ChooseOneProof;

internal static class BarbedThornProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_525", (g, p, id) => {
        var dagger = (Weapon)Entity.FromCard(p, Cards.FromId("CS2_082"));
        dagger.Poisonous = true; dagger.Poisonous = false;
        Core2026Proof.Check(!dagger.Poisonous, "explicit zero clears weapon Poisonous");
        OptionsSurvive(g, p, id, "EDR_525A", "EDR_525B");
        var loti = Add(p, "TRL_343");
        var lotiOrder = new[] { "TRL_343at1", "TRL_343ct1", "TRL_343dt1", "TRL_343bt1" };
        Core2026Proof.Check(loti.ChooseOnePlayables.Select(o => o.Card.Id).SequenceEqual(lotiOrder), "legacy Loti special order preserved");
        var transformedLoti = Generic.ChangeEntityBlock(p, Add(p, "GAME_005"), Cards.FromId("TRL_343"), false);
        Core2026Proof.Check(transformedLoti.ChooseOnePlayables.Select(o => o.Card.Id).SequenceEqual(lotiOrder), "transformed Loti special order preserved");
        p.HandZone.Remove(loti); p.SetasideZone.Add(loti);
        p.HandZone.Remove(transformedLoti); p.SetasideZone.Add(transformedLoti);
        var noHistory = g.CloneExact(history: false);
        var quietWeapon = (Weapon)PlayOption(noHistory, noHistory.Player1, id, 1);
        Core2026Proof.Check(quietWeapon.Poisonous, "history-free Poisonous granted");
        End(noHistory); Core2026Proof.Check(!quietWeapon.Poisonous, "history-free Poisonous expires");
        var enemy = Summon(p.Opponent, "CS2_182");
        OptionsSurvive(g, p, "EX1_154", "EX1_154a", "EX1_154b");
        var weapon = (Weapon)PlayOption(g, p, id, 1);
        Core2026Proof.Check(weapon.AttackDamage == 1 && weapon.Durability == 3 && weapon.Poisonous
            && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_525e1"), "weapon gains exact temporary poison enchantment");
        p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == enemy.Id)), "poison weapon attacks");
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "one damage poisons a five-Health minion");
        var saved = Game.LoadSnapshot(g.SaveSnapshot());
        End(saved); Core2026Proof.Check(!saved.Player1.Hero.Weapon.Poisonous, "saved poison expires at end turn");
        End(g); Core2026Proof.Check(!weapon.Poisonous && weapon[GameTag.POISONOUS] == 0, "poison explicit-zero expiry"); End(g);
        var friend = Summon(p, "CS2_182"); enemy = Summon(p.Opponent, "CS2_182");
        p.Hero[GameTag.SPELLPOWER] = 3;
        weapon = (Weapon)PlayOption(g, p, id, 2);
        Core2026Proof.Check(!weapon.Poisonous && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_525e"), "permanent deathrattle option");
        foreach (var copy in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            Play(copy, copy.Player1, "CS2_106");
            Core2026Proof.Check(((Minion)copy.IdEntityDic[enemy.Id]).Damage == 2 && copy.Player2.Hero.Damage == 2,
                "cloned/saved weapon retains granted deathrattle");
        }
        int heroDamage = p.Opponent.Hero.Damage;
        Play(g, p, "CS2_106");
        Core2026Proof.Check(enemy.Damage == 2 && friend.Damage == 0 && p.Opponent.Hero.Damage == heroDamage + 2,
            "replaced weapon damages all enemies by two, ignoring spell damage");
        var fandral = Both(p);
        weapon = (Weapon)PlayOption(g, p, id, -1);
        Core2026Proof.Check(weapon.Poisonous && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_525e"), "Choose Both grants both weapon enchantments");
        End(g); End(g); Core2026Proof.Check(!weapon.Poisonous, "Choose Both poison also expires");
        heroDamage = p.Opponent.Hero.Damage;
        for (int i = 0; i < 3; i++) {
            p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
            Core2026Proof.Check(g.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id)), "weapon durability attack");
        }
        Core2026Proof.Check(p.Hero.Weapon == null && enemy.Damage == 4 && p.Opponent.Hero.Damage == heroDamage + 5,
            "broken weapon fires deathrattle once after three attacks");
    });
}
