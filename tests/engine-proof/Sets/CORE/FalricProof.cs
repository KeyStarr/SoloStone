using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FalricProof
{
    internal static void Unpinned_DrawAndGain(Game g, Controller p, string id)
    {
        PoolPilotProof.ClassDeck(p);
        var miss = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var first = Entity.FromCard(p, Cards.FromId("CORE_RLK_118"), zone: p.DeckZone);
        var second = Entity.FromCard(p, Cards.FromId("EDR_813"), zone: p.DeckZone);
        var third = Entity.FromCard(p, Cards.FromId("FIR_951"), zone: p.DeckZone);
        var random = g.Random.Clone();
        var matches = new[] { first, second, third };
        var expected = matches[random.Next(3)];
        var card = Add(p, id);
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; owner.BaseMana = 10; owner.UsedMana = 0;
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner, next.IdEntityDic[card.Id])), "Falric plays");
            Core2026Proof.Check(owner.HandZone.Single().Id == expected.Id && owner.DeckZone.Any(e => e.Id == miss.Id), "seeded physical Corpse-spender tutor");
            Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState()), "tutor consumes exactly one random pick");
            CorpseRules.Gain(owner, 2); Core2026Proof.Check(CorpseRules.Available(owner) == 4, "explicit Corpse gain doubled");
        });
        // No match never draws an arbitrary card or causes fatigue.
        var branch = DeathKnightC1Proof.NewGame(g.Seed); var b = branch.Player1;
        int deck = b.DeckZone.Count; var cursor = branch.Random.CaptureState();
        Play(branch, b, id);
        Core2026Proof.Check(b.HandZone.IsEmpty && b.DeckZone.Count == deck && cursor.Equals(branch.Random.CaptureState()), "no-match tutor is inert");
        PoolPilotProof.ClassDeck(b);
        var burn = Entity.FromCard(b, Cards.FromId("CORE_RLK_118"), zone: b.DeckZone);
        PoolPilotProof.FillHand(b);
        PoolPilotProof.Resolve(branch, b.BoardZone.Single());
        Core2026Proof.Check(b.HandZone.Count == 10 && burn.Zone == b.GraveyardZone && b.Hero.Fatigue == 0, "full-hand tutor burns physical matching card");
    }
}
