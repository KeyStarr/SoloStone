using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0a, lane l2b: 16 Legacy/Classic neutral minions held to the 37.0 text. This file holds the group proof and the
// Expert cards; the Legacy-set cards are in the LEGACY folder (WildY0aL2BLegacyProof) and run from here.
// EX1_399, EX1_506 and EX1_508 are BLOCKED-STANDARD (LocalCardCatalog.NewBasicIds): see WildY0aL2BLegacyProof.Blocked_*.
internal static class WildY0aL2BProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case (used by Standard until #87).
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    internal const string Yeti = "CS2_182", Moonfire = "CS2_008", Murloc = "CS2_168", Heal = "CS2_007", Mark = "CS2_009",
        IceBarrier = "EX1_289", Counterspell = "EX1_287", MirrorEntity = "EX1_294", IceBlock = "EX1_295", Boar = "CS2_171";

    [GroupProof("wild-y0a-l2b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildY0aL2BLegacyProof.Run();

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_249", (g, p, id) => {
            var baron = (Minion)Play(g, p, id);
            Check(baron.AttackDamage == 7 && baron.Health == 7, "Baron Geddon is a 7/7");
            var friend = Summon(p, Yeti); var enemy = Summon(p.Opponent, Yeti);
            var doomed = Summon(p.Opponent, Yeti); doomed.Damage = 3;
            Replay(g, b => {
                End(b);
                Check(((Minion)b.IdEntityDic[friend.Id]).Damage == 2 && ((Minion)b.IdEntityDic[enemy.Id]).Damage == 2,
                    "the end of my turn deals 2 to every other minion, mine and theirs");
                Check(b.IdEntityDic[doomed.Id].Zone.Type == Zone.GRAVEYARD, "a minion at exactly 2 Health dies");
                Check(b.Player1.Hero.Damage == 2 && b.Player2.Hero.Damage == 2, "both heroes take 2");
                Check(((Minion)b.IdEntityDic[baron.Id]).Damage == 0, "Baron Geddon spares itself");
            });
            Check(p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 2, "original game agrees with its clone and snapshot");
            End(g);
            Check(p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 2 && friend.Damage == 2 && baron.Damage == 0,
                "the end of the opponent's turn does nothing");
            friend.Damage = 0;
            var mage = Summon(p, "EX1_582");
            End(g); End(g);
            Check(p.Opponent.Hero.Damage == 4 && p.Hero.Damage == 4, "Spell Damage does not raise a minion's damage: " + p.Opponent.Hero.Damage);
            var rival = Summon(p.Opponent, id);
            End(g);
            Check(rival.Damage == 2 && baron.Damage == 0, "two Barons: mine hits theirs at my end of turn only");
            End(g);
            Check(baron.Damage == 2 && rival.Damage == 2, "their Baron hits mine at their end of turn");
            friend = Summon(p, Yeti);
            Mute(g, baron); int hero = p.Opponent.Hero.Damage, friendHp = friend.Damage;
            End(g); End(g);
            Check(p.Opponent.Hero.Damage == hero + 2 && friend.Damage == friendHp + 2, "Silenced Baron stops; only their own Baron still pings: hero " + hero + ">" + p.Opponent.Hero.Damage + " friend " + friendHp + ">" + friend.Damage
                + " mine " + p.Hero.Damage + " rival " + rival.Damage + " baron " + baron.Damage + " " + baron.IsSilenced);
            Kill(g, rival); hero = p.Opponent.Hero.Damage;
            End(g);
            Check(p.Opponent.Hero.Damage == hero, "a dead Baron deals nothing");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_283", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var hiding = Summon(p.Opponent, "CS2_059");
            var friend = Summon(p, Yeti); var ownHiding = Summon(p, "CS2_059");
            var held = Add(p, id);
            var plays = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(plays.Length > 0 && plays.All(t => t.Target != null), "a target is required because a hero is always available");
            var targets = plays.Select(t => t.Target.Id).ToHashSet();
            Check(targets.SetEquals(new[] { p.Hero.Id, p.Opponent.Hero.Id, enemy.Id, friend.Id, ownHiding.Id }),
                "any character, friendly or enemy, but not an enemy Stealth minion");
            var frost = (Minion)held;
            PlayEntity(g, p, held, enemy);
            Check(enemy.IsFrozen && !frost.IsFrozen && !friend.IsFrozen && !p.Hero.IsFrozen && !p.Opponent.Hero.IsFrozen,
                "freezes exactly the chosen enemy minion, not itself");
            Check(frost.AttackDamage == 5 && frost.Health == 5, "Frost Elemental is a 5/5");
            Replay(g, b => {
                End(b);
                Check(!b.Player2.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id),
                    "a frozen enemy minion cannot attack on its turn");
                End(b);
                Check(!((Minion)b.IdEntityDic[enemy.Id]).IsFrozen, "Freeze wears off after its owner's turn");
            });
            Play(g, p, id, p.Opponent.Hero);
            Check(p.Opponent.Hero.IsFrozen && !p.Hero.IsFrozen, "freezes the enemy hero");
            Play(g, p, id, p.Hero);
            Check(p.Hero.IsFrozen, "freezes my own hero");
            Play(g, p, id, friend);
            Check(friend.IsFrozen && !CanAttack(p, friend, p.Opponent.Hero), "freezes my own minion, which then cannot attack");
            var second = (Minion)Play(g, p, id, ownHiding);
            Check(ownHiding.IsFrozen && !second.IsFrozen, "a friendly Stealth minion is a legal target");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_284", (g, p, id) => {
            Trim(p, 0);
            int deck = p.DeckZone.Count;
            var drake = (Minion)Add(p, id); int hand = p.HandZone.Count;
            Check(drake.AttackDamage == 4 && drake.Health == 5 && drake.Card.IsRace(Race.DRAGON), "Azure Drake is a 4/5 Dragon");
            PlayEntity(g, p, drake);
            Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == hand, "Battlecry draws exactly one card");
            Check(p.CurrentSpellPower == 1 && p.Opponent.CurrentSpellPower == 0, "Spell Damage +1 for me only");
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 2, "my spell deals one extra");
            Summon(p, id);
            Check(p.DeckZone.Count == deck - 1, "a summoned Drake does not draw");
            End(g);
            Play(g, g.Player2, Moonfire, p.Hero);
            Check(p.Hero.Damage == 1, "their spell gets no bonus from my Drake");
            End(g);
            Replay(g, b => {
                var q = b.Player1;
                foreach (var m in q.BoardZone.Where(m => m.Card.Id == id).ToArray()) m.Silence();
                b.DeathProcessingAndAuraUpdate();
                Check(q.CurrentSpellPower == 0, "Silence removes Spell Damage from every Drake");
                int before = q.Opponent.Hero.Damage;
                Play(b, q, Moonfire, q.Opponent.Hero);
                Check(q.Opponent.Hero.Damage == before + 1, "after Silence the spell deals its printed damage");
            });
            foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
            int life = p.Hero.Damage, cards = p.HandZone.Count;
            Play(g, p, id);
            Check(p.HandZone.Count == cards && p.Hero.Damage > life, "with an empty deck the Battlecry costs fatigue damage");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_298", (g, p, id) => {
            var rag = (Minion)Play(g, p, id);
            Check(rag.AttackDamage == 8 && rag.Health == 8 && rag.CantAttack, "Ragnaros is an 8/8 that can't attack");
            var friend = Summon(p, Yeti);
            p.Opponent.Hero.Armor = 200;
            for (int i = 0; i < 3; i++) Summon(p.Opponent, Yeti);
            Replay(g, b => {
                End(b);
                var o = b.Player2;
                int alive = o.BoardZone.Count, armor = o.Hero.Armor;
                Check((alive == 3 && armor == 192) || (alive == 2 && armor == 200),
                    "exactly one random enemy takes 8: " + alive + " minions, armor " + armor);
                Check(((Minion)b.IdEntityDic[friend.Id]).Damage == 0 && b.Player1.Hero.Damage == 0, "my side is untouched");
            });
            End(g);
            Check(friend.Damage == 0 && p.Hero.Damage == 0 && rag.Damage == 0, "their end of turn does nothing");
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == rag.Id),
                "a ready Ragnaros has no attack option");
            StandardSetsProof.ParkBoard(p.Opponent); p.Opponent.Hero.Armor = 200;
            End(g);
            Check(p.Opponent.Hero.Armor == 192, "with no enemy minion the hero takes the 8");
            End(g);
            int heroHits = 0, minionHits = 0;
            for (int turn = 0; turn < 14; turn++)
            {
                var only = Summon(p.Opponent, Yeti); p.Opponent.Hero.Armor = 200;
                End(g);
                bool hero = p.Opponent.Hero.Armor == 192, minion = only.Zone.Type == Zone.GRAVEYARD;
                Check(hero != minion, "exactly one of hero and the lone minion is hit each turn");
                if (hero) heroHits++; else minionHits++;
                End(g);
                if (only.Zone.Type == Zone.PLAY) StandardSetsProof.ParkBoard(p.Opponent);
            }
            Check(heroHits > 0 && minionHits > 0, "both candidates are hit across turns: " + heroHits + "/" + minionHits);
            Mute(g, rag);
            Check(CanAttack(p, rag, p.Opponent.Hero), "Silence removes the attack ban");
            StandardSetsProof.ParkBoard(p.Opponent); p.Opponent.Hero.Armor = 200;
            End(g);
            Check(p.Opponent.Hero.Armor == 200, "Silence removes the end-of-turn damage");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_390", (g, p, id) => Enrage(g, p, id, 2, 3, taunt: true, windfury: false));
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_393", (g, p, id) => Enrage(g, p, id, 2, 3, taunt: false, windfury: false));
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_412", (g, p, id) => {
            Enrage(g, p, id, 3, 1, taunt: false, windfury: true);
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var worgen = Summon(p, id);
            Attack(g, worgen, p.Opponent.Hero);
            Check(!CanAttack(p, worgen, p.Opponent.Hero), "an undamaged Worgen attacks once");
            Play(g, p, Moonfire, worgen);
            Check(worgen.HasWindfury && worgen.AttackDamage == 4 && CanAttack(p, worgen, p.Opponent.Hero),
                "damage after the first attack grants Windfury, so it can attack again");
            Attack(g, worgen, p.Opponent.Hero);
            Check(!CanAttack(p, worgen, p.Opponent.Hero) && p.Opponent.Hero.Damage == 3 + 4, "the second attack hits for 4, then no third");
            End(g); End(g);
            Attack(g, worgen, p.Opponent.Hero); Attack(g, worgen, p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 3 + 4 + 8 && !CanAttack(p, worgen, p.Opponent.Hero), "still damaged next turn: two more attacks");
            Play(g, p, Heal, worgen);
            Check(!worgen.HasWindfury && worgen.AttackDamage == 3, "healed Worgen loses Windfury and the bonus");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_396", (g, p, id) => Taunt(g, p, id, 1, 7));
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_405", (g, p, id) => {
            var card = Cards.FromId(id);
            Check(card.IsRace(Race.DRAENEI) && card.ATK == 0 && card.Health == 4 && card.Cost == 1, "Shieldbearer is a 0/4 Draenei for 1");
            Taunt(g, p, id, 0, 4);
            var guard = Summon(p, id); guard.IsExhausted = false;
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == guard.Id),
                "a 0 Attack minion has no attack option");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_507", (g, p, id) => {
            var raider = Summon(p, Murloc); var yeti = Summon(p, Yeti); var foe = Summon(p.Opponent, Murloc);
            var held = Add(p, Murloc);
            var warleader = (Minion)Play(g, p, id);
            Check(warleader.AttackDamage == 3 && warleader.Health == 3, "Murloc Warleader is a 3/3");
            Check(raider.AttackDamage == 4 && yeti.AttackDamage == 4 && foe.AttackDamage == 2, "only my other Murlocs get +2");
            Check(Summon(p, Murloc).AttackDamage == 4, "a Murloc summoned later is buffed at once");
            Check(held.Card.ATK == 2 && ((Minion)held).AttackDamage == 2, "a Murloc in hand is not buffed");
            var oracle = Summon(p, "EX1_508");
            Check(oracle.AttackDamage == 3 && raider.AttackDamage == 5, "stacks with Grimscale Oracle: " + oracle.AttackDamage + " " + raider.AttackDamage);
            var second = (Minion)Play(g, p, id);
            Check(warleader.AttackDamage == 6 && second.AttackDamage == 6 && raider.AttackDamage == 7, "two Warleaders buff each other, and the Oracle buffs them");
            Replay(g, b => {
                var r = (Minion)b.IdEntityDic[raider.Id];
                Mute(b, (Minion)b.IdEntityDic[second.Id]);
                Check(r.AttackDamage == 5 && ((Minion)b.IdEntityDic[warleader.Id]).AttackDamage == 4,
                    "Silence removes that Warleader's aura and leaves the other's and the Oracle's bonus");
            });
            Check(raider.AttackDamage == 5 && warleader.AttackDamage == 4, "the original game agrees");
            var rival = Summon(p.Opponent, id);
            Check(foe.AttackDamage == 4 && raider.AttackDamage == 5 && rival.AttackDamage == 3, "an enemy Warleader buffs only its own side");
            Kill(g, warleader);
            Check(raider.AttackDamage == 3, "death of the last Warleader removes the bonus");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_509", (g, p, id) => {
            var caller = (Minion)Play(g, p, id);
            Check(caller.AttackDamage == 1 && caller.Health == 2, "Murloc Tidecaller is a 1/2 that ignores its own arrival");
            Play(g, p, Murloc);
            Check(caller.AttackDamage == 2, "playing a Murloc gives +1 Attack");
            Summon(p, Murloc);
            Check(caller.AttackDamage == 3, "a summoned Murloc counts too");
            Play(g, p, Yeti);
            Check(caller.AttackDamage == 3 && caller.Health == 2, "a non-Murloc gives nothing");
            Park(p, caller);
            Summon(p.Opponent, Murloc);
            Check(caller.AttackDamage == 3, "an enemy Murloc gives nothing");
            Play(g, p, "EX1_506");
            Check(caller.AttackDamage == 5, "Tidehunter and its Scout each give +1: " + caller.AttackDamage);
            Park(p, caller);
            Replay(g, b => {
                Summon(b.Player1, Murloc);
                Check(((Minion)b.IdEntityDic[caller.Id]).AttackDamage == 6, "the bonus carries into an exact clone");
            });
            Summon(p, Murloc);
            var other = (Minion)Play(g, p, id);
            Check(caller.AttackDamage == 8 && other.AttackDamage == 1, "a second Tidecaller is itself a Murloc for the first, but not for itself");
            Summon(p, Murloc);
            Check(caller.AttackDamage == 9 && other.AttackDamage == 2, "both Tidecallers grow from the next Murloc");
            Mute(g, caller);
            Check(caller.AttackDamage == 1, "Silence removes the gained Attack");
            Summon(p, Murloc);
            Check(caller.AttackDamage == 1 && other.AttackDamage == 3, "a Silenced Tidecaller stops growing");
            End(g);
            Summon(g.Player2, Murloc);
            var foe = Summon(g.Player2, id);
            Summon(g.Player2, Murloc);
            Check(foe.AttackDamage == 2 && other.AttackDamage == 3, "an enemy Tidecaller grows only from its owner's Murlocs");
        });
    }

    // Enrage shared by the three damaged-bonus Worgen-style minions.
    private static void Enrage(Game g, Controller p, string id, int baseAttack, int bonus, bool taunt, bool windfury)
    {
        var minion = (Minion)Play(g, p, id);
        Check(minion.AttackDamage == baseAttack && minion.HasTaunt == taunt && minion.HasWindfury == false, id + " starts undamaged and plain");
        Play(g, p, Moonfire, minion);
        Check(minion.Damage == 1 && minion.AttackDamage == baseAttack + bonus && minion.HasWindfury == windfury,
            id + " enrages at one damage: " + minion.AttackDamage);
        Replay(g, b => {
            var m = (Minion)b.IdEntityDic[minion.Id];
            Play(b, b.Player1, Heal, m);
            Check(m.Damage == 0 && m.AttackDamage == baseAttack && !m.HasWindfury, id + " calms down when healed, on a clone too");
        });
        Check(minion.Damage == 0 && minion.AttackDamage == baseAttack, id + " original game healed alike");
        Play(g, p, Mark, minion);
        Check(minion.AttackDamage == baseAttack + 2, id + " Mark of the Wild adds +2 Attack");
        Play(g, p, Moonfire, minion);
        Check(minion.AttackDamage == baseAttack + 2 + bonus, id + " enrage stacks on top of a permanent buff");
        minion.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(minion.AttackDamage == baseAttack && !minion.HasTaunt && !minion.HasWindfury, id + " Silence removes enrage, buff and keywords");
        Play(g, p, Moonfire, minion);
        Check(minion.AttackDamage == baseAttack, id + " a Silenced minion no longer enrages");
        var enemy = Summon(p.Opponent, id);
        Check(enemy.AttackDamage == baseAttack, id + " enemy copy is plain");
        Play(g, p, Moonfire, enemy);
        Check(enemy.AttackDamage == baseAttack + bonus && enemy.HasWindfury == windfury, id + " enrages on the opponent's side too");
        Play(g, p, Heal, enemy); Play(g, p, Moonfire, enemy); Play(g, p, Moonfire, enemy);
        Check(enemy.Damage == 2 && enemy.AttackDamage == baseAttack + bonus, id + " enrage does not stack with more damage");
    }

    private static void Taunt(Game g, Controller p, string id, int attack, int health)
    {
        var keeper = (Minion)Play(g, p, id);
        Check(keeper.AttackDamage == attack && keeper.Health == health && keeper.HasTaunt, id + " has its printed stats and Taunt");
        var friend = Summon(p, Yeti); var enemy = Summon(p.Opponent, Yeti);
        End(g);
        var theirs = g.Player2.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id)
            .Select(t => t.Target.Id).ToArray();
        Check(theirs.Length == 1 && theirs[0] == keeper.Id, id + " forces enemy attackers onto it");
        End(g);
        keeper.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g);
        var free = g.Player2.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id)
            .Select(t => t.Target.Id).ToHashSet();
        Check(free.Contains(friend.Id) && free.Contains(p.Hero.Id) && !keeper.HasTaunt, id + " Silence lifts the Taunt");
        End(g);
        var foeKeeper = Summon(p.Opponent, id);
        var mine = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == friend.Id)
            .Select(t => t.Target.Id).ToArray();
        Check(mine.Length == 1 && mine[0] == foeKeeper.Id, id + " on their side forces my attackers onto it");
        Replay(g, b => {
            Kill(b, (Minion)b.IdEntityDic[foeKeeper.Id]);
            Check(b.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == b.Player2.Hero.Id),
                id + " leaving play opens their hero to attack");
        });
    }

    private static void Check(bool ok, string why) => Core2026Proof.Check(ok, why);

    internal static bool CanAttack(Controller player, Minion minion, ICharacter target) =>
        player.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id && t.Target?.Id == target.Id);

    internal static void Mute(Game game, Minion minion) { minion.Silence(); game.DeathProcessingAndAuraUpdate(); }

    internal static void Park(Controller player, params Minion[] keep)
    {
        foreach (var minion in player.BoardZone.ToArray())
            if (!keep.Contains(minion)) player.SetasideZone.Add(player.BoardZone.Remove(minion));
        player.Game.DeathProcessingAndAuraUpdate();
    }

    internal static void Trim(Controller player, int keep, int protect = -1)
    {
        while (player.HandZone.Count > keep)
        {
            var card = player.HandZone.Last(c => c.Id != protect);
            player.HandZone.Remove(card); player.SetasideZone.Add(card);
        }
    }

    internal static Game FairView(Game game, int viewer)
    {
        var state = FairBotState.Create(game, viewer, 71, DeckStyle.Value);
        return (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(state);
    }

    // True when the seat `viewer`'s fair view hides the entity's identity (an entity the view does not carry leaks nothing).
    internal static bool Hidden(Game game, int viewer, int entityId)
    {
        var view = FairView(game, viewer);
        try { return view.IdEntityDic[entityId] is Unknown; }
        catch (Exception e) when (e is KeyNotFoundException || e is ArgumentOutOfRangeException) { return true; }
    }

    // Runs the continuation on the game, an exact clone and a snapshot restore; all three must end in the same state.
    internal static void Replay(Game game, Action<Game> continuation)
    {
        var branches = new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) };
        foreach (var next in branches) Check(Canonical.State(game) == Canonical.State(next), "l2b state before continuation");
        continuation(game);
        foreach (var next in branches)
        {
            continuation(next);
            Check(Canonical.State(game) == Canonical.State(next), "l2b state after continuation matches the original branch");
        }
    }
}
