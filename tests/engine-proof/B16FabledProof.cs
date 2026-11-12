using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16FabledProof
{
    [GroupProof("b16-fabled")]
    internal static void Run()
    {
        var ids = FabledRules.ExpandForDeckbuilding(new[]{"TIME_005"}.Concat(Enumerable.Repeat("CS2_182",30))).ToArray();
        Core2026Proof.Check(ids.Length == 40 && !FabledRules.DeckErrors(ids).Any(), "Fabled+ accepts ten Rafaams plus thirty other slots");
        Core2026Proof.Check(FabledRules.DeckErrors(ids.Where(id=>id!="TIME_005t9")).Any(), "missing companion rejected");
        Core2026Proof.Check(FabledRules.DeckSize(ids)==40 && FabledRules.DeckSize(new[]{"CS2_182"})==30,"size rule scoped to Fabled+");
        var game=new Game(new SabberStoneCore.Config.GameConfig { StartPlayer=1,RandomSeed=19,Shuffle=false,
            SkipMulligan=true,History=true,Logging=false,Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),12).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),12).ToList() });
        game.StartGame();
        var original=Add(game.Player1,"TIME_020"); FabledRules.Vanish(original);
        foreach(var next in new[]{game,game.CloneExact(history:true),Game.LoadSnapshot(game.SaveSnapshot())})
        {
            var returned=FabledRules.ReturnVanished(next.Player2,120074);
            Core2026Proof.Check(returned?.Id==original.Id && returned.Controller==next.Player2 && returned.Zone==next.Player2.HandZone,"stolen final returns unique original to completing player");
            Core2026Proof.Check(FabledRules.ReturnVanished(next.Player1,120074)==null,"original owner cannot claim already completed reward");
        }
        B16FabledEdgesProof.Run();
        Console.WriteLine("PASS Fabled+ construction and companion persistence");
    }
}
