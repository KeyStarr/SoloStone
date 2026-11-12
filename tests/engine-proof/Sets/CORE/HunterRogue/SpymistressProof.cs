using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpymistressProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_701", (g, p, id) => {

        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasStealth && source.AttackDamage == 3 && source.Health == 1, "printed Stealth 3/1");
        End(g); var spell = Add(p.Opponent, "CS2_029"); var ordinary = Summon(p, "CS2_182");
        Core2026Proof.Check(p.Opponent.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == ordinary.Id), "ordinary control target");
        Core2026Proof.Check(!p.Opponent.Options().Any(t => (t.Source?.Id == spell.Id || t.PlayerTaskType == PlayerTaskType.HERO_POWER) && t.Target?.Id == source.Id), "Stealth blocks enemy spell and power targeting");
        var attacker = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false;
        Core2026Proof.Check(!p.Opponent.Options().Any(t => t.Source?.Id == attacker.Id && t.Target?.Id == source.Id), "Stealth blocks attacks");
        End(g); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(!source.HasStealth && p.Opponent.Hero.Damage == 3, "attacking removes Stealth");
        var fresh = Summon(p, id); fresh.Silence(); Core2026Proof.Check(!fresh.HasStealth, "Silence removes printed Stealth");
    });
}
