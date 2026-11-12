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

internal static class CopyInitializationProof
{
    private static void Check(bool condition, string message) => SelfChecks.Check(condition, "copy initialization " + message);

    internal static void Run()
    {
        var game = new Game(new GameConfig { RandomSeed = 71, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        var player = game.Player1;
        player.BaseMana = 10;
        var source = Generic.DrawCard(player, Cards.FromId("CS2_182"));
        var sword = Generic.DrawCard(player, Cards.FromId("EX1_366"));
        NativeSession session = null;
        Minion copy = null;
        int options = 0;
        var match = new NativeMatch((id, packet) => {
            // Inspect the decoded client state at the actual production options send boundary.
            if (id == 14)
            {
                options++;
                Check(copy != null && session.Client.Entities.TryGetValue(copy.Id, out _), "copy revealed before options");
                var visible = session.Client.Entities[copy.Id];
                Check(visible.Get(GameTag.ATK) == 9 && visible.Get(GameTag.HEALTH) == 9
                    && visible.Get(GameTag.DIVINE_SHIELD) == 1 && visible.Get(GameTag.ZONE) == (int)Zone.PLAY,
                    "initialized 8/8 shield plus Sword +1/+1 reaches client before options");
                Check(!Invariants.Compare(game, session.Client).Any(), "strict client parity before options: " + string.Join("; ", Invariants.Compare(game, session.Client)));
            }
            session.Receive(id, packet);
        });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        session.Flush();
        session.Process(PlayCardTask.Any(player, sword));
        int cursor = session.Packets.Count;
        copy = (Minion)Generic.Copy(player, player.Hero, source, Zone.PLAY, initialize: entity => {
            var minion = (Minion)entity;
            minion.AttackDamage = 8;
            minion.BaseHealth = 8;
            minion.HasDivineShield = true;
        });
        game.DeathProcessingAndAuraUpdate();
        session.Flush();
        session.PublishDecision(player);
        Check(options == 1 && session.Packets.Last().Id == 14, "publishes normal options after history");
        Check(copy.AttackDamage == 9 && copy.BaseHealth == 9 && copy.HasDivineShield
            && player.Hero.Weapon.Durability == 4, "engine retains initializer and exactly one summon buff");
        Check(source[GameTag.ATK] == 4 && source[GameTag.HEALTH] == 5 && source[GameTag.DIVINE_SHIELD] == 0,
            "initializer leaves hand source unchanged");

        // Replay each serialized history entry, proving 8/8 + shield precedes the 9/9 summon buff.
        var replay = new ClientModel();
        bool initialized = false, buffed = false;
        foreach (var wire in session.Packets.Skip(cursor).Where(p => p.Id == 19))
        {
            var packet = new PegasusGame.PowerHistory();
            using (var stream = new MemoryStream(Convert.FromBase64String(wire.Base64))) packet.Deserialize(stream);
            foreach (var entry in packet.List)
            {
                var single = new PegasusGame.PowerHistory();
                single.List.Add(entry);
                replay.Apply(single);
                if (!replay.Entities.TryGetValue(copy.Id, out var visible)) continue;
                if (visible.Get(GameTag.ATK) == 8 && visible.Get(GameTag.HEALTH) == 8 && visible.Get(GameTag.DIVINE_SHIELD) == 1)
                    initialized = true;
                if (visible.Get(GameTag.ATK) == 9 && visible.Get(GameTag.HEALTH) == 9)
                {
                    Check(initialized, "initialized stats/shield precede summon buff in native history");
                    buffed = true;
                }
            }
        }
        Check(initialized && buffed, "wire contains both initialized and summon-buffed states");
        Console.WriteLine("PASS copy initialization transport: production NativeMatch history/options; initialized 8/8 shield then Sword 9/9; client parity before options");
    }
}
