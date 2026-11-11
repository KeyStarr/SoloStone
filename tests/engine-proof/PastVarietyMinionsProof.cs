using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class PastVarietyMinionsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "Past minions: " + message);
    private static void Kill(Game game, Minion minion)
    { Core2026Proof.Kill(game, minion); game.ProcessTasks(); game.DeathProcessingAndAuraUpdate(); }
    internal static readonly string[] Ids = ("DRG_054 EX1_561 EX1_562 NEW1_030 BRM_004 AT_017 BRM_031 LOOT_137 " +
        "CS2_065 EX1_301 EX1_304 EX1_313 BRM_006 GVG_021 GVG_100 EX1_185 AT_023 CFM_751 DAL_185 " +
        "EX1_029 FP1_001 GVG_082 CS2_150 CS2_187 BOT_537 DRG_225 ULD_217").Split(' ');

    private static Card Generated(Game game, string id)
    {
        // Past minions retain the canonical legacy definition (only spells need a school clone).
        var result = OfflineCardPools.Candidates(game, game.Player1.HeroClass,
            OfflinePoolFilter.Random(scope: PoolScope.Past), 0).SingleOrDefault(c => c.Id == id);
        Check(result == null || ReferenceEquals(result, Cards.FromId(id)), id + " generated definition identity");
        return result ?? Cards.FromId(id);
    }

    [GroupProof("pastvar-minions")]
    internal static void Run()
    {
        Unpinned_DormantRandomEligibility();
        Unpinned_DormantAdjacency();
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var json = JsonDocument.Parse(gzip);
        var export = json.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        var failures = new List<string>();
        foreach (string token in ("EX1_116t EX1_561e BRM_004e AT_017e EX1_304e BRM_006t GVG_021e GVG_100e EX1_185e " +
            "PART_001 PART_001e PART_002 PART_003 PART_004 PART_004e PART_005 PART_006 PART_006a PART_007 PART_007e " +
            "BOT_537t DRG_225t DRG_225e ULD_217e").Split(' '))
        {
            var card = Cards.FromId(token); var row = export[token];
            Check(card.Implemented && card.AssetId == row.GetProperty("dbfId").GetInt32() && !card.Collectible, token + " real implemented dependency");
            foreach (string tag in new[] { "45", "47", "48", "190", "194", "197", "849", "1085", "2534" })
                Check(card[(GameTag)int.Parse(tag)] == (row.GetProperty("tags").TryGetProperty(tag, out var value) ? value.GetInt32() : 0), token + " pinned token stats/keyword " + tag);
        }
        foreach (string id in Ids)
        {
            try
            {
                var c = Cards.FromId(id); var row = export[id];
                Check(c.Implemented && c.Collectible && c.AssetId == row.GetProperty("dbfId").GetInt32(), id + " implemented identity");
                Check(c.Cost == row.GetProperty("cost").GetInt32() && c.ATK == row.GetProperty("atk").GetInt32()
                    && c.Health == row.GetProperty("health").GetInt32(), id + " current printed stats");
                Check(c.Races.Select(r => r.ToString()).ToHashSet().SetEquals(row.GetProperty("races").EnumerateArray().Select(r => r.GetString())), id + " current races");
                foreach (int tag in new[] { 190, 192, 194, 197, 217, 218, 403, 849, 1085, 2534 })
                    if (row.GetProperty("tags").TryGetProperty(tag.ToString(), out var expected) && expected.GetInt32() > 0)
                        Check(c[(GameTag)tag] == expected.GetInt32(), id + " keyword " + tag);
                foreach (int seed in new[] { 1, 9, 27 })
                foreach (int seat in new[] { 1, 2 })
                {
                    Scenario(id, seed, seat);
                }
                Boundaries(id);
                PoolProofEvidence.Passed(id, "PastVarietyMinionsProof.Run:" + id);
                Console.WriteLine("PASS qualified Past minion " + id);
            }
            catch (Exception e) { failures.Add(id + ": " + e.Message); }
        }
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        var poolGame = PastLegacyQualificationProof.New();
        foreach (var (name, filter, cls, destination) in new[] {
            ("END_027", OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DRAGON, scope: PoolScope.Past), CardClass.PRIEST, PoolDestination.Hand),
            ("TIME_444", OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON, scope: PoolScope.Past), CardClass.DEMONHUNTER, PoolDestination.Hand),
            ("TIME_446-Past", OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON, minCost: 5, scope: PoolScope.Past), CardClass.DEMONHUNTER, PoolDestination.Hand),
            ("TIME_016", new OfflinePoolFilter(PoolScope.Past, PoolClassMode.Exact, CardClass.PALADIN, CardType.MINION, race: Race.MECHANICAL), CardClass.PALADIN, PoolDestination.Hand),
            ("TIME_040", OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5, scope: PoolScope.Past), CardClass.MAGE, PoolDestination.Hand),
            ("TIME_711", OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1, scope: PoolScope.Past), CardClass.ROGUE, PoolDestination.Summon) })
            Console.WriteLine("POOL " + name + "=" + OfflineCardPools.Candidates(poolGame, cls, filter, 0, destination).Count);
    }

    [GroupProof("pastvar-minions-dormant-random")]
    internal static void Unpinned_DormantRandomEligibility()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (int activeCount in new[] { 0, 1, 2 })
        {
            var game = PastLegacyQualificationProof.New();
            if (seat == 2) End(game);
            var p = game.CurrentPlayer;
            var sleeper = Summon(p.Opponent, "YOD_010");
            DormantRules.Make(sleeper, 2, "TIME_058e", p.Opponent.Hero);
            for (int i = 0; i < activeCount; i++) Summon(p.Opponent, "YOD_010")[GameTag.REBORN] = 0;
            var spell = Generic.DrawCard(p, OfflineCardPools.Candidates(game, p.HeroClass,
                OfflinePoolFilter.Random(scope: PoolScope.Past), 0).Single(c => c.Id == "EX1_251"));
            foreach (var fork in new[] { game }.Concat(Forks(game)))
            {
                var owner = fork.CurrentPlayer;
                owner.BaseMana = 10; owner.UsedMana = 0;
                var source = fork.IdEntityDic[spell.Id];
                Check(owner.Options().Any(t => t.Source?.Id == source.Id) == (activeCount > 0),
                    $"Unpinned_DormantRandomEligibility Forked Lightning seat={seat}: active={activeCount}");
                Check(fork.Process(PlayCardTask.Any(owner, source)) == (activeCount > 0), "Forked Lightning explicit play legality");
                Check(activeCount > 0 || source.Zone == owner.HandZone && owner.UsedMana == 0 && owner.OverloadOwed == 0,
                    "rejected all-Dormant play preserves card, mana and Overload");
                Check(owner.Opponent.BoardZone.GetAll().Length == 0, "Forked Lightning hits every eligible active target");
                var untouched = (Minion)fork.IdEntityDic[sleeper.Id];
                Check(untouched.IsDormant && !untouched.ToBeDestroyed, "Forked Lightning never selects sleeper");
                DormantRules.Awaken(untouched); fork.DeathProcessingAndAuraUpdate();
                Check(untouched.Zone == untouched.Controller.BoardZone && untouched.AttackDamage == 2 && untouched.Health == 2,
                    "Forked Lightning sleeper unchanged after awakening");
                Equal(fork, "Forked Lightning Dormant selection and legality");
            }
        }
        Console.WriteLine("PASS Unpinned_DormantRandomEligibility: both seats, zero/one/two active enemies, clone/snapshot");
    }

    [GroupProof("pastvar-minions-dormant-adjacency")]
    internal static void Unpinned_DormantAdjacency()
    {
        // Same shipped guess as Unpinned_DormantExclusions; sleepers occupy slots but ignore effects.
        var failures = new List<string>();
        foreach (int seat in new[] { 1, 2 })
        foreach (bool sleepingLeft in new[] { false, true })
        foreach (bool activeNeighbor in new[] { false, true })
        {
            var game = PastLegacyQualificationProof.New();
            if (seat == 2) End(game);
            var owner = game.CurrentPlayer;
            var distant = sleepingLeft ? Summon(owner, "CS2_182") : null;
            Minion active = !sleepingLeft && activeNeighbor ? Summon(owner, "CS2_182") : null;
            var sleeper = Summon(owner, "YOD_010");
            DormantRules.Make(sleeper, 2, "TIME_058e", owner.Hero);
            if (sleepingLeft && activeNeighbor) active = Summon(owner, "CS2_182");
            if (!sleepingLeft) distant = Summon(owner, "CS2_182");
            if (active != null) active.Damage = 2;
            var source = Generic.DrawCard(owner, Generated(game, "EX1_304"));
            Equal(game, "Dormant adjacency before play");
            foreach (var fork in new[] { game }.Concat(Forks(game)))
            {
                try
                {
                    var p = fork.CurrentPlayer;
                    p.BaseMana = 10; p.UsedMana = 0;
                    var body = (Minion)fork.IdEntityDic[source.Id];
                    Check(fork.Process(PlayCardTask.Any(p, body, zonePosition: sleepingLeft ? 2 : activeNeighbor ? 1 : 0)), "Dormant Void Terror play");
                    Check(body.AttackDamage == (activeNeighbor ? 7 : 3)
                        && body.Health == (activeNeighbor ? 7 : 4),
                        $"Unpinned_DormantAdjacency seat={seat} left={sleepingLeft} active={activeNeighbor}: actual {body.AttackDamage}/{body.Health}");
                    Check(fork.IdEntityDic[distant.Id].Zone == p.BoardZone
                        && (active == null || fork.IdEntityDic[active.Id].Zone == p.GraveyardZone),
                        "only immediate active neighbor consumed; never skip over sleeper");
                    Equal(fork, "Dormant adjacency after play");
                    foreach (var resumed in new[] { fork }.Concat(Forks(fork)))
                    {
                        var untouched = (Minion)resumed.IdEntityDic[sleeper.Id];
                        Check(untouched.Zone == untouched.Controller.BoardZone && untouched.IsDormant && !untouched.ToBeDestroyed,
                            "Dormant neighbor untouched before awakening");
                        DormantRules.Awaken(untouched); resumed.DeathProcessingAndAuraUpdate();
                        Check(untouched.Zone == untouched.Controller.BoardZone && untouched.AttackDamage == 2 && untouched.Health == 2,
                            "Dormant neighbor survives unchanged after awakening");
                        Equal(resumed, "Dormant adjacency awakened");
                    }
                }
                catch (Exception error) { failures.Add(error.Message); }
            }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
        Console.WriteLine("PASS Unpinned_DormantAdjacency: both seats, both sides, active/distant controls, clone/snapshot and awakening");
    }

    private static void Equal(Game game, string id)
    {
        Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), id + " clone");
        var bytes = game.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(restored.SaveSnapshot()) && Canonical.State(game) == Canonical.State(restored), id + " snapshot bytes/state");
    }

    private static void Scenario(string id, int seed, int seat)
    {
        var g = PastLegacyQualificationProof.New(seed);
        if (seat == 2) End(g);
        var p = g.CurrentPlayer;
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        if (id is "BRM_004" or "AT_017") Add(p, "EX1_561");
        if (id == "NEW1_030") { Add(p, "CS2_182"); Add(p, "CS2_182"); }
        if (id == "EX1_561") { p.Opponent.Hero.Damage = 25; p.Opponent.Hero.Armor = 7; }
        if (id == "FP1_001") p.Opponent.Hero.Damage = 12;
        if (id == "EX1_304") friend.Damage = 2;
        var source = (Minion)Generic.DrawCard(p, Generated(g, id));
        if (source.IsRace(Race.DRAGON))
        {
            Check(DarkGifts.Registry[6].IsEligible(source) == (id is "DRG_054" or "EX1_561" or "EX1_562" or "NEW1_030" or "BRM_004" or "AT_017"), id + " Rude Awakening Battlecry eligibility");
            Check(DarkGifts.Registry[3].IsEligible(source) == !source.HasTaunt, id + " Bundled Up excludes existing Taunt");
            Check(DarkGifts.Registry[2].IsEligible(source) == (source.AttackDamage >= 3), id + " Short Claws Attack eligibility");
        }
        p.BaseMana = 10; p.UsedMana = 0;
        int deckBefore = p.DeckZone.Count, opponentHand = p.Opponent.HandZone.Count;
        ICharacter target = id == "EX1_561" ? p.Opponent.Hero : id == "CS2_150" ? enemy : null;
        Check(g.Process(PlayCardTask.Any(p, source, target)), id + " generated play");
        switch (id)
        {
            case "DRG_054": Check(p.DeckZone.Count == deckBefore - 1 && p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_182" && p.Opponent.HandZone.Count == opponentHand, "Whelp draws actual top card for owner only"); break;
            case "EX1_561": Check(p.Opponent.Hero.Health == 15 && p.Opponent.Hero.Armor == 7 && p.Opponent.Hero.BaseHealth == 30, "Alex remaining Health, maximum and Armor"); break;
            case "EX1_562": Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "EX1_116t" && m.AttackDamage == 1 && m.Health == 1) == 5, "Onyxia fills free slots"); break;
            case "NEW1_030": Check(p.BoardZone.Count == 1 && source.Zone == p.BoardZone && p.Opponent.BoardZone.Count == 0 && p.HandZone.Count == 0, "Deathwing survives, clears others and hand"); break;
            case "BRM_004": Check(source.Health == 3, "Whelp holding Dragon +2 Health"); break;
            case "AT_017": Check(source.AttackDamage == 3 && source.HasTaunt, "Guardian holding Dragon +1 Attack Taunt"); break;
            case "BRM_031":
                int hand = p.HandZone.Count;
                new DrawTask().Process(g, p, p.Hero, null);
                Check(p.HandZone.Count == hand + 2 && p.HandZone.TakeLast(2).All(m => m.Card.Id == "CS2_182"), "Chromaggus draws and copies"); break;
            case "LOOT_137": case "CS2_065": case "CS2_187": Check(source.HasTaunt, id + " printed Taunt"); break;
            case "EX1_301": Check(p.BaseMana == 9 && source.HasTaunt, "Felguard destroys crystal"); break;
            case "EX1_304": Check(friend.Zone == p.GraveyardZone && source.AttackDamage == 7 && source.Health == 7, "Void Terror consumes current 4/3 neighbor"); break;
            case "EX1_313": Check(p.Hero.Damage == 5, "Pit Lord damages owner"); break;
            case "BRM_006":
                new DamageTask(1, EntityType.TARGET).Process(g, p, p.Hero, source); g.DeathProcessingAndAuraUpdate();
                Check(p.BoardZone.Any(m => m.Card.Id == "BRM_006t" && m.AttackDamage == 1 && m.Health == 1), "Gang Boss damage creates Imp"); break;
            case "GVG_021":
                var demon = Summon(p, "CS2_065");
                Check(demon.AttackDamage == 3 && demon.Health == 5 && p.Hero.IsImmune && !p.Opponent.Hero.IsImmune && friend.AttackDamage == 4, "Mal'Ganis other Demons and owner immunity"); break;
            case "GVG_100":
                new DamageTask(1, EntityType.HERO).Process(g, p, p.Hero, null); g.DeathProcessingAndAuraUpdate();
                Check(source.AttackDamage == 6 && source.Health == 6, "Watcher owner turn damage +2/+2"); break;
            case "EX1_185":
                var other = Summon(p, "CS2_065");
                Check(source.HasTaunt && source.AttackDamage == 5 && other.AttackDamage == 2 && friend.AttackDamage == 4, "Siegebreaker other Demons only"); break;
            case "AT_023":
                var task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
                p.UsedMana = 0; Check(g.Process(task), "Void Crusher native hero power");
                Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 0, "Void Crusher destroys one minion per player"); break;
            case "CFM_751": Check(friend.Damage == 3 && enemy.Damage == 3 && p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3 && source.Damage == 0, "Enforcer all other characters"); break;
            case "DAL_185":
                Check(source.HasTaunt, "Broodmother Taunt"); p.Hero.Damage = 9;
                foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
                p.DeckZone.Add(Entity.FromCard(p, Generated(g, id)));
                new DrawTask().Process(g, p, p.Hero, null);
                Check(p.Hero.Damage == 5 && p.HandZone.Any(c => c.Card.Id == id), "Broodmother drawn heals 4 and stays in hand"); break;
            case "EX1_029": Kill(g, source); Check(p.Opponent.Hero.Damage == 2, "Leper death damages enemy"); break;
            case "FP1_001": Kill(g, source); Check(p.Opponent.Hero.Damage == 7, "Chow death heals enemy 5"); break;
            case "GVG_082":
                var parts = Cards.FromId(id).Entourage;
                string expectedPart = parts[g.Random.Clone().Next(parts.Length)];
                Kill(g, source); Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == expectedPart, "Gnome exact seeded Spare Part among seven"); break;
            case "CS2_150": Check(enemy.Damage == 2, "Commando deals 2 to chosen target"); break;
            case "BOT_537": Kill(g, source); Check(p.BoardZone.Any(m => m.Card.Id == "BOT_537t" && m.AttackDamage == 8 && m.Health == 8), "Egg summons real 8/8 Robosaur"); break;
            case "DRG_225": Check(p.BoardZone.Count(m => m.Card.Id == "DRG_225t" && m.AttackDamage == 2 && m.Health == 1) == 2 && source.AttackDamage == 1 && friend.AttackDamage == 4, "Sky Claw two buffed Microcopters"); break;
            case "ULD_217": End(g); Check(friend.AttackDamage == 5 && source.AttackDamage == 1, "Mummy buffs another friendly at owner turn end"); break;
            default: throw new Exception("missing card effect assertion " + id);
        }
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Equal(g, id);
        if (seed == 1 && seat == 1 && source.Zone == source.Controller.BoardZone) Persistent(g, source);
    }

    private static IEnumerable<Game> Forks(Game game) => new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) };

    private static void Persistent(Game game, Minion source)
    {
        foreach (var g in Forks(game))
        {
            var m = (Minion)g.IdEntityDic[source.Id]; var p = m.Controller;
            if (g.CurrentPlayer != p) End(g);
            int attack = m.AttackDamage, health = m.Health;
            Play(g, p, "TIME_001");
            Check(g.Rewind != null, "real Rewind offer with " + m.Card.Id);
            var decision = g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
                new[] { g.Rewind.RewindEntityId }), out var error), "Rewind retry " + error);
            if (g.Rewind != null)
            {
                decision = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
                    new[] { g.Rewind.KeepEntityId }), out error), "Rewind keep " + error);
            }
            m = (Minion)g.IdEntityDic[source.Id];
            Check(m.AttackDamage == attack && m.Health == health && m.Zone == m.Controller.BoardZone, m.Card.Id + " Rewind preserves minion state");
            m.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(m.AttackDamage == m.Card.ATK && !m.HasTaunt && !m.HasDivineShield && m[GameTag.REBORN] == 0,
                m.Card.Id + " Silence strips text and personal buffs");
            Equal(g, m.Card.Id + " after Silence");
            Check(g.Process(ConcedeTask.Any(m.Controller)), "Concede remains available");
        }
    }

    private static void Boundaries(string id)
    {
        if (id is "BRM_004" or "AT_017")
        {
            var g = PastLegacyQualificationProof.New(); var p = g.Player1;
            var m = (Minion)Play(g, p, id);
            Check(m.Health == m.Card.Health && m.AttackDamage == m.Card.ATK && !m.HasTaunt, id + " cannot count itself as held Dragon");
        }
        if (id is "GVG_021" or "EX1_185" or "DRG_225")
        {
            var g = PastLegacyQualificationProof.New(); var p = g.Player1;
            var m = (Minion)Play(g, p, id);
            var ally = Summon(p, id == "DRG_225" ? "BOT_537" : "CS2_065");
            var enemy = Summon(p.Opponent, ally.Card.Id);
            int bonus = id == "GVG_021" ? 2 : 1;
            foreach (var fork in Forks(g))
            {
                var owner = fork.Player1; var body = (Minion)fork.IdEntityDic[m.Id];
                var target = (Minion)fork.IdEntityDic[ally.Id];
                Check(target.AttackDamage == target.Card.ATK + bonus && ((Minion)fork.IdEntityDic[enemy.Id]).AttackDamage == enemy.Card.ATK, id + " friendly aura only");
                new ControlTask(EntityType.TARGET).Process(fork, owner.Opponent, owner.Opponent.Hero, body);
                fork.ProcessTasks(); fork.DeathProcessingAndAuraUpdate();
                Check(target.AttackDamage == target.Card.ATK && ((Minion)fork.IdEntityDic[enemy.Id]).AttackDamage == enemy.Card.ATK + bonus, id + " aura follows control");
                Check(id != "GVG_021" || !owner.Hero.IsImmune && owner.Opponent.Hero.IsImmune, "immunity follows controller");
                Check(Generic.ReturnToHandBlock(owner.Opponent, body), id + " bounce"); fork.DeathProcessingAndAuraUpdate();
                Check(((Minion)fork.IdEntityDic[enemy.Id]).AttackDamage == enemy.Card.ATK && !owner.Opponent.Hero.IsImmune, id + " aura leaves play");
                Equal(fork, id + " control and bounce");
            }
        }
        if (id is "BRM_031" or "GVG_100" or "BRM_006" or "ULD_217") TriggerBoundaries(id);
        if (id is "EX1_029" or "FP1_001" or "GVG_082" or "BOT_537" or "ULD_217")
        {
            foreach (bool silence in new[] { false, true })
            {
                var g = PastLegacyQualificationProof.New(); var p = g.Player1; var m = (Minion)Play(g, p, id);
                while (!p.HandZone.IsFull) Add(p, "CS2_182");
                while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
                if (silence) m.Silence();
                Kill(g, m);
                Check(p.HandZone.Count == 10, id + " full-hand capacity");
                if (id == "BOT_537") Check(p.BoardZone.Count(c => c.Card.Id == "BOT_537t") == (silence ? 0 : 1), "Egg vacated slot or silenced no summon");
                if (id == "ULD_217") Check(p.BoardZone.Count(c => c.Card.Id == id) == (silence ? 0 : 1)
                    && p.BoardZone.Where(c => c.Card.Id == id).All(c => c.Health == 1 && c[GameTag.REBORN] == 0), "Mummy Reborn consumed; Silence prevents");
                if (id == "EX1_029") Check(p.Opponent.Hero.Damage == (silence ? 0 : 2), "Leper Silence prevents death damage");
                Equal(g, id + " full capacities and Silence");
            }
        }
        if (id == "DRG_054")
        {
            foreach (bool emptyDeck in new[] { false, true })
            foreach (int seat in new[] { 1, 2 })
            {
                var g = PastLegacyQualificationProof.New(); if (seat == 2) End(g);
                var p = g.CurrentPlayer;
                if (emptyDeck)
                    foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
                var source = (Minion)Generic.DrawCard(p, Generated(g, id));
                while (!p.HandZone.IsFull) Add(p, "CS2_182");
                p.BaseMana = 10; p.UsedMana = 0;
                int deck = p.DeckZone.Count, damage = p.Hero.Damage, fatigue = p.Hero.Fatigue;
                Check(g.Process(PlayCardTask.Any(p, source)), "Whelp full-hand generated play");
                Check(p.HandZone.Count == (emptyDeck ? 9 : 10) && p.DeckZone.Count == (emptyDeck ? 0 : deck - 1), "Whelp freed hand slot draws once or empty deck stays empty");
                Check(p.Hero.Damage == damage + (emptyDeck ? fatigue + 1 : 0), "Whelp empty deck deals actual fatigue");
                source.Silence(); int hand = p.HandZone.Count;
                End(g); Check(p.HandZone.Count == hand, "Whelp Battlecry never repeats on opponent turn");
                Equal(g, "Whelp full hand/empty deck both seats");
            }
        }
        if (id == "EX1_562")
        {
            var g = PastLegacyQualificationProof.New(); for (int i = 0; i < 6; i++) Summon(g.Player1, "CS2_182");
            Play(g, g.Player1, id); Check(g.Player1.BoardZone.Count == 7 && g.Player1.BoardZone.All(c => c.Card.Id != "EX1_116t"), "Onyxia no free slots");
            var extra = Add(g.Player1, id); g.Player1.UsedMana = 0;
            Check(g.Player1.Options().All(task => task.PlayerTaskType != PlayerTaskType.PLAY_CARD || task.Source?.Id != extra.Id), "full board offers no extra minion play");
        }
        if (id == "EX1_304")
        {
            foreach (bool neighbors in new[] { false, true })
            {
                var g = PastLegacyQualificationProof.New(); var p = g.Player1;
                Minion left = null, right = null, distant = null;
                if (neighbors) { left = Summon(p, "CS2_182"); left.Damage = 2; right = Summon(p, "CS2_065"); distant = Summon(p, "CS2_182"); }
                var body = (Minion)Generic.DrawCard(p, Generated(g, id)); p.BaseMana = 10; p.UsedMana = 0;
                Check(g.Process(PlayCardTask.Any(p, body, zonePosition: neighbors ? 1 : 0)), "Void Terror positional play");
                Check(body.AttackDamage == (neighbors ? 8 : 3) && body.Health == (neighbors ? 10 : 4) && !body.HasTaunt,
                    "Void Terror zero/two adjacent current stats, no keyword theft");
                Check(!neighbors || left.Zone == p.GraveyardZone && right.Zone == p.GraveyardZone && distant.Zone == p.BoardZone, "Void Terror only immediate neighbors");
                Equal(g, "Void Terror neighbor boundary");
            }
        }
        if (id == "AT_023")
        {
            foreach (int seed in new[] { 1, 9, 27 })
            {
                var g = PastLegacyQualificationProof.New(seed); var p = g.Player1; Play(g, p, id);
                for (int i = 0; i < 3; i++) { Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_182"); }
                foreach (var fork in Forks(g))
                {
                    var owner = fork.Player1; var random = fork.Random.Clone();
                    int friendly = owner.BoardZone[random.Next(owner.BoardZone.Count)].Id;
                    int enemy = owner.Opponent.BoardZone[random.Next(owner.Opponent.BoardZone.Count)].Id;
                    owner.BaseMana = 10; owner.UsedMana = 0;
                    Check(fork.Process(owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER)), "Crusher hero power");
                    Check(fork.IdEntityDic[friendly].Zone == owner.GraveyardZone && fork.IdEntityDic[enemy].Zone == owner.Opponent.GraveyardZone,
                        "Crusher exact seeded ordered picks from four/three candidates");
                    Equal(fork, "Crusher seeded result");
                }
            }
        }
    }

    private static void TriggerBoundaries(string id)
    {
        foreach (int seed in new[] { 1, 9, 27 })
        {
            var g = PastLegacyQualificationProof.New(seed); var p = g.Player1;
            var m = (Minion)Play(g, p, id);
            var friends = Enumerable.Range(0, 3).Select(_ => Summon(p, "CS2_182")).ToArray();
            foreach (var fork in Forks(g))
            {
                var body = (Minion)fork.IdEntityDic[m.Id]; var owner = fork.Player1;
                if (id == "ULD_217")
                {
                    int expected = friends[fork.Random.Clone().Next(3)].Id;
                    End(fork);
                    Check(friends.All(c => ((Minion)fork.IdEntityDic[c.Id]).AttackDamage == 4 + (c.Id == expected ? 1 : 0)), "Mummy exact seeded target among three");
                    int attack = friends.Sum(c => ((Minion)fork.IdEntityDic[c.Id]).AttackDamage);
                    End(fork); Check(friends.Sum(c => ((Minion)fork.IdEntityDic[c.Id]).AttackDamage) == attack, "opponent end does not trigger Mummy");
                }
                if (id == "GVG_100")
                {
                    new DamageTask(1, EntityType.OP_HERO).Process(fork, owner, owner.Hero, null); fork.ProcessTasks();
                    Check(body.AttackDamage == 4, "opponent hero damage does not trigger Watcher");
                    End(fork); new DamageTask(1, EntityType.HERO).Process(fork, owner, owner.Hero, null); fork.ProcessTasks();
                    Check(body.AttackDamage == 4, "owner damage on opponent turn does not trigger Watcher");
                }
                if (id == "BRM_031")
                {
                    int hand = owner.HandZone.Count; new DrawTask().Process(fork, owner.Opponent, owner.Opponent.Hero, null);
                    Check(owner.HandZone.Count == hand, "opponent draw does not trigger Chromaggus");
                    while (owner.HandZone.Count < 9) Add(owner, "CS2_182");
                    new DrawTask().Process(fork, owner, owner.Hero, null); fork.ProcessTasks();
                    Check(owner.HandZone.Count == 10, "Chromaggus full-hand copy burns");
                }
                if (id == "BRM_006")
                {
                    while (!owner.BoardZone.IsFull) Summon(owner, "CS2_182");
                    new DamageTask(1, EntityType.TARGET).Process(fork, owner, owner.Hero, body); fork.ProcessTasks(); fork.DeathProcessingAndAuraUpdate();
                    Check(owner.BoardZone.Count == 7 && owner.BoardZone.All(c => c.Card.Id != "BRM_006t"), "Gang Boss full board loses summon");
                }
                body.Silence(); fork.DeathProcessingAndAuraUpdate();
                int count = owner.HandZone.Count, board = owner.BoardZone.Count;
                if (id == "BRM_031") { foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card)); new DrawTask().Process(fork, owner, owner.Hero, null); Check(owner.HandZone.Count == 1, "silenced Chromaggus no copy"); }
                if (id == "BRM_006") { Kill(fork, (Minion)fork.IdEntityDic[friends[0].Id]); new DamageTask(1, EntityType.TARGET).Process(fork, owner, owner.Hero, body); fork.ProcessTasks(); Check(owner.BoardZone.Count == board - 1, "silenced Gang Boss no Imp"); }
                if (id == "GVG_100") { if (fork.CurrentPlayer != owner) End(fork); new DamageTask(1, EntityType.HERO).Process(fork, owner, owner.Hero, null); fork.ProcessTasks(); Check(body.AttackDamage == 4, "silenced Watcher no growth"); }
                if (id == "ULD_217") { int sum = friends.Sum(c => ((Minion)fork.IdEntityDic[c.Id]).AttackDamage); End(fork); Check(friends.Sum(c => ((Minion)fork.IdEntityDic[c.Id]).AttackDamage) == sum, "silenced Mummy no buff"); }
                fork.DeathProcessingAndAuraUpdate(); Equal(fork, id + " resumed trigger lifecycle");
            }
        }
    }
}
