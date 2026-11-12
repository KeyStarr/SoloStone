using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16AutoplayTerrorProof
{
    internal static void Run() => DeathKnightC1Proof.Case("TLC_433", Unpinned_TerrorCycle);
    private static void Unpinned_TerrorCycle(Game game, Controller p, string id)
    {
        var quest = (Spell)Play(game, p, id);
        CorpseRules.Gain(p, 15);
        Core2026Proof.Check(CorpseRules.TrySpend(p, 14) && quest.QuestProgress == 14, "actual corpse progress");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        Core2026Proof.Check(CorpseRules.TrySpend(p, 1) && !p.HandZone.Any(c => c.Card.Id == "TLC_433t"), "full-hand quest reward waits");
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            var owner = next.Player1;
            Generic.DiscardBlock(owner, owner.HandZone[0]);
            var reward = owner.HandZone.Single(c => c.Card.Id == "TLC_433t");
            PlayEntity(next, owner, reward);
            Kill(next, (Minion)reward);
            var grave = owner.BoardZone.GetLocations().Single();
            Core2026Proof.Check(grave.Card.Id == "TLC_433t2" && grave.Durability == 2, "Tyrax opens ready grave");
            LocationProofs.Use(grave, owner.Opponent.Hero);
            B16AutoplayProof.Ready(grave); LocationProofs.Use(grave, owner.Opponent.Hero);
            Core2026Proof.Check(owner.Opponent.Hero.Damage == 8 && owner.BoardZone.Single().Card.Id == "TLC_433t", "two grave hits then Tyrax resummon");
            owner.BoardZone.Single().Silence(); Kill(next, owner.BoardZone.Single());
            Core2026Proof.Check(!owner.BoardZone.HasLocations, "silenced Tyrax does not open grave");
        }
    }
}
