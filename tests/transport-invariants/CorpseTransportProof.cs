using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

internal static class CorpseTransportProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Corpses " + message);
    private static NativeSession Attach(Game game)
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        return session = new NativeSession(match);
    }
    internal static void Run()
    {
        var ids = Simulation.DeckIds("death-knight-v1");
        var required = ("BE_284 BE_325 CATA_467 CATA_469 CORE_EDR_002 CORE_RLK_062 CORE_RLK_121 EDR_812 EDR_816 "
            + "JAIL_440 JAIL_441 JAIL_445 JAIL_450 JAIL_454 RLK_708 RLK_958 TIME_611 TLC_401 TLC_439 TLC_443").Split(' ');
        Check(required.All(id => ids.SelectMany(key => Simulation.Deck(key).Cards).Contains(id)), "suite covers all 20 existing DK cards");
        foreach (string id in ids)
        {
            var deck = Simulation.Deck(id); var cards = deck.Cards.Select(Cards.FromId).ToArray();
            Check(deck.Hero == "HERO_11" && cards.Length == 30 && cards.All(c => c.Implemented && CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT))
                && RuneRules.IsLegalForClass(RuneRules.FromCards(cards), CardClass.DEATHKNIGHT), "registered/rune-legal fixture " + id);
        }
        foreach (var cls in new[] { CardClass.MAGE, CardClass.DEATHKNIGHT })
        {
            var game = new Game(new GameConfig { EnableCorpses = true, RandomSeed = 37, StartPlayer = 1, SkipMulligan = true,
                Shuffle = false, History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); var p = game.Player1; p.BaseMana = 10;
            var session = Attach(game);
            void Flush() { session.Flush(); game.PowerHistory.Last.Clear(); }
            Flush();
            foreach (var owner in new[] { p, p.Opponent })
            {
                for (int i = 0; i < 2; i++)
                {
                    var token = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_101t"));
                    Generic.SummonBlock(game, token, -1, null); token.Destroy();
                }
            }
            game.DeathProcessingAndAuraUpdate(); Flush();
            Check(!Invariants.Compare(game, session.Client).Any(), "simultaneous friendly/opponent deaths parity");
            Generic.DrawCard(p, Cards.FromId("RLK_708")); Flush();
            Check(CorpseRules.Available(p) == 2 && session.Client.Entities[p.Id].Get(GameTag.CORPSES) == 2,
                "off-class acquisition preserves earlier earned Corpses on player entity");
            Check(CorpseRules.TrySpend(p, 1), "spend accepted"); Flush();
            Check(session.Client.Entities[p.Id].Get(GameTag.CORPSES) == 2
                && session.Client.Entities[p.Id].Get(GameTag.CORPSES_SPENT_THIS_GAME) == 1
                && !Invariants.Compare(game, session.Client).Any(), "earned/spent normal TAG_CHANGE parity");
            foreach (var tag in new[] { GameTag.CORPSES, GameTag.CORPSES_SPENT_THIS_GAME })
            {
                var tags = session.Client.Entities[p.Id].Tags; int before = tags[(int)tag]; tags[(int)tag] = before + 9;
                var violations = Invariants.Compare(game, session.Client).ToArray();
                Check(violations.Any(v => v.Entity == p.Id && v.Tag == tag.ToString())
                    && violations.Any(v => v.Entity == p.Id && v.Tag == "CORPSES_AVAILABLE"), "corrupted oracle detects " + tag);
                tags[(int)tag] = before;
            }
            var snapshot = game.SaveSnapshot(); var resumed = Game.LoadSnapshot(snapshot);
            var resumedSession = Attach(resumed); resumedSession.Client = session.Client.Copy();
            Check(CorpseRules.TrySpend(resumed.Player1, 1), "resumed spending");
            Check(Invariants.Compare(resumed, resumedSession.Client).Any(v => v.Tag == "CORPSES_AVAILABLE"),
                "suppressing resumed spend packet fails parity");
            resumedSession.Flush();
            Check(!Invariants.Compare(resumed, resumedSession.Client).Any(), "resumed delta parity");
            Check(session.Client.Entities[p.Id].Get(GameTag.CORPSES_SPENT_THIS_GAME) == 1,
                "resumed spend cannot update original client");
            // Continue original with its own client model after the resumed branch.
            CorpseRules.TrySpend(p, 1);
            Check(Invariants.Compare(game, session.Client).Any(v => v.Tag == "CORPSES_AVAILABLE"),
                "suppressing original spend packet fails parity");
            Flush();
            Check(!Invariants.Compare(game, session.Client).Any(), "original delta parity");
            if (cls == CardClass.DEATHKNIGHT)
            {
                Check(game.Process(HeroPowerTask.Any(p)), "Ghoul Charge accepted"); Flush();
                var ghoul = p.BoardZone.Single();
                Check(session.Client.Entities[ghoul.Id].Get(GameTag.CHARGE) == 1
                    && session.Client.Entities[ghoul.Id].Get(GameTag.CARDRACE) == (int)Race.UNDEAD, "Ghoul Charge/Undead packets");
                session.Process(EndTurnTask.Any(p)); game.PowerHistory.Last.Clear();
                Check(CorpseRules.Available(p) == 1 && !Invariants.Compare(game, session.Client).Any(), "end-turn Ghoul corpse packets");
            }
        }
        Console.WriteLine("PASS corpse transport: gain/spend/simultaneous deaths/off-class acquisition/independent snapshot branches/Ghoul; suppressed branch spend packets and corrupted earned/spent/available oracle rejected");
    }
}
