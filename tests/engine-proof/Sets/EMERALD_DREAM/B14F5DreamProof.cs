using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14F5DreamProof
{
    internal static readonly string[] Dreams = { "DREAM_01", "DREAM_02", "DREAM_03", "DREAM_04", "DREAM_05" };
    internal static void Run(Game g, Controller p, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            PoolPilotProof.EmptyHand(p);
            if (reverse) { Summon(p, "CS2_182"); Summon(p, "CS2_200"); }
            g.Random.SetSeed(g.Seed);
            var expected = g.CloneExact().Random.Next(5);
            PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
            var output = p.HandZone.Single();
            Core2026Proof.Check(output.Card.Id == Dreams[expected] && output[GameTag.DISPLAYED_CREATOR] != 0, "seeded exact Dream, independent of board");
            if (output.Card.Id == "DREAM_01") Core2026Proof.Check(output.Cost == 2 && output.Card.Text == "<b>Elusive</b>", "current Laughing Sister snapshot");
            StandardSetsProof.ClearBoard(g, p);
        }
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        var before = g.Random.DrawCount; Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == before + 1, "last slot generates one Dream");
        // Resolve directly with a full hand to exercise burn behavior without freeing the source's slot.
        var source = Entity.FromCard(p, Cards.FromId(id)); before = g.Random.DrawCount;
        PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == before + 1, "full hand burns generated Dream but consumes its seeded pick");
    }
}
