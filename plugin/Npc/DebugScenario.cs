using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EngineCards = SabberStoneCore.Model.Cards;
#if ENGINE_SOURCE
using EngineGame = SabberStoneCore.Model.Game;
using EngineController = SabberStoneCore.Model.Entities.Controller;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
#endif

// One-shot debug scenario (docs/agent-workflows/steam-deck.md, `deck.py scenario`): stages the NEXT match so a live run can
// show a specific situation (the NPC drawing Illusion first, a human with Fyrakk on turn 1, ...). The file is key=value lines:
//   npcDeckTop=ID,ID   humanDeckTop=ID,...   (new hidden deck entities, drawn in the listed order)
//   humanOpeningHand=ID,... npcOpeningHand=ID,... (replace opening slots before mulligan offers; Coin excluded)
//   npcHand=ID,...     humanHand=ID,...      (new cards added to the hand before turn 1)
//   npcBoard=ID,...    humanBoard=ID,...     (up to seven minions/locations before turn 1)
//   npcBoardStats=ATK,HEALTH  humanBoardStats=ATK,HEALTH   (every minion that side's board key summons gets exactly these
//       Attack (0..999) and Health (1..999, max health too); locations are unaffected. Capture-only, needs the board key.
//       The value REPLACES the base stat (set, not added): auras on that side add on top, and base-stat changes applied during the
//       summon are overwritten.)
//   npcSummonStats=CARDID,ATK,HEALTH   (for the rest of this match every minion with that card id the NPC side summons gets those
//       base stats, Attack 0..9999 and Health 1..9999: a game-local AfterSummon handler, never a card definition. Capture-only.)
//   humanHandMods=CARDID,COST,ATK,HEALTH,PREMIUM,...   (groups of five; '-' = unchanged, spells use -,- for attack and health.
//       Applies to the first card with that id placed by humanHand: Cost 0..99, Attack 0..9999, Health 1..9999, PREMIUM -, golden,
//       diamond or signature. Capture-only; every id must be in humanHand.)
//   humanHandHealth=CARDID,...   (the first card with that id placed by humanHand costs Health instead of Mana this turn, as a Death
//       Knight Herald Soldier's gift does: a payment override, no enchantment. Needs humanSeat=first; every id must be in humanHand.
//       Capture-only.)
//   humanHandHealthWing=CARDID,...   (the same, as Onyxia's Wing's gift does: the Health cost hangs on a CATA_155e2 enchantment.)
//   humanHandClear=1   (discards the cards dealt to the human's hand, Coin kept, before humanHand places its own)
//   humanManaRaw=CUR,MAX  npcManaRaw=CUR,MAX   (0..999: the mana counter's tags written directly after main-begin, past the engine's
//       10 cap, for a counter gag such as 32/333. Needs humanSeat=first; excludes humanMana/npcMana; npcManaRaw CUR <= 10 (its own
//       turn start would otherwise send a negative RESOURCES_USED). Capture-only.)
//   npcMana=N          humanMana=N           (mana crystals on that player's FIRST turn, full)
//   runId=<this launch's run id>   (REQUIRED by Read: the file is bound to one launch, like enable-test-control)
// ENGINE_SOURCE builds only (the Deck and lane builds); other builds ignore the file. A file is honoured only while test
// control is live and its runId is this launch's; anything else is rejected (renamed .rejected-<utc>), and any file left over
// from an earlier launch is deleted at plugin start (ClearStale). The match is flagged on its pending
// record (LocalMatchRecord.DebugScenario) so it never settles: see LocalProfile.Settle. Applied after both mulligans, before
// main-begin. OpeningHand overrides apply before offers; "first draw" remains exact.
public sealed class DebugScenario
{
#if SOLOSTONE_RELEASE
    public static string Path = null;
#else
    public static string Path = @"C:\hs-native-ui-"+@"prototype\reports\debug-scenario";
#endif
    public const int MaxDeckTop = 10, MaxHand = 5, MaxBoard = 7, MaxMana = 10;
    // This launch's run id (set by the plugin at start); Read requires the file's runId to equal it.
    public static string RunId;
    public sealed class Outcome
    {
        public string Status { get; internal set; }
        public string Detail { get; internal set; }
        public string RunId { get; internal set; }
    }
    public static Outcome LastOutcome { get; private set; }
    internal static void RecordOutcome(string status, string detail, string runId) =>
        LastOutcome = new Outcome { Status = status, Detail = detail, RunId = runId };
    private static readonly string[] Keys = { "npcDeckTop", "humanDeckTop", "humanOpeningHand", "npcOpeningHand", "npcHand", "humanHand", "npcBoard", "humanBoard", "npcBoardStats", "humanBoardStats", "npcSummonStats", "humanHandMods", "humanHandHealth", "humanHandHealthWing", "humanHandClear", "humanManaRaw", "npcManaRaw", "npcMana", "humanMana", "humanSkin", "npcSkin", "heroAttack", "heroPower", "heroPowerUpgrade", "attackSecret", "attackArmor", "attackWeapon", "attackLethal", "humanSeat", "overdraw", "runId" };

