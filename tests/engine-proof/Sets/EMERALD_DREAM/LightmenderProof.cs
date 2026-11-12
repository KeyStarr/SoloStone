using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch9Proof;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static ChooseOneProof;

internal static class LightmenderProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_257", (g, p, id) => {
        var batch9 = g.CloneExact(history: true);
        RunBatch9(batch9, batch9.Player1, id);
        Core2026Proof.Check(HsSim.Canonical.State(batch9) == HsSim.Canonical.State(batch9.CloneExact(history: true)) &&
            HsSim.Canonical.State(batch9) == HsSim.Canonical.State(SabberStoneCore.Model.Game.LoadSnapshot(batch9.SaveSnapshot())), "batch-9 proof clone/snapshot future");
        RunSelectors(g, p, id);
    });

    private static void RunBatch9(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        var attack = (Minion)Choose(g, p, id, 1);
        Core2026Proof.Check(attack.HasTaunt && attack.AttackDamage == 6 && attack.Health == 3 && attack.HasDivineShield && !attack.HasLifeSteal,
            "first option: 6/3 Taunt Divine Shield only");
        var enemy = Summon(p.Opponent, "CS2_182"); End(g); Attack(g, enemy, attack); End(g);
        Core2026Proof.Check(attack.Health == 3 && !attack.HasDivineShield, "granted Divine Shield absorbs a hit");
        attack.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(attack.AttackDamage == 3 && attack.Health == 3 && !attack.HasTaunt && !attack.HasDivineShield, "Silence removes Attack buff and keywords");
        var health = (Minion)Choose(g, p, id, 2);
        Core2026Proof.Check(health.HasTaunt && health.AttackDamage == 3 && health.Health == 6 && !health.HasDivineShield && health.HasLifeSteal,
            "second option: 3/6 Taunt Lifesteal only");
        p.Hero.Damage = 8; Attack(g, health, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 5, "granted Lifesteal heals owner");
        End(g); End(g);
        Core2026Proof.Check(health.Health == 6 && health.HasLifeSteal, "buffs persist across turns");
        health.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(health.AttackDamage == 3 && health.Health == 3 && !health.HasLifeSteal && !health.HasTaunt, "Silence removes Health buff and Lifesteal");
        var plain = Summon(p, id);
        Core2026Proof.Check(plain.AttackDamage == 3 && plain.Health == 3 && plain.HasTaunt && !plain.HasDivineShield && !plain.HasLifeSteal,
            "summoned without playing receives neither option");
        p.ChooseBoth = true;
        var both = (Minion)Choose(g, p, id, 0);
        Core2026Proof.Check(both.AttackDamage == 6 && both.Health == 6 && both.HasTaunt && both.HasDivineShield && both.HasLifeSteal,
            "Choose Both applies both exact options");
        p.ChooseBoth = false;
    }

    private static void RunSelectors(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        OptionsSurvive(g, p, id, "EDR_257a", "EDR_257b");
        var attack = (Minion)PlayOption(g, p, id, 1);
        Core2026Proof.Check(attack.HasTaunt && attack.AttackDamage == 6 && attack.Health == 3 && attack.HasDivineShield && !attack.HasLifeSteal
            && attack.AppliedEnchantments.Any(e => e.Card.Id == "EDR_257ae"), "attack and Divine Shield branch");
        var health = (Minion)PlayOption(g, p, id, 2);
        Core2026Proof.Check(health.HasTaunt && health.AttackDamage == 3 && health.Health == 6 && !health.HasDivineShield && health.HasLifeSteal
            && health.AppliedEnchantments.Any(e => e.Card.Id == "EDR_257be"), "health and Lifesteal branch");
        p.Hero.Damage = 10; var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, health, enemy);
        Core2026Proof.Check(p.Hero.Damage == 7, "granted Lifesteal heals actual damage");
        End(g); End(g); Core2026Proof.Check(attack.AttackDamage == 6 && health.BaseHealth == 6, "both stat enchantments persist across turns");
        Both(p); var both = (Minion)PlayOption(g, p, id, -1);
        Core2026Proof.Check(both.AttackDamage == 6 && both.Health == 6 && both.HasTaunt && both.HasDivineShield && both.HasLifeSteal,
            "Fandral combines Lightmender branches");
        Play(g, p, "BE_228", both);
        Core2026Proof.Check(both.AttackDamage == 3 && both.BaseHealth == 3 && !both.HasTaunt && !both.HasDivineShield && !both.HasLifeSteal, "silence removes both choices");
    }
}
