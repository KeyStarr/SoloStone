using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_VioletTreasuregillProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_438", (g, p, id) => {
        PoolPilotProof.ClassDeck(p);
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var buff = Entity.FromCard(p, Cards.FromId("CS2_004"), zone: p.DeckZone);
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; var source = (Minion)Play(next, owner, id);
            var cast = (Spell)next.IdEntityDic[buff.Id];
            Core2026Proof.Check(source.Health == 4 && owner.HandZone.Single().Card.Id == "CS2_182" && owner.DeckZone.Count == 0
                && cast.Zone == owner.GraveyardZone && cast.CardTarget == source.Id,
                "Unpinned_PreferredSelf consumes original before spell draw");
            AutomaticCastProof.Attempt(next, cast);
        });
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
        var secret = Entity.FromCard(p, Cards.FromId("CORE_EX1_610"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(secret.Zone == p.SecretZone, "deck Secret original installs");
        var duplicate = Entity.FromCard(p, Cards.FromId("CORE_EX1_610"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(duplicate.Zone == p.GraveyardZone && p.SecretZone.Single().Id == secret.Id, "duplicate deck Secret consumed");
        ClearBoard(g, p);
        var enemyOnly = Entity.FromCard(p, Cards.FromId("CS2_063"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(enemyOnly.Zone == p.GraveyardZone && enemyOnly.CardTarget == 0, "enemy-only required target fizzles");
        AutomaticCastProof.Attempt(g, (Spell)enemyOnly);
        ClearBoard(g, p); PoolPilotProof.ClassDeck(p);
        var enemy = Summon(p.Opponent, "CS2_200");
        enemyOnly = Entity.FromCard(p, Cards.FromId("CS2_063"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(enemyOnly.CardTarget == enemy.Id, "illegal self falls back to legal enemy");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); PoolPilotProof.ClassDeck(p);
        var discover = Entity.FromCard(p, Cards.FromId("CORE_GIL_836"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && discover.Zone == p.GraveyardZone
            && p.HandZone.Count == 1, "automatic Discover leaves no prompt");
        ClearBoard(g, p); PoolPilotProof.ClassDeck(p);
        var mode = Entity.FromCard(p, Cards.FromId("EX1_154"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(p.Choice == null && mode.CardTarget != 0 && mode.Zone == p.GraveyardZone, "Choose One picks mode before preferred target");
        ClearBoard(g, p); PoolPilotProof.ClassDeck(p);
        var expensive = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(expensive.Zone == p.DeckZone, "no affordable deck spell does nothing");
    });
}
