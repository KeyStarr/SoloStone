using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SabberStoneCore.Enums;
using EngineZone = SabberStoneCore.Enums.Zone;
using EngineCard = SabberStoneCore.Model.Card;
using SabberStoneCore.Kettle;
using NativeHistory = PegasusGame.PowerHistoryData;
using E = SabberStoneCore.Kettle.IPowerHistoryEntry;

// Pet XP likes and treats (docs/design/pet-xp.md): one row per like of every family table, both the firing case and the
// does-not-fire cases (opponent's action, wrong card), once per game, display gate, rewind, and the native transport of
// treat counters.
internal static partial class Program
{
    private static void PetLikesAndTreats()
    {
        PetLikeRows();
        PetLikeActivations();
        PetLikeCounters();
        PetLikesNativeMatch();
        PetLikesFaultIsolation();
        PetLikesRealEngine();
        PetTreatsNativeMatch();
        Console.WriteLine("PASS pet likes and treats: every like row (fires, does not fire, once per game), gate deferral, rewind, treats, recipient and NPC isolation");
    }

    private const int LikePet = 9999, LikeHero = 10, LikeFoeHero = 20, LikeMyPlayerEntity = 2, LikeFoePlayerEntity = 3;
    private static int likeNextId = 100;

    private static EngineCard LikeCard(string label, Func<EngineCard, bool> filter)
    {
        var all = SabberStoneCore.Model.Cards.All.Where(c => c.Id != null && filter(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var card = all.FirstOrDefault(c => c.Collectible) ?? all.FirstOrDefault();
        Check(card != null, "pet likes fixture card exists: " + label);
        return card;
    }

    private static PowerHistoryFullEntity LikeEntity(int id, int owner, EngineCard card, EngineZone zone, params (GameTag, int)[] extra)
    {
        var tags = new Dictionary<GameTag, int> {
            [GameTag.CONTROLLER] = owner, [GameTag.CARDTYPE] = (int)card.Type, [GameTag.COST] = card.Cost, [GameTag.ZONE] = (int)zone };
        if (card.Type == CardType.MINION) { tags[GameTag.ATK] = card.ATK; tags[GameTag.HEALTH] = card.Health; }
        foreach (var (tag, value) in extra) tags[tag] = value;
        return new PowerHistoryFullEntity { Entity = new PowerHistoryEntity { Id = id, Name = card.Id, Tags = tags } };
    }

    // Game 1, my player entity 2 (player 1, current), the enemy's entity 3 (player 2), heroes 10/20, my minion 11, enemy minion 21.
    private static List<E> LikeBase(int turn = 5)
    {
        var list = new List<E> { new PowerHistoryCreateGame {
            Game = new PowerEntity { Id = 1, Tags = new Dictionary<GameTag, int> { [GameTag.TURN] = turn } },
            Players = new[] { 1, 2 }.Select(owner => new PowerPlayer { PlayerId = owner, PowerEntity = new PowerEntity { Id = owner + 1,
                Tags = new Dictionary<GameTag, int> { [GameTag.CURRENT_PLAYER] = owner == 1 ? 1 : 0 } } }).ToArray() } };
        PowerHistoryFullEntity Plain(int id, int owner, CardType type, EngineZone zone) => new PowerHistoryFullEntity { Entity = new PowerHistoryEntity {
            Id = id, Tags = new Dictionary<GameTag, int> { [GameTag.CONTROLLER] = owner, [GameTag.CARDTYPE] = (int)type, [GameTag.ZONE] = (int)zone } } };
        list.Add(Plain(LikeHero, 1, CardType.HERO, EngineZone.PLAY));
        list.Add(Plain(LikeFoeHero, 2, CardType.HERO, EngineZone.PLAY));
        list.Add(Plain(11, 1, CardType.MINION, EngineZone.PLAY));
        list.Add(Plain(21, 2, CardType.MINION, EngineZone.PLAY));
        list.Add(PowerHistoryBuilder.TagChange(1, GameTag.STEP, 10)); // ends the seeding block
        return list;
    }

    private static E LikeTag(int entity, GameTag tag, int value) => PowerHistoryBuilder.TagChange(entity, tag, value);
    private static E LikeTag(int entity, int tag, int value) => PowerHistoryBuilder.TagChange(entity, (GameTag)tag, value);
    private static E LikeBlock(BlockType type, int source) => PowerHistoryBuilder.BlockStart(type, source, "", 0, 0);
    private static E LikeEnd() => PowerHistoryBuilder.BlockEnd();
    private static E[] LikePlay(EngineCard card, int owner = 1, params (GameTag, int)[] extra)
    {
        int id = likeNextId++;
        var list = new List<E> { LikeEntity(id, owner, card, EngineZone.HAND, extra), LikeBlock(BlockType.PLAY, id) };
        if (card.Type == CardType.MINION) list.Add(LikeTag(id, GameTag.ZONE, (int)EngineZone.PLAY));
        list.Add(LikeEnd());
        return list.ToArray();
    }
    private static E[] LikeSummon(EngineCard card, int owner = 1) => new E[] { LikeEntity(likeNextId++, owner, card, EngineZone.PLAY) };
    private static E[] LikeMeta(MetaDataType type, int amount, int target) => new E[] { new PowerHistoryMetaData { Type = type, Data = amount, Info = new List<int> { target } } };
    private static E[] LikeDie(int id) => new E[] { LikeBlock(BlockType.DEATHS, 1), LikeTag(id, GameTag.ZONE, (int)EngineZone.GRAVEYARD), LikeEnd() };
    private static E[] LikeFoeTurn() => new E[] { LikeTag(LikeMyPlayerEntity, GameTag.CURRENT_PLAYER, 0), LikeTag(LikeFoePlayerEntity, GameTag.CURRENT_PLAYER, 1) };
    private static E[] Cat(params E[][] parts) => parts.SelectMany(part => part).ToArray();

    private sealed class LikeRun
    {
        internal NativePetLikes Likes;
        internal List<(int Index, int Tag, int Value)> Tags = new List<(int, int, int)>();
        internal void Feed(List<E> entries)
        {
            foreach (var pair in Likes.Project(entries, 1, LikePet))
                foreach (var change in pair.Value) { Check(change.TagChange.Entity == LikePet, "like tags target the pet"); Tags.Add((pair.Key, change.TagChange.Tag, change.TagChange.Value)); }
        }
    }
    private static LikeRun StartLikes(int family, PetXpPolicy policy = null, int turn = 5)
    {
        var run = new LikeRun { Likes = new NativePetLikes(family, policy ?? PetXpPolicy.PresetB) };
        run.Feed(LikeBase(turn));
        return run;
    }

    private static void PetLikeRows()
    {
        var spell = new Func<int, Func<EngineCard, bool>>(school => c => c.Type == CardType.SPELL && c[(GameTag)1635] == school && c.Class != CardClass.DEATHKNIGHT);
        var nature = LikeCard("nature spell", spell(4)); var fire = LikeCard("fire spell", spell(2)); var frost = LikeCard("frost spell", spell(3));
        var shadow = LikeCard("shadow spell", spell(6)); var untypedSpell = LikeCard("untyped spell", spell(0));
        var dkSpell = LikeCard("DK spell", c => c.Type == CardType.SPELL && c.Class == CardClass.DEATHKNIGHT);
        EngineCard Minion(string label, Func<EngineCard, bool> f) => LikeCard(label, c => c.Type == CardType.MINION && f(c));
        var plain = Minion("plain minion", c => c.Races.Count == 1 && !c.IsRace(SabberStoneCore.Enums.Race.DRAGON) && !c.IsRace(SabberStoneCore.Enums.Race.BEAST)
            && !c.IsRace(SabberStoneCore.Enums.Race.PIRATE) && !c.IsRace(SabberStoneCore.Enums.Race.UNDEAD) && c.Rarity != Rarity.LEGENDARY && c.Class != CardClass.DEATHKNIGHT
            && !c.Rush && !c.Taunt && !c.Deathrattle && c.Cost < 10 && c.ATK < 12 && c[(GameTag)4309] == 0 && c.Name != null && !c.Name.StartsWith("Ysera"));
        var typeless = Minion("typeless", c => c.Races.Count == 0 && c.Rarity != Rarity.LEGENDARY && !c.Rush && !c.Taunt && !c.Deathrattle && c.Cost < 10 && c.ATK < 12);
        var legendary = Minion("legendary", c => c.Rarity == Rarity.LEGENDARY && c.Races.Count == 1 && !c.IsRace(SabberStoneCore.Enums.Race.DRAGON)
            && c.Cost < 10 && c.ATK < 12 && !c.Rush && !c.Taunt && !c.Deathrattle);
        var pirate = Minion("pirate", c => c.IsRace(SabberStoneCore.Enums.Race.PIRATE));
        var dragon5 = Minion("dragon cost 5+", c => c.IsRace(SabberStoneCore.Enums.Race.DRAGON) && c.Cost >= 5 && c.Cost < 10 && c.Rarity != Rarity.LEGENDARY);
        var dragon4 = Minion("dragon cost <5", c => c.IsRace(SabberStoneCore.Enums.Race.DRAGON) && c.Cost < 5 && c.Rarity != Rarity.LEGENDARY);
        var beast = new Func<int, int, EngineCard>((low, high) => Minion("beast " + low + "-" + high, c => c.IsRace(SabberStoneCore.Enums.Race.BEAST) && c.Cost >= low && c.Cost <= high
            && !c.IsRace(SabberStoneCore.Enums.Race.ALL) && c.Rarity != Rarity.LEGENDARY && !c.Rush));
        var beast3 = beast(0, 3); var beast45 = beast(4, 5); var beast6 = beast(6, 9);
        var rush = Minion("rush", c => c.Rush && c.Races.Count == 1 && c.Rarity != Rarity.LEGENDARY && !c.IsRace(SabberStoneCore.Enums.Race.BEAST));
        var undead3 = Minion("undead cost <=3", c => c.IsRace(SabberStoneCore.Enums.Race.UNDEAD) && c.Cost <= 3 && c.Rarity != Rarity.LEGENDARY);
        var undead5 = Minion("undead cost >=4", c => c.IsRace(SabberStoneCore.Enums.Race.UNDEAD) && c.Cost >= 4 && c.Cost < 10 && c.Rarity != Rarity.LEGENDARY && c.Class != CardClass.DEATHKNIGHT);
        var dkMinion = Minion("DK minion", c => c.Class == CardClass.DEATHKNIGHT && !c.IsRace(SabberStoneCore.Enums.Race.UNDEAD) && c.Rarity != Rarity.LEGENDARY);
        var taunt = Minion("taunt", c => c.Taunt && c.Rarity != Rarity.LEGENDARY);
        var deathrattle = Minion("deathrattle", c => c.Deathrattle && c.Rarity != Rarity.LEGENDARY);
        var big = Minion("12+ attack", c => c.ATK >= 12 && c.Cost < 10);
        var oldGod = LikeCard("old god", c => c.Id == "BE_013");
        var ysera = Minion("ysera", c => c.Name != null && c.Name.StartsWith("Ysera"));
        var qonzu = Minion("qonzu", c => c.Name != null && c.Name.StartsWith("Q'onzu"));
        var kelthuzad = Minion("kel'thuzad", c => c.Name != null && c.Name.StartsWith("Kel'Thuzad"));
        var cost10 = LikeCard("10-cost card", c => c.Cost == 10 && c.Type == CardType.MINION && c.Rarity != Rarity.LEGENDARY && c.ATK < 12 && c.Races.Count == 0);
        var cost10b = LikeCard("10-cost card, any", c => c.Cost == 10);
        var cost9 = LikeCard("9-cost card", c => c.Cost == 9 && c.Type == CardType.MINION && c.Races.Count == 0 && c.Rarity != Rarity.LEGENDARY && c.ATK < 12);
        var herald = LikeCard("herald card", c => c[(GameTag)4309] > 0 && c.Type == CardType.MINION);
        var colossal = Minion("colossal", c => c.ColossalAppendageIds != null && c.ColossalAppendageIds.Count > 0);
        var heroPower = LikeCard("hero power", c => c.Type == CardType.HERO_POWER);
        var cheap = Minion("cheap vanilla", c => c.Races.Count == 1 && !c.IsRace(SabberStoneCore.Enums.Race.DRAGON) && c.Cost < 5 && c.Rarity != Rarity.LEGENDARY && !c.Taunt);
        Check(taunt.Taunt && big.ATK >= 12 && deathrattle.Deathrattle && herald[(GameTag)4309] > 0, "fixture cards carry the printed facts under test");

        var rows = new Dictionary<string, (E[] Fires, E[][] Silent)>();
        E[] Events(object value) => value is E single ? new[] { single } : (E[])value;
        void Row(string id, object fires, params object[] silent) { Check(!rows.ContainsKey(id), "one scenario per like " + id); rows[id] = (Events(fires), silent.Select(Events).ToArray()); }
        // Minions and spells played from hand by me count; the same cards played by the opponent never do.
        Row("rush-minion", LikePlay(rush), LikePlay(rush, 2), LikePlay(plain));
        Row("beast-6plus", LikePlay(beast6), LikePlay(beast6, 2), LikePlay(beast45), LikePlay(plain));
        Row("beast-4to5", LikePlay(beast45), LikePlay(beast45, 2), LikePlay(beast6), LikePlay(beast3));
        Row("beast-4plus", LikePlay(beast45), LikePlay(beast45, 2), LikePlay(beast3), LikePlay(plain));
        Row("nature-spell", LikePlay(nature), LikePlay(nature, 2), LikePlay(fire));
        Row("fire-spell", LikePlay(fire), LikePlay(fire, 2), LikePlay(nature));
        Row("frost-spell", LikePlay(frost), LikePlay(frost, 2), LikePlay(fire));
        Row("shadow-spell", LikePlay(shadow), LikePlay(shadow, 2), LikePlay(nature));
        Row("untyped-spell", LikePlay(untypedSpell), LikePlay(untypedSpell, 2), LikePlay(nature));
        Row("dk-spell", LikePlay(dkSpell), LikePlay(dkSpell, 2), LikePlay(nature));
        Row("untyped-minion", LikePlay(typeless), LikePlay(typeless, 2), LikePlay(plain));
        Row("legendary-minion", LikePlay(legendary), LikePlay(legendary, 2), LikePlay(plain));
        Row("pirate", LikePlay(pirate), LikePlay(pirate, 2), LikePlay(plain));
        Row("dragon-5plus", LikePlay(dragon5), LikePlay(dragon5, 2), LikePlay(dragon4), LikePlay(plain));
        Row("undead-3minus", LikePlay(undead3), LikePlay(undead3, 2), LikePlay(undead5));
        Row("undead-or-dk-minion", LikePlay(undead5), LikePlay(undead5, 2), LikePlay(plain));
        Row("colossal", LikePlay(colossal), LikePlay(colossal, 2), LikePlay(plain));
        Row("herald", LikePlay(herald), LikePlay(herald, 2), LikePlay(plain));
        Row("ysera-minion", LikePlay(ysera), LikePlay(ysera, 2), LikePlay(plain));
        Row("qonzu-minion", LikePlay(qonzu), LikePlay(qonzu, 2), LikePlay(plain));
        Row("kelthuzad", LikePlay(kelthuzad), LikePlay(kelthuzad, 2), LikePlay(plain));
        Row("cost-10-card", LikePlay(cost10), LikePlay(cost10, 2), LikePlay(cost9));
        Row("dark-gift-card", LikePlay(plain, 1, ((GameTag)3880, 1)), LikePlay(plain, 2, ((GameTag)3880, 1)), LikePlay(plain));
        Row("hero-power", LikePlay(heroPower), LikePlay(heroPower, 2));
        // Counts inside one turn: the third spell, the fourth card, the third minion; a new turn or the opponent never adds.
        Row("spells-3-turn", Cat(LikePlay(nature), LikePlay(fire), LikePlay(frost)), Cat(LikePlay(nature), LikePlay(fire)),
            Cat(LikePlay(nature), LikePlay(fire), new E[] { LikeTag(1, GameTag.TURN, 6) }, LikePlay(frost)), Cat(LikePlay(nature), LikePlay(fire), LikePlay(frost, 2)));
        Row("cards-4-turn", Cat(LikePlay(plain), LikePlay(nature), LikePlay(cheap), LikePlay(fire)), Cat(LikePlay(plain), LikePlay(nature), LikePlay(cheap)),
            Cat(LikePlay(plain), LikePlay(nature), LikePlay(cheap), new E[] { LikeTag(1, GameTag.TURN, 6) }, LikePlay(fire)),
            Cat(LikePlay(plain), LikePlay(nature), LikePlay(cheap), LikePlay(fire, 2), LikePlay(heroPower)));
        Row("minions-3-turn", Cat(LikePlay(plain), LikePlay(cheap), LikePlay(typeless)), Cat(LikePlay(plain), LikePlay(cheap)),
            Cat(LikePlay(plain), LikePlay(cheap), LikePlay(typeless, 2)), Cat(LikePlay(plain), LikePlay(cheap), LikePlay(nature)));
        // Summons count on entering play, played or not.
        Row("summon-taunt", LikeSummon(taunt), LikeSummon(taunt, 2), LikeSummon(plain));
        Row("summon-12-attack", LikeSummon(big), LikeSummon(big, 2), LikeSummon(plain));
        Row("deathrattle-summon", LikeSummon(deathrattle), LikeSummon(deathrattle, 2), LikeSummon(plain));
        Row("old-god-summon", LikeSummon(oldGod), LikeSummon(oldGod, 2), LikeSummon(plain));
        // Hero effects and damage, always from my side; the enemy's turn and my own side never count.
        Row("heal-hero", LikeMeta(MetaDataType.HEALING, 3, LikeHero), LikeMeta(MetaDataType.HEALING, 3, LikeFoeHero), LikeMeta(MetaDataType.HEALING, 0, LikeHero), LikeMeta(MetaDataType.HEALING, 3, 11));
        Row("damage-enemy-hero", LikeMeta(MetaDataType.DAMAGE, 2, LikeFoeHero), LikeMeta(MetaDataType.DAMAGE, 2, LikeHero), LikeMeta(MetaDataType.DAMAGE, 2, 21),
            Cat(LikeFoeTurn(), LikeMeta(MetaDataType.DAMAGE, 2, LikeFoeHero)));
        Row("damage-10plus", LikeMeta(MetaDataType.DAMAGE, 10, LikeFoeHero), LikeMeta(MetaDataType.DAMAGE, 9, LikeFoeHero), LikeMeta(MetaDataType.DAMAGE, 12, LikeHero),
            Cat(LikeFoeTurn(), LikeMeta(MetaDataType.DAMAGE, 10, LikeFoeHero)));
        Row("enemy-hit-for-1", LikeMeta(MetaDataType.DAMAGE, 1, 21), LikeMeta(MetaDataType.DAMAGE, 2, 21), LikeMeta(MetaDataType.DAMAGE, 1, 11), LikeMeta(MetaDataType.DAMAGE, 1, LikeHero));
        Row("hero-hurt-own-turn", LikeMeta(MetaDataType.DAMAGE, 3, LikeHero), LikeMeta(MetaDataType.DAMAGE, 3, LikeFoeHero), LikeMeta(MetaDataType.DAMAGE, 0, LikeHero),
            Cat(LikeFoeTurn(), LikeMeta(MetaDataType.DAMAGE, 3, LikeHero)));
        Row("friendly-died-own-turn", LikeDie(11), LikeDie(21), Cat(LikeFoeTurn(), LikeDie(11)), new E[] { LikeTag(11, GameTag.ZONE, (int)EngineZone.GRAVEYARD) });
        Row("discover", LikeTag(LikeMyPlayerEntity, 32414, 1), LikeTag(LikeFoePlayerEntity, 32414, 1), LikeTag(LikeMyPlayerEntity, 32414, 0));
        Row("discard", LikeTag(LikeMyPlayerEntity, GameTag.LAST_CARD_DISCARDED, 40), LikeTag(LikeFoePlayerEntity, GameTag.LAST_CARD_DISCARDED, 40), LikeTag(LikeMyPlayerEntity, GameTag.LAST_CARD_DISCARDED, 0));
        Row("armor-gain", LikeTag(LikeHero, GameTag.ARMOR, 5), LikeTag(LikeFoeHero, GameTag.ARMOR, 5), LikeTag(LikeHero, GameTag.ARMOR, 0));
        Row("mana-10", LikeTag(LikeMyPlayerEntity, GameTag.RESOURCES, 10), LikeTag(LikeMyPlayerEntity, GameTag.RESOURCES, 9), LikeTag(LikeFoePlayerEntity, GameTag.RESOURCES, 10));
        Row("corpse-gained", LikeTag(LikeMyPlayerEntity, 2186, 2), LikeTag(LikeFoePlayerEntity, 2186, 2), LikeTag(LikeMyPlayerEntity, 2186, 0));
        Row("freeze-enemy", LikeTag(21, GameTag.FROZEN, 1), LikeTag(11, GameTag.FROZEN, 1), LikeTag(21, GameTag.FROZEN, 0));
        Row("minion-attacks", new E[] { LikeBlock(BlockType.ATTACK, 11), LikeEnd() }, new E[] { LikeBlock(BlockType.ATTACK, 21), LikeEnd() },
            new E[] { LikeBlock(BlockType.ATTACK, LikeHero), LikeEnd() });
        Row("attack-kill", new E[] { LikeBlock(BlockType.ATTACK, 11), LikeTag(21, GameTag.TO_BE_DESTROYED, 1), LikeEnd() },
            new E[] { LikeBlock(BlockType.ATTACK, 11), LikeTag(LikeFoeHero, GameTag.TO_BE_DESTROYED, 1), LikeEnd() },
            new E[] { LikeBlock(BlockType.ATTACK, 11), LikeTag(11, GameTag.TO_BE_DESTROYED, 1), LikeEnd() },
            new E[] { LikeBlock(BlockType.ATTACK, 21), LikeTag(21, GameTag.TO_BE_DESTROYED, 1), LikeEnd() },
            new E[] { LikeTag(21, GameTag.TO_BE_DESTROYED, 1) });
        // three-died-turn needs three of MY minions dying inside DEATHS blocks, in one turn.
        // Three of MY minions dying inside DEATHS blocks, in one turn.
        Row("three-died-turn", Cat(new E[] { LikeEntity(31, 1, plain, EngineZone.PLAY), LikeEntity(32, 1, plain, EngineZone.PLAY), LikeEntity(33, 1, plain, EngineZone.PLAY) },
                LikeDie(31), LikeDie(32), LikeDie(33)),
            Cat(new E[] { LikeEntity(31, 1, plain, EngineZone.PLAY), LikeEntity(32, 1, plain, EngineZone.PLAY) }, LikeDie(31), LikeDie(32)),
            Cat(new E[] { LikeEntity(31, 1, plain, EngineZone.PLAY), LikeEntity(32, 1, plain, EngineZone.PLAY), LikeEntity(33, 1, plain, EngineZone.PLAY) },
                LikeDie(31), LikeDie(32), new E[] { LikeTag(1, GameTag.TURN, 6) }, LikeDie(33)),
            Cat(new E[] { LikeEntity(31, 2, plain, EngineZone.PLAY), LikeEntity(32, 2, plain, EngineZone.PLAY), LikeEntity(33, 2, plain, EngineZone.PLAY) },
                LikeDie(31), LikeDie(32), LikeDie(33)));
        // My seeded minion makes six summons a full board; five leave six; the enemy's minions never count.
        Row("board-full", Cat(Enumerable.Range(0, 6).Select(_ => LikeSummon(plain)).ToArray()),
            Cat(Enumerable.Range(0, 5).Select(_ => LikeSummon(plain)).ToArray()), Cat(Enumerable.Range(0, 6).Select(_ => LikeSummon(plain, 2)).ToArray()));

        var allIds = PetXpPolicy.AllLikeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(allIds.All(rows.ContainsKey) && rows.Keys.All(allIds.Contains), "every like in the family tables has a scenario, and every scenario a like: missing "
            + string.Join(",", allIds.Where(id => !rows.ContainsKey(id))) + " extra " + string.Join(",", rows.Keys.Where(id => !allIds.Contains(id))));
        foreach (int family in new[] { 3, 4, 6, 7, 8, 9, 10, 12, 15, 16 })
        {
            var table = PetXpPolicy.LikesFor(family);
            Check(table.Count > 0 && table.Distinct().Count() == table.Count, "family " + family + " lists distinct likes");
            foreach (string id in table)
            {
                var (fires, silent) = rows[id];
                var run = StartLikes(family);
                run.Feed(fires.ToList());
                Check(run.Likes.Fired.Contains(id), "family " + family + " like " + id + " fires");
                int mediumTags = run.Tags.Count(t => t.Tag == NativePetLikes.MediumTag);
                Check(run.Likes.Medium == 2 * run.Likes.Fired.Count && mediumTags == run.Likes.Fired.Count && run.Tags.All(t => t.Tag == NativePetLikes.MediumTag),
                    "family " + family + " like " + id + ": two MEDIUM ticks per fired like, shown as ONE counter update (preset B, 20 XP)");
                Check(run.Tags.Select(t => t.Value).SequenceEqual(Enumerable.Range(1, run.Tags.Count).Select(n => 2 * n)), "absolute counter values rise by two: " + id);
                // Once per game: the same events again pay nothing.
                int before = run.Likes.Medium, tagsBefore = run.Tags.Count;
                run.Feed(fires.ToList());
                Check(run.Likes.Medium == before && run.Tags.Count == tagsBefore, "family " + family + " like " + id + " pays once per game");
                foreach (var quiet in silent)
                {
                    var other = StartLikes(family);
                    other.Feed(quiet.ToList());
                    Check(!other.Likes.Fired.Contains(id), "family " + family + " like " + id + " does not fire for a non-matching or opposing action");
                }
            }
            // A like of another family never fires here.
            string foreign = allIds.FirstOrDefault(id => !table.Contains(id));
            if (foreign != null)
            {
                var run = StartLikes(family);
                run.Feed(rows[foreign].Fires.ToList());
                Check(!run.Likes.Fired.Contains(foreign), "family " + family + " ignores a like outside its table: " + foreign);
            }
        }
        // The documented list sizes (PROPOSED rows for Krush 3, Rabbit'ath 4 and Arfus 6 included).
        var expectedSizes = new Dictionary<int, int> { [3] = 7, [4] = 8, [6] = 8, [7] = 6, [8] = 7, [9] = 7, [10] = 8, [12] = 7, [15] = 9, [16] = 9 };
        foreach (var pair in expectedSizes) Check(PetXpPolicy.LikesFor(pair.Key).Count == pair.Value, "like count of family " + pair.Key);
        Check(PetXpPolicy.LikesFor(1000).Count == 0 && new NativePetLikes(1000, PetXpPolicy.PresetB).Project(LikeBase(), 1, LikePet).Count == 0, "unknown family has no likes");
    }

    // A location or Rift activation opens a PLAY block for a board object already in play: it is not a card played from hand.
    private static void PetLikeActivations()
    {
        var location = LikeCard("location", c => c.Type == (CardType)39);
        var rift = LikeCard("legendary minion", c => c.Type == CardType.MINION && c.Rarity == Rarity.LEGENDARY && c.Cost < 10 && c.Races.Count == 1 && !c.IsRace(SabberStoneCore.Enums.Race.DRAGON)
            && !c.Rush && !c.Taunt && !c.Deathrattle && c.ATK < 12);
        var cost10 = LikeCard("10-cost minion", c => c.Type == CardType.MINION && c.Cost == 10 && c.Races.Count == 0);
        var plain = LikeCard("plain minion", c => c.Type == CardType.MINION && c.Races.Count == 1 && c.Rarity != Rarity.LEGENDARY && c.Cost < 5 && !c.Taunt && !c.Rush && !c.Deathrattle
            && !c.IsRace(SabberStoneCore.Enums.Race.PIRATE) && !c.IsRace(SabberStoneCore.Enums.Race.DRAGON));
        var nature = LikeCard("nature spell", c => c.Type == CardType.SPELL && c[(GameTag)1635] == 4 && c.Class != CardClass.DEATHKNIGHT);
        E[] Activate(EngineCard card) { int id = likeNextId++; return new E[] { LikeEntity(id, 1, card, EngineZone.PLAY), LikeBlock(BlockType.PLAY, id), LikeEnd() }; }
        // Hogger: cards-4-turn. Two cards plus three activations is two cards; two more cards make four.
        var run = StartLikes(8);
        run.Feed(Cat(LikePlay(plain), LikePlay(nature), Activate(location), Activate(location), Activate(location)).ToList());
        Check(!run.Likes.Fired.Contains("cards-4-turn") && run.Likes.Medium == 2 * run.Likes.Fired.Count, "location activations are not cards played");
        run.Feed(Cat(LikePlay(plain), LikePlay(nature)).ToList());
        Check(run.Likes.Fired.Contains("cards-4-turn"), "four cards from hand still count after activations");
        // A location played from hand is a card; its zone only becomes PLAY inside the block.
        var placed = StartLikes(8);
        int locationId = likeNextId++;
        placed.Feed(Cat(LikePlay(plain), LikePlay(nature), LikePlay(plain), new E[] { LikeEntity(locationId, 1, location, EngineZone.HAND), LikeBlock(BlockType.PLAY, locationId),
            LikeTag(locationId, GameTag.ZONE, (int)EngineZone.PLAY), LikeEnd() }).ToList());
        Check(placed.Likes.Fired.Contains("cards-4-turn"), "a location played from hand counts as a card");
        // Rift: a minion already on the board that is activated is neither a legendary play nor a minion play nor a 10-cost card.
        var rifted = StartLikes(8);
        rifted.Feed(Cat(Activate(rift), Activate(rift), Activate(rift), Activate(cost10)).ToList());
        Check(rifted.Likes.Fired.Count == 0, "a board minion's activation fires no play like: " + string.Join(",", rifted.Likes.Fired));
        var costly = StartLikes(9);
        costly.Feed(Activate(cost10).ToList());
        Check(!costly.Likes.Fired.Contains("cost-10-card"), "activating a 10-cost board object is not playing a 10-cost card");
        // Automatic plays (Fyrakk's casts, autoplay) are not cards played: no card-count or spell like.
        var automatic = StartLikes(8);
        E[] AutoPlay(EngineCard card) { int id = likeNextId++; return new E[] { LikeEntity(id, 1, card, EngineZone.SETASIDE),
            new PowerHistoryEffectPlayStart { BlockType = BlockType.PLAY, Source = id, Index = -1, EffectCardId = "", ForceShowBigCard = true }, LikeEnd() }; }
        automatic.Feed(Cat(AutoPlay(nature), AutoPlay(nature), AutoPlay(nature), AutoPlay(nature), AutoPlay(plain)).ToList());
        Check(automatic.Likes.Fired.Count == 0, "automatic plays fire no play like: " + string.Join(",", automatic.Likes.Fired));
        // A hero power sits in the PLAY zone and is still a hero power use.
        var power = StartLikes(6);
        int powerId = likeNextId++;
        power.Feed(new List<E> { LikeEntity(powerId, 1, LikeCard("hero power", c => c.Type == CardType.HERO_POWER), EngineZone.PLAY), LikeBlock(BlockType.PLAY, powerId), LikeEnd() });
        Check(power.Likes.Fired.Contains("hero-power"), "a hero power already in play still counts when used");
    }

    private static void PetLikesFaultIsolation()
    {
        using var fixture = new LikeMatchFixture(true, PetFamilyWithNative(9));
        var capture = new Capture(); var trace = new List<string>();
        var match = new NativeMatch(capture.Send, "mage", "warrior", 1337) { BotTrace = trace.Add }; match.Start();
        match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
        var game = match.Game; var human = game.Player1;
        try
        {
            human.BaseMana = 10; human.UsedMana = 0; game.PowerHistory.Last.Clear();
            var first = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match);
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, first, game.Player2.Hero)), "first Fireball resolves");
            FlushHistory(match);
            int banked = match.PetXpCounts.Medium;
            Check(banked >= 1, "a like was banked before the fault");
            NativePetLikes.FaultHook = () => throw new InvalidOperationException("injected likes fault");
            human.UsedMana = 0; game.PowerHistory.Last.Clear();
            var second = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            int cursor = capture.Packets.Count;
            FlushHistory(match);
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, second, game.Player2.Hero)), "the engine action still resolves");
            FlushHistory(match);
            Check(NativeHistory(capture, cursor).Any(e => e.HasPowerStart) && NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Tag == 4038),
                "the history stream and the pet's reactions still reach the client");
            Check(trace.Count(line => line.Contains("pet likes disabled")) == 1, "a likes fault is logged exactly once");
            human.UsedMana = 0; game.PowerHistory.Last.Clear();
            var third = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match);
            game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, third, game.Player2.Hero)); FlushHistory(match);
            Check(trace.Count(line => line.Contains("pet likes disabled")) == 1, "the disabled evaluator is not run again");
            Check(match.PetXpCounts != null && match.PetXpCounts.Medium == banked, "what was banked before the fault still settles");
            Check(match.ReceiveNativePetTreat(67) && match.PetXpCounts.Medium == banked, "treats keep working but take no tick from a disabled evaluator");
        }
        finally { NativePetLikes.FaultHook = null; }
    }

    private static void PetLikeCounters()
    {
        // Preset B: every like and treat is MEDIUM (10 XP); A is SMALL; the tags carry absolute values in history order.
        var run = StartLikes(7, turn: 1);
        var nature = LikeCard("nature spell", c => c.Type == CardType.SPELL && c[(GameTag)1635] == 4 && c.Class != CardClass.DEATHKNIGHT);
        // Before the gate (turn 4): ticks bank silently.
        run.Feed(Cat(LikePlay(nature), LikeMeta(MetaDataType.HEALING, 3, LikeHero)).ToList());
        Check(run.Tags.Count == 0 && run.Likes.Medium == 4 && !run.Likes.GatePassed && !run.Likes.ShownTags().Any(), "ticks before the gate bank silently");
        run.Feed(new List<E> { LikeTag(1, GameTag.TURN, 3) });
        Check(run.Tags.Count == 0, "turn 3 is still before the gate");
        // The gate passes: one release update with the banked absolute value, right after the entry that reached the gate.
        run.Feed(new List<E> { LikeTag(1, GameTag.STEP, 5), LikeTag(1, GameTag.TURN, 4), LikeTag(1, GameTag.STEP, 6) });
        Check(run.Tags.Count == 1 && run.Tags[0] == (1, NativePetLikes.MediumTag, 4) && run.Likes.GatePassed, "gate releases one counter update with the banked value after the TURN entry");
        // After the gate every tick is immediate and in history order, absolute value.
        var after = new List<E> { LikeTag(2, 2186, 0), LikeTag(LikeMyPlayerEntity, GameTag.RESOURCES, 10) };
        run.Feed(after);
        Check(run.Tags.Count == 2 && run.Tags[1] == (1, NativePetLikes.MediumTag, 6), "after the gate a like (two ticks) is shown at the entry that caused it");
        Check(run.Likes.ShownTags().SequenceEqual(new[] { new KeyValuePair<int, int>(NativePetLikes.MediumTag, 6) }), "shown tags are what the client has seen");
        // Treat feeds follow the same gate.
        var early = StartLikes(7, turn: 1);
        Check(early.Likes.Treat(LikePet).Count == 0 && early.Likes.Medium == 2, "a treat (two ticks) before the gate is banked");
        early.Feed(new List<E> { LikeTag(1, GameTag.TURN, 4) });
        Check(early.Tags.Single() == (0, NativePetLikes.MediumTag, 2), "banked treat is released with the gate");
        var treat = early.Likes.Treat(LikePet);
        Check(treat.Single().TagChange.Tag == NativePetLikes.MediumTag && treat.Single().TagChange.Value == 4, "treat after the gate is one immediate absolute update of two ticks");
        // Preset A (retail-exact): SMALL counter. Mixed likes and treats land on their own counters.
        var small = StartLikes(7, PetXpPolicy.PresetA);
        small.Feed(LikePlay(nature).ToList());
        Check(small.Likes.Small == 1 && small.Likes.Medium == 0 && small.Tags.Single().Tag == NativePetLikes.SmallTag, "preset A pays the SMALL counter");
        var mixed = StartLikes(7, new PetXpPolicy { Like = PetCounter.Small, Treat = PetCounter.Large, GateTurns = 4, PacingPercent = 100 });
        mixed.Feed(LikePlay(nature).ToList()); mixed.Likes.Treat(LikePet);
        Check(mixed.Likes.Small == 1 && mixed.Likes.Large == 1, "like and treat counters are independent knobs");
        // Short games pay: no display gate at all.
        var shortPays = StartLikes(7, new PetXpPolicy { Like = PetCounter.Medium, Treat = PetCounter.Medium, PayShort = true, GateTurns = 4, PacingPercent = 100 }, turn: 1);
        shortPays.Feed(LikePlay(nature).ToList());
        Check(shortPays.Tags.Count == 1, "a policy that pays short games shows ticks immediately");
        // Rewind: a reset rebuilds the graph but never un-fires a like or double counts it.
        var rewind = StartLikes(7);
        rewind.Feed(LikePlay(nature).ToList());
        int tagsSoFar = rewind.Tags.Count;
        rewind.Feed(LikeBase(4));
        rewind.Feed(LikePlay(nature).ToList());
        Check(rewind.Tags.Count == tagsSoFar && rewind.Likes.Medium == 2, "a rewind reset does not pay the same like twice");
        rewind.Feed(new List<E> { new PowerHistoryResetGame { CreateGame = new PowerHistoryCreateGame { Game = new PowerEntity { Id = 1, Tags = new Dictionary<GameTag, int> { [GameTag.TURN] = 3 } },
            Players = new[] { 1, 2 }.Select(owner => new PowerPlayer { PlayerId = owner, PowerEntity = new PowerEntity { Id = owner + 1, Tags = new Dictionary<GameTag, int>() } }).ToArray() } } });
        rewind.Feed(LikePlay(nature).ToList());
        Check(rewind.Likes.Medium == 2, "a ResetGame payload keeps the fired set");
        Check(ReferenceEquals(PetXpPolicy.Current, PetXpPolicy.PresetB), "owner ruling 2026-10-28: preset B is the default policy");
        Check(PetXpPolicy.PresetA.RuleString(6750, 9) == "rule=3;like=small;treat=small;liketicks=1;treatticks=1;small=5;medium=10;large=10;gamecap=0;concede=0;short=0;turns=4;mult=100;ticks=9;cap=6750"
            && PetXpPolicy.PresetB.RuleString(6750, 18) == "rule=3;like=medium;treat=medium;liketicks=2;treatticks=2;small=5;medium=10;large=10;gamecap=0;concede=0;short=0;turns=4;mult=100;ticks=18;cap=6750"
            && PetXpPolicy.PresetB.RuleString(6750, 18, 5000).Contains(";mult=5000;"),
            "rule strings record every knob canonically");
    }

    // ---- native match: recipient, tags on the wire, settlement counts ----

    private sealed class LikeMatchFixture : IDisposable
    {
        internal readonly string Root; internal LocalProfile Profile; internal string Path;
        private readonly CosmeticsFixture shop = new CosmeticsFixture();
        private readonly Func<LocalProfile> previousProfile = NativeMatch.CosmeticsProfile;
        private readonly Func<string, int> previousVariant = NativeMatch.NativePetVariant;
        internal LikeMatchFixture(bool begin, string petCard)
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pet-likes-" + Guid.NewGuid()); Directory.CreateDirectory(Root);
            Path = System.IO.Path.Combine(Root, "profile.xml");
            using (var fresh = new LocalProfile(Path)) fresh.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData)); LocalProfileData data;
            using (var file = File.OpenRead(Path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 5000;
            using (var file = File.Create(Path)) serializer.Serialize(file, data);
            Profile = new LocalProfile(Path);
            Profile.Purchase("pet-" + petCard, 1, 5000, "pet-likes");
            Profile.SelectPet(petCard);
            if (begin) Profile.Begin("mage", "warrior", 1337);
            NativeMatch.CosmeticsProfile = () => Profile;
            NativeMatch.NativePetVariant = _ => 5;
        }
        public void Dispose()
        {
            Profile.Dispose();
            NativeMatch.CosmeticsProfile = previousProfile; NativeMatch.NativePetVariant = previousVariant; shop.Dispose();
            try { Directory.Delete(Root, true); } catch (IOException) { }
        }
    }

    private static string PetFamilyWithNative(int nativeId) => LocalShop.Default.FormsForPet(
        LocalShop.SupportedCheatPets.Single(pet => LocalShop.Default.PetNativeId(pet) == nativeId))[0];

    private static List<int[]> PetXpTags(IEnumerable<NativeHistory> history, int pet) => history
        .Where(e => e.HasTagChange && e.TagChange.Entity == pet && e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078)
        .Select(e => new[] { e.TagChange.Tag, e.TagChange.Value }).ToList();

    private static void PetLikesNativeMatch()
    {
        string deathling = PetFamilyWithNative(9); // Cast a Fire spell, Play a 10-cost card, Discard a card, ...
        // 1. A recipient (pending PetXpBefore): ticks bank before the gate, then release; the totals feed settlement.
        using (var fixture = new LikeMatchFixture(true, deathling))
        {
            Check(fixture.Profile.Pending.PetXpBefore == 0, "pending match freezes the pet XP recipient");
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
            byte[] profileBefore = File.ReadAllBytes(fixture.Path); // Start has persisted the human coin snapshot.
            match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var human = game.Player1;
            int pet = NativeHistory(capture).Single(e => e.HasFullEntity && e.FullEntity.Name == deathling).FullEntity.Entity;
            var created = NativeHistory(capture).Single(e => e.HasFullEntity && e.FullEntity.Name == deathling).FullEntity;
            Check(PetTag(created, 4281) == 1 && !created.Tags.Any(t => t.Name == 4280 || t.Name >= 4076 && t.Name <= 4078), "pet starts with one generated treat and no feeds or XP counters");
            int cursor = capture.Packets.Count;
            game.PowerHistory.Last.Clear(); human.BaseMana = 10; human.UsedMana = 0;
            var fireball = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match); cursor = capture.Packets.Count;
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, fireball, game.Player2.Hero)), "Fireball accepted");
            FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("fire-spell") && match.PetXpCounts.Medium == 2 * match.PetLikes.Fired.Count && match.PetXpCounts.Medium >= 2,
                "a real Fireball banks the fire-spell like: " + string.Join(",", match.PetLikes.Fired));
            Check(PetXpTags(NativeHistory(capture, cursor), pet).Count == 0 && game.Turn < 4, "no XP tag is shown before the turn gate");
            // Reach the gate in the engine's own turn counter: the banked ticks are released as one update.
            game.PowerHistory.Last.Clear(); game.Turn = 4; FlushHistory(match);
            var released = PetXpTags(NativeHistory(capture, cursor), pet);
            Check(released.Count == 1 && released[0][0] == 4077 && released[0][1] == match.PetXpCounts.Medium, "gate release is one absolute MEDIUM update");
            // After the gate a new like is shown immediately; a repeat of the same like pays nothing.
            game.PowerHistory.Last.Clear(); human.UsedMana = 0;
            var again = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match); int mark = capture.Packets.Count;
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, again, game.Player2.Hero)), "second Fireball accepted");
            FlushHistory(match);
            Check(PetXpTags(NativeHistory(capture, mark), pet).Count == 0, "the same like does not pay twice in one game");
            int banked = match.PetXpCounts.Medium;
            game.PowerHistory.Last.Clear();
            var discarded = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match); mark = capture.Packets.Count;
            SabberStoneCore.Actions.Generic.DiscardBlock(human, discarded); FlushHistory(match);
            var discardTags = PetXpTags(NativeHistory(capture, mark), pet);
            Check(discardTags.Count == 1 && discardTags[0][1] == banked + 2 && match.PetXpCounts.Medium == banked + 2, "discard pays once (two ticks), shown immediately as one absolute update");
            var history = NativeHistory(capture, mark);
            int marker = history.FindIndex(e => e.HasTagChange && e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078 && e.TagChange.Entity == pet);
            Check(marker > 0, "the counter tag follows the history entries that caused it");
            // The profile is untouched by the match, its likes and counters until settlement.
            Check(File.ReadAllBytes(fixture.Path).SequenceEqual(profileBefore), "likes never write the profile");
            capture.SerializeAll();
            // A rewind reset reinjects the pet carrying exactly the counters the client has been shown.
            var append = typeof(NativeMatch).GetMethod("AppendRewindReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var entries = new List<E> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, game.Player1.Hero.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
            entries.AddRange(game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e => PowerHistoryBuilder.FullEntity(e, reset: true)));
            entries.Add(PowerHistoryBuilder.BlockEnd());
            var reset = new PegasusGame.PowerHistory(); append.Invoke(match, new object[] { reset, entries, 0 });
            var reinjected = reset.List.Single(e => e.HasFullEntity && e.FullEntity.Name == deathling).FullEntity;
            Check(PetTag(reinjected, 4077) == match.PetXpCounts.Medium && PetTag(reinjected, 4281) == 1, "rewind reinjection carries the current counters");
        }
        // 2. Settlement: counts reach the receipt under rule 2; a concede after the gate banks zero XP (known gap, documented).
        using (var fixture = new LikeMatchFixture(true, deathling))
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
            match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            var human = match.Game.Player1;
            human.BaseMana = 10; human.UsedMana = 0;
            match.Game.PowerHistory.Last.Clear();
            var fireball = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match);
            match.Game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, fireball, match.Game.Player2.Hero));
            FlushHistory(match);
            var counts = match.PetXpCounts;
            Check(counts.Medium >= 1, "counts available for settlement");
            var settled = fixture.Profile.Settle(fixture.Profile.Pending.Id, "Won", 6, false, petXp: counts);
            Check(settled.PetSmall == 0 && settled.PetMedium == counts.Medium && settled.PetLarge == 0 && settled.PetXpAfter == 10 * counts.Medium
                && settled.PetRules == PetXpPolicy.PresetB.RuleString(LocalShop.Default.PetXpCap(deathling), PetXpPolicy.PresetB.MaxTicks(9)), "the receipt records the counters and pays 10 XP per event");
        }
        // 3. No recipient (pet equipped but no pending PetXpBefore, e.g. a cheat-only pet): no counters, no fake XP display.
        using (var fixture = new LikeMatchFixture(false, deathling))
        {
            Check(fixture.Profile.Pending == null, "no pending match, no XP recipient");
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
            match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            var human = match.Game.Player1; human.BaseMana = 10; human.UsedMana = 0; match.Game.PowerHistory.Last.Clear();
            var fireball = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match);
            match.Game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, fireball, match.Game.Player2.Hero));
            match.Game.Turn = 9; FlushHistory(match);
            Check(match.PetLikes == null && match.PetXpCounts == null, "no durable recipient, no likes evaluator");
            Check(!NativeHistory(capture).Any(e => e.HasTagChange && e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078)
                && !NativeHistory(capture).Any(e => e.HasFullEntity && e.FullEntity.Tags.Any(t => t.Name >= 4076 && t.Name <= 4078)), "no XP counter tag is ever sent without a recipient");
            // Treats still work without a recipient but tick no XP.
            Check(match.ReceiveNativePetTreat(67), "treat feed accepted");
            Check(!NativeHistory(capture).Any(e => e.HasTagChange && e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078), "a treat feed without a recipient shows no XP");
        }
        // 4. Preset C (flat, no counters): nothing is evaluated or sent even with a recipient.
        var previousPolicy = PetXpPolicy.Current;
        try
        {
            PetXpPolicy.Current = PetXpPolicy.PresetC;
            using var fixture = new LikeMatchFixture(true, deathling);
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
            Check(match.PetLikes == null && match.PetXpCounts == null, "preset C evaluates no likes");
            Check(match.ReceiveNativePetTreat(67), "treats still feed under preset C");
            Check(!NativeHistory(capture).Any(e => e.HasTagChange && e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078), "preset C shows no XP");
            var settled = fixture.Profile.Settle(fixture.Profile.Pending.Id, "Won", 6, false);
            Check(settled.PetRules == "rule=1;xp=100;cap=6750" && settled.PetXpAfter == 100 && !settled.PetSmall.HasValue, "preset C settles the flat 100 under rule 1");
        }
        finally { PetXpPolicy.Current = previousPolicy; }
    }

    // The real engine's history shape (PLAY/ATTACK/DEATHS blocks, metadata, RESOURCES) feeds the same evaluator.
    private static void PetLikesRealEngine()
    {
        NativeMatch Start(LikeMatchFixture fixture, out Capture capture)
        {
            capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
            match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            match.Game.PowerHistory.Last.Clear();
            return match;
        }
        using (var fixture = new LikeMatchFixture(true, PetFamilyWithNative(3))) // King Krush: attack, attack-kill, board-full (PROPOSED list)
        {
            var match = Start(fixture, out _); var game = match.Game; var human = game.Player1;
            var mine = new List<SabberStoneCore.Model.Entities.Minion>();
            for (int i = 0; i < 7; i++)
            {
                var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_182"));
                SabberStoneCore.Actions.Generic.SummonBlock(game, minion, -1, null); mine.Add(minion);
            }
            var victim = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, SabberStoneCore.Model.Cards.FromId("CS2_172"));
            SabberStoneCore.Actions.Generic.SummonBlock(game, victim, -1, null);
            game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("board-full") && !match.PetLikes.Fired.Contains("attack-kill"), "seven real summons fill the board: " + string.Join(",", match.PetLikes.Fired));
            mine[0].IsExhausted = false; game.PowerHistory.Last.Clear();
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.MinionAttackTask.Any(human, mine[0], victim)), "real attack accepted");
            FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("minion-attacks") && match.PetLikes.Fired.Contains("attack-kill"),
                "a real attack that destroys an enemy minion fires minion-attacks and attack-kill: " + string.Join(",", match.PetLikes.Fired));
        }
        using (var fixture = new LikeMatchFixture(true, PetFamilyWithNative(15))) // Bigglesworth: damage-enemy-hero
        {
            var match = Start(fixture, out _); var game = match.Game; var human = game.Player1;
            human.BaseMana = 10; human.UsedMana = 0;
            var fireball = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CORE_CS2_029"), zone: human.HandZone);
            FlushHistory(match);
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, fireball, game.Player2.Hero)), "Fireball accepted");
            FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("damage-enemy-hero"), "a real hit on the enemy hero fires damage-enemy-hero: " + string.Join(",", match.PetLikes.Fired));
        }
        using (var fixture = new LikeMatchFixture(true, PetFamilyWithNative(7))) // Ysera: mana-10, heal-hero
        {
            var match = Start(fixture, out _); var game = match.Game; var human = game.Player1;
            human.BaseMana = 10; FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("mana-10"), "reaching ten mana crystals fires mana-10: " + string.Join(",", match.PetLikes.Fired));
            game.PowerHistory.Last.Clear();
            human.Hero.TakeDamage(game.Player2.Hero, 5); human.Hero.TakeHeal(human.Hero, 3); FlushHistory(match);
            Check(match.PetLikes.Fired.Contains("heal-hero"), "a real heal of my hero fires heal-hero");
        }
    }

    private static void PetTreatsNativeMatch()
    {
        string pet = PetFamilyWithNative(7);
        using var fixture = new LikeMatchFixture(true, pet);
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "warrior", 1337) { OpponentPetCard = null };
        Check(!match.ReceiveNativePetTreat(68) && !match.ReceiveNativePetTreat(67), "treat requests outside a running match are ignored");
        match.Start();
        byte[] profileBefore = File.ReadAllBytes(fixture.Path); // Treats must not change the match cosmetic snapshot.
        match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
        int petId = NativeHistory(capture).Single(e => e.HasFullEntity && e.FullEntity.Name == pet).FullEntity.Entity;
        int cursor = capture.Packets.Count;
        List<NativeHistory> Standalone() => capture.Packets.Skip(cursor).Where(p => p.Id == 19).SelectMany(p => ((PegasusGame.PowerHistory)p.Message).List).Where(e => e.HasTagChange).ToList();
        // Retail: generated 1 at creation; one request makes 2; a third is refused. Feeds are capped by min(2, generated).
        Check(match.ReceiveNativePetTreat(67), "a feed is accepted while a treat is generated");
        var first = Standalone();
        Check(first.Count == 1 && first[0].TagChange.Entity == petId && first[0].TagChange.Tag == 4280 && first[0].TagChange.Value == 1, "feed sets PET_TREATS_FED (its XP tick is banked until the gate)");
        Check(!match.ReceiveNativePetTreat(67), "FED never exceeds GENERATED: second feed with one generated treat is ignored");
        Check(match.ReceiveNativePetTreat(68), "the client's generate request is accepted");
        Check(Standalone().Last().TagChange.Tag == 4281 && Standalone().Last().TagChange.Value == 2, "generate sets PET_TREATS_GENERATED to 2");
        Check(!match.ReceiveNativePetTreat(68), "GENERATED is capped at 2");
        int packetsBefore = capture.Packets.Count;
        Check(match.ReceiveNativePetTreat(67), "second feed accepted"); Check(!match.ReceiveNativePetTreat(67), "FED is capped at 2");
        Check(capture.Packets.Skip(packetsBefore).Count(p => p.Id == 19) == 1, "an ignored request sends nothing");
        Check(!match.ReceiveNativePetTreat(72) && !match.ReceiveNativePetTreat(0), "other packets are ignored");
        // Each feed is one standalone list on the pet: FED, then one XP tick (banked until the gate, so none shown yet).
        Check(Standalone().Where(e => e.TagChange.Tag == 4280).Select(e => e.TagChange.Value).SequenceEqual(new[] { 1, 2 }), "FED rises 1 then 2");
        Check(match.PetXpCounts.Medium == 4 && match.PetXpCounts.Small == 0, "two feeds bank four MEDIUM ticks (20 XP per feed)");
        Check(!Standalone().Any(e => e.TagChange.Tag >= 4076 && e.TagChange.Tag <= 4078), "treat XP waits for the turn gate like a like");
        match.Game.PowerHistory.Last.Clear(); match.Game.Turn = 4; FlushHistory(match);
        var release = PetXpTags(Standalone(), petId);
        Check(release.Count == 1 && release[0][0] == 4077 && release[0][1] == 4, "both treats (four ticks) release together as one update at the gate");
        // Treats never touch the profile; they are presentation plus one XP tick.
        Check(File.ReadAllBytes(fixture.Path).SequenceEqual(profileBefore), "treats do not write the profile");
        // After the game: ignored.
        match.Concede();
        Check(match.Game.State == State.COMPLETE, "conceding completes the match");
        int over = capture.Packets.Count;
        Check(!match.ReceiveNativePetTreat(67) && !match.ReceiveNativePetTreat(68) && capture.Packets.Count == over, "treats after the match are ignored");
        capture.SerializeAll();
        Check(NativeUIProbeFeaturesCap() == 2, "NetCacheFeatures.MaxPetTreatsAllowedPerGame is the retail-inferred 2");
        // Rewind reinjection carries FED and GENERATED.
        using var second = new LikeMatchFixture(true, pet);
        var capture2 = new Capture();
        var match2 = new NativeMatch(capture2.Send, "mage", "warrior", 1337); match2.Start();
        match2.SendChoices(capture2.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
        match2.ReceiveNativePetTreat(68); match2.ReceiveNativePetTreat(67); match2.ReceiveNativePetTreat(67);
        var game = match2.Game;
        var append = typeof(NativeMatch).GetMethod("AppendRewindReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var entries = new List<E> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, game.Player1.Hero.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
        entries.AddRange(game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e => PowerHistoryBuilder.FullEntity(e, reset: true)));
        entries.Add(PowerHistoryBuilder.BlockEnd());
        var reset = new PegasusGame.PowerHistory(); append.Invoke(match2, new object[] { reset, entries, 0 });
        var reinjected = reset.List.Single(e => e.HasFullEntity && e.FullEntity.Name == pet).FullEntity;
        Check(PetTag(reinjected, 4281) == 2 && PetTag(reinjected, 4280) == 2, "rewind reinjection carries the treat counters");
    }

    private static int NativeUIProbeFeaturesCap() => NativeMatch.MaxPetTreatsPerGame;
}
