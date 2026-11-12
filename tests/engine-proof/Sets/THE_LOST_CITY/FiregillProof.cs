using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FiregillProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_404", (g, p, id) => {
        var ally = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.MURLOC) && !source.IsRush && !ally.IsRush, "dual types; no previous turn match means no Rush");
        ParkBoard(p);
        foreach (string match in new[] { "CS2_033", "CS2_168" })
        {
            End(g); End(g); Play(g, p, match); End(g); End(g); ParkBoard(p);
            ally = Summon(p, "CS2_182");
            var location = LocationProofs.Location(p);
            var retained = Summon(p, "CS2_182");
            source = (Minion)Play(g, p, id);
            Core2026Proof.Check(ally.IsRush && !source.IsRush && !enemy.IsRush && location.Zone == p.BoardZone, "either previous-turn tribe grants only other friendly minions Rush");
            Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == ally.Id && t.Target?.Id == enemy.Id)
                && !p.Options().Any(t => t.Source?.Id == ally.Id && t.Target?.Id == p.Opponent.Hero.Id), "granted Rush permits same-turn minion attacks only");
            PoolPilotProof.Resume(g, next => {
                var target = (Minion)next.IdEntityDic[ally.Id];
                target.Silence(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(!target.IsRush && !next.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == target.Id), "Silence removes Rush and restores fresh exhaustion on clone/snapshot/original");
            });
            var later = Summon(p, "CS2_182"); Core2026Proof.Check(!later.IsRush, "later summons do not receive an aura");
            Kill(g, source); End(g); End(g);
            Core2026Proof.Check(!later.IsRush && retained.IsRush, "permanent grant survives source death/turn end; later minion has no Rush");
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
        }
        End(g); Play(g, p.Opponent, "CS2_033"); End(g);
        Play(g, p, "CS2_168"); ally = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(!ally.IsRush, "opponent previous-turn and own current-turn tribe do not qualify");
    });
}
