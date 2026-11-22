using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Engine and AI fixes found by live playtests and boss-AI sweeps (Dungeon Run work, 2026-11), none of them specific to a
// run: a card definition and the opponent AI's fair view of hidden cards. Four more bugs from the same work
// are fixed in open pull requests and are not repeated here: Silence and a card's own "Can't attack" (#144, #165),
// Voidcaller and Dark Wispers (#165), a switching Adaptive effect's history (Gemstudded Golem, #167).
internal static class PlaytestEngineFixesProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "playtest engine fixes: " + message);

    private static Game Duel(string p1 = "HERO_08", string p2 = "HERO_07", string[] p2Hand = null)
    {
        var game = new Game(new GameConfig {
            StartPlayer = 1, Player1HeroCard = Cards.FromId(p1), Player1HeroClass = Cards.FromId(p1).Class,
            Player2HeroCard = Cards.FromId(p2), Player2HeroClass = Cards.FromId(p2).Class,
            Player1Deck = Enumerable.Repeat("CS2_182", 20).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 20).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = 11,
        });
        game.StartGame();
        return game;
    }

    [GroupProof("playtest-engine-fixes")]
    internal static void Run()
    {
        // Sudden Betrayal: "When a minion attacks your hero" - an attacking hero is not redirected (it threw), a minion is.
        Game betrayal = Duel("HERO_06", "HERO_03");
        End(betrayal);
        Generic(betrayal.Player2, "LOOT_210");
        Summon(betrayal.Player2, "CS2_182");
        End(betrayal);
        var left = Summon(betrayal.Player1, "CS2_120"); var middle = Summon(betrayal.Player1, "CS2_182"); Summon(betrayal.Player1, "CS2_120");
        betrayal.Player1.Hero.HeroPower.IsExhausted = false;
        betrayal.Player1.BaseMana = 10; betrayal.Player1.UsedMana = 0;
        Check(betrayal.Process(HeroPowerTask.Any(betrayal.Player1)), "Shapeshift accepted");
        Check(betrayal.Process(HeroAttackTask.Any(betrayal.Player1, betrayal.Player2.Hero)), "the hero attacks");
        Check(betrayal.Player2.Hero.Damage == 1 && betrayal.Player2.SecretZone.Count == 1, "Sudden Betrayal: a hero's attack goes through");
        middle.IsExhausted = false;
        Check(betrayal.Process(MinionAttackTask.Any(betrayal.Player1, middle, betrayal.Player2.Hero)), "the minion attacks");
        Check(betrayal.Player2.Hero.Damage == 1 && betrayal.Player2.SecretZone.Count == 0, "Sudden Betrayal: a minion's attack turns on a neighbour");

        // The AI's fair view masks the opponent's private cards. C'Thun's ritual proxy (public: rituals are played in the open)
        // must stay in it, and no registered trigger may outlive its masked owner (Shifting Scroll's enchantment in hand);
        // either broke every search copy.
        Game view = Duel("HERO_08", "HERO_07");
        Add(view.Player1, "LOOT_104");
        Play(view, view.Player1, "OG_281");
        End(view); End(view); End(view);
        var state = FairBotState.Create(view, view.Player2.PlayerId, 7, DeckStyle.Value);
        var seen = state.FingerprintGame;
        Check(view.Player1.SeenCthun && seen.IdEntityDic.TryGetValue(view.Player1.ProxyCthun, out var proxy) && !(proxy is SabberStoneCore.Model.Entities.Unknown),
            "fair view: C'Thun's proxy is visible");
        Check(seen.Triggers.All(t => seen.IdEntityDic.ContainsKey(t.SourceId)), "fair view: every trigger's owner is in the view");
        Check(seen.CloneExact() != null, "fair view: the view copies");
    }

    private static void Generic(Controller owner, string secret)
    {
        var card = Add(owner, secret);
        owner.BaseMana = 10; owner.UsedMana = 0;
        Check(owner.Game.Process(PlayCardTask.Spell(owner, card)), secret + " set");
    }
}
