using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineController = SabberStoneCore.Model.Entities.Controller;
using EngineMinion = SabberStoneCore.Model.Entities.Minion;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

// The engine-only fixture and helpers of the quest proofs: native-match (QuestNativeProof.Run) and native-match-logic
// (QuestBotProof) both use them. QuestNativeProof is partial; its native proof is in tests/native-match.
internal static partial class QuestNativeProof
{
    internal static void Check(bool value, string message)
    { if (!value) throw new Exception("Quest native: " + message); }
    internal static EngineGame Fixture()
    {
        var game = new EngineGame(new GameConfig { RandomSeed = 19, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10;
        }
        return game;
    }
    internal static IPlayable Add(EngineController p, string id) => Generic.DrawCard(p, Cards.FromId(id));
    internal static IPlayable Play(EngineGame game, EngineController p, string id)
    {
        p.UsedMana = 0;
        var card = Add(p, id);
        Check(game.Process(p.Options().First(t => t.Source?.Id == card.Id)), "play " + id);
        return card;
    }
    internal static void Summon(EngineGame g, EngineController p, string id)
    {
        var card = (EngineMinion)EngineEntity.FromCard(p, Cards.FromId(id));
        Generic.SummonBlock(g, card, -1, null); g.DeathProcessingAndAuraUpdate();
    }
}
