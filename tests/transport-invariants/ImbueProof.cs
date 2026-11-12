using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

internal static class ImbueProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Imbue " + message);

    internal static void Run()
    {
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.ROGUE, CardClass.PRIEST })
        {
            var game = new Game(new GameConfig { RandomSeed = 37, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE,
                FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); var p = game.Player1; p.BaseMana = 10;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            void Flush() { session.Flush(); game.PowerHistory.Last.Clear(); }
            Flush();
            int old = p.Hero.HeroPower.Id;
            p.Hero.HeroPower.IsExhausted = true;
            int cursor = session.Packets.Count;
            ImbueRules.Imbue(p, p.Hero);
            Flush();
            var power = p.Hero.HeroPower;
            var shown = session.Client.Entities[power.Id];
            var history = session.Packets.Skip(cursor).Where(w => w.Id == 19).SelectMany(w => {
                var packet = new PegasusGame.PowerHistory();
                using var stream = new MemoryStream(Convert.FromBase64String(w.Base64));
                packet.Deserialize(stream);
                return packet.List;
            }).ToArray();
            var replacement = history.Single(e => e.HasFullEntity && e.FullEntity.Entity == power.Id).FullEntity;
            Check(!history.Any(e => e.HasResetGame) && !replacement.Tags.Any(t => t.Name == (int)GameTag.COLLECTIBLE)
                && !shown.Tags.ContainsKey((int)GameTag.COLLECTIBLE)
                && !match.SnapshotVisibleTransportTags(power).ContainsKey((int)GameTag.COLLECTIBLE),
                "replacement packet/client/cache omit zero COLLECTIBLE without Rewind " + power.Card.Id);
            Check(shown.CardId == power.Card.Id && shown.Get(GameTag.CARDTYPE) == 10
                && shown.Get(GameTag.CONTROLLER) == 1 && shown.Get(GameTag.ZONE) == (int)Zone.PLAY
                && shown.Get(GameTag.CREATOR) == p.Hero.Id && shown.Get(GameTag.COST) == power.Card.Cost
                && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 1 && shown.Get(GameTag.EXHAUSTED) == 0,
                "replacement FULL_ENTITY identity/controller/type/zone/cost/magnitude/refresh " + cls);
            Check(session.Client.Entities[old].Get(GameTag.ZONE) == (int)Zone.SETASIDE, "old power leaves play");
            Check(session.Client.Entities[p.Id].Get(GameTag.IMBUE) == 1, "controller progression published");
            session.Client.Entities[p.Id].Tags[(int)GameTag.IMBUE] = 9;
            Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == p.Id && v.Tag == "IMBUE"), "oracle detects corrupted progression");
            session.Client.Entities[p.Id].Tags[(int)GameTag.IMBUE] = 1;
            int full = session.Client.EntryCounts["FULL_ENTITY"];
            power.IsExhausted = true;
            ImbueRules.Imbue(p, p.Hero, 4); Flush();
            Check(p.Hero.HeroPower.Id == power.Id && session.Client.EntryCounts["FULL_ENTITY"] == full
                && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 5 && shown.Get(GameTag.EXHAUSTED) == 1
                && session.Client.Entities[p.Id].Get(GameTag.IMBUE) == 5, "upgrades emit deltas without replacement or refresh");
            if (power.Card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
                Check(shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_2) == 5, "second displayed magnitude published");
            Check(!Invariants.Compare(game, session.Client).Any(), "strict parity after upgrade");
            shown.Tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
            Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == power.Id && v.Tag == "TAG_SCRIPT_DATA_NUM_1"),
                "oracle detects wrong displayed magnitude");
            shown.Tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] = 5;
            var held = Generic.DrawCard(p, Cards.FromId("EDR_850pe"));
            var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_182"));
            Generic.SummonBlock(game, target, -1, null); game.DeathProcessingAndAuraUpdate();
            Flush();
            if (cls == CardClass.PRIEST)
            {
                power.IsExhausted = false;
                Flush(); session.PublishDecision(p);
                var options = (SabberStoneCore.Kettle.PowerAllOptions)typeof(NativeMatch)
                    .GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
                Check(options.PowerOptionList.Any(o => o.MainOption?.EntityId == power.Id && o.MainOption.PowerKeyword == 0),
                    "Moon ordinary HP option source");
                session.Process(HeroPowerTask.Any(p));
                session.PublishDecision(p);
                var offer = new PegasusGame.EntityChoices();
                using (var stream = new MemoryStream(Convert.FromBase64String(session.Packets.Last(w => w.Id == 17).Base64))) offer.Deserialize(stream);
                Check(offer.Source == power.Id && offer.PlayerId == p.PlayerId && offer.Entities.Count == 2,
                    "Moon native choice source/count");
                int selected = p.Choice.Choices[0];
                var beforeAnswer = game.SaveSnapshot(); bool stale = false;
                try { match.SendChoices(match.HumanChoiceId + 1, new List<int> { selected }); }
                catch (InvalidOperationException) { stale = true; }
                Check(stale && beforeAnswer.SequenceEqual(game.SaveSnapshot()), "Moon stale packet rejected without mutation");
                match.SendChoices(match.HumanChoiceId, new List<int> { selected });
                var generated = game.IdEntityDic[selected];
                Check(generated.Zone == p.HandZone && generated.Cost == Math.Max(0, generated.Card.Cost - 5)
                    && TemporaryRules.IsTemporary(generated) && p.UsedMana == power.Card.Cost
                    && power.IsExhausted && match.RewardFacts.DistinctCardsPlayed == 0,
                    "Moon printed HP payment, chosen discount/Temporary, no hand-play reward");
                Check(session.Client.Entities[selected].Get(GameTag.COST) == generated.Cost
                    && session.Client.Entities[selected].Get(GameTag.GHOSTLY) == 1,
                    "Moon selected hand cost/Temporary projected");
            }
            else ImbueRules.TriggerCurrentHeroPower(p);
            game.DeathProcessingAndAuraUpdate(); Flush();
            Check(!Invariants.Compare(game, session.Client).Any(), "generated token/buff visible " + cls);
            if (cls == CardClass.HUNTER)
                Check(session.Client.Entities[held.Id].Get(GameTag.ATK) == 8 && session.Client.Entities[held.Id].Get(GameTag.COST) == 0,
                    "Hunter buffed hand attack and cost visible");
            session.PublishDecision(p);

        }
        FullHandActivationOptions();
        MoonTemporaryLifecycle();
        Console.WriteLine("PASS Imbue transport: FULL_ENTITY replacement; upgrade deltas; progress; tokens/buffs; Moon choice/payment/Temporary");
    }
    private static void FullHandActivationOptions()
    {
        foreach (var cls in new[] { CardClass.ROGUE, CardClass.PRIEST })
        {
            var game = new Game(new GameConfig { RandomSeed = 37, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE,
                FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); var p = game.Player1; p.BaseMana = 10;
            while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("CS2_182"));
            ImbueRules.Imbue(p, p.Hero, 2);
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            bool HasPowerOption()
            {
                session.PublishDecision(p);
                var options = (SabberStoneCore.Kettle.PowerAllOptions)typeof(NativeMatch)
                    .GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
                return options.PowerOptionList.Any(o => o.MainOption?.EntityId == p.Hero.HeroPower.Id);
            }
            int choices = session.Packets.Count(w => w.Id == 17);
            Check(!HasPowerOption() && !game.Process(HeroPowerTask.Any(p))
                && p.Choice == null && game.Rewind == null && p.UsedMana == 0,
                "full-hand native HP option absent and activation rejected " + cls);
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(session.Packets.Count(w => w.Id == 17) == choices,
                "rejected full-hand power emits no native choice " + cls);
            p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Last()));
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(HasPowerOption(), "native HP option returns with one free hand slot " + cls);
            session.Process(HeroPowerTask.Any(p));
            int selected = cls == CardClass.ROGUE ? game.Rewind.KeepEntityId
                : p.Choice.Choices.First(id => game.IdEntityDic[id].Card.Shatter == null);
            session.Process(ChooseTask.Pick(p, selected));
            Check(p.HandZone.IsFull && p.Choice == null && game.Rewind == null
                && p.UsedMana == p.Hero.HeroPower.Card.Cost && p.NumTimesHeroPowerUsedThisGame == 1
                && !Invariants.Compare(game, session.Client).Any(),
                "native last-slot grant and choice cleanup preserve payment and parity " + cls);
        }
    }

    private static void MoonTemporaryLifecycle()
    {
        foreach (int owner in new[] { 1, 2 })
        foreach (int seed in new[] { 1, 2, 3 })
        foreach (bool fullHand in new[] { false, true })
        {
            // imbue-21/22 lean: selected offer keeps identity, gets discount/Temporary, then burns or expires.
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = owner, SkipMulligan = true,
                History = true, Logging = false, Player1HeroClass = CardClass.PRIEST, Player2HeroClass = CardClass.PRIEST,
                FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); var p = game.CurrentPlayer; p.BaseMana = 10;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            if (fullHand) for (int i = 0; i < 10; i++) Generic.DrawCard(p, Cards.FromId("CS2_182"));
            ImbueRules.Imbue(p, p.Hero, 2);
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            if (fullHand)
            {
                Check(ImbueRules.TriggerCurrentHeroPower(p) == SabberStoneCore.Tasks.TaskState.COMPLETE,
                    "direct full-hand Moon effect resolves");
                session.Flush(); game.PowerHistory.Last.Clear();
            }
            else session.Process(HeroPowerTask.Any(p));
            var offers = p.Choice.Choices.ToArray();
            // Shatter has separate split-identity proofs; this fixture follows one granted entity through expiry.
            int selected = offers.First(id => game.IdEntityDic[id].Card.Shatter == null);
            Check(offers.All(id => session.Client.Entities[id].CardId == (owner == 1 ? game.IdEntityDic[id].Card.Id : "")),
                "Moon offer identities respect perspective");
            Check(!Invariants.Compare(game, session.Client).Any(), "Moon offer strict parity");
            int[] held = p.HandZone.Select(c => c.Id).ToArray();
            session.Process(ChooseTask.Pick(p, selected));
            var generated = game.IdEntityDic[selected];
            Check(p.Choice == null && generated.Zone.Type == (fullHand ? Zone.GRAVEYARD : Zone.HAND)
                && p.UsedMana == (fullHand ? 0 : 2), $"Moon choice owner={owner} seed={seed} full={fullHand} card={generated.Card.Id} zone={generated.Zone.Type} mana={p.UsedMana} pending={p.Choice != null}");
            if (fullHand) Check(p.HandZone.Select(c => c.Id).SequenceEqual(held)
                && !p.DiscardedEntities.Contains(selected), "native full hand burns without discard or replacement");
            else
            {
                Check(TemporaryRules.IsTemporary(generated) && generated.Cost == Math.Max(0, generated.Card.Cost - 2)
                    && generated.AppliedEnchantments.Single().Card.Id == "GBL_999e", "chosen offer receives discount and Temporary");
                Check(session.Client.Entities[selected].CardId == (owner == 1 ? generated.Card.Id : ""), "chosen identity respects perspective");
            }
            Check(!Invariants.Compare(game, session.Client).Any(), "Moon grant/burn strict parity");
            session.Process(EndTurnTask.Any(p));
            Check(generated.Zone == p.GraveyardZone && (fullHand || !TemporaryRules.IsTemporary(generated))
                && p.DiscardedEntities.Contains(selected) == !fullHand, "owner end discards grant but never counts burn as discard");
            Check(!Invariants.Compare(game, session.Client).Any(), "Moon expiry strict parity");
        }
        Console.WriteLine("PASS MoonTemporaryLifecycle: both perspectives; seeds=1..3; revealed offer/discount/helper; full-hand burn; owner-end discard; strict invariants");
    }

}
