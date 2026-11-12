using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class BattlefieldNecromancerProof
{
    // corpses/{raise-fits-board,risen-no-corpse,risen-silence-corpse}.yaml
    internal static void Run() => DeathKnightC1Proof.Case("RLK_061", Unpinned_EndTurnRaise);
    private static void Unpinned_EndTurnRaise(Game game, Controller p, string id)
    {
        Play(game, p, id); var source = p.BoardZone.Single();
        End(game); End(game);
        Core2026Proof.Check(p.BoardZone.Count == 1 && CorpseRules.Available(p) == 0, "no Corpse, no raise");
        CorpseRules.Gain(p, 3);
        PoolPilotProof.Resume(game, next => End(next));
        Core2026Proof.Check(p.BoardZone.Count == 2 && CorpseRules.Available(p) == 2, "owner end raises once");
        End(game);
        Core2026Proof.Check(p.BoardZone.Count == 2 && CorpseRules.Available(p) == 2, "opponent end never raises");
        var token = p.BoardZone.Single(m => m.Card.Id == "RLK_061t");
        Core2026Proof.Check(token.AttackDamage == 1 && token.Health == 3 && token.HasTaunt && token[GameTag.DISPLAYED_CREATOR] == source.Id,
            "Necromancer exact Footman and creator");
        LocationProofs.Location(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        PoolPilotProof.Resume(game, next => End(next));
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && CorpseRules.Available(p) == 2, "Unpinned_full board spends zero");
        End(game);
        token.Silence(); Kill(game, token);
        Core2026Proof.Check(CorpseRules.Available(p) == 2, "Unpinned_silenced Footman leaves no Corpse");
        source.Silence(); End(game); End(game);
        Core2026Proof.Check(CorpseRules.Available(p) == 2, "silenced source stops raising");
        StandardSetsProof.ParkBoard(p);
        source = Summon(p, id);
        new ReturnHandTask(EntityType.TARGET).Process(game, p, p.Hero, source);
        End(game); End(game);
        Core2026Proof.Check(p.BoardZone.Count == 0 && CorpseRules.Available(p) == 2, "bounced source stops raising");
        source = Summon(p, id); Kill(game, source);
        End(game); End(game);
        Core2026Proof.Check(p.BoardZone.Count == 0 && CorpseRules.Available(p) == 3, "dead source stops raising; ordinary source leaves Corpse");
    }
}
