using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0bM1BProof;

/// <summary>Wave Y0b lane m1b: Curse of Naxxramas scripts proven against the 37.0 text. Run from <see cref="WildY0bM1BProof.Run"/>,
/// which owns the group; blocked ids are in <see cref="BlockedProofs"/> (see the BRM file for the reasons).</summary>
internal static class WildY0bM1BNaxxProof
{
    internal static void Run()
    {
        WildSetsProof.Case("FP1_002", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 1 && card.Health == 2 && card.IsRace(Race.BEAST), "Haunted Creeper is a 2-mana 1/2 Beast");
            var left = Summon(p, "CS2_182"); var creeper = Summon(p, id); var right = Summon(p, "CS2_182");
            Resume(g, next => {
                var twin = (Minion)next.IdEntityDic[creeper.Id]; Kill(next, twin);
                Core2026Proof.Check(twin.Controller.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, creeper);
            Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS2_182", "FP1_002t", "FP1_002t", "CS2_182" })
                && p.BoardZone[0].Id == left.Id && p.BoardZone[3].Id == right.Id, "two Spiders take the Creeper's place, between its neighbours");
            Core2026Proof.Check(p.BoardZone.Where(m => m.Card.Id == "FP1_002t").All(m => m.AttackDamage == 1 && m.Health == 1 && m.Controller == p && m.Card.IsRace(Race.BEAST)),
                "Spectral Spiders are 1/1 Beasts on the owner's side: " + string.Join(",", p.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health + ":" + m.Card.Races.Count + ":" + m.Controller.PlayerId)));
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
            var six = Summon(p, id); Kill(g, six);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "five minions and the Creeper: both Spiders fit");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            var seven = Summon(p, id); Kill(g, seven);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 1, "a full board: only one Spider fits");
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
            var baron = Summon(p, "FP1_031"); var doubled = Summon(p, id); Kill(g, doubled);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 4, "Baron Rivendare doubles the Deathrattle");
            Kill(g, baron);
            End(g);
            StandardSetsProof.ParkBoard(opp);
            var stolen = Summon(p, id); int mineSpiders = p.BoardZone.Count(m => m.Card.Id == "FP1_002t"); Play(g, opp, "CS1_113", stolen);
            Core2026Proof.Check(stolen.Controller == opp, "fixture: the Creeper changed sides");
            Kill(g, stolen);
            Core2026Proof.Check(opp.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2 && p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == mineSpiders, "a stolen Creeper's Spiders join its current controller");
            StandardSetsProof.ParkBoard(opp);
            var theirs = Summon(opp, id); StandardSetsProof.ParkBoard(p); Kill(g, theirs);
            Core2026Proof.Check(opp.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2 && p.BoardZone.Count == 0, "the opponent's Creeper leaves Spiders on the opponent's side");
            End(g); // a real action boundary: the proof kills outside Process, so summoned-minion bookkeeping is still open
        });
        WildSetsProof.Case("FP1_003", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 1 && card.Health == 2, "Echoing Ooze is a 2-mana 1/2");
            var neighbour = Summon(p, "CS2_182");
            var ooze = (Minion)Play(g, p, id); var after = Summon(p, "CS2_168");
            Core2026Proof.Check(p.BoardZone.Count == 3, "no copy at once: the copy comes at the end of the turn");
            Play(g, p, "CS2_092", ooze);
            Core2026Proof.Check(ooze.AttackDamage == 5 && ooze.Health == 6, "fixture: the Ooze is buffed before the copy");
            Resume(g, next => {
                End(next);
                var board = next.Player1.BoardZone.ToArray();
                Core2026Proof.Check(board.Length == 4 && board[0].Id == ooze.Id && board[1].Card.Id == id && board[1].AttackDamage == 5 && board[1].Health == 6,
                    "the end of turn adds a buffed copy right of the Ooze, on a clone and a snapshot restore: " + string.Join(",", board.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health)));
            });
            End(g);
            var copy = p.BoardZone[1];
            Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { ooze.Id, copy.Id, neighbour.Id, after.Id }) && copy.Card.Id == id && copy.Controller == p
                && copy.AttackDamage == 5 && copy.Health == 6, "an exact copy, buffs included, immediately right of the Ooze");
            End(g); End(g); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 4, "neither Ooze nor its copy copies again at later turn ends");
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            var silenced = (Minion)Play(g, p, id); silenced.Silence(); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 1, "Silence before the end of the turn: no copy");
            End(g);
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            var doomed = (Minion)Play(g, p, id); Kill(g, doomed); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 0, "an Ooze that died before the end of the turn leaves no copy");
            End(g);
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            Play(g, p, id); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "a full board: no copy");
            End(g);
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            Play(g, p, id); Play(g, p, id); End(g);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 4, "two Oozes make one copy each");
            End(g);
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            Summon(p, id); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 1, "summoning has no Battlecry");
            EmptyHand(opp); StandardSetsProof.ParkBoard(opp);
            Play(g, opp, id);
            Core2026Proof.Check(opp.BoardZone.Count == 1, "the opponent's Ooze makes no copy at once");
            End(g);
            Core2026Proof.Check(opp.BoardZone.Count == 2 && opp.BoardZone.All(m => m.Card.Id == id) && p.BoardZone.Count == 1,
                "the opponent's copy comes at the end of the opponent's turn, on the opponent's side");
            End(g); End(g);
            Core2026Proof.Check(opp.BoardZone.Count == 2 && p.BoardZone.Count == 1, "and only once");
        });
        WildSetsProof.Held("FP1_004", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 2 && card.Health == 2, "Mad Scientist is a 2-mana 2/2");
            var secrets = new[] { "EX1_287", "EX1_294", "EX1_289", "EX1_295", "EX1_594" };
            void Stock(Controller c, params string[] ids) { foreach (var s in ids) c.DeckZone.Add(Entity.FromCard(c, Cards.FromId(s))); }
            Stock(p, secrets);
            int deck = p.DeckZone.Count, oppDeck = opp.DeckZone.Count;
            var sci = Summon(p, id);
            Resume(g, next => {
                var twin = (Minion)next.IdEntityDic[sci.Id]; Kill(next, twin);
                Core2026Proof.Check(twin.Controller.SecretZone.Count == 1 && twin.Controller.DeckZone.Count == deck - 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, sci);
            Core2026Proof.Check(p.SecretZone.Count == 1 && secrets.Contains(p.SecretZone.Single().Card.Id) && p.DeckZone.Count == deck - 1
                && p.DeckZone.All(c => c.Id != p.SecretZone.Single().Id) && opp.DeckZone.Count == oppDeck, "one Secret leaves the deck and is in play");
            // Hidden information: the Secret put into play stays nameless for the other seat; the fair-search viewer does not model Secrets,
            // so the native client half is transport-invariants AutoCastProof ("Mad Scientist p1/p2"), which runs this very Deathrattle.
            var order = new List<string> { p.SecretZone.Single().Card.Id };
            for (int i = 0; i < 4; i++)
            {
                var next = Summon(p, id); Kill(g, next);
                order.Add(p.SecretZone.Last().Card.Id);
            }
            Core2026Proof.Check(p.SecretZone.Count == 5 && order.Distinct().Count() == 5 && p.DeckZone.All(c => !c.Card.IsSecret) && p.DeckZone.Count == deck - 5,
                "five Scientists put five different Secrets into play, never a duplicate: " + string.Join(",", order));
            Stock(p, "EX1_554");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.SecretZone.Count == 5 && p.DeckZone.Count(c => c.Card.Id == "EX1_554") == 1, "with five Secrets in play nothing more is put into play");
            p.SecretZone.Remove(p.SecretZone.First(s => s.Card.Id == "EX1_295"));
            // Only a duplicate left in the deck: nothing; a different Secret: that one.
            foreach (var d in p.DeckZone.Where(c => c.Card.IsSecret).ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(d));
            Stock(p, "EX1_287");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.SecretZone.Count == 4 && p.DeckZone.Count(c => c.Card.Id == "EX1_287") == 1, "a Secret already in play is not put into play twice");
            Stock(p, "EX1_295");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.SecretZone.Any(s => s.Card.Id == "EX1_295") && p.DeckZone.Count(c => c.Card.Id == "EX1_287") == 1 && p.DeckZone.All(c => c.Card.Id != "EX1_295"),
                "the legal Secret is chosen over the duplicate");
            StandardSetsProof.ParkBoard(p);
            foreach (var s in p.SecretZone.ToArray()) p.SetasideZone.Add(p.SecretZone.Remove(s));
            foreach (var d in p.DeckZone.Where(c => c.Card.IsSecret).ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(d));
            int plain = p.DeckZone.Count;
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.SecretZone.Count == 0 && p.DeckZone.Count == plain, "no Secret in the deck: nothing happens");
            Stock(p, "EX1_287", "EX1_294");
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.SecretZone.Count == 0, "Silence removes the Deathrattle");
            // The put-in secret is live: Counterspell counters the opponent's next spell.
            foreach (var d in p.DeckZone.Where(c => c.Card.IsSecret).ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(d));
            Stock(p, "EX1_287");
            Kill(g, Summon(p, id));
            End(g);
            var burn = Play(g, opp, "CS2_029", p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 0 && p.SecretZone.Count == 0, "the Secret put into play works: Counterspell counters the spell");
            // Opponent side: the opponent's Scientist reads the opponent's deck.
            Stock(opp, "EX1_289"); int mine = p.DeckZone.Count;
            Kill(g, Summon(opp, id));
            Core2026Proof.Check(opp.SecretZone.Count == 1 && opp.SecretZone.Single().Card.Id == "EX1_289" && p.DeckZone.Count == mine, "the opponent's Scientist puts the opponent's Secret into play");
        });
        WildSetsProof.Case("FP1_007", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 0 && card.Health == 2, "Nerubian Egg is a 2-mana 0/2");
            var left = Summon(p, "CS2_182"); var egg = Summon(p, id); var right = Summon(p, "CS2_182");
            Play(g, p, "CS2_092", egg);
            Resume(g, next => {
                var twin = (Minion)next.IdEntityDic[egg.Id]; Kill(next, twin);
                Core2026Proof.Check(twin.Controller.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, egg);
            var nerubian = p.BoardZone[1];
            Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, nerubian.Id, right.Id }) && nerubian.Card.Id == "FP1_007t"
                && nerubian.AttackDamage == 4 && nerubian.Health == 4 && nerubian.Controller == p, "a plain 4/4 Nerubian takes the Egg's place, buffs do not carry");
            Core2026Proof.Check(nerubian.Card.IsRace(Race.UNDEAD), "the Nerubian is Undead");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 1, "a full board still has room for the Egg's own slot");
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 2, "Baron Rivendare doubles the Deathrattle");
            End(g);
            StandardSetsProof.ParkBoard(opp);
            var stolen = Summon(p, id); int mineNerubians = p.BoardZone.Count(m => m.Card.Id == "FP1_007t");
            Play(g, opp, "CS1_113", stolen);
            Core2026Proof.Check(stolen.Controller == opp, "fixture: the Egg changed sides");
            Kill(g, stolen);
            Core2026Proof.Check(opp.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 1 && p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == mineNerubians,
                "a stolen Egg hatches for its current controller");
            StandardSetsProof.ParkBoard(opp);
            Kill(g, Summon(opp, id));
            Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.BoardZone.Single().Card.Id == "FP1_007t", "the opponent's Egg hatches on the opponent's side");
            End(g); // a real action boundary (see FP1_002)
        });
        WildSetsProof.Held("FP1_009", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 2 && card.Health == 8 && card.Tags.GetValueOrDefault(GameTag.TAUNT) == 1, "Deathlord is a 3-mana 2/8 Taunt");
            void Deck(Controller c, params string[] ids)
            {
                foreach (var d in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(d));
                foreach (var s in ids) c.DeckZone.Add(Entity.FromCard(c, Cards.FromId(s)));
            }
            var lord = Summon(p, id);
            Core2026Proof.Check(lord.HasTaunt, "Deathlord has Taunt");
            Deck(opp, "CS2_029", "CS2_182", "CS2_168", "CS2_179", "CS2_029");
            Resume(g, next => {
                var twin = (Minion)next.IdEntityDic[lord.Id]; Kill(next, twin);
                Core2026Proof.Check(next.Player2.BoardZone.Count == 1 && next.Player2.DeckZone.Count == 4, "the Deathrattle runs on a clone and a snapshot restore");
            });
            int mine = p.DeckZone.Count;
            Kill(g, lord);
            Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.DeckZone.Count == 4 && p.BoardZone.Count == 0 && p.DeckZone.Count == mine
                && opp.DeckZone.Count(c => c.Card.Type == CardType.SPELL) == 2, "the OPPONENT puts one minion from the opponent's deck into play; spells stay");
            var seen = new List<string> { opp.BoardZone.Single().Card.Id };
            for (int i = 0; i < 2; i++) { Kill(g, Summon(p, id)); seen.Add(opp.BoardZone.Last().Card.Id); }
            Core2026Proof.Check(seen.Distinct().Count() == 3 && opp.DeckZone.All(c => c.Card.Type == CardType.SPELL), "each death takes another random deck minion: " + string.Join(",", seen));
            int deckNow = opp.DeckZone.Count;
            Kill(g, Summon(p, id));
            Core2026Proof.Check(opp.BoardZone.Count == 3 && opp.DeckZone.Count == deckNow, "no minion in the deck: nothing");
            Deck(opp, "CS2_182", "CS2_182");
            StandardSetsProof.ParkBoard(opp); for (int i = 0; i < 7; i++) Summon(opp, "CS2_168");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(opp.BoardZone.Count == 7 && opp.DeckZone.Count == 2, "a full opponent board: the minion stays in the deck");
            StandardSetsProof.ParkBoard(opp);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(opp.BoardZone.Count == 0 && opp.DeckZone.Count == 2, "Silence removes the Deathrattle");
            End(g);
            Deck(p, "CS2_179", "CS2_029");
            Kill(g, Summon(opp, id));
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "CS2_179" && p.DeckZone.Count == 1 && opp.BoardZone.Count == 0,
                "the opponent's Deathlord gives ME a minion from my deck");
            End(g); // a real action boundary (see FP1_002)
        });
        WildSetsProof.Case("FP1_010", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 6 && card.ATK == 2 && card.Health == 8 && card.IsRace(Race.BEAST) && card.Tags.GetValueOrDefault(GameTag.POISONOUS) == 1
                && card.Tags.GetValueOrDefault(GameTag.ELITE) == 1, "Maexxna is a 6-mana 2/8 Legendary Poisonous Beast");
            var maex = Summon(p, id);
            Core2026Proof.Check(maex.Poisonous, "Maexxna is Poisonous");
            var big = Summon(opp, "CS2_182"); var shield = Summon(opp, "EX1_008");
            Attack(g, maex, big);
            Core2026Proof.Check(big.Zone.Type == Zone.GRAVEYARD && maex.Damage == 4 && maex.Zone.Type == Zone.PLAY, "an attack on a 4/5 destroys it; Maexxna takes its 4");
            Attack(g, maex, shield);
            Core2026Proof.Check(shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && maex.Damage == 5, "Divine Shield absorbs the poison");
            Attack(g, maex, shield);
            Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD, "the second hit kills the unshielded minion");
            Attack(g, maex, opp.Hero);
            Core2026Proof.Check(opp.Hero.Damage == 2 && opp.Hero.Health == 28, "a hero takes only the 2 damage: Poisonous affects minions");
            Resume(g, next => {
                var foe = Summon(next.Player2, "CS2_182"); var twin = (Minion)next.IdEntityDic[maex.Id];
                Attack(next, twin, foe);
                Core2026Proof.Check(foe.Zone.Type == Zone.GRAVEYARD, "Poisonous continues on a clone and a snapshot restore");
            });
            maex.Damage = 0;
            End(g);
            var raider = Summon(opp, "CS2_182");
            Attack(g, raider, maex);
            Core2026Proof.Check(raider.Zone.Type == Zone.GRAVEYARD && maex.Zone.Type == Zone.PLAY, "an attacker into Maexxna dies");
            End(g);
            maex.Silence();
            var foe2 = Summon(opp, "CS2_182");
            Core2026Proof.Check(!maex.Poisonous, "Silence removes Poisonous");
            Attack(g, maex, foe2);
            Core2026Proof.Check(foe2.Zone.Type == Zone.PLAY && foe2.Damage == 2, "a Silenced Maexxna only deals 2");
            // The opponent's own Maexxna.
            End(g);
            var theirs = Summon(opp, id); var mine = Summon(p, "CS2_182");
            Attack(g, theirs, mine);
            Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD, "the opponent's Maexxna is Poisonous too");
        });
    }

    internal static void BlockedProofs()
    {
        Blocked("FP1_001", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 1 && card.ATK == 2 && card.Health == 3 && card.IsRace(Race.UNDEAD), "Zombie Chow is a 1-mana 2/3 Undead");
            opp.Hero.Damage = 12; p.Hero.Damage = 12;
            var chow = Summon(p, id);
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[chow.Id]);
                Core2026Proof.Check(next.Player2.Hero.Damage == 7 && next.Player1.Hero.Damage == 12, "the Deathrattle heals the enemy hero on a clone and a snapshot restore");
            });
            Kill(g, chow);
            Core2026Proof.Check(opp.Hero.Damage == 7 && p.Hero.Damage == 12, "5 Health go to the ENEMY hero only");
            Kill(g, Summon(p, id)); Core2026Proof.Check(opp.Hero.Damage == 2, "a second Chow heals again");
            Kill(g, Summon(p, id)); Core2026Proof.Check(opp.Hero.Damage == 0, "healing stops at full Health");
            Kill(g, Summon(p, id)); Core2026Proof.Check(opp.Hero.Damage == 0 && opp.Hero.Armor == 0, "at full Health nothing happens, no Armor");
            opp.Hero.Damage = 9; var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(opp.Hero.Damage == 9, "Silence removes the Deathrattle");
            End(g);
            Kill(g, Summon(opp, id));
            Core2026Proof.Check(p.Hero.Damage == 7 && opp.Hero.Damage == 9, "the opponent's Chow heals MY hero");
            End(g);
        });
        WildSetsProof.Held("FP1_005", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 2 && card.Health == 2 && card.Tags.GetValueOrDefault(GameTag.STEALTH) == 1, "Shade of Naxxramas is a 3-mana 2/2 Stealth");
            var shade = Summon(p, id);
            Core2026Proof.Check(shade.HasStealth, "it has Stealth");
            Resume(g, next => {
                End(next); End(next);
                var twin = (Minion)next.IdEntityDic[shade.Id];
                Core2026Proof.Check(twin.AttackDamage == 3 && twin.Health == 3, "the trigger continues on a clone and a snapshot restore");
            });
            End(g);
            Core2026Proof.Check(shade.AttackDamage == 2 && shade.Health == 2, "the opponent's turn start does not buff it");
            End(g);
            Core2026Proof.Check(shade.AttackDamage == 3 && shade.Health == 3, "my turn start: +1/+1");
            shade.Damage = 2; End(g); End(g);
            Core2026Proof.Check(shade.AttackDamage == 4 && shade.Health == 2 && shade.Damage == 2, "it stacks, and the extra Health keeps the damage");
            shade.Silence();
            Core2026Proof.Check(shade.AttackDamage == 2 && shade.Health == 2 && !shade.HasStealth, "Silence removes the gains and Stealth");
            End(g); End(g);
            Core2026Proof.Check(shade.AttackDamage == 2 && shade.Health == 2, "a Silenced Shade no longer grows");
            var doomed = Summon(p, id); Kill(g, doomed); End(g); End(g);
            Core2026Proof.Check(doomed.Zone.Type == Zone.GRAVEYARD, "a dead Shade stays dead");
            var theirs = Summon(opp, id); End(g);
            Core2026Proof.Check(theirs.AttackDamage == 3, "the opponent's Shade grows at the opponent's turn start");
            End(g);
            Core2026Proof.Check(theirs.AttackDamage == 3, "not at mine");
        });
        WildSetsProof.Held("FP1_008", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 4 && card.Health == 6, "Spectral Knight is a 5-mana 4/6");
            var knight = Summon(opp, id); var own = Summon(p, id);
            IEnumerable<PlayerTask> Targeting(Controller c, IPlayable x, int target)
            { c.BaseMana = 10; c.UsedMana = 0; return c.Options().Where(t => t.Source?.Id == x.Id && t.Target?.Id == target); }
            var fire = Add(p, "CS2_029");
            Core2026Proof.Check(!Targeting(p, fire, knight.Id).Any() && !Targeting(p, fire, own.Id).Any() && Targeting(p, fire, opp.Hero.Id).Any(),
                "spells cannot target it, mine or the opponent's");
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == knight.Id), "nor can Hero Powers");
            var owl = Add(p, "CS2_203");
            Core2026Proof.Check(Targeting(p, owl, knight.Id).Any(), "a Battlecry can");
            Play(g, p, "CS2_032");
            Core2026Proof.Check(knight.Damage == 4 && knight.Zone.Type == Zone.PLAY, "area damage still hits it");
            var second = Summon(opp, id); Attack(g, own, second);
            Core2026Proof.Check(second.Damage == 4 && own.Damage == 4, "it can be attacked");
            Twin(g, p, owl, knight, "Ironbeak Owl");
            Core2026Proof.Check(knight.IsSilenced, "Silence applies through a Battlecry");
            Core2026Proof.Check(Targeting(p, Add(p, "CS2_029"), knight.Id).Any(), "once Silenced it is an ordinary target");
            End(g);
            var theirKnight = Summon(p, id);
            var mageSpell = Add(opp, "CS2_029");
            Core2026Proof.Check(!Targeting(opp, mageSpell, theirKnight.Id).Any(), "the opponent's spells cannot target my Knight either");
        });
        Blocked("FP1_011", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 1 && card.ATK == 1 && card.Health == 1 && card.IsRace(Race.BEAST) && card.Class == CardClass.HUNTER, "Webspinner is a 1-mana 1/1 Hunter Beast");
            EmptyHand(p); var web = Summon(p, id);
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[web.Id]);
                Core2026Proof.Check(next.Player1.HandZone.Count == 1 && next.Player1.HandZone.Single().Card.IsRace(Race.BEAST), "a Beast card on a clone and a snapshot restore");
            });
            var before = p.HandZone.Select(e => e.Id).ToHashSet();
            Kill(g, web);
            var got = p.HandZone.Where(e => !before.Contains(e.Id)).ToArray();
            Core2026Proof.Check(got.Length == 1 && got[0].Card.IsRace(Race.BEAST) && got[0].Card.Type == CardType.MINION && got[0].Card.Collectible && opp.HandZone.Count == 0,
                "the Deathrattle adds one random Beast minion to my hand: " + got[0].Card.Id);
            HiddenFromOpponent(g, p, got.Select(c => c.Id), "the Webspinner Beast");
            var seen = new List<string>();
            for (int i = 0; i < 10; i++) { EmptyHand(p); Kill(g, Summon(p, id)); seen.Add(p.HandZone.Single().Card.Id); }
            Core2026Proof.Check(seen.Distinct().Count() >= 4, "random Beasts: " + string.Join(",", seen));
            EmptyHand(p); while (p.HandZone.Count < 10) Add(p, "CS2_182");
            Kill(g, Summon(p, id)); Core2026Proof.Check(p.HandZone.Count == 10, "a full hand burns the Beast");
            EmptyHand(p); var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes the Deathrattle");
            End(g); EmptyHand(opp); Kill(g, Summon(opp, id));
            Core2026Proof.Check(opp.HandZone.Count == 1 && p.HandZone.Count == 0, "the opponent's Webspinner fills the opponent's hand");
            End(g);
        });
        WildSetsProof.Case("FP1_012", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 3 && card.Tags.GetValueOrDefault(GameTag.TAUNT) == 1, "Sludge Belcher is a 5-mana Taunt with 3 Attack (37.0 Health is 6, the card file says 5: IDENTITY-PENDING)");
            var left = Summon(p, "CS2_182"); var belcher = Summon(p, id); var right = Summon(p, "CS2_182");
            Core2026Proof.Check(belcher.HasTaunt, "Sludge Belcher has Taunt");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[belcher.Id]);
                Core2026Proof.Check(next.Player1.BoardZone.Count(m => m.Card.Id == "FP1_012t") == 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, belcher);
            var slime = p.BoardZone[1];
            Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, slime.Id, right.Id }) && slime.Card.Id == "FP1_012t" && slime.AttackDamage == 1
                && slime.Health == 2 && slime.HasTaunt, "a 1/2 Taunt Slime takes its place");
            StandardSetsProof.ParkBoard(p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            Kill(g, Summon(p, id)); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_012t") == 1, "a full board still has its own slot");
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet); Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id)); Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_012t") == 2, "Baron Rivendare doubles it");
            End(g); StandardSetsProof.ParkBoard(opp);
            Kill(g, Summon(opp, id)); Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.BoardZone.Single().Card.Id == "FP1_012t", "the opponent's Slime is on the opponent's side");
            End(g);
        });
    }
}
