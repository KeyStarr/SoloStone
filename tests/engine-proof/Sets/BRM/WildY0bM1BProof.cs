using System.Text.Json;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m1b: Blackrock Mountain scripts (second half) proven against the 37.0 text; it also runs the Naxxramas half
/// (<see cref="WildY0bM1BNaxxProof"/>), so the lane has one group. Ids that a Standard table names (BLOCKED-STANDARD) or whose English
/// text moved (BLOCKED-HSDATA, the locale files cannot be regenerated without hsdata) keep their proofs in <see cref="BlockedProofs"/>:
/// outside the manifest (BLOCKED-STANDARD ids only; the hsdata ids are registered cases now).</summary>
internal static class WildY0bM1BProof
{
    [GroupProof("wild-y0b-m1b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Held("BRM_026", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 5 && card.Health == 6 && card.IsRace(Race.DRAGON), "Hungry Dragon is a 4-mana 5/6 Dragon");
            var held = Add(p, id);
            int oh = opp.HandZone.Count, od = opp.DeckZone.Count;
            Twin(g, p, held, null, "Hungry Dragon");
            var dragon = (Minion)held; var gift = opp.BoardZone.Single();
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Id == dragon.Id && dragon.AttackDamage == 5 && dragon.Health == 6,
                "the Dragon enters its owner's board and nothing else does");
            Core2026Proof.Check(gift.Controller == opp && gift.Card.Cost == 1 && gift.Card.Type == CardType.MINION && gift.Card.Collectible,
                "the opponent receives one collectible 1-Cost minion: " + gift.Card.Id);
            Core2026Proof.Check(opp.HandZone.Count == oh && opp.DeckZone.Count == od, "the gift is summoned, not drawn or taken from the deck");
            var seen = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
                Play(g, p, id);
                var m = opp.BoardZone.Single();
                Core2026Proof.Check(m.Card.Cost == 1 && m.Card.Type == CardType.MINION && m.Card.Collectible && m.Health > 0, "every gift is a 1-Cost minion: " + m.Card.Id);
                seen.Add(m.Card.Id);
            }
            Core2026Proof.Check(seen.Distinct().Count() >= 4, "the gift is random: " + string.Join(",", seen));
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(opp, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(opp.BoardZone.Count == 7 && p.BoardZone.Count == 1, "with one free slot the gift takes it");
            Play(g, p, id);
            Core2026Proof.Check(opp.BoardZone.Count == 7 && p.BoardZone.Count == 2, "a full opponent board gets no gift and the Dragon still enters play");
            Summon(p, id);
            Core2026Proof.Check(opp.BoardZone.Count == 7 && p.BoardZone.Count == 3, "summoning without a play has no Battlecry");
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            End(g);
            Play(g, opp, id);
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Cost == 1 && opp.BoardZone.Single().Card.Id == id,
                "the opponent's Hungry Dragon gives the gift to me");
        });
        WildSetsProof.Held("BRM_027", "held: still wedges the client after the CONTROLLER fix (live, wy0b-fx-4); needs live diagnosis", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 9 && card.ATK == 9 && card.Health == 7 && card.Tags.GetValueOrDefault(GameTag.ELITE) == 1, "Majordomo Executus is a 9-mana 9/7 Legendary");
            var heroClass = p.HeroClass;
            var maj = Summon(p, id);
            Core2026Proof.Check(p.Hero.Card.Id != "BRM_027h" && maj.HasDeathrattle && p.Hero.Armor == 0, "the hero is untouched while Majordomo lives");
            p.Hero.Damage = 12;
            Resume(g, next => {
                var twin = (Minion)next.IdEntityDic[maj.Id]; Kill(next, twin);
                var hero = twin.Controller.Hero;
                Core2026Proof.Check(hero.Card.Id == "BRM_027h" && hero.Health == 8 && hero.Damage == 0 && twin.Controller.HeroClass == heroClass
                    && hero.HeroPower.Card.Id == "BRM_027p", "death replaces the hero on a clone and on a snapshot restore");
            });
            Kill(g, maj);
            var ragnaros = p.Hero;
            Core2026Proof.Check(ragnaros.Card.Id == "BRM_027h" && ragnaros.Health == 8 && ragnaros.Damage == 0 && ragnaros.AttackDamage == 0,
                "Ragnaros the Firelord has full 8 Health: damage taken is gone");
            Core2026Proof.Check(p.HeroClass == heroClass && p.Hero.HeroPower.Card.Id == "BRM_027p" && p.Hero.HeroPower.Cost == 2,
                "the hero power becomes DIE, INSECT! and the class stays for class rules");
            Core2026Proof.Check(maj.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Card.Id != "BRM_027h", "Majordomo stays in the graveyard (it is not removed) and the opponent's hero is untouched");
            var target = Summon(opp, "CS2_182");
            Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "the new hero power is playable");
            Core2026Proof.Check(opp.Hero.Damage == 8 || target.Damage == 8 || target.Zone.Type == Zone.GRAVEYARD, "DIE, INSECT! deals 8 to an enemy");
            p.Hero.Damage = 4;
            var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
            Core2026Proof.Check(p.Hero.Damage == 4 && silenced.Zone.Type == Zone.GRAVEYARD, "a Silenced Majordomo has no Deathrattle: the hero keeps its damage");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.Hero.Card.Id == "BRM_027h" && p.Hero.Damage == 0, "another Majordomo replaces the hero again (fresh 8 Health)");
            HeroReplacement();
            End(g);
            opp.Hero.Damage = 9;
            var silent = Summon(opp, id); silent.Silence(); Kill(g, silent);
            Core2026Proof.Check(opp.Hero.Card.Id != "BRM_027h" && opp.Hero.Damage == 9, "Silence prevents the replacement on the opponent's side");
            var their = Summon(opp, id); p.Hero.Damage = 3;
            Kill(g, their);
            Core2026Proof.Check(opp.Hero.Card.Id == "BRM_027h" && opp.Hero.Health == 8 && opp.Hero.Damage == 0 && p.Hero.Damage == 3,
                "the opponent's Majordomo replaces the opponent's hero only");
            opp.Hero.Damage = 5;
            var mine = Summon(p, id); Play(g, opp, "CS1_113", mine);
            Core2026Proof.Check(mine.Controller == opp, "fixture: Majordomo changed sides");
            Kill(g, mine);
            Core2026Proof.Check(opp.Hero.Damage == 0 && p.Hero.Damage == 3, "a stolen Majordomo replaces its CURRENT controller's hero");
        });
        WildSetsProof.Case("BRM_029", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 7 && card.ATK == 8 && card.Health == 4 && card.Tags.GetValueOrDefault(GameTag.ELITE) == 1, "Rend Blackhand is a 7-mana 8/4 Legendary");
            var legendFoe = Summon(opp, "EX1_016"); var legendFriend = Summon(p, "EX1_016"); var plainFoe = Summon(opp, "CS2_182");
            var rend = Add(p, id);
            IEnumerable<PlayerTask> Plays(IPlayable x) { x.Controller.BaseMana = 10; x.Controller.UsedMana = 0; return Options(x); }
            IEnumerable<PlayerTask> Options(IPlayable x) => x.Controller.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == x.Id);
            Core2026Proof.Check(Plays(rend).All(t => t.Target == null) && Plays(rend).Any(), "without a Dragon in hand there is no target: Rend plays untargeted");
            var dragon = Add(p, "BRM_004");
            var withDragon = Plays(rend).ToArray();
            Core2026Proof.Check(withDragon.All(t => t.Target is Minion m && m.Card.Tags.GetValueOrDefault(GameTag.ELITE) == 1)
                && withDragon.Select(t => t.Target.Id).Distinct().OrderBy(x => x).SequenceEqual(new[] { legendFoe.Id, legendFriend.Id }.OrderBy(x => x)),
                "with a Dragon in hand the targets are exactly the Legendary minions of both sides, no plain minion or hero: " + string.Join(",", withDragon.Select(t => t.Target?.ToString() ?? "none")));
            legendFoe.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(Plays(rend).Any(t => t.Target?.Id == legendFoe.Id), "a Silenced Legendary minion is still Legendary");
            var undeadDragon = Cards.All.Where(c => c.Implemented && c.Type == CardType.MINION && c.IsRace(Race.DRAGON) && c.Races.Count > 1).Select(c => c.Id).FirstOrDefault();
            Twin(g, p, rend, legendFoe, "Rend Blackhand");
            Core2026Proof.Check(legendFoe.Zone.Type == Zone.GRAVEYARD && plainFoe.Zone.Type == Zone.PLAY && legendFriend.Zone.Type == Zone.PLAY
                && p.BoardZone.Any(m => m.Card.Id == id) && opp.Hero.Damage == 0, "Rend destroys exactly the chosen Legendary minion");
            Core2026Proof.Check(dragon.Zone.Type == Zone.HAND, "the held Dragon stays in hand");
            // Dragon on the board or in the opponent's hand does not count.
            EmptyHand(p); Summon(p, "BRM_004"); Add(opp, "BRM_004");
            var second = Add(p, id);
            Core2026Proof.Check(Plays(second).Any() && Plays(second).All(t => t.Target == null), "a Dragon on the board or in the opponent's hand does not enable the Battlecry");
            PlayEntity(g, p, second);
            Core2026Proof.Check(legendFriend.Zone.Type == Zone.PLAY, "without the Dragon nothing is destroyed");
            // Dual-race Dragon counts; the Battlecry is optional when no Legendary is left in play.
            if (undeadDragon != null)
            {
                EmptyHand(p); Add(p, undeadDragon); var third = Add(p, id);
                Core2026Proof.Check(Plays(third).Any() && Plays(third).All(t => t.Target is Minion m && m.Card.Tags.GetValueOrDefault(GameTag.ELITE) == 1), "a dual-race Dragon in hand enables the Battlecry: " + undeadDragon);
            }
            Kill(g, legendFriend); EmptyHand(p); StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp); Add(p, "BRM_004"); var fourth = Add(p, id);
            Core2026Proof.Check(Plays(fourth).Any() && Plays(fourth).All(t => t.Target == null), "with a Dragon in hand and no Legendary in play Rend plays untargeted: " + string.Join(",", Plays(fourth).Select(t => (t.Target?.ToString() ?? "none"))) + " hand=" + p.HandZone.Count + " board=" + p.BoardZone.Count + "/" + opp.BoardZone.Count);
            PlayEntity(g, p, fourth);
            // A Legendary token without collectible rarity is still Legendary (ELITE), see legendary-elite-identity.
            var token = Cards.All.FirstOrDefault(c => c.Implemented && c.Type == CardType.MINION && !c.Collectible && c.Tags.GetValueOrDefault(GameTag.ELITE) == 1
                && c.Tags.GetValueOrDefault(GameTag.RARITY) == 0 && c.Health > 0 && c.Cost >= 0 && !c.Id.EndsWith("e") && c.Power?.DeathrattleTask == null);
            if (token != null)
            {
                var elite = Summon(opp, token.Id);
                EmptyHand(p); Add(p, "BRM_004"); var fifth = Add(p, id);
                Core2026Proof.Check(Plays(fifth).Any(t => t.Target?.Id == elite.Id), "an elite token without rarity is a legal target: " + token.Id);
            }
            End(g);
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            var mine = Summon(p, "EX1_016"); var theirDragon = Add(opp, "BRM_004"); var theirRend = Add(opp, id);
            PlayEntity(g, opp, theirRend, mine);
            Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD && theirDragon.Zone.Type == Zone.HAND, "the opponent's Rend destroys my Legendary minion");
        });
        WildSetsProof.Held("BRM_030", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 9 && card.ATK == 8 && card.Health == 8 && card.IsRace(Race.DRAGON) && card.Tags.GetValueOrDefault(GameTag.ELITE) == 1,
                "Nefarian is a 9-mana 8/8 Legendary Dragon");
            var held = Add(p, id); var before = p.HandZone.Select(e => e.Id).ToHashSet();
            int deck = p.DeckZone.Count, theirHand = opp.HandZone.Count;
            Twin(g, p, held, null, "Nefarian");
            var added = p.HandZone.Where(e => !before.Contains(e.Id)).ToArray();
            Core2026Proof.Check(added.Length == 2 && p.HandZone.Count == before.Count - 1 + 2, "Nefarian adds exactly two cards");
            Core2026Proof.Check(added.All(c => c.Card.Type == CardType.SPELL && c.Card.Class == opp.HeroClass && c.Card.Collectible),
                "both are spells of the opponent's class: " + string.Join(",", added.Select(c => c.Card.Id)));
            Core2026Proof.Check(p.DeckZone.Count == deck && opp.HandZone.Count == theirHand, "no deck card is drawn and the opponent's hand is untouched");
            HiddenFromOpponent(g, p, added.Select(c => c.Id), "Nefarian spells");
            var seen = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                EmptyHand(p); StandardSetsProof.ParkBoard(p);
                Play(g, p, id);
                var got = p.HandZone.ToArray();
                Core2026Proof.Check(got.Length == 2 && got.All(c => c.Card.Type == CardType.SPELL && c.Card.Class == CardClass.MAGE), "every pair is two class spells: " + string.Join(",", got.Select(c => c.Card.Id + "/" + c.Card.Type + "/" + c.Card.Class)));
                seen.AddRange(got.Select(c => c.Card.Id));
            }
            Core2026Proof.Check(seen.Distinct().Count() >= 6, "the spells are random and independent: " + string.Join(",", seen));
            EmptyHand(p); while (p.HandZone.Count < 9) Add(p, "CS2_182");
            var late = Add(p, id);
            Core2026Proof.Check(p.HandZone.Count == 10, "hand is full with Nefarian");
            PlayEntity(g, p, late);
            Core2026Proof.Check(p.HandZone.Count == 10, "a full hand keeps at most ten cards: " + p.HandZone.Count);
            Core2026Proof.Check(p.HandZone.Count(c => c.Card.Type == CardType.SPELL && c.Card.Class == CardClass.MAGE) == 1, "only the first spell fits; the second is burned");
            End(g);
            EmptyHand(opp); StandardSetsProof.ParkBoard(opp); int mineBefore = p.HandZone.Count;
            Play(g, opp, id);
            var theirs = opp.HandZone.ToArray();
            Core2026Proof.Check(theirs.Length == 2 && theirs.All(c => c.Card.Type == CardType.SPELL && c.Card.Class == CardClass.ROGUE) && p.HandZone.Count == mineBefore,
                "the opponent's Nefarian adds spells of MY class to the opponent's hand");
            EmptyHand(p); Summon(p, id); Core2026Proof.Check(p.HandZone.Count == 0, "summoning has no Battlecry");
        });
        WildSetsProof.Case("BRM_033", (g, p, id) => {
            var opp = p.Opponent;
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 2 && card.Health == 4 && !card.IsRace(Race.DRAGON), "Blackwing Technician is a 3-mana 2/4 non-Dragon");
            var none = (Minion)Play(g, p, id);
            Core2026Proof.Check(none.AttackDamage == 2 && none.Health == 4, "no Dragon in hand: no buff");
            Summon(p, "BRM_004"); Add(opp, "BRM_004");
            var board = (Minion)Play(g, p, id);
            Core2026Proof.Check(board.AttackDamage == 2 && board.Health == 4, "a Dragon on the board or in the opponent's hand does not count");
            var dragon = Add(p, "BRM_004");
            var held = Add(p, id);
            Twin(g, p, held, null, "Blackwing Technician");
            var buffed = (Minion)held;
            Core2026Proof.Check(buffed.AttackDamage == 3 && buffed.Health == 5 && buffed.Damage == 0 && dragon.Zone.Type == Zone.HAND, "a Dragon in hand: +1/+1, and the Dragon stays in hand");
            buffed.Silence(); Core2026Proof.Check(buffed.AttackDamage == 2 && buffed.Health == 4, "Silence removes the buff");
            buffed.Damage = 3; Core2026Proof.Check(buffed.Health == 1, "damage applies on the base stats");
            StandardSetsProof.ParkBoard(p);
            var second = (Minion)Play(g, p, id);
            Core2026Proof.Check(second.AttackDamage == 3 && second.Health == 5, "each Technician checks the hand again");
            var summoned = Summon(p, id);
            Core2026Proof.Check(summoned.AttackDamage == 2 && summoned.Health == 4, "summoning has no Battlecry");
            var dualId = Cards.All.Where(c => c.Implemented && c.Type == CardType.MINION && c.IsRace(Race.DRAGON) && c.Races.Count > 1).Select(c => c.Id).FirstOrDefault();
            if (dualId != null)
            {
                EmptyHand(p); StandardSetsProof.ParkBoard(p); Add(p, dualId);
                var dual = (Minion)Play(g, p, id);
                Core2026Proof.Check(dual.AttackDamage == 3, "a dual-race Dragon counts");
            }
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            var empty = (Minion)Play(g, p, id);
            Core2026Proof.Check(empty.AttackDamage == 2, "an empty hand gives no buff");
            End(g);
            Add(opp, "BRM_004");
            var theirs = (Minion)Play(g, opp, id);
            Core2026Proof.Check(theirs.AttackDamage == 3 && theirs.Health == 5, "the opponent's Technician reads the opponent's hand");
            EmptyHand(opp); Add(p, "BRM_004");
            var ours = (Minion)Play(g, opp, id);
            Core2026Proof.Check(ours.AttackDamage == 2, "my Dragon does not count for the opponent");
        });
        WildY0bM1BNaxxProof.Run();
        BlockedProofs();
    }

    /// <summary>Proofs of ids kept out of the manifest. BLOCKED-STANDARD: a Standard table names the id (pool-eligibility-v1.json:
    /// BRM_024, BRM_025, BRM_031, FP1_001, FP1_011 (dependency of CORE_AT_062)); their scripts are not touched. BLOCKED-HSDATA: the 37.0
    /// English text differs from the card file's, and the locale files cannot be regenerated without hsdata (BRM_028, BRM_034,
    /// FP1_005, FP1_008, FP1_012). IDENTITY-PENDING lines list what the Wild identity row would still change; they are not failures.</summary>
    /// <summary>A hero replaced by a Deathrattle or Battlecry (ReplaceHeroTask) reaches the client with its owner and entity id (Ragnaros is a Neutral
    /// hero: its inherit tags used to be a plain dictionary, so CONTROLLER and ENTITY_ID never reached FULL_ENTITY), and keeps a friendly
    /// hero-targeted aura (Fallen Hero's +1 Hero Power damage) like PlayCard.PlayHeroCore does. Real scripts: BRM_027 (killed by EX1_279), EX1_323.</summary>
    internal static void HeroReplacement()
    {
        foreach (string source in new[] { "BRM_027", "EX1_323" })
        {
            var f = new Game(new GameConfig { RandomSeed = 3, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                Player1HeroClass = CardClass.PALADIN, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
            f.StartGame();
            var fp = f.Player1;
            foreach (var opening in fp.HandZone.ToArray()) { fp.HandZone.Remove(opening); fp.SetasideZone.Add(opening); }
            fp.BaseMana = 10;
            Summon(fp, "AT_003"); // Fallen Hero: Your Hero Power deals 1 extra damage (an AuraType.HERO aura)
            var oldHero = fp.Hero;
            Core2026Proof.Check(oldHero[GameTag.HEROPOWER_DAMAGE] == 1, "fixture: the aura is on the first hero");
            if (source == "BRM_027") Kill(f, Summon(fp, "BRM_027")); // the Deathrattle replaces the hero
            else Play(f, fp, "EX1_323");
            f.DeathProcessingAndAuraUpdate();
            var hero = fp.Hero;
            string ragnarosOrJaraxxus = source == "BRM_027" ? "BRM_027h" : "EX1_323h";
            Core2026Proof.Check(hero.Id != oldHero.Id && hero.Card.Id == ragnarosOrJaraxxus, source + " replaced the hero");
            Core2026Proof.Check(hero[GameTag.CONTROLLER] == fp.PlayerId && hero[GameTag.ENTITY_ID] == hero.Id, source + ": the new hero carries CONTROLLER and ENTITY_ID");
            var full = f.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Last(x => x.Entity.Id == hero.Id);
            Core2026Proof.Check(full.Entity.Tags.TryGetValue(GameTag.CONTROLLER, out int owner) && owner == fp.PlayerId
                && full.Entity.Tags.TryGetValue(GameTag.ENTITY_ID, out int entityId) && entityId == hero.Id, source + ": the hero's FULL_ENTITY carries CONTROLLER and ENTITY_ID");
            Core2026Proof.Check(hero[GameTag.HEROPOWER_DAMAGE] == 1, source + ": the friendly hero aura moves to the new hero: new " + hero[GameTag.HEROPOWER_DAMAGE] + " old " + oldHero[GameTag.HEROPOWER_DAMAGE]);
        }
    }

    internal static void BlockedProofs()
    {
        BlockedFailures.Clear();
        Blocked("BRM_024", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 6 && card.ATK == 6 && card.Health == 6 && card.IsRace(Race.DRAGON), "Drakonid Crusher is a 6-mana 6/6 Dragon");
            opp.Hero.Damage = 14;
            var high = (Minion)Play(g, p, id);
            Core2026Proof.Check(high.AttackDamage == 6 && high.Health == 6, "an opponent at 16 Health: no buff");
            opp.Hero.Damage = 15; StandardSetsProof.ParkBoard(p);
            var held = Add(p, id); Twin(g, p, held, null, "Drakonid Crusher");
            var low = (Minion)held;
            Core2026Proof.Check(low.AttackDamage == 9 && low.Health == 9, "an opponent at exactly 15 Health: +3/+3");
            low.Silence(); Core2026Proof.Check(low.AttackDamage == 6 && low.Health == 6, "Silence removes the buff");
            opp.Hero.Armor = 10; opp.Hero.Damage = 14; StandardSetsProof.ParkBoard(p);
            Core2026Proof.Check(((Minion)Play(g, p, id)).AttackDamage == 6, "Armor is not Health");
            opp.Hero.Armor = 0; opp.Hero.Damage = 29; StandardSetsProof.ParkBoard(p);
            Core2026Proof.Check(((Minion)Play(g, p, id)).AttackDamage == 9, "a nearly dead opponent: buff");
            opp.Hero.Damage = 14; StandardSetsProof.ParkBoard(p);
            Core2026Proof.Check(Summon(p, id).AttackDamage == 6, "summoning has no Battlecry");
            p.Hero.Damage = 15; StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            Core2026Proof.Check(((Minion)Play(g, p, id)).AttackDamage == 6, "my own Health is not read");
            End(g);
            Core2026Proof.Check(((Minion)Play(g, opp, id)).AttackDamage == 9, "the opponent's Crusher reads MY Health (15)");
        });
        Blocked("BRM_025", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 6 && card.ATK == 6 && card.Health == 4 && card.IsRace(Race.DRAGON), "Volcanic Drake is a 6-mana 6/4 Dragon");
            var drake = Add(p, id);
            Kill(g, Summon(p, "CS2_182")); Kill(g, Summon(opp, "CS2_182"));
            Core2026Proof.Check(drake.Cost == 4, "deaths of both sides lower the cost");
            Twin(g, p, drake, null, "Volcanic Drake");
            var next = Add(p, id);
            for (int i = 0; i < 8; i++) Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(next.Cost == 0, "the discount floors at zero");
            End(g); Core2026Proof.Check(next.Cost == 6, "it expires with the turn");
            var theirs = Add(opp, id); Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(theirs.Cost == 5, "the opponent's copy counts deaths too");
        });
        WildSetsProof.Case("BRM_028", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            // 37.0 is a 5-mana 4/4 (the card file says 6-mana 5/5): reported as IDENTITY-PENDING until the identity row can land.
            Core2026Proof.Check(card.Tags.GetValueOrDefault(GameTag.ELITE) == 1, "Emperor Thaurissan is Legendary");
            var emp = Summon(p, id);
            var yeti = Add(p, "CS2_182"); var spell = Add(p, "CS2_029"); var cheap = Add(p, "CS2_189");
            int yetiCost = yeti.Cost, spellCost = spell.Cost; var late = (IPlayable)null;
            Resume(g, next => {
                End(next);
                var hand = next.Player1.HandZone;
                Core2026Proof.Check(hand.Single(e => e.Id == yeti.Id).Cost == yetiCost - 1, "the end of turn reduces by one on a clone and on a snapshot restore");
            });
            End(g);
            Core2026Proof.Check(yeti.Cost == yetiCost - 1 && spell.Cost == spellCost - 1 && cheap.Cost == Math.Max(0, Cards.FromId("CS2_189").Cost - 1),
                "every card in hand, spell or minion, is one cheaper");
            Core2026Proof.Check(opp.HandZone.All(c => c.Cost == c.Card.Cost), "the opponent's hand is untouched");
            End(g); Core2026Proof.Check(yeti.Cost == yetiCost - 1, "the opponent's turn end reduces nothing");
            late = Add(p, "CS2_182");
            End(g); End(g);
            Core2026Proof.Check(yeti.Cost == yetiCost - 2 && late.Cost == late.Card.Cost - 1, "each of my turn ends takes one more; later cards start at their turn-end step");
            for (int i = 0; i < 12; i++) { End(g); End(g); }
            Core2026Proof.Check(yeti.Cost == 0 && spell.Cost == 0, "the Cost never goes below zero");
            EmptyHand(p); var second = Summon(p, id); var probe = Add(p, "CS2_182"); int before = probe.Cost;
            End(g); End(g);
            Core2026Proof.Check(probe.Cost == before - 2, "two Emperors reduce by two");
            emp.Silence(); second.Silence(); EmptyHand(p); var held = Add(p, "CS2_182"); before = held.Cost;
            End(g); End(g);
            Core2026Proof.Check(held.Cost == before, "Silenced Emperors do nothing");
            StandardSetsProof.ParkBoard(p); EmptyHand(p); var late2 = Add(p, "CS2_182"); before = late2.Cost; var doomed = Summon(p, id); Kill(g, doomed);
            End(g); Core2026Proof.Check(late2.Cost == before, "an Emperor that died before the turn end does nothing");
            EmptyHand(opp); EmptyHand(p); var theirEmp = Summon(opp, id); var theirCard = Add(opp, "CS2_182"); int theirBefore = theirCard.Cost;
            var mine = Add(p, "CS2_182"); int mineBefore = mine.Cost;
            End(g); Core2026Proof.Check(theirCard.Cost == theirBefore - 1 && mine.Cost == mineBefore, "the opponent's Emperor reduces the opponent's hand only");
        });
        Blocked("BRM_031", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 8 && card.ATK == 6 && card.Health == 8 && card.IsRace(Race.DRAGON), "Chromaggus is an 8-mana 6/8 Dragon");
            EmptyHand(p); var chroma = Summon(p, id);
            Resume(g, next => {
                new DrawTask().Process(next, next.Player1, next.Player1.Hero, null); next.ProcessTasks();
                Core2026Proof.Check(next.Player1.HandZone.Count == 2, "a draw gives the card and a copy on a clone and a snapshot restore");
            });
            int deck = p.DeckZone.Count;
            new DrawTask().Process(g, p, p.Hero, null); g.ProcessTasks();
            Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone[0].Card.Id == p.HandZone[1].Card.Id && p.DeckZone.Count == deck - 1,
                "one card is drawn and one copy is put into hand");
            Core2026Proof.Check(p.HandZone[0].Id != p.HandZone[1].Id, "the copy is a new card");
            Summon(p, id); EmptyHand(p);
            new DrawTask().Process(g, p, p.Hero, null); g.ProcessTasks();
            Core2026Proof.Check(p.HandZone.Count == 3, "two Chromaggus: the drawn card plus one copy each (copies are not draws)");
            new DrawTask().Process(g, opp, opp.Hero, null); g.ProcessTasks();
            Core2026Proof.Check(opp.HandZone.Count == 1, "the opponent's draw does not trigger my Chromaggus");
            chroma.Silence();
            EmptyHand(p);
            while (p.HandZone.Count < 9) Add(p, "CS2_182");
            new DrawTask().Process(g, p, p.Hero, null); g.ProcessTasks();
            Core2026Proof.Check(p.HandZone.Count == 10, "a full hand burns the copy");
        });
        WildSetsProof.Case("BRM_034", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 5 && card.Health == 4 && !card.IsRace(Race.DRAGON), "Blackwing Corruptor is a 5-mana 5/4");
            var foe = Summon(opp, "CS2_182"); var friend = Summon(p, "CS2_182"); var hidden = Summon(opp, "CS2_059"); var knight = Summon(opp, "FP1_008");
            IEnumerable<PlayerTask> Plays(IPlayable x) { x.Controller.BaseMana = 10; x.Controller.UsedMana = 0; return x.Controller.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == x.Id); }
            var corruptor = Add(p, id);
            Core2026Proof.Check(Plays(corruptor).Any() && Plays(corruptor).All(t => t.Target == null), "without a Dragon in hand there is no target");
            PlayEntity(g, p, corruptor);
            Core2026Proof.Check(foe.Damage == 0 && opp.Hero.Damage == 0 && friend.Damage == 0, "without a Dragon nothing is damaged");
            Add(p, "BRM_004");
            var held = Add(p, id);
            var targets = Plays(held).Select(t => t.Target?.Id ?? 0).Distinct().ToArray();
            Core2026Proof.Check(!targets.Contains(0) && targets.Contains(foe.Id) && targets.Contains(friend.Id) && targets.Contains(p.Hero.Id) && targets.Contains(opp.Hero.Id)
                && targets.Contains(knight.Id) && !targets.Contains(hidden.Id), "with a Dragon in hand a target is required: any character except Stealth; Elusive does not stop a Battlecry");
            Twin(g, p, held, foe, "Blackwing Corruptor");
            Core2026Proof.Check(foe.Damage == 5 && foe.Zone.Type == Zone.GRAVEYARD, "Blackwing Corruptor deals 5 damage (37.0), not the 3 of the old text");
            Summon(p, "EX1_012"); Play(g, p, id, opp.Hero);
            Core2026Proof.Check(opp.Hero.Damage == 5, "a Battlecry's damage ignores Spell Damage");
            var bigger = Summon(opp, "CS2_182"); bigger.Damage = 0;
            Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 5, "it may hit my own hero");
            Play(g, p, id, knight); Core2026Proof.Check(knight.Damage == 5 && knight.Health == 1, "it hits an Elusive minion");
            EmptyHand(p); StandardSetsProof.ParkBoard(p); Summon(p, "BRM_004"); Add(opp, "BRM_004"); var third = Add(p, id);
            Core2026Proof.Check(Plays(third).Any() && Plays(third).All(t => t.Target == null), "a Dragon on the board or in the opponent's hand does not count");
            End(g);
            Add(opp, "BRM_004"); var mine = Summon(p, "CS2_182");
            Play(g, opp, id, mine);
            Core2026Proof.Check(mine.Damage == 5, "the opponent's Corruptor with its own Dragon hits my minion");
        });
        WildY0bM1BNaxxProof.BlockedProofs();
        if (BlockedFailures.Count > 0) throw new Exception(string.Join("\n", BlockedFailures));
    }

    // ---- shared helpers (also used by WildY0bM1BNaxxProof) ----
    internal static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    /// <summary>The seat that does not own the cards sees none of their identities.</summary>
    internal static void HiddenFromOpponent(Game g, Controller owner, IEnumerable<int> hiddenIds, string what)
    {
        var ids = hiddenIds.ToArray();
        Core2026Proof.Check(ids.Length > 0, what + ": the fixture hides at least one card");
        var viewer = HistoryFairOriginProof.FairGame(FairBotState.Create(g, owner.Opponent.PlayerId, 97, DeckStyle.Value));
        foreach (int id in ids)
            Core2026Proof.Check(!viewer.IdEntityDic.TryGetValue(id, out var actor) || actor is Unknown, what + " stays hidden from the other seat: " + id);
    }

    internal static void EmptyHand(Controller c)
    {
        foreach (var x in c.HandZone.ToArray()) { c.HandZone.Remove(x); c.SetasideZone.Add(x); }
    }

    /// <summary>The same play on the live game, on an exact clone and on a snapshot restore ends in the same state.</summary>
    internal static void Twin(Game g, Controller p, IPlayable card, ICharacter target, string what)
    {
        var clone = g.CloneExact(history: true);
        var restored = Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools);
        PlayEntity(g, p, card, target);
        foreach (var other in new[] { clone, restored })
        {
            var owner = p == g.Player1 ? other.Player1 : other.Player2;
            PlayEntity(other, owner, (IPlayable)other.IdEntityDic[card.Id], target == null ? null : (ICharacter)other.IdEntityDic[target.Id]);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(other), what + " replays identically on a clone and a snapshot restore");
        }
    }

    internal static readonly List<string> BlockedFailures = new();

    internal static void Blocked(string id, Action<Game, Controller, string> scenario)
    {
        var card = Cards.FromId(id);
        var definitions = ClientExport.TryCards();
        if (definitions != null && definitions.TryGetValue(id, out var data))
        {
            var pending = new List<string>();
            if (card.Cost != data.GetProperty("cost").GetInt32()) pending.Add("cost " + card.Cost + ">" + data.GetProperty("cost").GetInt32());
            if (card.ATK != data.GetProperty("atk").GetInt32()) pending.Add("attack " + card.ATK + ">" + data.GetProperty("atk").GetInt32());
            if (card.Health != data.GetProperty("health").GetInt32()) pending.Add("health " + card.Health + ">" + data.GetProperty("health").GetInt32());
            if (card.Text != data.GetProperty("text").GetString()) pending.Add("text");
            foreach (var tag in data.GetProperty("tags").EnumerateObject())
                if (card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) != tag.Value.GetInt32() && tag.Name != "183") pending.Add("tag " + tag.Name);
            if (pending.Count > 0) Console.WriteLine("IDENTITY-PENDING " + id + ": " + string.Join(", ", pending));
        }
        for (int seed = 1; seed <= 3; seed++)
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
            game.StartGame();
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                EmptyHand(owner);
                owner.BaseMana = 10;
            }
            try
            {
                scenario(game, game.Player1, id);
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after scenario");
                Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after scenario");
            }
            catch (Exception error)
            {
                var where = (error.StackTrace ?? "").Split('\n').Where(line => line.Contains("M1BProof.cs")).Select(line => line.Substring(line.LastIndexOf(':') + 1).Trim());
                BlockedFailures.Add(id + " seed " + seed + ": " + error.Message + " (proof " + string.Join(", ", where) + ")"); return;
            }
        }
        Console.WriteLine("PASS blocked " + id);
    }
}
