using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RavasaurMatriarchProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_825", (g, p, id) => {
        var enemy = Summon(p.Opponent, "EX1_561"); var source = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.Target == null)
            && source.IsValidPlayTarget(null) && !source.IsValidPlayTarget(enemy), "inactive Kindred forbids targeting");
        PlayEntity(g, p, source); Core2026Proof.Check(enemy.Damage == 0, "no previous-turn match deals no damage");
        ParkBoard(p); Play(g, p, "CS2_171"); source = Add(p, id);
        Core2026Proof.Check(source.IsValidPlayTarget(null) && !source.IsValidPlayTarget(enemy), "own current-turn Beast does not activate");
        p.SetasideZone.Add(p.HandZone.Remove(source)); End(g); End(g);
        source = Add(p, id); var friendly = Summon(p, "CS2_182"); var stealth = Summon(p.Opponent, "CS2_231"); stealth.HasStealth = true;
        var elusive = Summon(p.Opponent, "EDR_598");
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion && t.Target.Controller == p.Opponent)
            && options.Any(t => t.Target == enemy) && options.Any(t => t.Target == elusive) && options.All(t => t.Target != stealth)
            && !source.IsValidPlayTarget(null) && !source.IsValidPlayTarget(friendly) && !source.IsValidPlayTarget(p.Opponent.Hero), "active Kindred requires legal enemy minion; Elusive allowed, heroes/friendly/Stealth excluded");
        new SabberStoneCore.Tasks.SimpleTasks.AddEnchantmentTask("DINO_419e", EntityType.TARGET).Process(g, p, p.Hero, source);
        PoolPilotProof.Resume(g, next => {
            PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (ICharacter)next.IdEntityDic[enemy.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[enemy.Id]).Damage == 7, "damage uses current buffed Attack");
        });
        ParkBoard(p.Opponent); Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "no enemy minions means untargeted play without hero damage");
        ParkBoard(p); End(g); End(g); End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        enemy = Summon(p.Opponent, "CS2_200"); Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "opponent Beast never activates own Kindred");
        ParkBoard(p); Play(g, p, "DINO_413"); End(g); End(g);
        enemy.Damage = 0; source = Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 5, "dual-type previous-turn Beast matches");
        ((Minion)source).Silence(); int damage = enemy.Damage; End(g); End(g);
        Core2026Proof.Check(enemy.Damage == damage, "Kindred is a play effect with no surviving trigger");
    });
}
