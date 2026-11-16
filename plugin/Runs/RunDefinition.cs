using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// A run mode (Dungeon Run first; later Monster Hunt, Rumble Run, Dalaran Heist, Tombs of Terror) as authored data:
// data/runs/<id>-v1.json. Pure model and strict parser, no client or engine types, so the CI suite `runs` builds it.
// Retail served all of this from its servers; every list here is authored from public sources (each entry names one).
public enum RunRewardKind { Loot, PassiveTreasure, ActiveTreasure }

public sealed class RunClientIds
{
    // The client's own ids for the adventure: its Dungeon Crawl display reads the run from the server save key.
    public int Adventure, Mode, Scenario, Wing, ServerKey, ClientKey, Board;
}

public sealed class RunRound
{
    public int Number;
    // Offered after winning this round, in order (the client shows a pending treasure before pending loot).
    public IReadOnlyList<RunRewardKind> Rewards;
    public int BossStartingMana;
}

public sealed class RunClassStart
{
    public string Class;
    public IReadOnlyList<string> Deck;
}

public sealed class RunBoss
{
    // Family: the forms of one boss (Dungeon Run's later-round bosses are separate hero cards, e.g. Blackseed at 40 Health
    // with Evolvomancy and at 60 with Greater Evolution). A run fights at most one boss of a family. Defaults to the id.
    public string Id, Family, Hero, HeroPower, Source;
    public IReadOnlyList<int> Rounds;
    public IReadOnlyList<string> Deck;
    public IReadOnlyList<string> Board;
    // Cards the player gets in hand after the mulligans in this fight (The Darkness's Luminous Candles).
    public IReadOnlyList<string> PlayerHand;

    public IEnumerable<string> AllCards()
    {
        yield return Hero;
        yield return HeroPower;
        foreach (string id in Deck) yield return id;
        foreach (string id in Board) yield return id;
        foreach (string id in PlayerHand) yield return id;
    }
}

public sealed class RunBucket
{
    public string Id, Class, Title, Source;
    public IReadOnlyList<string> Cards;
    // "pool": "class" (Dungeon Run's Mage, Priest and Warlock "Unique" bundles): the bundle draws from the listed cards and
    // every collectible card of the class, three different cards the run deck does not hold yet.
    public bool ClassPool;
}

public sealed class RunDefinition
{
    public const string Schema = "solostone.run/1";
    // The classes a run can be played with (CardClass names).
    public static readonly IReadOnlyList<string> KnownClasses = new[] {
        "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR", "DEMONHUNTER", "DEATHKNIGHT" };

    public string Id, Name, Source;
    public int Version;
    public RunClientIds Client;
    public int BaseHealth, HealthPerWin;
    // Fight rules (Dungeon Run: the player always goes first, the boss gets no Coin).
    public bool PlayerFirst, BossCoin;
    public IReadOnlyList<RunRound> Rounds;
    public IReadOnlyList<RunClassStart> Classes;
    public IReadOnlyList<RunBoss> Bosses;
    public IReadOnlyList<RunBucket> Buckets;
    public IReadOnlyList<string> PassiveTreasures, ActiveTreasures;

    public string ResourceName => "SoloStone.Runs." + Id + "-v" + Version + ".json";
    public RunRound Round(int number) => Rounds[number - 1];
    public RunClassStart ClassStart(string cls) => Classes.FirstOrDefault(c => c.Class == cls);
    public RunBoss Boss(string id) => Bosses.FirstOrDefault(b => b.Id == id);
    public RunBucket Bucket(string id) => Buckets.FirstOrDefault(b => b.Id == id);