    public IReadOnlyList<string> NpcDeckTop { get; private set; } = new string[0];
    public IReadOnlyList<string> HumanDeckTop { get; private set; } = new string[0];
    public IReadOnlyList<string> NpcHand { get; private set; } = new string[0];
    public IReadOnlyList<string> HumanHand { get; private set; } = new string[0];
    public IReadOnlyList<string> HumanOpeningHand { get; private set; } = new string[0];
    public IReadOnlyList<string> NpcOpeningHand { get; private set; } = new string[0];
    public string Overdraw { get; private set; }
    public string HumanSkin { get; private set; }
    public string NpcSkin { get; private set; }
    public string HeroAttack { get; private set; }
    public string HeroPower { get; private set; }
    public string HeroPowerUpgrade { get; private set; }
    public string AttackSecret { get; private set; }
    public string AttackWeapon { get; private set; }
    public int AttackArmor { get; private set; }
    public bool AttackLethal { get; private set; }
    public IReadOnlyList<string> NpcBoard { get; private set; } = new string[0];
    public IReadOnlyList<string> HumanBoard { get; private set; } = new string[0];
    // Uniform per side: the stats every minion of that board key gets (MaxStat bounds both numbers).
    public sealed class BoardStats
    {
        public int Attack { get; }
        public int Health { get; }
        public BoardStats(int attack, int health) { Attack = attack; Health = health; }
        public override string ToString() => Attack.ToString(CultureInfo.InvariantCulture) + "," + Health.ToString(CultureInfo.InvariantCulture);
    }
    public const int MaxStat = 999, MaxSummonStat = 9999;
    public sealed class SummonStats
    {
        public string CardId { get; }
        public int Attack { get; }
        public int Health { get; }
        public SummonStats(string cardId, int attack, int health) { CardId = cardId; Attack = attack; Health = health; }
        public override string ToString() => CardId + "," + Attack.ToString(CultureInfo.InvariantCulture) + "," + Health.ToString(CultureInfo.InvariantCulture);
    }
    public SummonStats NpcSummonStats { get; private set; }
    // One gag card in the human's hand: null = unchanged.
    public sealed class HandMod
    {
        public string CardId { get; }
        public int? Cost { get; }
        public int? Attack { get; }
        public int? Health { get; }
        public int Premium { get; }   // native TAG_PREMIUM: 0 unchanged, 1 Golden, 2 Diamond, 3 Signature
        public static readonly string[] PremiumNames = { "-", "golden", "diamond", "signature" };
        public HandMod(string cardId, int? cost, int? attack, int? health, int premium)
        { CardId = cardId; Cost = cost; Attack = attack; Health = health; Premium = premium; }
        private static string Part(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";
        public override string ToString() => CardId + "," + Part(Cost) + "," + Part(Attack) + "," + Part(Health) + "," + PremiumNames[Premium];
    }
    public const int MaxHandCost = 99;
    public sealed class ManaRaw
    {
        public int Current { get; }
        public int Max { get; }
        public ManaRaw(int current, int max) { Current = current; Max = max; }
        public override string ToString() => Current.ToString(CultureInfo.InvariantCulture) + "," + Max.ToString(CultureInfo.InvariantCulture);
    }
    public const int MaxRawMana = 999;
    public ManaRaw HumanManaRaw { get; private set; }
    public ManaRaw NpcManaRaw { get; private set; }
    public bool HumanHandClear { get; private set; }
    public IReadOnlyList<HandMod> HumanHandMods { get; private set; } = new HandMod[0];
    public IReadOnlyList<string> HumanHandHealth { get; private set; } = new string[0];
    public IReadOnlyList<string> HumanHandHealthWing { get; private set; } = new string[0];
    // Filled by Apply: hand entity id -> REQUESTED premium (1..3; the host resolves it against the card) for the host to register as owned presentation (NativeMatch.entityPremiums),
    // because the native client only renders Diamond/Signature through the host's quality flags, never from a raw engine tag.
    public IDictionary<int, int> StagedPremiums { get; } = new Dictionary<int, int>();
    public BoardStats NpcBoardStats { get; private set; }
    public BoardStats HumanBoardStats { get; private set; }
    public int? NpcMana { get; private set; }
    public int? HumanMana { get; private set; }
    public int? FirstPlayer { get; private set; }
    private static int? Seat(Dictionary<string, string> values)
    {
        if (!values.TryGetValue("humanSeat", out string seat)) return null;
        if (seat == "first") return 1;
        if (seat == "second") return 2;
        throw new ArgumentException("humanSeat must be first or second.");
    }
    // The launch the file names (not part of Canonical: a record's replay does not need it).
    public string BoundRunId { get; private set; }
    // The file this scenario was read from (null for Parse); Consume retires it.
    public string SourcePath { get; private set; }

