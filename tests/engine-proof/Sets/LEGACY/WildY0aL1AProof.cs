using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wild wave Y0a, lane l1a: Legacy and Classic neutral minions. The group runs both set files (the group name is unique).
internal static class WildY0aL1ALegacyProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case.
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    private static readonly GameTag[] Words =
    {
        GameTag.TAUNT, GameTag.CHARGE, GameTag.WINDFURY, GameTag.STEALTH, GameTag.DIVINE_SHIELD, GameTag.RUSH,
        GameTag.LIFESTEAL, GameTag.POISONOUS, GameTag.DEATHRATTLE, GameTag.BATTLECRY, GameTag.SPELLPOWER, GameTag.REBORN, GameTag.AURA,
    };

    [GroupProof("wild-y0a-l1a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildY0aL1AExpertProof.Run();
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS1_042", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_118", (g, p, id) => Vanilla(g, p, id, Race.ELEMENTAL));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_119", (g, p, id) => Vanilla(g, p, id, Race.BEAST));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_120", (g, p, id) => Vanilla(g, p, id, Race.BEAST));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_121", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_122", RaidLeader);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_124", Charge);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_125", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_127", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_131", Charge);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_141", (g, p, id) => BattlecryDamage(g, p, id, 1));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_142", SpellDamage);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_147", GnomishInventor);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_150", (g, p, id) => BattlecryDamage(g, p, id, 2));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_155", SpellDamage);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_162", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_168", (g, p, id) => Vanilla(g, p, id, Race.MURLOC));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_171", Charge);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_172", (g, p, id) => Vanilla(g, p, id, Race.BEAST));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_173", Charge);
    }

    internal static HashSet<int> AttackTargets(Controller owner, Minion attacker) =>
        owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id)
            .Select(t => t.Target.Id).ToHashSet();

    internal static void Silence(Game g, Minion m) { m.Silence(); g.DeathProcessingAndAuraUpdate(); }

    // Exactly the listed keywords are on the minion and no other.
    internal static void Only(Minion m, string what, params GameTag[] keep)
    {
        foreach (var word in Words)
            Core2026Proof.Check((m[word] > 0) == keep.Contains(word), what + ": keyword " + word + (keep.Contains(word) ? " missing" : " must be absent"));
    }

    internal static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "Y0a l1a state before continuation");
            continuation(next);
        }
    }

    // Moves play to the next turn of the same player; the second End hands the turn back to Player 1.
    internal static void NextOwnTurn(Game g) { End(g); End(g); }

    internal static void Vanilla(Game g, Controller p, string id, Race race)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id);
        Core2026Proof.Check(m.Card.Text == "" || m.Card.Text == null, id + " has no text");
        Core2026Proof.Check(m.Card.Races.Contains(race) && m.Card.Races.Count == 1, id + " printed race");
        Core2026Proof.Check(m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health && AttackTargets(p, m).Count == 0,
            id + " vanilla body, summoning sick");
        Silence(g, m);
        Core2026Proof.Check(m.IsSilenced && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health, id + " Silence changes nothing on a vanilla body");
        NextOwnTurn(g);
        Core2026Proof.Check(AttackTargets(p, m).SetEquals(new[] { p.Opponent.Hero.Id }), id + " attacks the lone enemy hero next turn");
        Attack(g, m, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == m.Card.ATK, id + " deals exactly its Attack");
        var spare = (Minion)Play(g, p, id);
        if (spare.Health >= 2)
        {
            spare.Damage = spare.Health - 2; Play(g, p, "CS2_008", spare);
            Core2026Proof.Check(spare.Zone.Type == Zone.PLAY && spare.Health == 1, id + " survives one point short of lethal");
        }
        else spare.Damage = 0;
        Play(g, p, "CS2_008", spare);
        Core2026Proof.Check(spare.Zone.Type == Zone.GRAVEYARD, id + " dies at exact Health");
        var enemy = Summon(p.Opponent, id);
        Kill(g, enemy);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.BoardZone.Count == 1, id + " opponent copy leaves no trace");
    }

    internal static void Taunt(Game g, Controller p, string id)
    {
        var mine = (Minion)Play(g, p, id);
        Only(mine, id, GameTag.TAUNT);
        var enemy = Summon(p.Opponent, id);
        var attacker = Summon(p, "CS2_182"); attacker.IsExhausted = false;
        Core2026Proof.Check(AttackTargets(p, attacker).SetEquals(new[] { enemy.Id }), id + " opposing Taunt guards its hero and others");
        Silence(g, enemy);
        Core2026Proof.Check(AttackTargets(p, attacker).Count == 2 && AttackTargets(p, attacker).Contains(p.Opponent.Hero.Id),
            id + " Silenced Taunt no longer guards");
        End(g);
        var raider = Summon(p.Opponent, "CS2_182"); raider.IsExhausted = false;
        Core2026Proof.Check(AttackTargets(p.Opponent, raider).SetEquals(new[] { mine.Id }), id + " friendly Taunt guards my side");
        Resume(g, next => Core2026Proof.Check(AttackTargets(next.Player2, (Minion)next.IdEntityDic[raider.Id])
            .SetEquals(new[] { mine.Id }), id + " Taunt survives clone and snapshot"));
        Silence(g, mine);
        Core2026Proof.Check(!mine.HasTaunt && AttackTargets(p.Opponent, raider).Count == 3, id + " Silence removes Taunt");
        End(g);
    }

    internal static void Charge(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id, GameTag.CHARGE);
        var plain = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(AttackTargets(p, plain).Count == 0, "a minion without Charge cannot attack");
        Core2026Proof.Check(AttackTargets(p, m).Contains(p.Opponent.Hero.Id), id + " may attack a hero the turn it is played");
        var wall = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(AttackTargets(p, m).Contains(wall.Id) && AttackTargets(p, m).Count == 2, id + " may attack minions and the hero");
        Attack(g, m, p.Opponent.Hero);
        Core2026Proof.Check(AttackTargets(p, m).Count == 0, id + " attacks once");
        var second = (Minion)Play(g, p, id);
        Silence(g, second);
        Core2026Proof.Check(!second.HasCharge && AttackTargets(p, second).Count == 0, id + " Silence restores summoning sickness");
        Resume(g, next => Core2026Proof.Check(AttackTargets(next.Player1, (Minion)next.IdEntityDic[m.Id]).Count == 0
            && !((Minion)next.IdEntityDic[second.Id]).HasCharge, id + " Charge state survives clone and snapshot"));
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(AttackTargets(p.Opponent, theirs).Contains(p.Hero.Id), id + " opponent copy charges too");
        End(g);
    }

    internal static void SpellDamage(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id, GameTag.SPELLPOWER);
        var hero = p.Opponent.Hero;
        Play(g, p, "CS2_008", hero);
        Core2026Proof.Check(hero.Damage == 2 && p.CurrentSpellPower == 1, id + " adds one to Moonfire");
        var enemy = Summon(p.Opponent, id);
        Play(g, p, "CS2_008", hero);
        Core2026Proof.Check(hero.Damage == 4 && p.CurrentSpellPower == 1 && p.Opponent.CurrentSpellPower == 1,
            id + " enemy copy adds nothing to my spells");
        Resume(g, next => Core2026Proof.Check(next.Player1.CurrentSpellPower == 1 && next.Player2.CurrentSpellPower == 1,
            id + " Spell Damage survives clone and snapshot"));
        var other = Summon(p, id == "CS2_142" ? "CS2_155" : "CS2_142");
        Play(g, p, "CS2_008", hero);
        Core2026Proof.Check(hero.Damage == 7 && p.CurrentSpellPower == 2, id + " stacks with another Spell Damage minion");
        Kill(g, other);
        Silence(g, m);
        Play(g, p, "CS2_008", hero);
        Core2026Proof.Check(hero.Damage == 8 && p.CurrentSpellPower == 0, id + " Silence removes Spell Damage");
        var again = Summon(p, id);
        Kill(g, again);
        Play(g, p, "CS2_008", hero);
        Core2026Proof.Check(hero.Damage == 9, id + " Spell Damage ends on death");
        Kill(g, enemy);
    }

    internal static void BattlecryDamage(Game g, Controller p, string id, int amount)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = Add(p, id);
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id)
            .Select(t => t.Target?.Id ?? 0).ToHashSet();
        Core2026Proof.Check(targets.SetEquals(new[] { friend.Id, enemy.Id, p.Hero.Id, p.Opponent.Hero.Id }),
            id + " may target any other character, friendly or enemy, and not itself");
        PlayEntity(g, p, held, enemy);
        var minion = (Minion)held;
        Kill(g, minion);
        Core2026Proof.Check(enemy.Damage == amount && friend.Damage == 0 && minion.Damage == 0 && p.Hero.Damage == 0
            && p.Opponent.Hero.Damage == 0, id + " deals exactly " + amount + " to the chosen enemy minion only");
        Kill(g, (Minion)Play(g, p, id, p.Opponent.Hero));
        Core2026Proof.Check(p.Opponent.Hero.Damage == amount, id + " damages the enemy hero");
        Kill(g, (Minion)Play(g, p, id, p.Hero));
        Core2026Proof.Check(p.Hero.Damage == amount, id + " may damage my own hero");
        Kill(g, (Minion)Play(g, p, id, friend));
        Core2026Proof.Check(friend.Damage == amount, id + " may damage my own minion");
        enemy.Damage = enemy.Card.Health - amount;
        Kill(g, (Minion)Play(g, p, id, enemy));
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, id + " kills at exact Health");
        var geomancer = Summon(p, "CS2_142");
        var next = Summon(p.Opponent, "CS2_182");
        Kill(g, (Minion)Play(g, p, id, next));
        Core2026Proof.Check(next.Damage == amount && p.CurrentSpellPower == 1, id + " Battlecry damage ignores Spell Damage");
        var stealthy = Summon(p.Opponent, "CS2_161");
        var probe = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == probe.Id
            && t.Target?.Id == stealthy.Id), id + " cannot target an enemy Stealth minion");
        Kill(g, geomancer);
        var source = (Minion)Play(g, p, id, next);
        int before = next.Damage;
        Silence(g, source); Kill(g, source);
        Core2026Proof.Check(next.Damage == before, id + " Silence and death do not repeat the Battlecry");
        End(g);
        var theirs = Add(p.Opponent, id);
        var theirTargets = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == theirs.Id)
            .Select(t => t.Target?.Id ?? 0).ToHashSet();
        Core2026Proof.Check(theirTargets.Contains(p.Hero.Id) && theirTargets.Contains(stealthy.Id) && theirTargets.Contains(next.Id),
            id + " opponent may target my hero and its own Stealth minion");
        PlayEntity(g, p.Opponent, theirs, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == amount * 2, id + " opponent Battlecry damages my hero");
        End(g);
    }

    internal static void GnomishInventor(Game g, Controller p, string id)
    {
        var top = (IPlayable)Entity.FromCard(p, Cards.FromId("CS2_008"));
        p.DeckZone.PutOnTop(top);
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, theirHand = p.Opponent.HandZone.Count, theirDeck = p.Opponent.DeckZone.Count;
        var held = Add(p, id);
        PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1 && p.HandZone.Last().Id == top.Id,
            id + " draws exactly the top card for its owner");
        Core2026Proof.Check(p.Opponent.HandZone.Count == theirHand && p.Opponent.DeckZone.Count == theirDeck, id + " touches nothing of the opponent's hidden zones");
        Core2026Proof.Check(p.Hero.Damage == 0 && p.BoardZone.Count == 1, id + " is a plain 2/4 body");
        var source = (Minion)held;
        Silence(g, source); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == hand + 1, id + " Silence and death do not draw again");
        while (p.HandZone.Count > 0) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.First()));
        var filler = Enumerable.Range(0, 9).Select(_ => Add(p, "CS2_182")).ToArray();
        int graveyard = p.GraveyardZone.Count, deckNow = p.DeckZone.Count;
        var last = Add(p, id);
        Core2026Proof.Check(p.HandZone.Count == 10, "hand is full before the play");
        PlayEntity(g, p, last);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deckNow - 1 && p.GraveyardZone.Count == graveyard,
            id + " draws into the slot it frees and burns nothing");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        while (p.HandZone.Count > 1) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.First()));
        int damage = p.Hero.Damage;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == damage + 1, id + " with an empty deck takes the first fatigue damage");
        End(g);
        var theirs = Add(p.Opponent, id);
        int myHand = p.HandZone.Count, myDeck = p.DeckZone.Count, theirHandNow = p.Opponent.HandZone.Count, theirDeckNow = p.Opponent.DeckZone.Count;
        PlayEntity(g, p.Opponent, theirs);
        Core2026Proof.Check(p.Opponent.HandZone.Count == theirHandNow && p.Opponent.DeckZone.Count == theirDeckNow - 1
            && p.HandZone.Count == myHand && p.DeckZone.Count == myDeck, id + " opponent copy draws for the opponent only");
        End(g);
    }

    internal static void RaidLeader(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = Add(p, "CS2_182");
        var leader = (Minion)Play(g, p, id);
        Core2026Proof.Check(leader.AttackDamage == 2 && leader.Health == 3 && leader.Card.Cost == 3, id + " is a 3-mana 2/3 in 37.0");
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 5 && leader.AttackDamage == 2 && enemy.AttackDamage == 4
            && ((Minion)held).AttackDamage == 4, id + " gives other friendly board minions +1 Attack only");
        var late = Summon(p, "CS2_182");
        Core2026Proof.Check(late.AttackDamage == 5, id + " buffs minions that arrive later");
        friend.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friend.AttackDamage == 5, id + " aura survives Silence on the recipient");
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(leader.AttackDamage == 3 && second.AttackDamage == 3 && friend.AttackDamage == 6, id + " two leaders buff each other");
        Kill(g, second);
        Core2026Proof.Check(leader.AttackDamage == 2 && friend.AttackDamage == 5, id + " the aura leaves with its source");
        Resume(g, next => {
            var l = (Minion)next.IdEntityDic[leader.Id];
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 5, id + " aura survives clone and snapshot");
            Silence(next, l);
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 4, id + " Silence removes the aura after a restore");
        });
        Silence(g, leader);
        Core2026Proof.Check(friend.AttackDamage == 4 && late.AttackDamage == 4 && enemy.AttackDamage == 4, id + " Silence removes the aura");
        var again = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.AttackDamage == 5, id + " a fresh leader buffs again");
        Play(g, p, "CS1_113", enemy);
        Core2026Proof.Check(enemy.Controller == p && enemy.AttackDamage == 5, id + " stolen minions are buffed by my leader");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirs.AttackDamage == 2 && again.AttackDamage == 2 && friend.AttackDamage == 5,
            id + " opponent copy buffs only the opponent's board");
        Play(g, p.Opponent, "CS1_113", again);
        Core2026Proof.Check(again.Controller == p.Opponent && friend.AttackDamage == 4 && again.AttackDamage == 3 && theirs.AttackDamage == 3,
            id + " control change moves the aura with the leader");
        End(g);
    }
}