    // NpcWorldJson.Parser must be installed (plugin: EmbeddedJson.Parse; tests: MiniJSON).
    public static RunDefinition Parse(string text)
    {
        var root = NpcWorldJson.Object(NpcWorldJson.Parse(text));
        if (NpcWorldJson.Text(root, "schema") != Schema) throw new InvalidDataException("Run definition schema must be " + Schema);
        var definition = new RunDefinition {
            Id = Slug(NpcWorldJson.Text(root, "id"), "id"),
            Version = Positive(NpcWorldJson.Integer(root, "version"), "version"),
            Name = NonEmpty(NpcWorldJson.Text(root, "name"), "name"),
            Source = NonEmpty(NpcWorldJson.Text(root, "source"), "source"),
        };
        var client = NpcWorldJson.Object(root.TryGetValue("client", out object clientValue) ? clientValue : null);
        definition.Client = new RunClientIds {
            Adventure = Positive(NpcWorldJson.Integer(client, "adventure"), "client.adventure"),
            Mode = Positive(NpcWorldJson.Integer(client, "mode"), "client.mode"),
            Scenario = Positive(NpcWorldJson.Integer(client, "scenario"), "client.scenario"),
            Wing = Positive(NpcWorldJson.Integer(client, "wing"), "client.wing"),
            ServerKey = Positive(NpcWorldJson.Integer(client, "serverKey"), "client.serverKey"),
            ClientKey = Positive(NpcWorldJson.Integer(client, "clientKey"), "client.clientKey"),
            Board = Positive(NpcWorldJson.Integer(client, "board"), "client.board"),
        };
        var health = NpcWorldJson.Object(root.TryGetValue("playerHealth", out object healthValue) ? healthValue : null);
        definition.BaseHealth = Positive(NpcWorldJson.Integer(health, "base"), "playerHealth.base");
        definition.HealthPerWin = NonNegative(NpcWorldJson.Integer(health, "perWin"), "playerHealth.perWin");
        var fight = NpcWorldJson.Object(root.TryGetValue("fight", out object fightValue) ? fightValue : null);
        definition.PlayerFirst = NpcWorldJson.Boolean(fight, "playerFirst");
        definition.BossCoin = NpcWorldJson.Boolean(fight, "bossCoin");

        var rounds = new List<RunRound>();
        foreach (object item in NpcWorldJson.List(root, "rounds"))
        {
            var row = NpcWorldJson.Object(item);
            int number = NpcWorldJson.Integer(row, "round");
            if (number != rounds.Count + 1) throw new InvalidDataException("rounds must be numbered 1, 2, 3... in order; got " + number);
            rounds.Add(new RunRound {
                Number = number,
                Rewards = NpcWorldJson.Strings(row, "rewards", true).Select(RewardKind).ToList(),
                BossStartingMana = NonNegative(NpcWorldJson.Integer(row, "bossStartingMana", 0), "rounds.bossStartingMana"),
            });
        }
        if (rounds.Count == 0) throw new InvalidDataException("A run needs at least one round.");
        if (rounds[rounds.Count - 1].Rewards.Count != 0) throw new InvalidDataException("The last round ends the run: it offers no rewards.");
        definition.Rounds = rounds;

        var classes = new List<RunClassStart>();
        foreach (object item in NpcWorldJson.List(root, "classes"))
        {
            var row = NpcWorldJson.Object(item);
            string cls = NpcWorldJson.Text(row, "class");
            if (!KnownClasses.Contains(cls)) throw new InvalidDataException("Unknown class: " + cls);
            if (classes.Any(c => c.Class == cls)) throw new InvalidDataException("Duplicate class: " + cls);
            classes.Add(new RunClassStart { Class = cls, Deck = CardIds(NpcWorldJson.Strings(row, "deck", true), "classes." + cls + ".deck", false) });
        }
        if (classes.Count == 0) throw new InvalidDataException("A run needs at least one class.");
        definition.Classes = classes;

        var bosses = new List<RunBoss>();
        foreach (object item in NpcWorldJson.List(root, "bosses"))
        {
            var row = NpcWorldJson.Object(item);
            string id = Slug(NpcWorldJson.Text(row, "id"), "bosses.id");
            if (bosses.Any(b => b.Id == id)) throw new InvalidDataException("Duplicate boss: " + id);
            var bossRounds = NpcWorldJson.List(row, "rounds").Cast<object>().Select(r => RoundNumber(r, rounds.Count, id)).Distinct().ToList();
            if (bossRounds.Count == 0) throw new InvalidDataException(id + ": a boss needs at least one round.");
            string family = NpcWorldJson.Text(row, "family", false);
            bosses.Add(new RunBoss {
                Id = id,
                Family = family == null ? id : Slug(family, id + ".family"),
                Hero = CardId(NpcWorldJson.Text(row, "hero"), id + ".hero"),
                HeroPower = CardId(NpcWorldJson.Text(row, "heroPower"), id + ".heroPower"),
                Source = NonEmpty(NpcWorldJson.Text(row, "source"), id + ".source"),
                Rounds = bossRounds,
                Deck = Counted(NpcWorldJson.List(row, "deck"), id + ".deck"),
                Board = CardIds(NpcWorldJson.Strings(row, "board"), id + ".board", true),
                PlayerHand = CardIds(NpcWorldJson.Strings(row, "playerHand"), id + ".playerHand", true),
            });
        }
        definition.Bosses = bosses;

        var buckets = new List<RunBucket>();
        foreach (object item in NpcWorldJson.List(root, "buckets", false))
        {
            var row = NpcWorldJson.Object(item);
            string id = Slug(NpcWorldJson.Text(row, "id"), "buckets.id");
            if (buckets.Any(b => b.Id == id)) throw new InvalidDataException("Duplicate bucket: " + id);
            string cls = NpcWorldJson.Text(row, "class");
            if (classes.All(c => c.Class != cls)) throw new InvalidDataException(id + ": bucket class is not a run class: " + cls);
            buckets.Add(new RunBucket {
                Id = id, Class = cls,
                Title = CardId(NpcWorldJson.Text(row, "title"), id + ".title"),
                Source = NonEmpty(NpcWorldJson.Text(row, "source"), id + ".source"),
                Cards = CardIds(NpcWorldJson.Strings(row, "cards", true), id + ".cards", false),
                ClassPool = BucketPool(NpcWorldJson.Text(row, "pool", false), id),
            });
        }
        definition.Buckets = buckets;

        var treasures = root.TryGetValue("treasures", out object treasureValue) && treasureValue != null
            ? NpcWorldJson.Object(treasureValue) : new Dictionary<string, object>();
        definition.PassiveTreasures = Unique(CardIds(NpcWorldJson.Strings(treasures, "passive"), "treasures.passive", true), "treasures.passive");
        definition.ActiveTreasures = Unique(CardIds(NpcWorldJson.Strings(treasures, "active"), "treasures.active", true), "treasures.active");
        return definition;
    }

