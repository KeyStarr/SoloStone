using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ChronoLordEpochProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_714", (g, p, id) => {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.AttackDamage == 7 && body.Health == 5 && body.Card.ConsumesHistory, "no previous opponent turn; printed 7/5 Dragon");
        ClearBoard(g, p);
        var friendly = (Minion)Play(g, p, "CS2_182");
        End(g); var enemy = p.Opponent;
        var old = (Minion)Play(g, enemy, "CS2_182");
        End(g); End(g);
        var victim = (Minion)Play(g, enemy, "CS2_182");
        var summoned = Summon(enemy, "CS2_182");
        var bounced = (Minion)Play(g, enemy, "CS2_171"); Generic.ReturnToHandBlock(enemy, bounced); PlayEntity(g, enemy, bounced);
        var transformed = (Minion)Play(g, enemy, "CS2_120"); Generic.TransformBlock(enemy, SabberStoneCore.Model.Cards.FromId("CS2_200"), transformed);
        var newBody = enemy.BoardZone.Last();
        var stolen = (Minion)Play(g, enemy, "CS2_231"); Generic.ChangeEntityBlock(enemy, stolen, stolen.Card, false);
        End(g);
        // Existing control transfer preserves the play link even though it creates a fresh residence.
        var theft = victim;
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(EntityType.TARGET)
            .Process(g, p, p.Hero, theft);
        var clone = g.Clone(); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.IdEntityDic[victim.Id].Zone.Type == Zone.GRAVEYARD && victim.Zone == p.BoardZone, "clone-only destruction isolated");
        Play(g, p, id);
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && bounced.Zone.Type == Zone.GRAVEYARD, "opponent-played live incarnations die on either side");
        Core2026Proof.Check(old.Zone == enemy.BoardZone && summoned.Zone == enemy.BoardZone && newBody.Zone == enemy.BoardZone && friendly.Zone == p.BoardZone,
            "older play, same-ID summon, transformed new body and own play survive");
        ClearBoard(g, p); ClearBoard(g, enemy);
        End(g); enemy.NumTurnsLeft = 2;
        var priorExtra = (Minion)Play(g, enemy, "CS2_182"); End(g);
        var lastExtra = (Minion)Play(g, enemy, "CS2_231"); End(g);
        Play(g, p, id);
        Core2026Proof.Check(lastExtra.Zone.Type == Zone.GRAVEYARD && priorExtra.Zone == enemy.BoardZone, "opponent's last begun extra turn, no parity shortcut");
    });
}
