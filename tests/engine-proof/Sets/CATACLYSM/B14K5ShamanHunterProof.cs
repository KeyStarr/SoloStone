using SabberStoneCore.Model;

internal static class B14K5ShamanHunterProof
{
    [GroupProof("b14-k5-shaman-hunter-cards", ProofKind.Cards)]
    internal static void Run()
    {
        B14K5CataclysmProof.Run();
        B14K5LostCityProof.Run();
        B14K5LocationsProof.Run();
        B14K5TimeTravelProof.Run();
    }

    internal static IEnumerable<Game> Forks(Game game) => new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game };
}