    private static RunRewardKind RewardKind(string text)
    {
        switch (text)
        {
            case "loot": return RunRewardKind.Loot;
            case "passiveTreasure": return RunRewardKind.PassiveTreasure;
            case "activeTreasure": return RunRewardKind.ActiveTreasure;
            default: throw new InvalidDataException("Unknown reward kind: " + text);
        }
    }

    private static int RoundNumber(object value, int rounds, string owner)
    {
        if (!(value is long) && !(value is int)) throw new InvalidDataException(owner + ": rounds must be integers.");
        long number = Convert.ToInt64(value);
        if (number < 1 || number > rounds) throw new InvalidDataException(owner + ": round " + number + " is outside 1.." + rounds);
        return (int)number;
    }

    private static IReadOnlyList<string> Counted(System.Collections.IList rows, string owner)
    {
        var ids = new List<string>();
        foreach (object item in rows)
        {
            var row = NpcWorldJson.Object(item);
            string id = CardId(NpcWorldJson.Text(row, "id"), owner);
            int count = NpcWorldJson.Integer(row, "count");
            if (count < 1 || count > 30) throw new InvalidDataException(owner + ": count of " + id + " must be 1..30");
            if (ids.Contains(id)) throw new InvalidDataException(owner + ": " + id + " is listed twice; use count.");
            for (int i = 0; i < count; i++) ids.Add(id);
        }
        if (ids.Count == 0) throw new InvalidDataException(owner + " is empty.");
        return ids;
    }

    private static IReadOnlyList<string> CardIds(IEnumerable<string> ids, string owner, bool allowEmpty)
    {
        var list = ids.Select(id => CardId(id, owner)).ToList();
        if (!allowEmpty && list.Count == 0) throw new InvalidDataException(owner + " is empty.");
        return list;
    }

    private static IReadOnlyList<string> Unique(IReadOnlyList<string> ids, string owner)
    {
        string twice = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).FirstOrDefault();
        if (twice != null) throw new InvalidDataException(owner + ": " + twice + " is listed twice.");
        return ids;
    }

    private static string CardId(string id, string owner)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new InvalidDataException(owner + ": invalid card id '" + id + "'");
        return id;
    }

    private static string Slug(string id, string owner)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id.Any(c => !(char.IsLower(c) || char.IsDigit(c) || c == '-')))
            throw new InvalidDataException(owner + ": ids are lower-case letters, digits and '-': '" + id + "'");
        return id;
    }

    private static bool BucketPool(string pool, string owner)
    {
        if (pool == null || pool == "listed") return false;
        if (pool == "class") return true;
        throw new InvalidDataException(owner + ".pool must be \"listed\" or \"class\": " + pool);
    }

    private static string NonEmpty(string text, string owner)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException(owner + " is empty.");
        return text;
    }

    private static int Positive(int value, string owner)
    {
        if (value < 1) throw new InvalidDataException(owner + " must be positive.");
        return value;
    }

    private static int NonNegative(int value, string owner)
    {
        if (value < 0) throw new InvalidDataException(owner + " must not be negative.");
        return value;
    }
}
