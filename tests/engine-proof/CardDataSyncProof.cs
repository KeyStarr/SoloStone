using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class CardDataSyncProof
{
    [GroupProof("card-data-sync")]
    internal static void Run()
    {
        var game = ImbueProof.Make(CardClass.WARRIOR); var player = game.Player1;
        Core2026Proof.Check(Summon(player, "AT_069").HasTaunt, "Sparring Partner has printed Taunt");
        Core2026Proof.Check(Add(player, "AT_014").Cost == 2, "Shadowfiend has its 37.0 hand cost");
        Core2026Proof.Check(Cards.FromId("AT_030").IsRace(Race.UNDEAD), "Undercity Valiant is seen as Undead");
        Console.WriteLine("PASS card data sync: Taunt, hand cost, Undead race");
    }
}
