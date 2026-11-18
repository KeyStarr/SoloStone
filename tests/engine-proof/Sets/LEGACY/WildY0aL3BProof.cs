using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

// Wild wave Y0a, lane l3b: Legacy Rogue, Mage and Warlock cards. The group proof is declared in
// Sets/EXPERT1/WildY0aL3BProof.cs (group names are unique) and calls Run here.
internal static class WildY0aL3BLegacyProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case (used by Standard until #87).
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    private const string Yeti = "CS2_182", Squire = "EX1_008", Thalnos = "EX1_012", Voidwalker = "CS2_065", Coin = "GAME_005";

    internal static void Run()
    {
        WildSetsProof.Case("CS2_080", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti);
            Play(g, p, id);
            var blade = p.Hero.Weapon;
            Core2026Proof.Check(blade != null && blade.Card.Id == id && blade.AttackDamage == 2 && blade.Durability == 5 && p.Hero.AttackDamage == 2,
                "Assassin's Blade equips a 2/5 weapon with no other effect");
            HeroAttack(g, p, enemy);
            Core2026Proof.Check(enemy.Damage == 2 && p.Hero.Damage == 4 && blade.Durability == 4, "the hero attacks with 2 Attack and the blade loses 1 Durability");
            End(g); End(g);
            HeroAttack(g, p, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && blade.Durability == 3, "the blade hits the enemy hero");
            End(g); Play(g, p.Opponent, id);
            Core2026Proof.Check(p.Opponent.Hero.Weapon.Durability == 5 && p.Hero.Weapon.Durability == 3 && p.Hero.Weapon == blade,
                "the opponent's Assassin's Blade is independent of mine");
            End(g);
            Play(g, p, id);
            Core2026Proof.Check(p.Hero.Weapon != blade && p.Hero.Weapon.Durability == 5 && blade.Zone.Type != Zone.PLAY, "a new Assassin's Blade replaces the old weapon");
            p.Hero.Weapon.Durability = 1; HeroAttack(g, p, p.Opponent.Hero);
            Core2026Proof.Check(p.Hero.Weapon == null, "the blade breaks at 0 Durability");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_129", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var shield = Summon(p.Opponent, Squire); var mine = Summon(p, Yeti);
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, oHand = p.Opponent.HandZone.Count, oDeck = p.Opponent.DeckZone.Count;
            Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 1 && shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && shield.Damage == 0,
                "Fan of Knives deals 1 to each enemy minion (Divine Shield absorbs)");
            Core2026Proof.Check(mine.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Fan of Knives spares friendly minions and both heroes");
            Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1 && p.Opponent.HandZone.Count == oHand && p.Opponent.DeckZone.Count == oDeck,
                "Fan of Knives draws exactly one for its owner");
            Summon(p, Thalnos);
            Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 3 && shield.Zone.Type == Zone.GRAVEYARD, "Fan of Knives scales with Spell Damage and kills 1 Health minions");
            StandardSetsProof.ParkBoard(p.Opponent);
            hand = p.HandZone.Count; deck = p.DeckZone.Count;
            Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1, "Fan of Knives draws with no enemy minion");
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            Play(g, p, id);
            Core2026Proof.Check(p.Hero.Damage == 1, "Fan of Knives on an empty deck takes fatigue");
            End(g);
            var theirs = Summon(p, Squire);
            Play(g, p.Opponent, id);
            Core2026Proof.Check(theirs.Zone.Type == Zone.PLAY && !theirs.HasDivineShield && p.Opponent.DeckZone.Count == oDeck - 2, "Opponent's Fan of Knives hits my minions and draws from their deck");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_179", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); var shield = Summon(p.Opponent, Squire);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 3 && targets.All(t => t is Minion), "Icicle targets any minion, never a hero");
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, oHand = p.Opponent.HandZone.Count;
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Damage == 2 && p.HandZone.Count == hand - 1 && p.DeckZone.Count == deck, "Icicle deals 2 and draws nothing to an unfrozen minion");
            enemy.IsFrozen = true;
            hand = p.HandZone.Count;
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Damage == 4 && p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1 && p.Opponent.HandZone.Count == oHand,
                "Icicle draws one card from its owner's deck when the target is Frozen");
            Play(g, p, "CS2_026");
            Core2026Proof.Check(shield.IsFrozen, "Frost Nova Freezes the enemy Divine Shield minion");
            hand = p.HandZone.Count;
            Play(g, p, id, shield);
            Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.HandZone.Count == hand + 1, "Icicle pops Divine Shield and still draws from a Frozen target");
            Summon(p, Thalnos);
            enemy.Damage = 2; hand = p.HandZone.Count;
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1, "Icicle kills a Frozen minion (Spell Damage +1) and still draws");
            mine.IsFrozen = true; hand = p.HandZone.Count;
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.Damage == 3 && p.HandZone.Count == hand + 1, "Icicle draws for a Frozen friendly minion too");
            End(g); End(g);
            var thawed = Summon(p.Opponent, Yeti); hand = p.HandZone.Count;
            Play(g, p, id, thawed);
            Core2026Proof.Check(p.HandZone.Count == hand, "A minion that is no longer Frozen draws nothing");
            thawed.IsFrozen = true;
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            int damage = p.Hero.Damage;
            Play(g, p, id, thawed);
            Core2026Proof.Check(p.Hero.Damage == damage + 1, "Icicle on an empty deck draws into fatigue");
            End(g);
            var theirs = Summon(p, Yeti); theirs.IsFrozen = true; int oDeck = p.Opponent.DeckZone.Count;
            Play(g, p.Opponent, id, theirs);
            Core2026Proof.Check(p.Opponent.DeckZone.Count == oDeck - 1, "Opponent's Icicle draws from their own deck");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_180", (g, p, id) => {
            int oHand = p.Opponent.HandZone.Count, ownDeck = p.DeckZone.Count;
            var twin = g.CloneExact(history: true);
            var cards = Casts(g, p, id, 8);
            Core2026Proof.Check(cards.All(c => c.Type == CardType.SPELL && c.Class == CardClass.MAGE && c.Collectible),
                "Tome of Intellect only adds collectible Wild Mage spells: " + Describe(cards));
            Core2026Proof.Check(cards.Select(c => c.Id).Distinct().Count() >= 3, "Tome of Intellect shows at least three different spells over eight casts");
            Core2026Proof.Check(p.Opponent.HandZone.Count == oHand, "Tome of Intellect adds nothing to the opponent's hand");
            Core2026Proof.Check(Casts(twin, twin.Player1, id, 8).Select(c => c.Id).SequenceEqual(cards.Select(c => c.Id)), "Tome of Intellect is deterministic on an exact clone");
            Core2026Proof.Check(p.Hero.Damage == 0 && p.DeckZone.Count == ownDeck, "Tome of Intellect does not touch the deck");
            End(g);
            var theirs = Casts(g, p.Opponent, id, 4);
            Core2026Proof.Check(theirs.All(c => c.Class == CardClass.MAGE && c.Type == CardType.SPELL), "Opponent's Tome adds Mage spells to their hand");
        });
        WildSetsProof.Case("EX1_181", (g, p, id) => {
            var pool = RandomCardTask.GetCardList(p.Hero, CardType.MINION, CardClass.INVALID, race: Race.DEMON);
            PinRandomPool(id, pool, 58, CardSet.THE_LOST_CITY);
            int oHand = p.Opponent.HandZone.Count, ownDeck = p.DeckZone.Count;
            var twin = g.CloneExact(history: true);
            var cards = Casts(g, p, id, 8);
            Core2026Proof.Check(cards.All(c => c.Type == CardType.MINION && c.IsRace(Race.DEMON) && c.Collectible),
                "Call of the Void only adds collectible Wild Demons: " + Describe(cards));
            Core2026Proof.Check(cards.Select(c => c.Id).Distinct().Count() >= 3, "Call of the Void shows at least three different Demons over eight casts");
            Core2026Proof.Check(p.Opponent.HandZone.Count == oHand, "Call of the Void adds nothing to the opponent's hand");
            Core2026Proof.Check(Casts(twin, twin.Player1, id, 8).Select(c => c.Id).SequenceEqual(cards.Select(c => c.Id)), "Call of the Void is deterministic on an exact clone");
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.DeckZone.Count == ownDeck, "Call of the Void does not summon or draw");
            End(g);
            var theirs = Casts(g, p.Opponent, id, 4);
            Core2026Proof.Check(theirs.All(c => c.Type == CardType.MINION && c.IsRace(Race.DEMON)), "Opponent's Call of the Void adds Demons to their hand");
        });
        WildSetsProof.Case("EX1_182", (g, p, id) => {
            foreach (var heroClass in new[] { CardClass.ROGUE, CardClass.MAGE })
            {
                var classes = new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN,
                    CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR,
                    CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT };
                var pool = classes.Where(cardClass => cardClass != heroClass)
                    .SelectMany(cardClass => Cards.Wild[cardClass].Where(c => c.Class == cardClass && c.Collectible && c.Implemented
                        && c.Type != CardType.HERO && c[GameTag.QUEST] == 0)).ToArray();
                PinRandomPool(id + "/" + heroClass, pool, 1335, (CardSet)1994); // BE
            }
            int oHand = p.Opponent.HandZone.Count;
            var twin = g.CloneExact(history: true);
            var cards = Casts(g, p, id, 40);
            Core2026Proof.Check(cards.All(c => c.Class != CardClass.ROGUE && c.Class != CardClass.NEUTRAL && c.Collectible),
                "Pilfer adds a collectible Wild card from a class other than mine, never Neutral: " + Describe(cards));
            Core2026Proof.Check(cards.Select(c => c.Class).Distinct().Count() >= 3, "Pilfer draws from several other classes, not just the opponent's (Mage)");
            Core2026Proof.Check(cards.Any(c => c.Class != CardClass.MAGE), "Pilfer is not limited to the opponent's class");
            Core2026Proof.Check(p.Opponent.HandZone.Count == oHand, "Pilfer adds nothing to the opponent's hand");
            Core2026Proof.Check(Casts(twin, twin.Player1, id, 40).Select(c => c.Id).SequenceEqual(cards.Select(c => c.Id)), "Pilfer is deterministic on an exact clone");
            End(g);
            var theirs = Casts(g, p.Opponent, id, 60);
            Core2026Proof.Check(theirs.All(c => c.Class != CardClass.MAGE && c.Class != CardClass.NEUTRAL), "Opponent's Pilfer excludes their own class (Mage) and Neutral");
            Core2026Proof.Check(theirs.Select(c => c.Class).Distinct().Count() >= 3 && theirs.Any(c => c.Class == CardClass.ROGUE),
                "Opponent's Pilfer also spans several other classes, including the Rogue that cast the first one");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_185", (g, p, id) => {
            var voidwalker = Summon(p, Voidwalker); var yeti = Summon(p, Yeti); var enemyDemon = Summon(p.Opponent, Voidwalker);
            var heldDemon = Add(p, Voidwalker);
            var source = (Minion)Play(g, p, id);
            Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 8 && source.HasTaunt && source.Card.IsRace(Race.DEMON), "Siegebreaker is a 5/8 Taunt Demon");
            Core2026Proof.Check(voidwalker.AttackDamage == 2 && yeti.AttackDamage == 4 && enemyDemon.AttackDamage == 1 && heldDemon.Card.ATK == 1,
                "Siegebreaker gives only other friendly Demons on the board +1 Attack");
            var later = Summon(p, Voidwalker);
            Core2026Proof.Check(later.AttackDamage == 2, "A Demon summoned later is also buffed");
            var second = (Minion)Play(g, p, id);
            Core2026Proof.Check(source.AttackDamage == 6 && second.AttackDamage == 6 && voidwalker.AttackDamage == 3, "Two Siegebreakers buff each other and stack on other Demons");
            WildY0aL3ALegacyProof.Resume(g, next => {
                Core2026Proof.Check(((Minion)next.IdEntityDic[voidwalker.Id]).AttackDamage == 3 && ((Minion)next.IdEntityDic[source.Id]).AttackDamage == 6, "Aura survives clone and snapshot");
                Kill(next, (Minion)next.IdEntityDic[second.Id]);
                Core2026Proof.Check(((Minion)next.IdEntityDic[voidwalker.Id]).AttackDamage == 2 && ((Minion)next.IdEntityDic[source.Id]).AttackDamage == 5, "Aura ends when a Siegebreaker dies");
            });
            second.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(second.AttackDamage == 6 && !second.HasTaunt && voidwalker.AttackDamage == 2 && source.AttackDamage == 5,
                "Silence removes the Taunt and the aura of that Siegebreaker only; the other one's aura still applies to it");
            Kill(g, source);
            Core2026Proof.Check(voidwalker.AttackDamage == 1 && later.AttackDamage == 1 && second.AttackDamage == 5, "Death ends the aura");
            End(g);
            var theirs = (Minion)Play(g, p.Opponent, id);
            Core2026Proof.Check(enemyDemon.AttackDamage == 2 && voidwalker.AttackDamage == 1 && theirs.AttackDamage == 5, "Opponent's Siegebreaker buffs only their Demons");
        });
        // held: used by Standard (Victor Nefarius candidate list in engine/.../CATACLYSM/VictorNefarius.cs) until #87
        HeldForStandard("EX1_191", (g, p, id) => {
            var friend = Summon(p, Squire); var enemy = Summon(p.Opponent, Yeti);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held).Distinct().ToList();
            Core2026Proof.Check(targets.Count == 1 && targets[0] == friend, "Plaguebringer targets friendly minions only: no enemy, no hero n=" + targets.Count + " " + string.Join(",", targets.Select(t => t.Card.Id)));
            var bringer = (Minion)PlayAndReturn(g, p, held, friend);
            Core2026Proof.Check(bringer.AttackDamage == 3 && bringer.Health == 3 && bringer.Card.IsRace(Race.UNDEAD) && friend.Poisonous && !bringer.Poisonous && !enemy.Poisonous,
                "Plaguebringer is a 3/3 Undead and gives its target Poisonous");
            Attack(g, friend, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "the Poisonous minion kills a 5 Health minion");
            var other = Summon(p, Yeti);
            Play(g, p, id, bringer);
            Core2026Proof.Check(bringer.Poisonous, "Plaguebringer can give Poisonous to another Plaguebringer");
            End(g); End(g);
            Core2026Proof.Check(friend.Poisonous, "Poisonous is permanent");
            friend.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!friend.Poisonous, "Silence removes the Poisonous");
            Play(g, p, id, other);
            Core2026Proof.Check(other.Poisonous, "Plaguebringer targets a Yeti");
            StandardSetsProof.ParkBoard(p);
            var lone = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 1 && !lone.Poisonous, "Plaguebringer is playable with no target and cannot buff itself");
            End(g);
            var enemyMinion = Summon(p.Opponent, Squire);
            Play(g, p.Opponent, id, enemyMinion);
            Core2026Proof.Check(enemyMinion.Poisonous && !lone.Poisonous && other.Poisonous && enemyMinion.Controller == p.Opponent, "Opponent's Plaguebringer buffs only their minion");
        });
    }

    // PINNED GAP #141: frozen memberships can resolve to modern identities; Pilfer also includes live DH/DK lists.
    private static void PinRandomPool(string name, IReadOnlyCollection<Card> pool, int expectedSize, CardSet expectedNewest)
    {
        var newest = (CardSet)pool.Max(c => (int)c.Set);
        Console.WriteLine($"PINNED GAP #141 {name}: size={pool.Count}; newest-set={newest} ({(int)newest})");
        Core2026Proof.Check(pool.Count == expectedSize && newest == expectedNewest,
            "PINNED GAP #141 " + name + " pool changed; review format-aware generation and update this pin");
    }

    // Plays the held Plaguebringer on a target and returns the minion it became.
    private static IPlayable PlayAndReturn(Game g, Controller p, IPlayable held, ICharacter target)
    {
        PlayEntity(g, p, held, target);
        return p.BoardZone.Single(m => m.Card.Id == held.Card.Id);
    }

    private static string Describe(List<Card> cards) => string.Join(",", cards.Select(c => c.Id + "/" + c.Class + "/" + c.Type + "/" + c.Collectible + "/" + c.Implemented));

    private static void HeroAttack(Game g, Controller p, ICharacter target)
    {
        p.Hero.NumAttacksThisTurn = 0;
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == target.Id);
        Core2026Proof.Check(task != null && g.Process(task), "hero attack accepted");
    }

    // Casts a generator spell n times and returns the cards it added, removing each one from the hand again.
    private static List<Card> Casts(Game g, Controller p, string id, int n)
    {
        var cards = new List<Card>();
        for (int i = 0; i < n; i++)
        {
            int before = p.HandZone.Count;
            Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == before + 1, id + " adds exactly one card to its owner's hand");
            var added = p.HandZone[p.HandZone.Count - 1];
            cards.Add(added.Card);
            p.SetasideZone.Add(p.HandZone.Remove(added));
        }
        return cards;
    }

    }
