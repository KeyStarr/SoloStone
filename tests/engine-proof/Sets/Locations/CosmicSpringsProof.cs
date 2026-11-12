using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class CosmicSpringsProof
{
    internal static void StandardCase() => StandardSetsProof.Case("BE_251", (game, p, id) => {
        var springs = L.Play(p, id);
        foreach (var card in p.DeckZone.ToArray()) p.DeckZone.Remove(card);
        var drawn = Entity.FromCard(p, Cards.FromId("CS2_029"), zone:p.DeckZone);
        p.UsedMana = 5;
        L.Use(springs);
        L.Check(p.HandZone.Contains(drawn) && p.UsedMana == 3, "Springs draws spell then refreshes two");
        L.End(game);
    });
    internal static void Run()
    {
        L.Check(Cards.FromId("BE_251")?.Implemented == true, "Cosmic Springs registered");
        for (int seed = 1; seed <= 3; seed++)
        foreach (string id in new[] { "CS2_182", "CS2_029", "CS2_106", "" })
        foreach (bool burn in new[] { false, true })
        foreach (int spent in new[] { 0, 1, 2, 5 })
        {
            var game = L.Game(seed); var p = game.Player1; var springs = L.Play(p, "BE_251");
            foreach (var old in p.DeckZone.ToArray()) p.DeckZone.Remove(old);
            var drawn = id == "" ? null : Entity.FromCard(p, Cards.FromId(id), zone:p.DeckZone);
            if (burn) while (!p.HandZone.IsFull) Entity.FromCard(p, Cards.FromId("CS2_182"), zone:p.HandZone);
            p.UsedMana = spent; game.DeathProcessingAndAuraUpdate();
            foreach (var fork in new[] { game, game.CloneExact(history:true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var owner = fork.Player1; int health = owner.Hero.Health;
                L.Use((BoardObject)fork.IdEntityDic[springs.Id]);
                int expected = !burn && (id == "CS2_029" || id == "CS2_106") ? Math.Max(0, spent - 2) : spent;
                L.Check(owner.UsedMana == expected && owner.BaseMana == 10, "Springs type/fatigue/spent threshold after clone/snapshot");
                if (drawn != null) L.Check(fork.IdEntityDic[drawn.Id].Zone.Type == (burn ? Zone.GRAVEYARD : Zone.HAND), "actual drawn entity retained");
                else L.Check(owner.Hero.Health == health - 1, "fatigue deals damage without refresh");
                if (burn && drawn != null && drawn.Card.Type != CardType.MINION) Unpinned_BurnedDrawNoRefresh(owner, spent);
            }
        }
        Console.WriteLine("PASS Cosmic Springs seeds 1-3 minion/spell/weapon/fatigue/burn/spent thresholds/clone/snapshot; Unpinned_BurnedDrawNoRefresh");
    }
    private static void Unpinned_BurnedDrawNoRefresh(Controller p, int expected) => L.Check(p.UsedMana == expected, "burned non-minion does not refresh");
}
