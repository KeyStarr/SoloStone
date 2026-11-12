using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DragonbaneProof
{
    private static void UsePower(Game game, Controller player)
    {
        player.BaseMana = 10; player.UsedMana = 0;
        var task = player.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER
            && (t.Target == null || t.Target.Id == player.Opponent.Hero.Id));
        Core2026Proof.Check(game.Process(task), "native Hero Power accepted");
    }
    internal static void Run() => StandardSetsProof.Case("CORE_DRG_256", (g, p, id) => {

        var held = Add(p, id); var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.IsRace(Race.MECHANICAL), "printed Mech");
        UsePower(g, p); Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && held.Zone == p.HandZone, "friendly power deals one five-damage hit; held copy inactive");
        End(g); UsePower(g, p.Opponent); Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "opponent power ignored"); End(g);
        p.Opponent.Hero.HasDivineShield = true; UsePower(g, p);
        Core2026Proof.Check(!p.Opponent.Hero.HasDivineShield && p.Opponent.Hero.Damage == 5, "hero shield absorbs entire five damage");
        End(g); End(g); Summon(p, "EX1_012");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 30;
        var clone = g.CloneExact(history: true); UsePower(clone, clone.Player1);
        Core2026Proof.Check(enemy.Damage == 0 && p.Opponent.Hero.Damage == 5, "clone power trigger isolated");
        UsePower(g, p); Core2026Proof.Check(enemy.Damage + p.Opponent.Hero.Damage == 10, "random enemy hit exactly five, no Spell Damage");
        Core2026Proof.Check(enemy.Damage == ((Minion)clone.IdEntityDic[enemy.Id]).Damage, "same RNG state selects same enemy");
        source.Silence(); End(g); End(g); int before = enemy.Damage + p.Opponent.Hero.Damage; UsePower(g, p);
        Core2026Proof.Check(enemy.Damage + p.Opponent.Hero.Damage == before && source.ActivatedTrigger == null, "Silence removes power trigger");
        source = Summon(p, id); Kill(g, source); End(g); End(g); UsePower(g, p);
        Core2026Proof.Check(enemy.Damage + p.Opponent.Hero.Damage == before && source.ActivatedTrigger == null, "dead source no longer triggers");
    });
}
