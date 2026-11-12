using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class WrathspikeBruteProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_510", (original, owner, id) => {
        var originalBrute = Summon(owner, id); var originalOther = Summon(owner, "CS2_182"); originalOther.HasTaunt = true;
        var failures = new List<string>();
        foreach (string scenario in new[] { "minion", "hero", "hero-autocast", "lethal-minion", "lethal-hero", "friendly-attack", "other-defender", "self-attack", "redirect-away", "redirect-to", "silenced" })
        {
            var g = scenario.Contains("hero") ? Game.LoadSnapshotWithPools(original.SaveSnapshot(), original.OfflinePools) : original.CloneExact(history: true); var p = g.Player1;
            var brute = (Minion)g.IdEntityDic[originalBrute.Id]; var other = (Minion)g.IdEntityDic[originalOther.Id];
            bool friendly = scenario is "friendly-attack" or "self-attack";
            if (!friendly) End(g);
            var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 40;
            var bystander = Summon(p.Opponent, "CS2_182");
            if (scenario.StartsWith("lethal")) brute.Damage = 5;
            if (scenario == "silenced") { brute.Silence(); other.HasTaunt = false; }
            ICharacter attacker = scenario.Contains("hero") ? p.Opponent.Hero : friendly ? scenario == "self-attack" ? brute : other : enemy;
            if (attacker is Hero)
            {
                if (scenario == "hero-autocast")
                {
                    new WeaponTask("BE_055t").Process(g, p.Opponent, p.Opponent.Hero, null);
                    PayloadRules.StoreCard(p.Opponent.Hero.Weapon, PayloadRules.WeaponSpell, Cards.FromId("TLC_435"));
                }
                else Play(g, p.Opponent, "CS2_106");
                p.Opponent.Hero.IsExhausted = false;
            }
            ICharacter proposed = friendly ? p.Opponent.Hero : scenario is "other-defender" or "redirect-to" ? other : brute;
            ICharacter actual = scenario == "redirect-away" ? other : scenario == "redirect-to" ? brute : proposed;
            if (scenario.StartsWith("redirect")) g.TriggerManager.AttackTrigger += _ => g.CurrentEventData.EventTarget = actual;
            bool retaliates = actual == brute && scenario != "silenced";
            bool observed = false;
            g.TriggerManager.AfterAttackTrigger += sender => {
                observed = sender == attacker && g.CurrentEventData?.EventSource == attacker && g.CurrentEventData.EventTarget == actual;
            };
            bool taskMetadata = true;
            if (brute.ActivatedTrigger != null)
            {
                var effect = brute.ActivatedTrigger.SingleTask;
                brute.ActivatedTrigger.SingleTask = new CustomTask((game, controller, source, target, stack) => {
                    taskMetadata = game.CurrentEventData?.EventTarget == source;
                    effect.Process(game, controller, source, target, stack);
                });
            }
            int cursor = g.PowerHistory.Full.Count, face = p.Opponent.Hero.Damage, damage = bystander.Damage;
            attacker.IsExhausted = false;
            bool accepted = g.Process(attacker is Hero ? HeroAttackTask.Any(attacker.Controller, proposed)
                : MinionAttackTask.Any(attacker.Controller, (Minion)attacker, proposed));
            int triggers = g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryBlockStart>().Count(b => b.BlockType == BlockType.TRIGGER && b.Source == brute.Id);
            int combatFace = attacker is Hero && actual == brute ? brute.AttackDamage : proposed == p.Opponent.Hero ? attacker.AttackDamage : 0;
            bool pass = accepted && observed && g.CurrentEventData == null && triggers == (retaliates ? 1 : 0)
                && bystander.Damage == damage + (retaliates ? 1 : 0)
                && p.Opponent.Hero.Damage == face + combatFace + (retaliates ? 1 : 0)
                && (!scenario.StartsWith("lethal") || brute.Zone == p.GraveyardZone);
            Console.WriteLine($"BRUTE {scenario}: {(pass ? "PASS" : "FAIL")} blocks={triggers}; metadata={observed}; taskMetadata={taskMetadata}; face={p.Opponent.Hero.Damage-face}; bystander={bystander.Damage-damage}");
            if (!pass) failures.Add(scenario);
        }
        Core2026Proof.Check(failures.Count == 0, "Brute regressions: " + string.Join(", ", failures));
    });
}
