using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("TEST PoweredUp: " + message); }

    internal sealed class Fixture
    {
        private readonly List<PegasusGame.PowerHistory> packets = new List<PegasusGame.PowerHistory>();
        internal readonly NativeMatch Match;
        internal SabberStoneCore.Model.Game Game => Match.Game;
        internal Controller Player => Game.Player1;
        internal IEnumerable<PowerHistoryData> Entries => packets.SelectMany(packet => packet.List);

        internal Fixture()
        {
            var choices = new List<EntityChoices>();
            Match = new NativeMatch((id, message) => {
                if (id == 19) packets.Add((PegasusGame.PowerHistory)message);
                if (id == 17) choices.Add((EntityChoices)message);
            });
            Match.Start();
            Match.SendChoices(choices.Last().Id, new List<int>());
            Check(Game.State == State.RUNNING && Game.CurrentPlayer == Player, "fixture must start on Player1's turn");
            ClearHand();
            Mana(10);
        }

        internal void ClearHand()
        {
            foreach (var card in Player.HandZone.ToArray()) Player.SetasideZone.Add(Player.HandZone.Remove(card));
            Flush();
        }

        internal IPlayable Draw(string id) => SabberStoneCore.Actions.Generic.DrawCard(Player, Cards.FromId(id));
        internal Minion Summon(string id, Controller player) =>
            (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(id), zone: player.BoardZone);
        internal void Mana(int amount) { Player.BaseMana = amount; Player.UsedMana = 0; }

        internal void Flush()
        {
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Match, new object[] { Game.PowerHistory.Last });
            Game.PowerHistory.Last.Clear();
        }

        internal void Process(PlayerTask task)
        {
            Flush();
            // Use the native dispatcher so suspended decision recipes receive their typed answer.
            typeof(NativeMatch).GetMethod("Process", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Match, new object[] { task });
            Flush();
        }

        internal int Glow(IPlayable card)
        {
            int value = 0;
            foreach (var entry in Entries)
            {
                var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                if (entity?.Entity == card.Id && entity.Tags.Any(t => t.Name == (int)GameTag.POWERED_UP))
                    value = entity.Tags.Last(t => t.Name == (int)GameTag.POWERED_UP).Value;
                if (entry.HasTagChange && entry.TagChange.Entity == card.Id && entry.TagChange.Tag == (int)GameTag.POWERED_UP)
                    value = entry.TagChange.Value;
            }
            return value;
        }

        internal void Expected(IPlayable card, int value, string scenario)
        {
            Check(Glow(card) == value, scenario + " packet expected " + value + ", got " + Glow(card));
            var snapshot = Match.SnapshotVisibleTransportTags(card);
            Check((snapshot.TryGetValue((int)GameTag.POWERED_UP, out int actual) ? actual : 0) == value,
                scenario + " live audit expected " + value + ", got " + actual);
        }
    }

    private static readonly HashSet<string> OutcomeCases = new HashSet<string>(StringComparer.Ordinal);

    private static void Scenario(string id, Action<Fixture, IPlayable, bool> arrange,
        Func<Fixture, IPlayable, PlayerTask> play, Func<Fixture, IPlayable, bool> outcome)
    {
        foreach (bool met in new[] { false, true })
        {
            var fixture = new Fixture();
            var card = fixture.Draw(id);
            arrange(fixture, card, met);
            fixture.Flush();
            fixture.Expected(card, met ? 1 : 0, id + " before play, condition=" + met);
            fixture.Mana(10);
            fixture.Process(play(fixture, card));
            fixture.Expected(card, 0, id + " after play");
            Check(outcome(fixture, card) == met, id + " conditional outcome disagrees with pre-play glow=" + met);
        }
        OutcomeCases.Add(id);
    }

    private static bool NeverMet(IPlayable source) => false;

    private static void RegistryContract()
    {
        Check(PoweredUp.RegisteredIds.Contains("CATA_483"), "Spellcaster condition was not registered");
        foreach (var card in Cards.All.Where(c => c[GameTag.OUTCAST] == 1 || c[(GameTag)4028] == 1))
            Check(PoweredUp.HasCondition(card), "printed keyword lacks a condition: " + card.Id);

        var definition = Cards.FromId("CATA_483").Clone();
        Check(PoweredUp.HasCondition(definition), "cloned definition lost its registered condition");
        bool captured = false;
        Func<IPlayable, bool> multiple = NeverMet;
        multiple += NeverMet;
        foreach (var invalid in new Func<IPlayable, bool>[] { null, source => captured, multiple })
        {
            bool rejected = false;
            try { Cards.FromId("CS2_231").Clone().PoweredUpWhen(invalid); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "registry accepted null, captured or multicast predicate");
        }
        bool duplicate = false;
        try { definition.PoweredUpWhen(NeverMet); }
        catch (ArgumentException) { duplicate = true; }
        Check(duplicate, "registry accepted a second predicate for the same card Id");

        var fixture = new Fixture();
        fixture.Draw("CS2_231");
        definition.Tags[GameTag.OUTCAST] = 1;
        var combined = SabberStoneCore.Actions.Generic.DrawCard(fixture.Player, definition);
        Check(PoweredUp.IsMet(combined), "custom false condition suppressed generic Outcast");
        fixture.Draw("CS2_231");
        Check(!PoweredUp.IsMet(combined), "unmet custom and middle Outcast must not qualify");
        fixture.Process(PlayCardTask.SpellTarget(fixture.Player, fixture.Draw("CS2_029"), fixture.Game.Player2.Hero));
        Check(PoweredUp.IsMet(combined), "generic false condition suppressed registered predicate on cloned definition");
        fixture.Player.SetasideZone.Add(fixture.Player.HandZone.Remove(combined));
        Check(!PoweredUp.IsMet(combined), "met condition outside own hand must not qualify");
        fixture.Player.HandZone.Add(fixture.Player.SetasideZone.Remove(combined));
        fixture.Process(ConcedeTask.Any(fixture.Player));
        Check(!PoweredUp.IsMet(combined), "met condition after game end must not qualify");
    }

    private static void ConditionsAreReadOnly()
    {
        var fixture = new Fixture();
        var ids = PoweredUp.RegisteredIds.Concat(Cards.All
            .Where(c => c[GameTag.OUTCAST] == 1 || c[(GameTag)4028] == 1).Select(c => c.Id)).Distinct().ToArray();
        foreach (string id in ids)
        {
            fixture.ClearHand();
            var card = fixture.Draw(id);
            Check(card.Zone == fixture.Player.HandZone, "read-only fixture must be in hand: " + id);
            var entities = fixture.Game.IdEntityDic.Values.Cast<IEntity>()
                .Concat(new IEntity[] { fixture.Game, fixture.Player, fixture.Game.Player2 }).Distinct().ToArray();
            var tags = entities.ToDictionary(e => e, e => new Dictionary<GameTag, int>(e.NativeTags));
            int entityCount = fixture.Game.IdEntityDic.Count;
            int facts = fixture.Game.HistoryFacts.Count;
            int full = fixture.Game.PowerHistory.Full.Count, last = fixture.Game.PowerHistory.Last.Count;
            PoweredUp.IsMet(card);
            Check(entityCount == fixture.Game.IdEntityDic.Count && facts == fixture.Game.HistoryFacts.Count
                && full == fixture.Game.PowerHistory.Full.Count && last == fixture.Game.PowerHistory.Last.Count,
                "condition changed entities/history: " + id);
            foreach (var entity in entities)
                Check(entity.NativeTags.Count == tags[entity].Count
                    && tags[entity].All(pair => entity.NativeTags.TryGetValue(pair.Key, out int value) && value == pair.Value),
                    "condition changed entity tags: " + id + "/" + entity.Id);
            fixture.Player.SetasideZone.Add(fixture.Player.HandZone.Remove(card));
            Check(!PoweredUp.IsMet(card), "condition true outside hand: " + id);
        }
    }

    internal static void Run()
    {
        OutcomeCases.Clear();
        RegistryContract();
        ConditionsAreReadOnly();
        GroupProofs.Run(ProofKind.PoweredUp);
        InventoryIsComplete();
        Console.WriteLine("PASS PoweredUp hand glow: shared conditions, outcomes, turns and opponent privacy");
    }

}