    // known: extra id check, e.g. the native client's own card definitions. Throws ArgumentException on the first problem;
    // nothing is partially accepted.
    public static DebugScenario Parse(string text, Func<string, bool> known = null)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string row in (text ?? "").Split('\n'))
        {
            string line = row.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int split = line.IndexOf('=');
            if (split <= 0 || split == line.Length - 1) throw new ArgumentException("Expected key=value, got: " + line);
            string key = line.Substring(0, split).Trim(), value = line.Substring(split + 1).Trim();
            if (Array.IndexOf(Keys, key) < 0) throw new ArgumentException("Unknown key: " + key + " (known: " + string.Join(", ", Keys) + ")");
            if (values.ContainsKey(key)) throw new ArgumentException("Duplicate key: " + key);
            values.Add(key, value);
        }
        if (values.Count == 0) throw new ArgumentException("Empty scenario.");
        string boundRun = null;
        if (values.TryGetValue("runId", out string run))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(run, "^[0-9A-Fa-f-]{8,64}$")) throw new ArgumentException("runId is not a run id.");
            boundRun = run;
            values.Remove("runId");
            if (values.Count == 0) throw new ArgumentException("Empty scenario.");
        }
        if (values.TryGetValue("humanHandClear", out string clear) && clear == "1" && values.ContainsKey("humanOpeningHand"))
            throw new ArgumentException("humanHandClear=1 would discard the cards humanOpeningHand places; give only one.");
        var humanHand = Cards(values, "humanHand", MaxHand, known);
        var handHealth = HandHealth(values, "humanHandHealth", humanHand);
        var handHealthWing = HandHealth(values, "humanHandHealthWing", humanHand);
        if (handHealth.Intersect(handHealthWing).Any())
            throw new ArgumentException("humanHandHealth and humanHandHealthWing list the same card: give each card one Health cost.");
        return new DebugScenario {
            BoundRunId = boundRun, FirstPlayer = Seat(values),
            Overdraw = Choice(values, "overdraw", "human", "npc", "human-godfrey", "npc-godfrey"),
            HumanSkin = Skin(values, "humanSkin", known), NpcSkin = Skin(values, "npcSkin", known),
            HeroAttack = Choice(values, "heroAttack", "human", "npc"),
            HeroPower = Choice(values, "heroPower", "human", "npc"),
            HeroPowerUpgrade = Choice(values, "heroPowerUpgrade", "human", "npc", "both"),
            AttackSecret = Choice(values, "attackSecret", "none", "CORE_EX1_289", "CORE_EX1_287", "EX1_610", "EX1_594"),
            AttackWeapon = Choice(values, "attackWeapon", "none", "TLC_478", "CS2_106"),
            AttackArmor = Number(values, "attackArmor", 0, 30), AttackLethal = Number(values, "attackLethal", 0, 1) == 1,
            NpcDeckTop = Cards(values, "npcDeckTop", MaxDeckTop, known), HumanDeckTop = Cards(values, "humanDeckTop", MaxDeckTop, known),
            HumanOpeningHand = Cards(values, "humanOpeningHand", 4, known), NpcOpeningHand = Cards(values, "npcOpeningHand", 4, known),
            NpcHand = Cards(values, "npcHand", MaxHand, known), HumanHand = humanHand,
            HumanHandMods = HandMods(values, "humanHandMods", humanHand, known),
            HumanHandHealth = handHealth, HumanHandHealthWing = handHealthWing,
            HumanHandClear = Number(values, "humanHandClear", 0, 1) == 1,
            HumanManaRaw = ManaRawValue(values, "humanManaRaw", "humanMana"), NpcManaRaw = ManaRawValue(values, "npcManaRaw", "npcMana"),
            NpcBoard = Cards(values, "npcBoard", MaxBoard, known), HumanBoard = Cards(values, "humanBoard", MaxBoard, known),
            NpcBoardStats = Stats(values, "npcBoardStats", "npcBoard"), HumanBoardStats = Stats(values, "humanBoardStats", "humanBoard"),
            NpcSummonStats = SummonRule(values, "npcSummonStats", known),
            NpcMana = Mana(values, "npcMana"), HumanMana = Mana(values, "humanMana") };
    }

    // ATTACK,HEALTH for the minions a board key summons. Digits only, so no sign, space-inside, decimal or exponent forms.
    private static BoardStats Stats(Dictionary<string, string> values, string key, string boardKey)
    {
        if (!values.TryGetValue(key, out string text)) return null;
        if (!values.ContainsKey(boardKey)) throw new ArgumentException(key + " needs " + boardKey + " (it sets the stats of the minions that key summons).");
        var parts = text.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length != 2) throw new ArgumentException(key + " must be ATTACK,HEALTH, e.g. 999,999 (got '" + text + "').");
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int attack) || parts[0].Length > 3 || attack > MaxStat)
            throw new ArgumentException(key + ": attack must be a whole number 0.." + MaxStat + " (got '" + parts[0] + "').");
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int health) || parts[1].Length > 3 || health < 1 || health > MaxStat)
            throw new ArgumentException(key + ": health must be a whole number 1.." + MaxStat + " (got '" + parts[1] + "').");
        return new BoardStats(attack, health);
    }

    // CUR,MAX written straight to the player's mana tags (0..999, CUR <= MAX). Both seats need the human to play first: the NPC's
    // own turn start would refill its tags, and a human second turn would refill ours.
    private static ManaRaw ManaRawValue(Dictionary<string, string> values, string key, string plainKey)
    {
        if (!values.TryGetValue(key, out string text)) return null;
        if (values.ContainsKey(plainKey)) throw new ArgumentException(key + " replaces " + plainKey + "; give only one.");
        if (!values.TryGetValue("humanSeat", out string seat) || seat != "first")
            throw new ArgumentException(key + " needs humanSeat=first (a later turn start would refill the counter).");
        var parts = text.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length != 2) throw new ArgumentException(key + " must be CUR,MAX, e.g. 32,333 (got '" + text + "').");
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int current) || parts[0].Length > 3 || current > MaxRawMana)
            throw new ArgumentException(key + ": current must be a whole number 0.." + MaxRawMana + " (got '" + parts[0] + "').");
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int max) || parts[1].Length > 3 || max > MaxRawMana)
            throw new ArgumentException(key + ": max must be a whole number 0.." + MaxRawMana + " (got '" + parts[1] + "').");
        if (current > max) throw new ArgumentException(key + ": current " + current + " is above max " + max + ".");
        // The NPC's own turn start refills through ChangeManaCrystal, which adds (10 - MAX) to RESOURCES_USED: with CUR above 10 that
        // sends the client a negative RESOURCES_USED. Our own first turn is already past, so humanManaRaw may go higher.
        if (key == "npcManaRaw" && current > 10)
            throw new ArgumentException(key + ": current must be 0..10 (the NPC's turn-start refill would send a negative RESOURCES_USED).");
        return new ManaRaw(current, max);
    }

    // Card ids placed by humanHand that cost Health this turn. The override expires after the current turn, so only a human who plays
    // first can use it.
    private static IReadOnlyList<string> HandHealth(Dictionary<string, string> values, string key, IReadOnlyList<string> hand)
    {
        if (!values.TryGetValue(key, out string text)) return new string[0];
        if (!values.TryGetValue("humanSeat", out string seat) || seat != "first")
            throw new ArgumentException(key + " needs humanSeat=first (the Health cost lasts only the current turn).");
        var ids = text.Split(',').Select(id => id.Trim()).ToArray();
        foreach (string id in ids)
        {
            if (ids.Count(other => other == id) > 1) throw new ArgumentException(key + ": " + id + " is listed twice (it applies to the first such card).");
            if (!hand.Contains(id)) throw new ArgumentException(key + ": " + id + " is not in humanHand (it applies to cards humanHand places).");
        }
        return ids;
    }

    // CARDID,COST,ATK,HEALTH groups for cards placed by humanHand ('-' keeps the printed value).
    private static IReadOnlyList<HandMod> HandMods(Dictionary<string, string> values, string key, IReadOnlyList<string> hand, Func<string, bool> known)
    {
        if (!values.TryGetValue(key, out string text)) return new HandMod[0];
        var parts = text.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length % 5 != 0)
            throw new ArgumentException(key + " takes groups of CARDID,COST,ATTACK,HEALTH,PREMIUM (use - for unchanged); got " + parts.Length + " values.");
        if (parts.Length / 5 > MaxHand) throw new ArgumentException(key + " takes at most " + MaxHand + " cards.");
        int? Number(string part, string what, int min, int max)
        {
            if (part == "-") return null;
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || part.Length > 4 || n < min || n > max)
                throw new ArgumentException(key + ": " + what + " must be - or a whole number " + min + ".." + max + " (got '" + part + "').");
            return n;
        }
        var mods = new List<HandMod>();
        for (int i = 0; i < parts.Length; i += 5)
        {
            string id = parts[i];
            if (mods.Any(m => m.CardId == id)) throw new ArgumentException(key + ": " + id + " is listed twice (a mod applies to the first such card).");
            if (!hand.Contains(id)) throw new ArgumentException(key + ": " + id + " is not in humanHand (mods apply to cards humanHand places).");
            SabberStoneCore.Model.Card card;
            try { card = EngineCards.FromId(id); }
            catch (KeyNotFoundException) { card = null; }
            if (card == null) throw new ArgumentException(key + ": unknown card id " + id);
            if (known != null && !known(id)) throw new ArgumentException(key + ": " + id + " has no native client definition.");
            int? cost = Number(parts[i + 1], id + " cost", 0, MaxHandCost);
            int? attack = Number(parts[i + 2], id + " attack", 0, MaxSummonStat), health = Number(parts[i + 3], id + " health", 1, MaxSummonStat);
            int premium = Array.IndexOf(HandMod.PremiumNames, parts[i + 4]);
            if (premium < 0) throw new ArgumentException(key + ": " + id + " premium must be - , golden, diamond or signature (got '" + parts[i + 4] + "').");
            if ((attack.HasValue || health.HasValue) && card.Type.ToString() != "MINION")
                throw new ArgumentException(key + ": " + id + " is not a minion, so its attack and health must be - and -.");
            if (!cost.HasValue && !attack.HasValue && !health.HasValue && premium == 0) throw new ArgumentException(key + ": " + id + " changes nothing.");
            mods.Add(new HandMod(id, cost, attack, health, premium));
        }
        return mods;
    }

    // CARDID,ATK,HEALTH for every minion of that card the NPC side summons during the match (up to 4 digits each).
    private static SummonStats SummonRule(Dictionary<string, string> values, string key, Func<string, bool> known)
    {
        if (!values.TryGetValue(key, out string text)) return null;
        var parts = text.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length != 3) throw new ArgumentException(key + " must be CARDID,ATTACK,HEALTH, e.g. DINO_410t,9999,9999 (got '" + text + "').");
        string id = parts[0];
        if (id.Length == 0 || id.Length > 64 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
            throw new ArgumentException(key + " has an invalid card id: '" + id + "'");
        SabberStoneCore.Model.Card card;
        try { card = EngineCards.FromId(id); }
        catch (KeyNotFoundException) { card = null; }
        if (card == null) throw new ArgumentException(key + ": unknown card id " + id);
        if (card.Type.ToString() != "MINION") throw new ArgumentException(key + ": " + id + " is not a minion.");
        if (known != null && !known(id)) throw new ArgumentException(key + ": " + id + " has no native client definition.");
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int attack) || parts[1].Length > 4 || attack > MaxSummonStat)
            throw new ArgumentException(key + ": attack must be a whole number 0.." + MaxSummonStat + " (got '" + parts[1] + "').");
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int health) || parts[2].Length > 4 || health < 1 || health > MaxSummonStat)
            throw new ArgumentException(key + ": health must be a whole number 1.." + MaxSummonStat + " (got '" + parts[2] + "').");
        return new SummonStats(id, attack, health);
    }

    private static string Skin(Dictionary<string, string> values, string key, Func<string, bool> known)
    {
        if (!values.TryGetValue(key, out string id)) return null;
        if (!HeroSkinData.All.ContainsKey(id) || known != null && !known(id)) throw new ArgumentException(key + ": unknown hero skin " + id);
        return id;
    }
    private static string Choice(Dictionary<string, string> values, string key, params string[] allowed)
    {
        if (!values.TryGetValue(key, out string value)) return null;
        if (!allowed.Contains(value)) throw new ArgumentException(key + ": expected " + string.Join(", ", allowed));
        return value;
    }
    private static int Number(Dictionary<string, string> values, string key, int min, int max)
    {
        if (!values.TryGetValue(key, out string text)) return 0;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < min || n > max)
            throw new ArgumentException(key + ": out of range");
        return n;
    }

    private static IReadOnlyList<string> Cards(Dictionary<string, string> values, string key, int maximum, Func<string, bool> known)
    {
        if (!values.TryGetValue(key, out string text)) return new string[0];
        var ids = text.Split(',').Select(id => id.Trim()).ToArray();
        if (ids.Length > maximum) throw new ArgumentException(key + " takes at most " + maximum + " cards.");
        foreach (string id in ids)
        {
            if (id.Length == 0 || id.Length > 64 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
                throw new ArgumentException(key + " has an invalid card id: '" + id + "'");
            SabberStoneCore.Model.Card card;
            try { card = EngineCards.FromId(id); }
            catch (KeyNotFoundException) { card = null; }
            if (card == null) throw new ArgumentException(key + ": unknown card id " + id);
            if (key.EndsWith("OpeningHand", StringComparison.Ordinal) && id == "GAME_005")
                throw new ArgumentException(key + ": the starting Coin cannot replace an opening card.");
            string type = card.Type.ToString();
            // Hero cards (Jaraxxus, Deathwing...) are played from hand; base hero portraits (cost 0) are not.
            bool heroCard = type == "HERO" && card.Collectible && card.Cost > 0;
            if (type != "MINION" && type != "SPELL" && type != "WEAPON" && type != "LOCATION" && !heroCard)
                throw new ArgumentException(key + ": " + id + " is a " + type + ", not a playable card.");
            if (key.EndsWith("Board", StringComparison.Ordinal) && type != "MINION" && type != "LOCATION")
                throw new ArgumentException(key + ": " + id + " is not a board minion or location.");
            if (known != null && !known(id)) throw new ArgumentException(key + ": " + id + " has no native client definition.");
        }
        return ids;
    }

    private static int? Mana(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string text)) return null;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int mana) && mana >= 1 && mana <= MaxMana) return mana;
        throw new ArgumentException(key + " must be 1.." + MaxMana + ".");
    }

    // Stable text: the same scenario always prints, records and parses back identically (Replay re-parses it).
    public string Canonical()
    {
        var lines = new List<string>();
        void Add(string key, IReadOnlyList<string> ids) { if (ids.Count > 0) lines.Add(key + "=" + string.Join(",", ids)); }
        Add("npcDeckTop", NpcDeckTop); Add("humanDeckTop", HumanDeckTop); Add("npcHand", NpcHand); Add("humanHand", HumanHand);
        Add("humanOpeningHand", HumanOpeningHand); Add("npcOpeningHand", NpcOpeningHand);
        Add("npcBoard", NpcBoard); Add("humanBoard", HumanBoard);
        if (NpcBoardStats != null) lines.Add("npcBoardStats=" + NpcBoardStats);
        if (HumanBoardStats != null) lines.Add("humanBoardStats=" + HumanBoardStats);
        if (NpcSummonStats != null) lines.Add("npcSummonStats=" + NpcSummonStats);
        if (HumanHandMods.Count > 0) lines.Add("humanHandMods=" + string.Join(",", HumanHandMods));
        Add("humanHandHealth", HumanHandHealth); Add("humanHandHealthWing", HumanHandHealthWing);
        if (HumanHandClear) lines.Add("humanHandClear=1");
        if (HumanManaRaw != null) lines.Add("humanManaRaw=" + HumanManaRaw);
        if (NpcManaRaw != null) lines.Add("npcManaRaw=" + NpcManaRaw);
        if (NpcMana.HasValue) lines.Add("npcMana=" + NpcMana.Value.ToString(CultureInfo.InvariantCulture));
        if (HumanMana.HasValue) lines.Add("humanMana=" + HumanMana.Value.ToString(CultureInfo.InvariantCulture));
        if (FirstPlayer.HasValue) lines.Add("humanSeat=" + (FirstPlayer == 1 ? "first" : "second"));
        if (Overdraw != null) lines.Add("overdraw=" + Overdraw);
        if (HumanSkin != null) lines.Add("humanSkin=" + HumanSkin);
        if (NpcSkin != null) lines.Add("npcSkin=" + NpcSkin);
        if (HeroAttack != null) lines.Add("heroAttack=" + HeroAttack);
        if (HeroPower != null) lines.Add("heroPower=" + HeroPower);
        if (HeroPowerUpgrade != null) lines.Add("heroPowerUpgrade=" + HeroPowerUpgrade);
        if (AttackSecret != null) lines.Add("attackSecret=" + AttackSecret);
        if (AttackWeapon != null) lines.Add("attackWeapon=" + AttackWeapon);
        if (AttackArmor != 0) lines.Add("attackArmor=" + AttackArmor);
        if (AttackLethal) lines.Add("attackLethal=1");
        return string.Join("\n", lines);
    }
    public string Describe() => Canonical().Replace("\n", " ");

    // Builds that cannot stage it (no engine source) never read the file, so a stray file there stays inert.
    // runId: this launch's run id; testControlLive: the test-control endpoint is up. A file for another launch (or none), or
    // read while test control is off, is rejected whole and retired. Rejected content bound to this live launch throws:
    // callers must refuse before BeginLaunch, never fall through into an ordinary match.
    public static DebugScenario Read(string path, Action<string> log, Func<string, bool> known, string runId, bool testControlLive)
    {
#if ENGINE_SOURCE
        path = path ?? Path;
        // A new stage must never report a previous match's applied scenario in this same launch.
        RecordOutcome("absent", "No scenario file for this match", runId);
        bool bound = false;
        string status = "unreadable";
        try
        {
            if (!File.Exists(path)) return null;
            var text = new System.Text.StringBuilder();
            using (var reader = File.OpenText(path))
            {
                string row;
                while ((row = reader.ReadLine()) != null)
                {
                    text.AppendLine(row);
                    int split = row.IndexOf('=');
                    // Establish intent before parsing: malformed content must not turn a test into a real match.
                    if (testControlLive && !string.IsNullOrEmpty(runId) && split > 0
                        && row.Substring(0, split).Trim() == "runId"
                        && string.Equals(row.Substring(split + 1).Trim(), runId, StringComparison.OrdinalIgnoreCase))
                        bound = true;
                }
            }
            status = "rejected";
            var scenario = Parse(text.ToString(), known);
            if (!testControlLive) throw new InvalidOperationException("test control is not live (deck.py control on first)");
            if (string.IsNullOrEmpty(runId)) throw new InvalidOperationException("this launch has no run id");
            if (scenario.BoundRunId == null) throw new InvalidOperationException("the file has no runId line (not bound to a launch)");
            if (!string.Equals(scenario.BoundRunId, runId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("the file's runId belongs to another launch");
            scenario.SourcePath = path;
            RecordOutcome("armed", scenario.Describe(), runId);
            log?.Invoke("DEBUG_SCENARIO armed " + scenario.Describe());
            return scenario;
        }
        catch (Exception error)
        {
            RecordOutcome(status, error.Message, runId);
            log?.Invoke("DEBUG_SCENARIO " + status + ", nothing applied: " + error.Message);
            Retire(path, "rejected", log);
            if (bound) throw new InvalidOperationException("Debug scenario refused: " + error.Message, error);
            return null;
        }
#else
        return null;
#endif
    }

    // Plugin start: no scenario file survives into a new launch (it can only name an earlier run id at best).
    public static void ClearStale(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch (Exception) { }
    }

    // Renames the file away so it can never apply to a second match. Throws when it cannot (the caller refuses the start).
    public void Consume(Action<string> log = null)
    {
        if (SourcePath == null) return;
        string target = RetiredName(SourcePath, "applied");
        File.Move(SourcePath, target);
        log?.Invoke("DEBUG_SCENARIO consumed -> " + System.IO.Path.GetFileName(target));
        SourcePath = null;
    }

    private static string RetiredName(string path, string kind) =>
        path + "." + kind + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);

    private static void Retire(string path, string kind, Action<string> log)
    {
        try { File.Move(path, RetiredName(path, kind)); }
        catch (Exception error)
        {
            try { File.Delete(path); }
            catch (Exception) { log?.Invoke("DEBUG_SCENARIO file could not be retired: " + error.Message); }
        }
    }

#if ENGINE_SOURCE
    // Everything Apply needs, checked without touching the game: room for the new deck/hand cards (a hand card also leaves
    // space for that player's next draw, so it is not burned). Returns null when it fits, else the reason.
    public string Validate(EngineGame game)
    {
        string Fit(EngineController player, string name, int top, int hand)
        {
            if (top > 0 && player.DeckZone.FreeSpace < top) return name + " deck has no room for " + top + " more cards.";
            if (hand > 0 && player.HandZone.FreeSpace < hand + 1) return name + " hand has no room for " + hand + " more cards plus its draw.";
            return null;
        }
        foreach (string id in NpcDeckTop.Concat(HumanDeckTop).Concat(NpcHand).Concat(HumanHand).Concat(HumanOpeningHand).Concat(NpcOpeningHand).Concat(NpcBoard).Concat(HumanBoard))
            if (EngineCards.FromId(id) == null) return "unknown card " + id;
        if (HumanOpeningHand.Count > game.Player1.HandZone.Count(c => c.Card.Id != "GAME_005") ||
            NpcOpeningHand.Count > game.Player2.HandZone.Count(c => c.Card.Id != "GAME_005")) return "Opening hand has too few replaceable slots.";
        if (HumanSkin != null && HeroSkinData.All[HumanSkin].BaseHero != game.Player1.Hero.Card.Id) return "humanSkin class mismatch";
        if (NpcSkin != null && HeroSkinData.All[NpcSkin].BaseHero != game.Player2.Hero.Card.Id) return "npcSkin class mismatch";
        if (game.Player2.BoardZone.FreeSpace < NpcBoard.Count || game.Player1.BoardZone.FreeSpace < HumanBoard.Count) return "Board has no room for scenario bodies.";
        return Fit(game.Player2, "NPC", NpcDeckTop.Count, NpcHand.Count) ?? Fit(game.Player1, "Human", HumanDeckTop.Count, HumanHand.Count);
    }

    // After the normal deal, before either offer is sent/answered. Transform real dealt entities, never the Coin.
    public void ApplyOpening(EngineGame game)
    {
        if (HumanOpeningHand.Count == 0 && NpcOpeningHand.Count == 0) return;
        string problem = Validate(game);
        if (problem != null) throw new InvalidOperationException(problem);
        void Replace(EngineController player, IReadOnlyList<string> ids)
        {
            var slots = player.HandZone.Where(c => c.Card.Id != "GAME_005").Take(ids.Count).ToArray();
            for (int i = 0; i < ids.Count; i++)
                SabberStoneCore.Actions.Generic.ChangeEntityBlock(player, slots[i], EngineCards.FromId(ids[i]), false);
        }
        Replace(game.Player1, HumanOpeningHand); Replace(game.Player2, NpcOpeningHand);
    }

    // After main-begin (the first turn's refill and draw are done): the raw mana counters. Returns the log summary or null.
    public string ApplyAfterMainBegin(EngineGame game)
    {
        var applied = new List<string>();
        void Raw(EngineController player, ManaRaw raw, string name)
        {
            if (raw == null) return;
            // The Controller's own tags (RESOURCES, RESOURCES_USED, TEMP_RESOURCES), the ones the client's counter reads; only the
            // turn-start refill caps at 10, so a direct write holds for this turn.
            player.BaseMana = raw.Max; player.UsedMana = raw.Max - raw.Current; player.TemporaryMana = 0;
            applied.Add(name + "=" + raw);
        }
        Raw(game.Player1, HumanManaRaw, "humanManaRaw"); Raw(game.Player2, NpcManaRaw, "npcManaRaw");
        return applied.Count == 0 ? null : string.Join("; ", applied);
    }

    // Game is paused at BEGIN_MULLIGAN with both mulligans DONE; main-begin has not run. Returns the log summary.
    // Deterministic given the game, so Replay reproduces it exactly (it calls this at the same point).
    public string Apply(EngineGame game)
    {
        string problem = Validate(game);
        if (problem != null) throw new InvalidOperationException(problem);
        var applied = new List<string>();
        // Last in the zone = next drawn, so the listed order is added in reverse.
        void Top(EngineController player, IReadOnlyList<string> ids, string name)
        {
            if (ids.Count == 0) return;
            for (int i = ids.Count - 1; i >= 0; i--) EngineEntity.FromCard(player, EngineCards.FromId(ids[i]), zone: player.DeckZone);
            applied.Add(name + "=" + string.Join(",", ids) + " (deck " + player.DeckZone.Count + ")");
        }
        var humanPlaced = new List<SabberStoneCore.Model.Entities.IPlayable>();
        void Hand(EngineController player, IReadOnlyList<string> ids, string name)
        {
            if (ids.Count == 0) return;
            // Retail finds the human's opening Coin by its final hand position during the mulligan animation.
            var coin = player == game.Player1 && game.FirstPlayer != player
                ? player.HandZone.FirstOrDefault(card => card.Card.Id == "GAME_005") : null;
            foreach (string id in ids) {
                var card = EngineEntity.FromCard(player, EngineCards.FromId(id), zone: player.HandZone);
                if (player == game.Player1) humanPlaced.Add(card);
                if (coin != null) player.HandZone.Swap(coin, card);
            }
            applied.Add(name + "=" + string.Join(",", ids) + " (hand " + player.HandZone.Count + ")");
        }
        void Board(EngineController player, IReadOnlyList<string> ids, string name, BoardStats stats)
        {
            foreach (string id in ids)
            {
                var body = EngineEntity.FromCard(player, EngineCards.FromId(id));
                if (body is SabberStoneCore.Model.Entities.Minion minion)
                {
                    SabberStoneCore.Actions.Generic.SummonBlock(game, minion, -1, null);
                    // Same tag-write path as heroAttack's hero ATK: the Character setters set the printed value and max
                    // health (no damage) and record a TagChange, which the native transport sends in the same history packet
                    // as the summon, before main-begin. In play already, so it is an ordinary public stat change for both seats.
                    if (stats != null)
                    {
                        minion[SabberStoneCore.Enums.GameTag.ATK] = stats.Attack;
                        minion[SabberStoneCore.Enums.GameTag.HEALTH] = stats.Health;
                    }
                }
                else player.BoardZone.Add((SabberStoneCore.Model.Entities.BoardObject)body);
            }
            if (ids.Count > 0) applied.Add(name + "=" + string.Join(",", ids) + (stats == null ? "" : " stats=" + stats));
        }
        void Mana(EngineController player, int? mana, string name)
        {
            if (!mana.HasValue) return;
            // The first turn's own +1 crystal brings it to the asked amount, with all of it available.
            player.BaseMana = mana.Value - 1;
            applied.Add(name + "=" + mana.Value);
        }
        if (HumanHandClear)
        {
            // Dealt cards leave the hand (Coin stays); the entities sit in SETASIDE like any other removed card.
            var dealt = game.Player1.HandZone.Where(card => card.Card.Id != "GAME_005").ToArray();
            foreach (var card in dealt) game.Player1.SetasideZone.Add(game.Player1.HandZone.Remove(card));
            applied.Add("humanHandClear=" + dealt.Length);
        }
        Top(game.Player2, NpcDeckTop, "npcDeckTop"); Top(game.Player1, HumanDeckTop, "humanDeckTop");
        Hand(game.Player2, NpcHand, "npcHand"); Hand(game.Player1, HumanHand, "humanHand");
        foreach (var mod in HumanHandMods)
        {
            // The first card placed by humanHand with this id. Cost goes through the persistent SET path (the Cost setter: it
            // replaces the printed cost, is remembered against later cost recalculation and records a COST TagChange); stats go
            // through the Character setters like the board stats.
            var card = humanPlaced.First(placed => placed.Card.Id == mod.CardId);
            if (mod.Cost.HasValue) card.Cost = mod.Cost.Value;
            if (card is SabberStoneCore.Model.Entities.Minion handMinion)
            {
                if (mod.Attack.HasValue) handMinion[SabberStoneCore.Enums.GameTag.ATK] = mod.Attack.Value;
                if (mod.Health.HasValue) handMinion[SabberStoneCore.Enums.GameTag.HEALTH] = mod.Health.Value;
            }
            if (mod.Premium != 0) StagedPremiums[card.Id] = mod.Premium;
            applied.Add("humanHandMods=" + mod);
        }
        foreach (var (id, wing) in HumanHandHealth.Select(id => (id, false)).Concat(HumanHandHealthWing.Select(id => (id, true))))
        {
            // Parse keeps each id under one key, once.
            var card = (SabberStoneCore.Model.Entities.Playable)humanPlaced.First(placed => placed.Card.Id == id);
            int actor = wing ? SabberStoneCore.Model.Entities.Enchantment.GetInstance(game.Player1, game.Player1.Hero, card,
                EngineCards.FromId("CATA_155e2")).Id : 0;
            SabberStoneCore.Actions.CostRules.AddPaymentOverride(card, SabberStoneCore.Model.Costs.CostPaymentKind.Health, game.Turn, actor);
            applied.Add((wing ? "humanHandHealthWing=" : "humanHandHealth=") + id);
        }
        if (Overdraw != null)
        {
            var player = Overdraw.StartsWith("npc", StringComparison.Ordinal) ? game.Player2 : game.Player1;
            while (!player.HandZone.IsFull)
                EngineEntity.FromCard(player, EngineCards.FromId("CS2_182"), zone: player.HandZone);
            if (Overdraw.EndsWith("-godfrey", StringComparison.Ordinal))
            {
                if (player.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_509e") != true)
                    SabberStoneCore.Actions.QuestRules.AddPlayerEffect(player, player.Hero, "JAIL_509e");
                player.Hero[(SabberStoneCore.Enums.GameTag)4717] = 1;
            }
            applied.Add("overdraw=" + Overdraw);
        }
        Board(game.Player2, NpcBoard, "npcBoard", NpcBoardStats); Board(game.Player1, HumanBoard, "humanBoard", HumanBoardStats);
        if (NpcSummonStats != null)
        {
            // Registered AFTER the staged boards, so it only affects later summons (never re-stats the boards above).
            // Game-local: a plain handler on this game's AfterSummon channel (fires right after a minion enters play, inside the
            // summoning block). The engine does not copy plugin handlers into search clones, and card definitions stay untouched.
            var rule = NpcSummonStats;
            game.TriggerManager.AfterSummonTrigger += sender =>
            {
                if (sender is SabberStoneCore.Model.Entities.Minion hatched && hatched.Card.Id == rule.CardId
                    && hatched.Controller == game.Player2 && hatched.Zone == hatched.Controller.BoardZone)
                {
                    hatched[SabberStoneCore.Enums.GameTag.ATK] = rule.Attack;
                    hatched[SabberStoneCore.Enums.GameTag.HEALTH] = rule.Health;
                }
            };
            applied.Add("npcSummonStats=" + rule);
        }
        Mana(game.Player2, NpcMana, "npcMana"); Mana(game.Player1, HumanMana, "humanMana");
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            string seat = player == game.Player1 ? "human" : "npc";
            if (HeroPower == seat && !(seat == "human" ? HumanMana : NpcMana).HasValue) player.BaseMana = 9;
            if (HeroPowerUpgrade == seat || HeroPowerUpgrade == "both")
            {
                int upgrade = player.Hero.HeroPower.Card[(SabberStoneCore.Enums.GameTag)1086];
                if (upgrade != 0)
                    new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(EngineCards.FromAssetId(upgrade))
                        .Process(game, player, player.Hero, null);
                applied.Add("heroPowerUpgrade=" + seat + ":" + player.Hero.HeroPower.Card.Id);
            }
        }
        if (HeroPower != null) applied.Add("heroPower=" + HeroPower);
        if (HeroAttack != null)
        {
            var attacker = HeroAttack == "human" ? game.Player1 : game.Player2;
            var defender = attacker.Opponent;
            if (AttackWeapon != null && AttackWeapon != "none")
            {
                var weapon = (SabberStoneCore.Model.Entities.Weapon)EngineEntity.FromCard(attacker, EngineCards.FromId(AttackWeapon));
                attacker.Hero.AddWeapon(weapon);
                weapon.Card.Power?.Trigger?.Activate(weapon);
                weapon.Card.Power?.Aura?.Activate(weapon);
            }
            else attacker.Hero[SabberStoneCore.Enums.GameTag.ATK] = 3;
            defender.Hero.Armor = AttackArmor;
            if (AttackLethal) defender.Hero.Damage = defender.Hero.BaseHealth - 1;
            if (AttackSecret != null && AttackSecret != "none")
            {
                var secret = EngineEntity.FromCard(defender, EngineCards.FromId(AttackSecret), zone: defender.SecretZone);
                secret.Card.Power?.Trigger?.Activate(secret);
            }
            applied.Add("heroAttack=" + HeroAttack + "; weapon=" + (AttackWeapon ?? "none")
                + "; secret=" + (AttackSecret ?? "none") + "; armor=" + AttackArmor + "; lethal=" + AttackLethal);
        }
        if (HumanSkin != null) applied.Add("humanSkin=" + HumanSkin);
        if (NpcSkin != null) applied.Add("npcSkin=" + NpcSkin);
        return string.Join("; ", applied);
    }
#endif
}
