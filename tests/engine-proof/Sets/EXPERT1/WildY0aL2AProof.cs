using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0a, lane l2a: 19 Legacy/Classic neutral minions held to the 37.0 text.
internal static class WildY0aL2AProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case.
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    private const string Yeti = "CS2_182", Wisp = "CS2_231", Moonfire = "CS2_008", Murloc = "CS2_168", IceBarrier = "EX1_289";

    [GroupProof("wild-y0a-l2a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_067", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti);
            var commander = (Minion)Play(g, p, id);
            Check(commander.HasCharge && commander.HasDivineShield && commander.AttackDamage == 4 && commander.Health == 2,
                "Argent Commander is a 4/2 with Charge and Divine Shield");
            Check(CanAttack(p, commander, enemy) && CanAttack(p, commander, p.Opponent.Hero), "Charge attacks minion and hero at once");
            Attack(g, commander, enemy);
            Check(!commander.HasDivineShield && commander.Damage == 0 && commander.Health == 2 && enemy.Damage == 4,
                "Divine Shield absorbs the whole counter-hit");
            Check(commander.Zone.Type == Zone.PLAY, "Commander survives the first exchange");
            Play(g, p, Moonfire, commander); Play(g, p, Moonfire, commander);
            Check(commander.Zone.Type == Zone.GRAVEYARD, "Commander dies at exact lethal once the shield is gone");
            var silenced = Summon(p, id); Mute(g, silenced);
            Check(!silenced.HasCharge && !silenced.HasDivineShield, "Silence removes Charge and Divine Shield");
            var fresh = (Minion)Play(g, p, id); Mute(g, fresh);
            Check(!CanAttack(p, fresh, enemy), "Silenced Charge minion cannot attack the turn it was played");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_076", (g, p, id) => {
            var pint = Summon(p, id);
            var first = Held(g, p, Yeti); var spell = Held(g, p, "CS2_029"); var wisp = Held(g, p, Wisp);
            var enemyCard = Held(g, p.Opponent, Yeti);
            Check(first.Cost == 3, "first minion costs 1 less: " + first.Cost);
            Check(spell.Cost == 4 && wisp.Cost == 0 && enemyCard.Cost == 4, "spells, zero-cost minions and the enemy hand are untouched");
            Summon(p, Yeti);
            Check(first.Cost == 3, "summoning (not playing) a minion does not use the discount");
            PlayEntity(g, p, first);
            Check(Held(g, p, Yeti).Cost == 4, "only the first minion played this turn is discounted");
            Replay(g, b => {
                End(b); End(b); Check(b.Player1.HandZone.Where(c => c.Card.Id == Yeti).All(c => c.Cost == 3), "discount returns on the next own turn");
                var played = b.Player1.HandZone.First(c => c.Card.Id == Yeti);
                PlayEntity(b, b.Player1, played);
                Check(b.Player1.HandZone.Where(c => c.Card.Id == Yeti).All(c => c.Cost == 4), "discount spent again");
            });
            StandardSetsProof.ParkBoard(p);
            Play(g, p, Wisp); Summon(p, id);
            Check(Held(g, p, Yeti).Cost == 4, "Pint-Sized Summoner arriving after a minion was played gives no discount this turn");
            End(g); End(g);
            var held = p.HandZone.Last(c => c.Card.Id == Yeti);
            Check(held.Cost == 3, "the discount is back on the next own turn");
            var silenced = p.BoardZone.Single(m => m.Card.Id == id); Mute(g, silenced);
            Check(held.Cost == 4, "Silence ends the discount");
            var second = Summon(p, id);
            Check(held.Cost == 3, "a second Pint-Sized Summoner discounts again");
            var both = Summon(p, id);
            Check(held.Cost == 2, "two Pint-Sized Summoners stack");
            Kill(g, both); Kill(g, second);
            Check(held.Cost == 4, "leaving play ends the discount");
            var stolenBy = Summon(p.Opponent, id);
            Check(held.Cost == 4, "an enemy Pint-Sized Summoner does not discount this hand");
            End(g);
            Check(Held(g, p.Opponent, Yeti).Cost == 3 && held.Cost == 4, "on its own turn the enemy Summoner discounts its owner, not me: " + held.Cost);
            Play(g, g.Player2, Yeti);
            Check(Held(g, p.Opponent, Yeti).Cost == 4, "the enemy's discount is spent by its first minion");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_080", (g, p, id) => {
            var keeper = (Minion)Play(g, p, id);
            Check(keeper.AttackDamage == 1 && keeper.Health == 2, "Secretkeeper is 1/2");
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(keeper.AttackDamage == 1 && keeper.Health == 2, "a non-Secret spell gives nothing");
            Play(g, p, IceBarrier);
            Check(keeper.AttackDamage == 2 && keeper.Health == 3, "own Secret gives +1/+1");
            Play(g, p, "EX1_554");
            Check(keeper.AttackDamage == 3 && keeper.Health == 4, "a second own Secret stacks");
            var silenced = Summon(p, id); Mute(g, silenced);
            var lateKeeper = Summon(p, id);
            Replay(g, b => {
                End(b); Play(b, b.Player2, IceBarrier);
                Check(((Minion)b.IdEntityDic[keeper.Id]).AttackDamage == 4 && ((Minion)b.IdEntityDic[keeper.Id]).Health == 5,
                    "an opponent's Secret also gives +1/+1");
                Check(((Minion)b.IdEntityDic[lateKeeper.Id]).AttackDamage == 2, "every Secretkeeper gains");
                Check(((Minion)b.IdEntityDic[silenced.Id]).AttackDamage == 1, "silenced Secretkeeper gains nothing");
            });
            End(g);
            Check(keeper.AttackDamage == 4 && keeper.Health == 5 && silenced.AttackDamage == 1, "gain survives turn changes");
            Play(g, p, Yeti);
            Check(keeper.AttackDamage == 4, "playing a minion is not a Secret");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_082", (g, p, id) => {
            var friend = Summon(p, Yeti); var near = Summon(p.Opponent, Yeti); var far = Summon(p.Opponent, Yeti);
            var spellPower = Summon(p, "EX1_284");
            var all = new ICharacter[] { p.Hero, p.Opponent.Hero, friend, near, far, spellPower };
            var hit = new HashSet<int>();
            for (int i = 0; i < 12; i++)
            {
                foreach (var c in all) c.Damage = 0;
                var bomber = (Minion)Play(g, p, id);
                Check(all.Sum(c => c.Damage) == 3, "exactly three damage lands, split across other characters (cast " + i + ")");
                Check(bomber.Damage == 0, "Mad Bomber never hits itself");
                foreach (var c in all.Where(c => c.Damage > 0)) hit.Add(c.Id);
                Kill(g, bomber);
            }
            Check(hit.Count >= 3, "the split reaches different characters across casts: " + hit.Count);
            Check(hit.Contains(p.Hero.Id) || hit.Contains(p.Opponent.Hero.Id), "heroes are eligible");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            p.Hero.Damage = 0; p.Opponent.Hero.Damage = 0;
            var alone = (Minion)Play(g, p, id);
            Check(p.Hero.Damage + p.Opponent.Hero.Damage == 3 && alone.Damage == 0, "with empty boards only the two heroes take the 3");
            Replay(g, b => {
                var other = Summon(b.Player2, Yeti); var mine = Summon(b.Player1, Yeti);
                var branchAll = new ICharacter[] { b.Player1.Hero, b.Player2.Hero, other, mine };
                foreach (var c in branchAll) c.Damage = 0;
                Play(b, b.Player1, id);
                Check(branchAll.Sum(c => c.Damage) == 3, "split continues deterministically on clone and snapshot");
            });
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_083", (g, p, id) => {
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var lonely = (Minion)Play(g, p, id);
            Check(p.BoardZone.Count == 1 && lonely.Card.Id == id && p.Opponent.BoardZone.Count == 0, "no other minion: nothing happens");
            var outcomes = new HashSet<string>();
            for (int i = 0; i < 24; i++)
            {
                StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
                var a = Summon(p, Yeti); var b2 = Summon(p.Opponent, Yeti); var c2 = Summon(p.Opponent, Yeti);
                a.Damage = 3; b2.Damage = 2;
                var tinker = (Minion)Play(g, p, id);
                Check(tinker.Card.Id == id && tinker.AttackDamage == 3 && tinker.Health == 3 && tinker.Controller == p, "Tinkmaster is never its own target");
                var changed = p.BoardZone.Concat(p.Opponent.BoardZone).Where(m => m.Card.Id != id && m.Card.Id != Yeti).ToArray();
                Check(changed.Length == 1, "exactly one other minion is transformed (cast " + i + ")");
                var result = changed[0];
                Check((result.Card.Id == "EX1_tk29" && result.AttackDamage == 5 && result.Health == 5)
                    || (result.Card.Id == "EX1_tk28" && result.AttackDamage == 1 && result.Health == 1), "result is a fresh 5/5 Devilsaur or 1/1 Squirrel");
                Check(result.Damage == 0, "transformed minion has no old damage");
                outcomes.Add(result.Card.Id + "/" + (result.Controller == p ? "mine" : "theirs"));
            }
            Check(outcomes.Count >= 3, "both forms and both sides occur over casts: " + string.Join(",", outcomes));
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var hoarder = Summon(p.Opponent, "EX1_096");
            var tink = (Minion)Play(g, p, id);
            int hand = p.Opponent.HandZone.Count, deck = p.Opponent.DeckZone.Count;
            var turned = p.Opponent.BoardZone.Single();
            Check(turned.Card.Id.StartsWith("EX1_tk2"), "Loot Hoarder was transformed");
            Kill(g, turned);
            Check(p.Opponent.HandZone.Count == hand && p.Opponent.DeckZone.Count == deck, "the transform removed the Deathrattle");
            Kill(g, tink);
            var kept = Summon(p.Opponent, Yeti); var maker = (Minion)Play(g, p, id);
            var result2 = p.Opponent.BoardZone.Single(); Check(result2.Card.Id.StartsWith("EX1_tk2"), "target transformed");
            Mute(g, maker); Kill(g, maker);
            Check(result2.Zone.Type == Zone.PLAY && result2.Card.Id.StartsWith("EX1_tk2"), "Tinkmaster leaving play does not revert the transform");
            Replay(g, b => {
                StandardSetsProof.ParkBoard(b.Player1); StandardSetsProof.ParkBoard(b.Player2);
                var x = Summon(b.Player2, Yeti); Play(b, b.Player1, id);
                Check(((Minion)b.IdEntityDic[x.Id]).Card.Id.StartsWith("EX1_tk2"), "transform is deterministic on clone and snapshot");
            });
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_093", (g, p, id) => {
            var left = Summon(p, Yeti); var right = Summon(p, Yeti); var far = Summon(p, Yeti);
            var foe = Summon(p.Opponent, Yeti);
            left.Damage = 2;
            var defender = StandardSetsProof.PlayAt(g, p, id, 1);
            Check(defender.AttackDamage == 3 && defender.Health == 3 && !defender.HasTaunt, "Defender of Argus is a plain 3/3");
            Check(left.AttackDamage == 5 && left.Health == 4 && left.Damage == 2 && left.HasTaunt, "left neighbour gains +1/+1 and Taunt, keeping its damage");
            Check(right.AttackDamage == 5 && right.Health == 6 && right.HasTaunt, "right neighbour gains +1/+1 and Taunt");
            Check(far.AttackDamage == 4 && !far.HasTaunt && foe.AttackDamage == 4 && !foe.HasTaunt, "others are untouched");
            Replay(g, b => {
                End(b); End(b);
                var l = (Minion)b.IdEntityDic[left.Id];
                Check(l.AttackDamage == 5 && l.HasTaunt, "the buff is permanent");
            });
            Mute(g, defender);
            Check(left.AttackDamage == 5 && right.HasTaunt, "Silencing the Defender keeps the buffs");
            Mute(g, right);
            Check(right.AttackDamage == 4 && right.Health == 5 && !right.HasTaunt, "Silencing the neighbour removes it");
            StandardSetsProof.ParkBoard(p);
            var edgeTarget = Summon(p, Yeti);
            var edge = StandardSetsProof.PlayAt(g, p, id, 0);
            Check(edge.ZonePosition == 0 && edgeTarget.AttackDamage == 5 && edgeTarget.HasTaunt, "at the edge only the single neighbour gains");
            StandardSetsProof.ParkBoard(p);
            var alone = (Minion)Play(g, p, id);
            Check(p.BoardZone.Count == 1 && alone.AttackDamage == 3 && !alone.HasTaunt, "empty board: nothing to buff, no self buff");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_095", (g, p, id) => {
            var auctioneer = (Minion)Play(g, p, id);
            int deck = p.Opponent.DeckZone.Count, mine = p.DeckZone.Count, hand = p.HandZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.DeckZone.Count == mine - 1 && p.HandZone.Count == hand + 1, "a cast spell draws one card");
            Check(Hidden(g, p.Opponent.PlayerId, p.HandZone.Last().Id) && !Hidden(g, p.PlayerId, p.HandZone.Last().Id),
                "the drawn card is hidden from the opponent and visible to its owner");
            Play(g, p, Yeti);
            Check(p.DeckZone.Count == mine - 1, "playing a minion draws nothing");
            Play(g, p, IceBarrier);
            Check(p.DeckZone.Count == mine - 2, "a Secret is a spell");
            Replay(g, b => {
                End(b); int own = b.Player1.DeckZone.Count; Play(b, b.Player2, Moonfire, b.Player1.Hero);
                Check(b.Player1.DeckZone.Count == own, "the enemy's spells draw nothing for the owner");
                End(b); own = b.Player1.DeckZone.Count; int before = b.Player1.HandZone.Count;
                Play(b, b.Player1, Moonfire, b.Player2.Hero);
                Check(b.Player1.DeckZone.Count == own - 1 && b.Player1.HandZone.Count == before + 1, "draw continues on clone and snapshot");
            });
            var second = Summon(p, id);
            mine = p.DeckZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.DeckZone.Count == mine - 2, "two Auctioneers draw two");
            Mute(g, second);
            mine = p.DeckZone.Count; Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.DeckZone.Count == mine - 1, "a silenced Auctioneer draws nothing");
            Summon(p, id);
            while (p.HandZone.Count < 9) Add(p, Yeti);
            mine = p.DeckZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.HandZone.Count == 10 && p.DeckZone.Count == mine - 2, "with a full hand the second draw is burned");
            foreach (var m in p.BoardZone.Where(m => m.Card.Id == id).ToArray()) Kill(g, m);
            Trim(p, 3);
            mine = p.DeckZone.Count; Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.DeckZone.Count == mine, "no Auctioneer, no draw");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_096", (g, p, id) => {
            var hoarder = Summon(p, id);
            int deck = p.DeckZone.Count, hand = p.HandZone.Count, enemyHand = p.Opponent.HandZone.Count, enemyDeck = p.Opponent.DeckZone.Count;
            Check(hoarder.AttackDamage == 2 && hoarder.Health == 1, "Loot Hoarder is a 2/1");
            Kill(g, hoarder);
            Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == hand + 1 && p.Opponent.HandZone.Count == enemyHand
                && p.Opponent.DeckZone.Count == enemyDeck, "owner draws exactly one on death");
            var drawn = p.HandZone.Last();
            Check(Hidden(g, p.Opponent.PlayerId, drawn.Id) && !Hidden(g, p.PlayerId, drawn.Id), "the drawn card is hidden from the opponent, shown to its owner");
            var silenced = Summon(p, id); Mute(g, silenced); deck = p.DeckZone.Count; Kill(g, silenced);
            Check(p.DeckZone.Count == deck, "Silence removes the Deathrattle");
            var enemy = Summon(p.Opponent, id); enemyDeck = p.Opponent.DeckZone.Count; deck = p.DeckZone.Count;
            Replay(g, b => {
                int own = b.Player1.DeckZone.Count, theirs = b.Player2.DeckZone.Count;
                Kill(b, (Minion)b.IdEntityDic[enemy.Id]);
                Check(b.Player2.DeckZone.Count == theirs - 1 && b.Player1.DeckZone.Count == own, "an enemy Loot Hoarder draws for its own owner");
            });
            var full = Summon(p, id); while (p.HandZone.Count < 10) Add(p, Yeti);
            deck = p.DeckZone.Count; Kill(g, full);
            Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand: the drawn card is burned");
            var attacker = Summon(p, id); var blocker = Summon(p.Opponent, Yeti);
            Trim(p, 5);
            deck = p.DeckZone.Count; hand = p.HandZone.Count;
            Attack(g, attacker, blocker);
            Check(attacker.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1, "dying in combat draws");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_097", (g, p, id) => {
            var abomination = Summon(p, id);
            var friend = Summon(p, Yeti); var enemy = Summon(p.Opponent, Yeti); var frail = Summon(p.Opponent, "CS2_231");
            Summon(p, "EX1_284");
            var shielded = Summon(p.Opponent, "EX1_067");
            Check(abomination.HasTaunt && abomination.AttackDamage == 4 && abomination.Health == 4, "Abomination is a 4/4 Taunt");
            Kill(g, abomination);
            Check(friend.Damage == 2 && enemy.Damage == 2 && p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 2,
                "two damage to every character, both heroes and both sides, ignoring Spell Damage");
            Check(frail.Zone.Type == Zone.GRAVEYARD && !shielded.HasDivineShield && shielded.Damage == 0, "kills 1-Health minions; Divine Shield absorbs");
            var silenced = Summon(p, id); Mute(g, silenced); int damage = p.Hero.Damage; Kill(g, silenced);
            Check(p.Hero.Damage == damage && friend.Damage == 2, "Silence removes the Deathrattle");
            friend.Damage = 0; enemy.Damage = 0;
            var chainA = Summon(p, id); var chainB = Summon(p.Opponent, id); chainB.Damage = 2;
            damage = p.Opponent.Hero.Damage; int enemyDamage = enemy.Damage;
            Replay(g, b => {
                Kill(b, (Minion)b.IdEntityDic[chainA.Id]);
                Check(b.Player2.Hero.Damage == damage + 4 && ((Minion)b.IdEntityDic[enemy.Id]).Damage == enemyDamage + 4
                    && ((Minion)b.IdEntityDic[chainB.Id]).Zone.Type == Zone.GRAVEYARD, "a second Abomination dying in the blast chains its own Deathrattle");
            });
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_100", (g, p, id) => {
            var cho = (Minion)Play(g, p, id);
            Check(cho.AttackDamage == 0 && cho.Health == 4, "Lorewalker Cho is a 0/4");
            int enemyHand = p.Opponent.HandZone.Count, ownHand = p.HandZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.Opponent.HandZone.Count == enemyHand + 1 && p.Opponent.HandZone.Last().Card.Id == Moonfire
                && p.HandZone.Count == ownHand, "my spell is copied to the opponent's hand, not mine");
            Check(HandShows(g, p.Opponent.PlayerId, p.Opponent.PlayerId, Moonfire) && !HandShows(g, p.PlayerId, p.Opponent.PlayerId, Moonfire),
                "the copy is visible to its new owner and hidden from the caster's fair view");
            Play(g, p, Yeti);
            Check(p.Opponent.HandZone.Count == enemyHand + 1, "minions are not copied");
            Replay(g, b => {
                End(b); var opp = b.Player2; int mine = b.Player1.HandZone.Count; int his = opp.HandZone.Count;
                Play(b, opp, Moonfire, b.Player1.Hero);
                Check(b.Player1.HandZone.Count == mine + 1 && b.Player1.HandZone.Last().Card.Id == Moonfire && opp.HandZone.Count == his,
                    "the opponent's spell is copied to me");
                End(b);
            });
            var second = Summon(p, id);
            enemyHand = p.Opponent.HandZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.Opponent.HandZone.Count == enemyHand + 2, "two Chos copy twice");
            Mute(g, second); enemyHand = p.Opponent.HandZone.Count;
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.Opponent.HandZone.Count == enemyHand + 1, "a silenced Cho does nothing");
            Kill(g, second); Kill(g, cho);
            while (p.Opponent.HandZone.Count < 10) Add(p.Opponent, Yeti);
            Summon(p, id);
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(p.Opponent.HandZone.Count == 10, "a full hand receives nothing");
            Play(g, p, IceBarrier);
            Check(p.Opponent.HandZone.Count == 10 && p.SecretZone.Count == 1, "Secrets: full hand again, still just one on my side");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_102", (g, p, id) => {
            var friend = Summon(p, Yeti); var near = Summon(p.Opponent, Yeti); var far = Summon(p.Opponent, Yeti);
            var demolisher = Summon(p, id);
            var targets = new ICharacter[] { p.Opponent.Hero, near, far };
            Check(demolisher.AttackDamage == 1 && demolisher.Health == 4, "Demolisher is a 1/4");
            End(g);
            Check(targets.Sum(c => c.Damage) == 0 && friend.Damage == 0 && p.Hero.Damage == 0, "nothing at the enemy's turn start");
            End(g);
            Check(targets.Sum(c => c.Damage) == 2 && friend.Damage == 0 && p.Hero.Damage == 0 && demolisher.Damage == 0,
                "2 damage to one enemy character at my turn start, none on my side");
            var victims = new HashSet<int>(targets.Where(c => c.Damage > 0).Select(c => c.Id));
            for (int turn = 0; turn < 9; turn++)
            {
                foreach (var c in targets) c.Damage = 0;
                End(g); End(g);
                Check(targets.Sum(c => c.Damage) == 2, "one hit each turn");
                foreach (var c in targets.Where(c => c.Damage > 0)) victims.Add(c.Id);
            }
            Check(victims.Count >= 2, "the victim is random over turns: " + victims.Count);
            Replay(g, b => {
                var t = new ICharacter[] { b.Player2.Hero, b.IdEntityDic[near.Id] as ICharacter, b.IdEntityDic[far.Id] as ICharacter };
                foreach (var c in t) c.Damage = 0;
                End(b); End(b);
                Check(t.Sum(c => c.Damage) == 2, "trigger continues on clone and snapshot");
            });
            foreach (var c in targets) c.Damage = 0;
            Mute(g, demolisher); End(g); End(g);
            Check(targets.Sum(c => c.Damage) == 0, "Silence removes the trigger");
            var second = Summon(p, id);
            StandardSetsProof.ParkBoard(p.Opponent);
            End(g); End(g);
            Check(p.Opponent.Hero.Damage == 2, "with no enemy minions the hero is hit");
            Kill(g, second); p.Opponent.Hero.Damage = 0; End(g); End(g);
            Check(p.Opponent.Hero.Damage == 0, "a dead Demolisher does nothing");
            var enemyOwned = Summon(p.Opponent, id);
            End(g);
            Check(p.Hero.Damage + p.BoardZone.Sum(m => m.Damage) == 2, "an enemy Demolisher hits my side at its owner's turn start");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_103", (g, p, id) => {
            var raider = Summon(p, Murloc); var yeti = Summon(p, Yeti); var enemyMurloc = Summon(p.Opponent, Murloc);
            var handMurloc = Add(p, Murloc);
            var seer = (Minion)Play(g, p, id);
            Check(raider.Health == 3 && raider.AttackDamage == 2, "a friendly Murloc gains +2 Health");
            Check(seer.Health == 3 && seer.AttackDamage == 2, "the Seer does not buff itself");
            Check(yeti.Health == 5 && enemyMurloc.Health == 1 && ((Minion)handMurloc).Health == 1, "non-Murlocs, enemy Murlocs and held Murlocs are untouched");
            Replay(g, b => { End(b); End(b); Check(((Minion)b.IdEntityDic[raider.Id]).Health == 3, "the buff is permanent"); });
            Mute(g, raider);
            Check(raider.Health == 1, "Silence removes the buff");
            var second = (Minion)Play(g, p, id);
            Check(raider.Health == 3 && seer.Health == 5, "a second Seer buffs the first (other Murlocs)");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var alone = (Minion)Play(g, p, id);
            Check(alone.Health == 3 && p.BoardZone.Count == 1, "no other Murloc: nothing happens");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_105", (g, p, id) => {
            bool Playable(IPlayable card) => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
            var giant = Add(p, id);
            g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 12, "alone in hand the Giant costs 12");
            Check(!Playable(giant), "12 is not payable with 10 crystals");
            Add(p, Yeti); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 11 && !Playable(giant), "one other card: 11, not payable: " + giant.Cost);
            Add(p, Yeti); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 10 && Playable(giant), "two others: 10, exactly payable: " + giant.Cost);
            Add(p.Opponent, Yeti); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 10, "the opponent's hand does not matter");
            var twin = Add(p, id); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 9 && twin.Cost == 9, "two Giants each count the other: " + giant.Cost + "/" + twin.Cost);
            p.HandZone.Remove(twin); p.SetasideZone.Add(twin); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 10, "a card leaving the hand raises it");
            Replay(g, b => {
                var held = (IPlayable)b.IdEntityDic[giant.Id];
                Check(held.Cost == 10, "cost follows the hand on clone and snapshot");
                Add(b.Player1, Yeti); b.DeathProcessingAndAuraUpdate(); Check(held.Cost == 9, "a drawn card lowers it");
                Trim(b.Player1, b.Player1.HandZone.Count - 1); b.DeathProcessingAndAuraUpdate();
                Check(held.Cost == 10, "a removed card raises it");
            });
            while (p.HandZone.Count < 10) Add(p, Yeti);
            g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 3, "nine other cards: 3");
            Trim(p, 3, giant.Id); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 10, "back to two other cards");
            PlayEntity(g, p, giant);
            var board = p.BoardZone.Single(m => m.Card.Id == id);
            Check(board.AttackDamage == 8 && board.Health == 8 && p.UsedMana == 10, "the Giant is an 8/8 played for its current cost");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_116", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti);
            var leeroy = (Minion)Play(g, p, id);
            Check(leeroy.AttackDamage == 6 && leeroy.Health == 2 && leeroy.HasCharge, "Leeroy is a 6/2 Charge");
            var whelps = p.Opponent.BoardZone.Where(m => m.Card.Id == "EX1_116t").ToArray();
            Check(whelps.Length == 2 && whelps.All(m => m.Controller == p.Opponent && m.AttackDamage == 1 && m.Health == 1) && p.BoardZone.Count == 1,
                "two 1/1 Whelps for the opponent");
            Check(p.Opponent.BoardZone.Last().Card.Id == "EX1_116t" && p.Opponent.BoardZone.First().Id == enemy.Id, "Whelps enter at the right end");
            Check(CanAttack(p, leeroy, enemy), "Charge");
            Replay(g, b => {
                StandardSetsProof.ParkBoard(b.Player2);
                for (int i = 0; i < 6; i++) Summon(b.Player2, Yeti);
                Play(b, b.Player1, id);
                Check(b.Player2.BoardZone.Count == 7 && b.Player2.BoardZone.Count(m => m.Card.Id == "EX1_116t") == 1,
                    "with six enemy minions only one Whelp fits");
            });
            StandardSetsProof.ParkBoard(p.Opponent);
            for (int i = 0; i < 7; i++) Summon(p.Opponent, Yeti);
            Play(g, p, id);
            Check(p.Opponent.BoardZone.Count == 7 && !p.Opponent.BoardZone.Any(m => m.Card.Id == "EX1_116t"), "full enemy board: no Whelps");
            StandardSetsProof.ParkBoard(p.Opponent);
            End(g);
            Play(g, g.Player2, id);
            Check(p.BoardZone.Count(m => m.Card.Id == "EX1_116t") == 2 && g.Player2.BoardZone.Count == 1, "played by the opponent, the Whelps are mine");
            Summon(p, id);
            Check(p.BoardZone.Count(m => m.Card.Id == "EX1_116t") == 2, "summoning without playing triggers no Battlecry");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_162", (g, p, id) => {
            var left = Summon(p, Yeti); var right = Summon(p, Yeti); var far = Summon(p, Yeti);
            var foe = Summon(p.Opponent, Yeti);
            var wolf = StandardSetsProof.PlayAt(g, p, id, 1);
            Check(wolf.AttackDamage == 2 && wolf.Health == 2, "Dire Wolf Alpha is a 2/2");
            Check(left.AttackDamage == 5 && right.AttackDamage == 5 && far.AttackDamage == 4 && foe.AttackDamage == 4 && wolf.AttackDamage == 2,
                "only the two neighbours gain +1 Attack, not the wolf, the far minion or the enemy");
            Check(p.Hero.AttackDamage == 0, "heroes are not buffed");
            var sergeant = (Minion)Play(g, p, "CS2_188", left);
            Check(left.AttackDamage == 7, "aura stacks with a temporary buff");
            Kill(g, sergeant);
            Replay(g, b => {
                End(b); Check(((Minion)b.IdEntityDic[left.Id]).AttackDamage == 5, "only the aura remains after the temporary buff ends");
                End(b);
            });
            Mute(g, left);
            Check(left.AttackDamage == 5, "Silencing a neighbour keeps the aura");
            var newcomer = StandardSetsProof.PlayAt(g, p, id, 3);
            Check(newcomer.AttackDamage == 2 && far.AttackDamage == 5 && right.AttackDamage == 6, "a second wolf buffs its own neighbours");
            Kill(g, right);
            Check(wolf.AttackDamage == 3 && newcomer.AttackDamage == 3 && left.AttackDamage == 5 && far.AttackDamage == 5,
                "after a neighbour dies the next minion is adjacent; adjacent wolves buff each other");
            Mute(g, wolf);
            Check(left.AttackDamage == 4 && newcomer.AttackDamage == 2 && wolf.AttackDamage == 3 && far.AttackDamage == 5, "Silence removes the aura: " + Stats(left, wolf, newcomer, far));
            Kill(g, newcomer);
            Check(far.AttackDamage == 4 && wolf.AttackDamage == 2 && left.AttackDamage == 4, "a dead wolf's aura disappears");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_170", (g, p, id) => {
            var victim = Summon(p.Opponent, Yeti); var shielded = Summon(p.Opponent, "EX1_067");
            var cobra = (Minion)Play(g, p, id);
            Check(cobra.Poisonous && cobra.AttackDamage == 2 && cobra.Health == 3, "Emperor Cobra is a 2/3 Poisonous");
            Attack(g, cobra, victim);
            Check(victim.Zone.Type == Zone.GRAVEYARD, "Poisonous destroys a healthy 5-Health minion");
            var second = (Minion)Play(g, p, id);
            Attack(g, second, shielded);
            Check(shielded.Zone.Type == Zone.PLAY && !shielded.HasDivineShield, "Divine Shield absorbs the poison");
            var third = (Minion)Play(g, p, id);
            Attack(g, third, p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 2 && g.State != State.COMPLETE, "heroes take plain damage");
            Replay(g, b => {
                var target = Summon(b.Player2, Yeti);
                var attacker = Summon(b.Player1, id);
                Attack(b, attacker, target);
                Check(target.Zone.Type == Zone.GRAVEYARD, "poison continues on clone and snapshot");
            });
            var silenced = (Minion)Play(g, p, id); Mute(g, silenced);
            var tough = Summon(p.Opponent, Yeti);
            Attack(g, silenced, tough);
            Check(tough.Zone.Type == Zone.PLAY && tough.Damage == 2, "Silence removes Poisonous");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var guard = Summon(p, id); var raider = Summon(p.Opponent, Yeti);
            End(g); Attack(g, raider, guard);
            Check(raider.Zone.Type == Zone.GRAVEYARD, "attacking into a Cobra destroys the attacker");
        });
    }

    private static void Mute(Game game, Minion minion) { minion.Silence(); game.DeathProcessingAndAuraUpdate(); }

    private static string Stats(params Minion[] ms) => string.Join(" ", ms.Select(m => m.AttackDamage + "/" + m.Health));

    // A Deathrattle summon leaves its minion pending until the next cleanup pass, as after every real action.
    private static void Slay(Game game, Minion minion)
    {
        Kill(game, minion);
        while (game.SummonedMinions.Count != 0) game.DeathProcessingAndAuraUpdate();
    }

    private static IPlayable Held(Game game, Controller owner, string id)
    {
        var card = Add(owner, id); game.DeathProcessingAndAuraUpdate(); return card;
    }

    // BLOCKED-HSDATA: 37.0 English text differs and no hsdata CardDefs was available to regenerate the locale files.
    // Proven against the identity rows on 2026-11-15; register with WildSetsProof.Case("EX1_085", Blocked_EX1_085) once the batch list has it.
    private static void Blocked_EX1_085(Game g, Controller p, string id)
    {
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            for (int i = 0; i < 3; i++) Summon(p.Opponent, Yeti);
            for (int i = 0; i < 5; i++) Summon(p, Yeti);
            var tech = (Minion)Play(g, p, id);
            Check(tech.AttackDamage == 3 && tech.Health == 3 && p.Opponent.BoardZone.Count == 3 && p.BoardZone.Count == 6,
                "three enemy minions: no control; friendly minions do not count");
            StandardSetsProof.ParkBoard(p);
            var stealth = Summon(p.Opponent, "CS2_059");
            var pool = p.Opponent.BoardZone.ToArray();
            foreach (var m in pool) m.Damage = Math.Min(1, m.Health - 1);
            var ids = pool.Select(m => m.Id).ToHashSet();
            Replay(g, b => {
                Play(b, b.Player1, id);
                var taken = b.Player1.BoardZone.Where(m => ids.Contains(m.Id)).ToArray();
                Check(taken.Length == 1 && b.Player2.BoardZone.Count == 3 && taken[0].Controller == b.Player1,
                    "four enemy minions (Stealth counts): exactly one is taken");
                Check(taken[0].IsExhausted && !CanAttack(b.Player1, taken[0], b.Player2.Hero), "a stolen minion cannot attack this turn");
                Check(taken[0].Damage == Math.Min(1, taken[0].Card.Health - 1), "the stolen minion keeps its damage");
            });
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            for (int i = 0; i < 6; i++) { Summon(p.Opponent, Yeti); Summon(p, Yeti); }
            int before = p.Opponent.BoardZone.Count;
            var full = (Minion)Play(g, p, id);
            Check(p.BoardZone.Count == 7 && p.Opponent.BoardZone.Count == before - 1 && p.BoardZone.All(m => m.Controller == p),
                "full board: the chosen enemy minion is destroyed, not taken");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            for (int i = 0; i < 4; i++) Summon(p, Yeti);
            var mine = p.BoardZone.Select(m => m.Id).ToHashSet();
            End(g);
            Play(g, g.Player2, id);
            Check(g.Player2.BoardZone.Count == 2 && p.BoardZone.Count == 3 && g.Player2.BoardZone.Any(m => mine.Contains(m.Id)),
                "the opponent seat uses it on the first player's minions");
    }

    // BLOCKED-HSDATA: 37.0 English text differs and no hsdata CardDefs was available to regenerate the locale files.
    // Proven against the identity rows on 2026-11-15; register with WildSetsProof.Case("EX1_089", Blocked_EX1_089) once the batch list has it.
    private static void Blocked_EX1_089(Game g, Controller p, string id)
    {
            p.Opponent.BaseMana = 5; p.Opponent.UsedMana = 0;
            var enemy = Summon(p.Opponent, Yeti);
            var golem = (Minion)Play(g, p, id);
            Check(golem.AttackDamage == 4 && golem.Health == 2 && golem.HasCharge, "Arcane Golem is a 4/2 Charge");
            Check(p.Opponent.BaseMana == 6 && p.Opponent.RemainingMana == 5 && p.BaseMana == 10, "the opponent gains one empty crystal (spendable mana unchanged), the owner none: " + p.Opponent.BaseMana + "/" + p.Opponent.RemainingMana);
            Check(CanAttack(p, golem, enemy), "Charge");
            Mute(g, golem);
            Check(p.Opponent.BaseMana == 6, "Silence does not take the crystal back");
            p.Opponent.BaseMana = 10;
            Play(g, p, id);
            Check(p.Opponent.BaseMana == 10, "a full crystal row gains nothing");
            p.Opponent.BaseMana = 9; p.Opponent.UsedMana = 4;
            Play(g, p, id);
            Check(p.Opponent.BaseMana == 10 && p.Opponent.RemainingMana == 5, "an empty crystal is added without refreshing spent ones: " + p.Opponent.RemainingMana);
            p.Opponent.BaseMana = 6; p.Opponent.UsedMana = 0;
            Replay(g, b => {
                End(b);
                Check(b.Player2.BaseMana == 7 && b.Player2.RemainingMana == 7, "the extra crystal is a real crystal on the next turn");
            });
    }

    // BLOCKED-HSDATA: 37.0 English text differs and no hsdata CardDefs was available to regenerate the locale files.
    // Proven against the identity rows on 2026-11-15; register with WildSetsProof.Case("EX1_110", Blocked_EX1_110) once the batch list has it.
    // Needs `cards.Add("EX1_110t", new CardDef { Configure = card => card.SetPrintedStats(5, 5) })` in Expert1CardsGen (37.0 Baine is 5/5);
    // held back because it moves 26 audited classic-source golden games (warrior-control) and belongs with Cairne's identity row.
    private static void Blocked_EX1_110(Game g, Controller p, string id)
    {
            var left = Summon(p, Yeti);
            var cairne = StandardSetsProof.PlayAt(g, p, id, 1);
            var right = Summon(p, Yeti);
            Check(cairne.AttackDamage == 5 && cairne.Health == 5 && cairne.HasTaunt, "Cairne is a 5/5 Taunt");
            Replay(g, b => {
                Slay(b, (Minion)b.IdEntityDic[cairne.Id]);
                var baine = b.Player1.BoardZone.Single(m => m.Card.Id == "EX1_110t");
                Check(baine.AttackDamage == 5 && baine.Health == 5 && baine.ZonePosition == 1 && baine.Controller == b.Player1,
                    "the Deathrattle summons a 5/5 Baine in Cairne's slot: " + baine.AttackDamage + "/" + baine.Health + " pos " + baine.ZonePosition);
                Check(b.Player2.BoardZone.Count == 0, "nothing on the enemy side");
            });
            var quiet = Summon(p, id); Mute(g, quiet); int baines = p.BoardZone.Count(m => m.Card.Id == "EX1_110t"); Slay(g, quiet);
            Check(p.BoardZone.Count(m => m.Card.Id == "EX1_110t") == baines, "Silence removes the Deathrattle");
            StandardSetsProof.ParkBoard(p);
            var full = Summon(p, id);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Check(p.BoardZone.Count == 7, "fixture: full board");
            Slay(g, full);
            Check(p.BoardZone.Count == 7 && p.BoardZone.Any(m => m.Card.Id == "EX1_110t"), "on a full board the freed slot takes Baine");
            var enemyCairne = Summon(p.Opponent, id);
            Slay(g, enemyCairne);
            Check(p.Opponent.BoardZone.Single().Card.Id == "EX1_110t" && p.Opponent.BoardZone.Single().Controller == p.Opponent
                && p.Opponent.BoardZone.Single().AttackDamage == 5, "an enemy Cairne gives its owner the Baine");
    }

    private static void Check(bool ok, string why) => Core2026Proof.Check(ok, why);

    private static bool CanAttack(Controller player, Minion minion, ICharacter target) =>
        player.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id && t.Target?.Id == target.Id);

    private static void Trim(Controller player, int keep, int protect = -1)
    {
        while (player.HandZone.Count > keep)
        {
            var card = player.HandZone.Last(c => c.Id != protect);
            player.HandZone.Remove(card); player.SetasideZone.Add(card);
        }
    }

    private static Game FairView(Game game, int viewer)
    {
        var state = FairBotState.Create(game, viewer, 71, DeckStyle.Value);
        return (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(state);
    }

    // True when the seat `viewer`'s fair view hides the entity's identity (an entity the view does not carry leaks nothing).
    private static bool Hidden(Game game, int viewer, int entityId)
    {
        var view = FairView(game, viewer);
        try { return view.IdEntityDic[entityId] is Unknown; }
        catch (Exception e) when (e is KeyNotFoundException || e is ArgumentOutOfRangeException) { return true; }
    }

    // True when the viewer's fair view shows a card with this id in the owner's hand.
    private static bool HandShows(Game game, int viewer, int owner, string cardId)
    {
        var view = FairView(game, viewer);
        var hand = (view.Player1.PlayerId == owner ? view.Player1 : view.Player2).HandZone;
        return hand.Any(c => c is not Unknown && c.Card.Id == cardId);
    }

    // Runs the continuation on the game, an exact clone and a snapshot restore; all three must end in the same state.
    private static void Replay(Game game, Action<Game> continuation)
    {
        var branches = new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) };
        foreach (var next in branches) Check(Canonical.State(game) == Canonical.State(next), "l2a state before continuation");
        continuation(game);
        foreach (var next in branches)
        {
            continuation(next);
            Check(Canonical.State(game) == Canonical.State(next), "l2a state after continuation matches the original branch");
        }
    }
}
