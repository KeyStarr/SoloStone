using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m1a: Blackrock Mountain scripts proven against the 37.0 text.
/// BLOCKED-STANDARD ids (a Standard table names them) keep their proofs in <see cref="BlockedStandard"/>, outside the manifest.</summary>
internal static class WildY0bM1AProof
{
    [GroupProof("wild-y0b-m1a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("BRM_001", (g, p, id) => {
            var card = Add(p, id);
            Core2026Proof.Check(card.Cost == 5, "Solemn Vigil costs 5 with no deaths");
            var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            Core2026Proof.Check(card.Cost == 5, "a minion entering play lowers nothing");
            Kill(g, friend); Core2026Proof.Check(card.Cost == 4, "a friendly death lowers the cost by one");
            Kill(g, enemy); Core2026Proof.Check(card.Cost == 3, "an enemy death counts too");
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, oh = p.Opponent.HandZone.Count, od = p.Opponent.DeckZone.Count;
            var handBefore = p.HandZone.Select(e => e.Id).ToHashSet();
            Twin(g, p, card, null, "Solemn Vigil");
            HiddenFromOpponent(g, p, p.HandZone.Select(e => e.Id).Where(i => !handBefore.Contains(i)), "cards drawn by Solemn Vigil");
            Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 2 && p.Opponent.HandZone.Count == oh
                && p.Opponent.DeckZone.Count == od, "Solemn Vigil draws exactly two for its owner and pays the reduced cost");
            var next = Add(p, id);
            for (int i = 0; i < 6; i++) Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(next.Cost == 0, "the discount floors at zero");
            End(g);
            Core2026Proof.Check(next.Cost == 5, "deaths of the previous turn no longer count");
            Kill(g, Summon(p.Opponent, "CS2_182")); Core2026Proof.Check(next.Cost == 4, "a card in the other seat's hand counts deaths of the current turn");
            End(g); Core2026Proof.Check(next.Cost == 5, "the count resets again");
            End(g);
            var theirs = Add(p.Opponent, id); Kill(g, Summon(p, "CS2_182")); Core2026Proof.Check(theirs.Cost == 4, "opponent copy counts both sides");
            int mine = p.HandZone.Count, theirsHand = p.Opponent.HandZone.Count, theirDeck = p.Opponent.DeckZone.Count;
            var theirBefore = p.Opponent.HandZone.Select(e => e.Id).ToHashSet();
            PlayEntity(g, p.Opponent, theirs);
            HiddenFromOpponent(g, p.Opponent, p.Opponent.HandZone.Select(e => e.Id).Where(i => !theirBefore.Contains(i)), "cards drawn by the opponent's Solemn Vigil");
            Core2026Proof.Check(p.HandZone.Count == mine && p.Opponent.HandZone.Count == theirsHand + 1 && p.Opponent.DeckZone.Count == theirDeck - 2,
                "opponent Solemn Vigil draws for the opponent only");
            var opp = p.Opponent;
            while (opp.HandZone.Count < 9) Add(opp, "CS2_182");
            var full = Add(opp, id); int burnDeck = opp.DeckZone.Count;
            Core2026Proof.Check(opp.HandZone.Count == 10, "hand is full");
            PlayEntity(g, opp, full);
            Core2026Proof.Check(opp.HandZone.Count == 10 && opp.DeckZone.Count == burnDeck - 2, "with a full hand the second draw is burned");
        });
        WildSetsProof.Case("BRM_003", (g, p, id) => {
            var enemy = Summon(p.Opponent, "CS2_182");
            var card = Add(p, id);
            Core2026Proof.Check(card.Cost == 5, "Dragon's Breath costs 5 with no deaths");
            Kill(g, Summon(p, "CS2_182")); Kill(g, Summon(p.Opponent, "CS2_182"));
            Core2026Proof.Check(card.Cost == 3, "each death on either side lowers the cost");
            PlayEntity(g, p, card, enemy);
            Core2026Proof.Check(enemy.Damage == 4, "Dragon's Breath deals 4 to a minion");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "Dragon's Breath hits the enemy hero");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 4, "Dragon's Breath may target the friendly hero");
            Summon(p, "EX1_012"); Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 9, "Dragon's Breath adds Spell Damage");
            for (int i = 0; i < 6; i++) Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(Add(p, id).Cost == 0, "the discount floors at zero");
            End(g);
            var held = Add(p, id);
            Core2026Proof.Check(held.Cost == 5, "the discount does not carry into the next turn");
            var theirs = Add(p.Opponent, id);
            Kill(g, Summon(p.Opponent, "CS2_182"));
            Core2026Proof.Check(theirs.Cost == 4, "opponent copy gets the discount");
            PlayEntity(g, p.Opponent, theirs, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 13, "opponent Dragon's Breath damages my hero");
        });
        WildSetsProof.Case("BRM_005", (g, p, id) => {
            var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            var raider = Summon(p.Opponent, "CS2_168"); var imp = Summon(p, "CS2_065"); var theirDemon = Summon(p.Opponent, "CS2_065");
            var dualId = new[] { "DINO_132", "TTN_866", "EDR_421", "CFM_637" }.First(c => Cards.FromId(c) != null && Cards.FromId(c).Implemented
                && Cards.FromId(c).IsRace(Race.DEMON) && Cards.FromId(c).Races.Count > 1);
            var dual = Summon(p.Opponent, dualId);
            Core2026Proof.Check(dual.Card.IsRace(Race.DEMON), "dual-race fixture is a Demon");
            Play(g, p, id);
            Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2 && raider.Zone.Type == Zone.GRAVEYARD, "Demonwrath hits every non-Demon minion on both sides");
            Core2026Proof.Check(imp.Damage == 0 && theirDemon.Damage == 0 && dual.Damage == 0, "Demons, including dual-race, are spared");
            Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "heroes are not damaged");
            imp.Silence(); g.DeathProcessingAndAuraUpdate(); Play(g, p, id);
            Core2026Proof.Check(imp.Damage == 0 && friend.Damage == 4, "a Silenced Demon is still a Demon");
            Summon(p, "EX1_012"); Play(g, p, id);
            Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD && enemy.Zone.Type == Zone.GRAVEYARD, "Demonwrath adds Spell Damage and kills at exact Health");
            Core2026Proof.Check(imp.Damage == 0, "Demon still unharmed with Spell Damage");
            End(g);
            var yours = Summon(p, "CS2_182"); var minion = Summon(p.Opponent, "CS2_179");
            Play(g, p.Opponent, id);
            Core2026Proof.Check(yours.Damage == 2 && minion.Damage == 2, "opponent Demonwrath also hits both sides");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p.Opponent, id);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Demonwrath on empty boards is legal and does nothing");
        });
        WildSetsProof.Held("BRM_007", "held: generated-shuffle FX scope", (g, p, id) => {
            var held = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == held.Id), "Gang Up needs a minion target");
            var enemy = Summon(p.Opponent, "CS2_179"); enemy.Damage = 2;
            Play(g, p, "CS2_104", enemy);
            Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 6 && enemy.Damage == 2, "fixture target is buffed and damaged");
            var stealthy = Summon(p.Opponent, "CS2_059");
            Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == held.Id).All(t => t.Target is Minion m && m.Id != stealthy.Id)
                && p.Options().Any(t => t.Source?.Id == held.Id && t.Target?.Id == enemy.Id), "Gang Up targets minions only, not hidden Stealth, not heroes");
            int deck = p.DeckZone.Count, od = p.Opponent.DeckZone.Count;
            Twin(g, p, held, enemy, "Gang Up");
            var copies = p.DeckZone.Where(c => c.Card.Id == "CS2_179").ToArray();
            Core2026Proof.Check(p.DeckZone.Count == deck + 3 && copies.Length == 3 && p.Opponent.DeckZone.Count == od
                && p.Opponent.DeckZone.All(c => c.Card.Id != "CS2_179"), "Gang Up shuffles three copies of an enemy minion into my deck only");
            Core2026Proof.Check(copies.All(c => c.Zone.Type == Zone.DECK && c.Controller == p && ((Minion)c).AttackDamage == 3 && ((Minion)c).Health == 5
                && ((Minion)c).Damage == 0), "copies are fresh base cards without damage or buffs");
            Core2026Proof.Check(enemy.Controller == p.Opponent && enemy.AttackDamage == 6 && enemy.Damage == 2, "the original is untouched");
            HiddenFromOpponent(g, p, copies.Select(c => c.Id), "Gang Up copies in my deck");
            var friend = Summon(p, "CS2_182"); int yetis = p.DeckZone.Count(c => c.Card.Id == "CS2_182");
            Play(g, p, id, friend);
            Core2026Proof.Check(p.DeckZone.Count(c => c.Card.Id == "CS2_182") == yetis + 3 && p.DeckZone.Count == deck + 6, "Gang Up also copies a friendly minion");
            var mine = p.DeckZone.ToArray();
            Core2026Proof.Check(mine.Select(c => c.Card.Id).Where(c => c != "CS2_182").All(c => c == "CS2_179"), "deck holds only expected cards");
            while (p.DeckZone.Count < 98) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
            Play(g, p, id, friend);
            Core2026Proof.Check(p.DeckZone.Count == 99 && p.DeckZone.Count(c => c.Card.Id == "CS2_182") >= 1, "a nearly full deck takes only the copies that fit");
            Play(g, p, id, friend);
            Core2026Proof.Check(p.DeckZone.Count == 99, "a full deck takes no copy");
            End(g);
            var theirCard = Add(p.Opponent, id); int myDeck = p.DeckZone.Count, theirDeck = p.Opponent.DeckZone.Count;
            PlayEntity(g, p.Opponent, theirCard, friend);
            Core2026Proof.Check(p.DeckZone.Count == myDeck && p.Opponent.DeckZone.Count == theirDeck + 3, "opponent Gang Up fills the opponent deck only");
            HiddenFromOpponent(g, p.Opponent, p.Opponent.DeckZone.Where(c => c.Card.Id == "CS2_182" && c.Id > friend.Id).Select(c => c.Id), "Gang Up copies in the opponent deck");
        });
        WildSetsProof.Case("BRM_009", (g, p, id) => {
            var card = Add(p, id);
            Core2026Proof.Check(card.Cost == 9, "Volcanic Lumberer costs 9 with no deaths");
            Kill(g, Summon(p, "CS2_182")); Kill(g, Summon(p.Opponent, "CS2_182")); Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(card.Cost == 6, "three deaths lower the cost to six");
            var second = Add(p, id); Core2026Proof.Check(second.Cost == 6, "every copy in hand is discounted");
            var minion = (Minion)card; var board = p.BoardZone.Count;
            PlayEntity(g, p, card);
            Core2026Proof.Check(p.BoardZone.Count == board + 1 && minion.HasTaunt && minion.AttackDamage == 7 && minion.Health == 8, "Lumberer is a 7/8 Taunt");
            for (int i = 0; i < 9; i++) Kill(g, Summon(p.Opponent, "CS2_182"));
            Core2026Proof.Check(second.Cost == 0, "the discount floors at zero");
            End(g);
            Core2026Proof.Check(second.Cost == 9, "the discount resets next turn");
            var theirs = Add(p.Opponent, id); Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(theirs.Cost == 8, "opponent copy counts both sides");
            PlayEntity(g, p.Opponent, theirs);
            Core2026Proof.Check(p.Opponent.BoardZone.Any(m => m.Card.Id == id && m.HasTaunt), "opponent plays Lumberer with the discount");
            minion.Silence(); Core2026Proof.Check(minion.HasTaunt == false, "Silence removes Taunt");
        });
        WildSetsProof.Held("BRM_010", "held: FX-MISSING live (choose:BRM_010a, choose:BRM_010b)", (g, p, id) => {
            var held = Add(p, id);
            var picks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.ChooseOne).Distinct().OrderBy(x => x).ToArray();
            Core2026Proof.Check(picks.SequenceEqual(new[] { 1, 2 }), "Druid of the Flame offers exactly two branches: " + string.Join(",", picks));
            Core2026Proof.Check(held.Card.Tags.GetValueOrDefault(GameTag.CHOOSE_ONE) == 1 && held.Cost == 3, "pinned Choose One identity");
            PlayChoice(g, p, held, 1);
            var cat = p.BoardZone.Single();
            Core2026Proof.Check(cat.Card.Id == "BRM_010t" && cat.AttackDamage == 5 && cat.Health == 2 && cat.Card.IsRace(Race.BEAST) && cat.Card.IsRace(Race.ELEMENTAL),
                "branch one becomes a 5/2 Elemental Beast: " + cat.Card.Id + " " + cat.AttackDamage + "/" + cat.Health);
            Core2026Proof.Check(!cat.HasTaunt && cat.Cost == 3, "form keeps plain stats");
            var held2 = Add(p, id); PlayChoice(g, p, held2, 2);
            var hawk = p.BoardZone.First(m => m.Id != cat.Id && m.Card.Id != id) ;
            Core2026Proof.Check(hawk.Card.Id == "BRM_010t2" && hawk.AttackDamage == 2 && hawk.Health == 5 && hawk.Card.IsRace(Race.BEAST) && hawk.Card.IsRace(Race.ELEMENTAL),
                "branch two becomes a 2/5 Elemental Beast");
            cat.Silence(); Core2026Proof.Check(cat.AttackDamage == 5 && cat.Health == 2, "Silence keeps the transformed body");
            var fandral = Summon(p, "OG_044");
            var held3 = Add(p, id);
            var all = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held3.Id).Select(t => t.ChooseOne).Distinct().OrderBy(x => x).ToArray();
            Core2026Proof.Check(all.SequenceEqual(new[] { -1 }), "Fandral replaces the two branches by one combined play: " + string.Join(",", all));
            PlayChoice(g, p, held3, -1);
            var both = p.BoardZone.OrderByDescending(m => m.Id).First();
            Core2026Proof.Check(both.Card.Id == "OG_044b" && both.AttackDamage == 5 && both.Health == 5 && both.Card.IsRace(Race.BEAST) && both.Card.IsRace(Race.ELEMENTAL),
                "with Fandral the combined form is a 5/5 Elemental Beast: " + both.Card.Id);
            fandral.Silence(); g.DeathProcessingAndAuraUpdate();
            var held4 = Add(p, id);
            Core2026Proof.Check(p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held4.Id).Select(t => t.ChooseOne).Distinct().OrderBy(x => x).SequenceEqual(new[] { 1, 2 }),
                "a Silenced Fandral no longer combines the branches");
            End(g);
            var theirs = Add(p.Opponent, id); PlayChoice(g, p.Opponent, theirs, 2);
            Core2026Proof.Check(p.Opponent.BoardZone.Single().Card.Id == "BRM_010t2", "opponent Druid of the Flame transforms on its own side");
            StandardSetsProof.ParkBoard(p.Opponent);
            for (int i = 0; i < 7; i++) Summon(p.Opponent, "CS2_182");
            var blocked = Add(p.Opponent, id);
            Core2026Proof.Check(!p.Opponent.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == blocked.Id), "no play on a full board");
        });
        WildSetsProof.Case("BRM_011", (g, p, id) => {
            var target = Summon(p.Opponent, "CS2_182");
            p.OverloadLocked = 2; p.OverloadOwed = 1;
            var held = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
            Core2026Proof.Check(p.RemainingMana == 8, "two crystals are locked");
            Twin(g, p, held, target, "Lava Shock");
            Core2026Proof.Check(target.Damage == 2, "Lava Shock deals 2");
            Core2026Proof.Check(p.OverloadLocked == 0 && p.OverloadOwed == 0 && p.RemainingMana == 8, "locked and owed Overload are both removed; the two crystals can be used");
            p.OverloadOwed = 3; p.Opponent.OverloadOwed = 2; p.Opponent.OverloadLocked = 1;
            Summon(p, "EX1_012"); Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.OverloadOwed == 0, "Lava Shock adds Spell Damage");
            Core2026Proof.Check(p.Opponent.OverloadOwed == 2 && p.Opponent.OverloadLocked == 1, "Lava Shock leaves the opponent Overload alone");
            Play(g, p, "BRM_012");
            Core2026Proof.Check(p.OverloadOwed == 1, "Fireguard Destroyer Overload is owed");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.OverloadOwed == 0 && p.Hero.Damage == 3, "Lava Shock clears a freshly owed Overload and may hit my hero");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.OverloadOwed == 0 && p.OverloadLocked == 0, "Lava Shock without Overload is harmless");
            End(g);
            p.Opponent.OverloadOwed = 0; p.Opponent.OverloadLocked = 2; p.OverloadOwed = 1;
            Play(g, p.Opponent, id, p.Hero);
            Core2026Proof.Check(p.Opponent.OverloadLocked == 0 && p.OverloadOwed == 1, "opponent Lava Shock clears only its own Overload");
        });
        WildSetsProof.Case("BRM_012", (g, p, id) => {
            var seen = new List<int>();
            Core2026Proof.Check(Cards.FromId(id).ATK == 3 && Cards.FromId(id).Health == 6, "Fireguard Destroyer is 3/6");
            for (int i = 0; i < 6; i++)
            {
                var held = Add(p, id);
                Twin(g, p, held, null, "Fireguard Destroyer");
                var m = (Minion)held;
                int gained = m.AttackDamage - 3; seen.Add(gained);
                Core2026Proof.Check(gained >= 1 && gained <= 4 && m.Health == 6, "Fireguard gains 1-4 Attack: " + gained);
                Core2026Proof.Check(p.OverloadOwed == i + 1, "Overload (1) per copy");
            }
            Core2026Proof.Check(seen.Distinct().Count() >= 3, "the random Attack range is actually spread: " + string.Join(",", seen));
            var first = p.BoardZone.First(); first.Silence();
            Core2026Proof.Check(first.AttackDamage == 3 && p.OverloadOwed == 6, "Silence removes the Attack gain but not the owed Overload");
            Core2026Proof.Check(p.Opponent.OverloadOwed == 0, "opponent Overload untouched");
            var full = Add(p, "CS2_182");
            Summon(p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7, "board fills");
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == full.Id), "no play on a full board");
            End(g);
            var theirs = Add(p.Opponent, id); PlayEntity(g, p.Opponent, theirs);
            Core2026Proof.Check(p.Opponent.OverloadOwed == 1 && p.OverloadOwed == 6 && ((Minion)theirs).AttackDamage >= 4, "opponent Fireguard owes its own Overload");
        });
        WildSetsProof.Case("BRM_014", (g, p, id) => {
            Core2026Proof.Check(Cards.FromId(id).Races.Contains(Race.BEAST) && Cards.FromId(id).Races.Contains(Race.ELEMENTAL), "Core Rager is an Elemental Beast");
            var rager = (Minion)Play(g, p, id);
            Core2026Proof.Check(rager.AttackDamage == 7 && rager.Health == 7, "empty hand: +3/+3");
            Add(p, "CS2_182");
            var other = (Minion)Play(g, p, id);
            Core2026Proof.Check(other.AttackDamage == 4 && other.Health == 4, "one card left in hand: no buff");
            EmptyHand(p);
            var held = Add(p, id); Add(p.Opponent, "CS2_182");
            var third = (Minion)held; PlayEntity(g, p, held);
            Core2026Proof.Check(third.AttackDamage == 7, "the opponent hand does not matter");
            Core2026Proof.Check(p.DeckZone.Count > 0, "deck has cards that do not count");
            rager.Silence(); Core2026Proof.Check(rager.AttackDamage == 4 && rager.Health == 4, "Silence removes the buff");
            EmptyHand(p); var sum = Summon(p, id);
            Core2026Proof.Check(sum.AttackDamage == 4, "summoning is no Battlecry");
            End(g); EmptyHand(p.Opponent);
            var theirs = (Minion)Add(p.Opponent, id); Add(p, "CS2_182");
            PlayEntity(g, p.Opponent, theirs);
            Core2026Proof.Check(theirs.AttackDamage == 7 && p.HandZone.Count > 0, "opponent with empty hand gets the buff");
        });
        WildSetsProof.Case("BRM_015", (g, p, id) => {
            var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var raider = Summon(p.Opponent, "CS2_168");
            Play(g, p, id);
            Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1 && raider.Zone.Type == Zone.GRAVEYARD, "above 12 Health: 1 damage to every minion");
            Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "heroes are not hit");
            p.Hero.Damage = 17; Play(g, p, id);
            Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2, "13 Health still deals 1");
            p.Hero.Damage = 18; Play(g, p, id);
            Core2026Proof.Check(friend.Damage == 5 || friend.Zone.Type == Zone.GRAVEYARD, "12 Health deals 3");
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "3 damage kills the 5 Health minion at 2 damage taken");
            p.Hero.Armor = 20; var a = Summon(p, "CS2_182"); var b = Summon(p.Opponent, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(a.Damage == 3 && b.Damage == 3, "Armor does not count as Health");
            p.Hero.Armor = 0; p.Hero.Damage = 0; p.Opponent.Hero.Damage = 25;
            Play(g, p, id);
            Core2026Proof.Check(a.Damage == 4 && b.Damage == 4, "only my own Health is read");
            Summon(p, "EX1_012"); p.Hero.Damage = 18; Play(g, p, id);
            Core2026Proof.Check(a.Zone.Type == Zone.GRAVEYARD && b.Zone.Type == Zone.GRAVEYARD, "Spell Damage adds to the 3");
            End(g);
            var c = Summon(p, "CS2_182"); var d = Summon(p.Opponent, "CS2_182");
            p.Hero.Damage = 18; p.Opponent.Hero.Damage = 0; Play(g, p.Opponent, id);
            Core2026Proof.Check(c.Damage == 1 && d.Damage == 1, "opponent Revenge reads the opponent's Health");
            p.Opponent.Hero.Damage = 18; Play(g, p.Opponent, id);
            Core2026Proof.Check(c.Damage == 4 && d.Damage == 4, "opponent at 12 Health deals 3");
        });
        WildSetsProof.Case("BRM_017", (g, p, id) => {
            var held = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id), "no legal play before a friendly minion has died");
            Kill(g, Summon(p.Opponent, "CS1_042"));
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id), "an enemy death does not count");
            var buffed = Summon(p, "CS2_179"); buffed.Damage = 1; Play(g, p, "CS2_104", buffed); buffed.Silence(); Kill(g, buffed);
            Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id), "a friendly death makes it playable");
            Twin(g, p, held, null, "Resurrect");
            var back = p.BoardZone.Single();
            Core2026Proof.Check(back.Card.Id == "CS2_179" && back.AttackDamage == 3 && back.Health == 5 && back.Damage == 0 && !back.IsSilenced
                && back.HasTaunt && back.Controller == p, "the only candidate returns fresh, without buff, damage or Silence");
            Kill(g, Summon(p, "CS2_182")); Kill(g, Summon(p, "CS2_168"));
            End(g); End(g);
            var results = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var again = Add(p, id);
                Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == again.Id), "deaths from earlier turns still count");
                int before = p.BoardZone.Count; PlayEntity(g, p, again);
                Core2026Proof.Check(p.BoardZone.Count == before + 1, "exactly one minion returns");
                results.Add(p.BoardZone.OrderByDescending(m => m.Id).First().Card.Id);
            }
            Core2026Proof.Check(results.All(r => r == "CS2_179" || r == "CS2_182" || r == "CS2_168") && results.Distinct().Count() >= 2,
                "results come only from my dead minions and vary: " + string.Join(",", results));
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.Opponent.GraveyardZone.Any(m => m.Card.Id == "CS1_042"), "the enemy minion stays dead");
            while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
            var blocked = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == blocked.Id), "no legal play on a full board");
            End(g);
            Kill(g, Summon(p.Opponent, "CS2_179"));
            var theirs = Add(p.Opponent, id); int mine = p.BoardZone.Count;
            PlayEntity(g, p.Opponent, theirs);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && (p.Opponent.BoardZone.Single().Card.Id == "CS2_179" || p.Opponent.BoardZone.Single().Card.Id == "CS1_042")
                && p.BoardZone.Count == mine, "opponent Resurrect returns only its own dead minions");
        });
        WildSetsProof.Held("BRM_018", (g, p, id) => {
            var drake = Add(p, "BRM_024"); var yeti = Add(p, "CS2_182"); var theirs = Add(p.Opponent, "BRM_024");
            Core2026Proof.Check(drake.Cost == 6, "Dragon costs 6 before Consort");
            var consort = (Minion)Play(g, p, id);
            Core2026Proof.Check(drake.Cost == 4 && yeti.Cost == 4 && theirs.Cost == 6, "only my Dragon is cheaper, by 2");
            var late = Add(p, "BRM_024"); var lateWhelp = Add(p, "BRM_004"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(late.Cost == 4 && lateWhelp.Cost == 0, "Dragons drawn later are cheaper too, floored at zero: " + late.Cost + "," + lateWhelp.Cost);
            PlayEntity(g, p, yeti);
            Core2026Proof.Check(drake.Cost == 4, "a non-Dragon does not consume the discount");
            End(g); End(g);
            Core2026Proof.Check(drake.Cost == 4, "the discount lasts across turns");
            consort.Silence(); Core2026Proof.Check(drake.Cost == 4, "Silencing Consort does not end it");
            var second = Add(p, "BRM_024");
            PlayEntity(g, p, drake);
            Core2026Proof.Check(second.Cost == 6, "the discount ends after the Dragon is played");
            Play(g, p, id);
            var chain = Add(p, "BRM_024"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(chain.Cost == 4, "a second Consort grants its own discount");
            var consort2 = (Minion)Add(p, id); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(consort2.Cost == 3, "a Consort in hand is itself a Dragon and is discounted");
            PlayEntity(g, p, consort2);
            Core2026Proof.Check(chain.Cost == 4, "the Dragon discount of the played Consort is not consumed by itself");
            PlayEntity(g, p, chain);
            var after = Add(p, "BRM_024"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(after.Cost == 6, "one Dragon consumes the discount; later Dragons cost full price");
            End(g);
            var their = Add(p.Opponent, id); var mine = Add(p, "BRM_024");
            Core2026Proof.Check(their.Cost == 5, "opponent copy costs 5");
            PlayEntity(g, p.Opponent, their);
            var drake2 = Add(p.Opponent, "BRM_024"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(drake2.Cost == 4 && mine.Cost == 6, "opponent Consort discounts the opponent's Dragons only");
        });
        WildSetsProof.Held("BRM_020", (g, p, id) => {
            int Max(Minion m) => m.Health + m.Damage;
            var sorc = (Minion)Play(g, p, id);
            Core2026Proof.Check(sorc.AttackDamage == 3 && Max(sorc) == 5 && sorc.Card.IsRace(Race.DRAGON), "Dragonkin Sorcerer is a 3/5 Dragon");
            Play(g, p, "CS2_008", sorc);
            Core2026Proof.Check(sorc.AttackDamage == 4 && Max(sorc) == 6 && sorc.Damage == 1, "my spell targeting it gives +1/+1");
            Play(g, p, "CS2_008", sorc);
            Core2026Proof.Check(sorc.AttackDamage == 5 && Max(sorc) == 7 && sorc.Damage == 2, "it triggers every time");
            Play(g, p, "CS2_008", p.Hero);
            Core2026Proof.Check(sorc.AttackDamage == 5 && Max(sorc) == 7, "a spell on another target does nothing");
            Summon(p.Opponent, "CS2_182");
            Play(g, p, "BRM_005");
            Core2026Proof.Check(sorc.AttackDamage == 5 && Max(sorc) == 7 && sorc.Damage == 4, "an area spell does not target it");
            Play(g, p, "CS2_188", sorc);
            Core2026Proof.Check(sorc.AttackDamage == 7 && Max(sorc) == 7, "a Battlecry is no spell (only the Sergeant's own +2 Attack applies)");
            End(g);
            Play(g, p.Opponent, "CS2_008", sorc);
            Core2026Proof.Check(sorc.AttackDamage == 5 && Max(sorc) == 7 && sorc.Damage == 5, "the opponent's spell does not trigger it");
            End(g);
            var plain = Summon(p, id);
            Play(g, p, "CS2_008", plain);
            Core2026Proof.Check(Max(plain) == 6 && plain.AttackDamage == 4, "fresh Sorcerer gains once");
            Resume(g, next => {
                var again = (Minion)next.IdEntityDic[plain.Id];
                Play(next, next.Player1, "CS2_008", again);
                Core2026Proof.Check(again.AttackDamage == 5 && Max(again) == 7, "the trigger continues on a clone or a snapshot restore");
            });
            plain.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(plain.AttackDamage == 3 && Max(plain) == 5 && plain.Damage == 0, "Silence removes the stat gains");
            Play(g, p, "CS2_008", plain);
            Core2026Proof.Check(plain.AttackDamage == 3 && Max(plain) == 5 && plain.Damage == 1, "a Silenced Sorcerer no longer triggers");
            End(g);
            var theirs = Summon(p.Opponent, id);
            Play(g, p.Opponent, "CS2_008", theirs);
            Core2026Proof.Check(theirs.AttackDamage == 4 && Max(theirs) == 6, "its controller's spell triggers it");
            End(g);
            Play(g, p, "CS2_008", theirs);
            Core2026Proof.Check(theirs.AttackDamage == 4 && Max(theirs) == 6, "my spell on an enemy Sorcerer does not trigger it");
        });
    }

    /// <summary>Proofs of ids that a Standard table names (pool-eligibility-v1.json, core-2026-unlocks.json, plugin NPC trigger values).
    /// Not in the manifest and never registered: to run them, put [GroupProof("wild-y0b-m1a-blocked", ProofKind.WildCards)] on this
    /// method for one run and use --wild-y0b-m1a-blocked-only. BRM_016 (stats 2/5 vs 37.0 2/6) and BRM_022 (summons the 1/1 BRM_004t
    /// instead of the 2/1 Dragon BRM_022t) are known to fail until their Standard-table blocks are lifted.</summary>
    internal static void BlockedStandard()
    {
        BlockedFailures.Clear();
        Blocked("BRM_008", (g, p, id) => {
            var fresh = Summon(p.Opponent, "CS2_182"); var hurt = Summon(p.Opponent, "CS2_182"); hurt.Damage = 1;
            var weak = Summon(p.Opponent, "CS2_168"); var shield = Summon(p.Opponent, "EX1_008");
            var ally = Summon(p, "CS2_182"); var healed = Summon(p.Opponent, "CS2_182"); healed.Damage = 3; healed.Damage = 0;
            Core2026Proof.Check(shield.HasDivineShield, "fixture Divine Shield");
            // BLOCKED-HSDATA: the 37.0 cost is 4 (engine 5); its English text changed, so the identity row waits for the locale files.
            Twin(g, p, Add(p, id), null, "Dark Iron Skulker");
            Core2026Proof.Check(fresh.Damage == 2 && healed.Damage == 2 && weak.Zone.Type == Zone.GRAVEYARD, "undamaged enemy minions take 2, even if once healed");
            Core2026Proof.Check(hurt.Damage == 1, "an already damaged enemy minion is skipped");
            Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "Divine Shield minions are undamaged targets");
            Core2026Proof.Check(ally.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "friends and heroes are untouched");
            Play(g, p, id);
            Core2026Proof.Check(fresh.Damage == 2 && hurt.Damage == 1 && shield.Zone.Type == Zone.GRAVEYARD, "a second Skulker skips minions already damaged, including by the first");
            StandardSetsProof.ParkBoard(p);
            var damagedBefore = p.Opponent.BoardZone.Sum(m => m.Damage); Summon(p, id);
            Core2026Proof.Check(p.Opponent.BoardZone.Sum(m => m.Damage) == damagedBefore, "summoning has no Battlecry");
            Summon(p, "EX1_012"); healed.Damage = 0;
            StandardSetsProof.ParkBoard(p.Opponent); var lone = Summon(p.Opponent, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(lone.Damage == 2, "Spell Damage does not change a Battlecry");
            StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p, id);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "Skulker with no enemy minions is legal");
            End(g);
            var mine = Summon(p, "CS2_182"); var shielded = Summon(p, "EX1_008");
            Play(g, p.Opponent, id);
            Core2026Proof.Check(mine.Damage == 2 && !shielded.HasDivineShield, "opponent Skulker hits my undamaged minions");
        });
        Blocked("BRM_002", (g, p, id) => {
            var fw = (Minion)Play(g, p, id);
            var e1 = Summon(p.Opponent, "CS2_182"); var e2 = Summon(p.Opponent, "CS2_179"); var e3 = Summon(p.Opponent, "CS2_168");
            int Total() => p.Opponent.Hero.Damage + p.Opponent.BoardZone.Sum(m => m.Damage);
            Core2026Proof.Check(Total() == 0, "Flamewaker does nothing when played");
            Play(g, p, "CS2_008", e1);
            Core2026Proof.Check(Total() + (e3.Zone.Type == Zone.GRAVEYARD ? 1 : 0) == 3, "a spell then two pings of one damage");
            Core2026Proof.Check(p.Hero.Damage == 0 && fw.Damage == 0, "friendly characters untouched");
            Twin(g, p, Add(p, "CS2_008"), p.Hero, "Flamewaker");
            fw.Silence(); int silenced = Total() + p.Opponent.GraveyardZone.Count; Play(g, p, "CS2_008", p.Hero);
            Core2026Proof.Check(Total() + p.Opponent.GraveyardZone.Count == silenced, "Silence removes the trigger");
            StandardSetsProof.ParkBoard(p.Opponent); var lone = Summon(p.Opponent, "CS2_168");
            var fw2 = (Minion)Play(g, p, id); p.Opponent.Hero.Damage = 0;
            Play(g, p, "CS2_008", p.Hero);
            Core2026Proof.Check((lone.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 1) || (lone.Zone.Type != Zone.GRAVEYARD && p.Opponent.Hero.Damage == 2),
                "a dead target is skipped: two damage always land");
            End(g);
            Play(g, p.Opponent, "CS2_008", p.Hero);
            Core2026Proof.Check(fw2.Damage == 0 && p.Opponent.Hero.Damage <= 2, "an opponent spell does not trigger my Flamewaker");
        });
        Blocked("BRM_004", (g, p, id) => {
            Add(p, "BRM_024");
            var a = (Minion)Play(g, p, id);
            Core2026Proof.Check(a.AttackDamage == 2 && a.Health == 3 && a.Card.IsRace(Race.DRAGON), "holding a Dragon: +2 Health");
            EmptyHand(p); Add(p, "CS2_182");
            var b = (Minion)Play(g, p, id);
            Core2026Proof.Check(b.Health == 1, "a non-Dragon in hand does not count");
            EmptyHand(p);
            Summon(p, "BRM_024"); Add(p.Opponent, "BRM_024");
            var c = (Minion)Play(g, p, id);
            Core2026Proof.Check(c.Health == 1, "a Dragon on board, in the deck or in the opponent hand does not count");
            var second = Add(p, id); var d = (Minion)Play(g, p, id);
            Core2026Proof.Check(d.Health == 3, "another Twilight Whelp in hand is a Dragon");
            a.Silence(); Core2026Proof.Check(a.Health == 1, "Silence removes the Health");
            EmptyHand(p); Add(p, "BRM_024"); var s = Summon(p, id);
            Core2026Proof.Check(s.Health == 1, "summoning is no Battlecry");
        });
        Blocked("BRM_006", (g, p, id) => {
            var boss = (Minion)Play(g, p, id);
            Play(g, p, "CS2_008", boss);
            Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Last().Card.Id == "BRM_006t" && p.BoardZone.Last().AttackDamage == 1, "damage summons a 1/1 Imp to its right");
            Core2026Proof.Check(p.BoardZone.Single(m => m.Card.Id == "BRM_006t").ZonePosition > boss.ZonePosition, "Imp is to the right");
            Play(g, p, "CS2_008", boss); Core2026Proof.Check(p.BoardZone.Count == 3, "every damage instance summons one Imp");
            var shielded = Summon(p, "EX1_008"); Play(g, p, "CS2_008", shielded); Core2026Proof.Check(p.BoardZone.Count == 4, "Divine Shield absorbed damage on another minion summons nothing");
            boss.Damage = 3; Play(g, p, "CS2_008", boss);
            Core2026Proof.Check(boss.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count(m => m.Card.Id == "BRM_006t") == 3, "lethal damage still summons an Imp");
            var boss2 = Summon(p, id); boss2.Silence(); int n = p.BoardZone.Count; Play(g, p, "CS2_008", boss2);
            Core2026Proof.Check(p.BoardZone.Count == n, "Silenced Boss summons nothing");
            StandardSetsProof.ParkBoard(p); var full = Summon(p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            Play(g, p, "CS2_008", full); Core2026Proof.Check(p.BoardZone.Count == 7, "full board summons nothing");
            End(g);
            var theirs = Summon(p.Opponent, id); Play(g, p.Opponent, "CS2_008", theirs);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 2 && p.BoardZone.Count == 7, "opponent Imp appears on the opponent's side");
        });
        Blocked("BRM_013", (g, p, id) => {
            var target = Summon(p.Opponent, "CS2_182");
            var spell = Add(p, id);
            PlayEntity(g, p, spell, target);
            Core2026Proof.Check(target.Damage == 3, "Quick Shot deals 3");
            Core2026Proof.Check(p.HandZone.Count == 1, "empty hand: draws a card");
            Add(p, "CS2_182"); int hand = p.HandZone.Count;
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.HandZone.Count == hand, "a non-empty hand does not draw");
            EmptyHand(p); Summon(p, "EX1_012");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 7 && p.HandZone.Count == 1, "Spell Damage and draw together");
            EmptyHand(p); foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage > 4, "an empty deck draws fatigue");
            End(g); EmptyHand(p.Opponent);
            int mine = p.HandZone.Count; Play(g, p.Opponent, id, p.Hero);
            Core2026Proof.Check(p.HandZone.Count == mine && p.Opponent.HandZone.Count == 1, "opponent draws for the opponent only");
        });
        Blocked("BRM_016", (g, p, id) => {
            Core2026Proof.Check(Cards.FromId(id).ATK == 2 && Cards.FromId(id).Health == 6, "Axe Flinger is 2/6");
            var flinger = (Minion)Play(g, p, id);
            Play(g, p, "CS2_008", flinger);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0, "damage pings the enemy hero for 2");
            Play(g, p, "CS2_008", flinger); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "each damage instance triggers");
            flinger.Damage = 5; Play(g, p, "CS2_008", flinger);
            Core2026Proof.Check(flinger.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 6, "lethal damage still triggers");
            var shield = Summon(p, id); shield.HasDivineShield = true; Play(g, p, "CS2_008", shield);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "absorbed damage does not trigger");
            shield.Silence(); Play(g, p, "CS2_008", shield); Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "Silence removes the trigger");
            End(g);
            var theirs = Summon(p.Opponent, id); Play(g, p.Opponent, "CS2_008", theirs);
            Core2026Proof.Check(p.Hero.Damage == 2, "opponent Axe Flinger hits my hero");
        });
        Blocked("BRM_019", (g, p, id) => {
            var a = (Minion)Play(g, p, id);
            Play(g, p, "CS2_008", a);
            Core2026Proof.Check(p.BoardZone.Count == 2 && a.Damage == 1 && p.BoardZone.Count(m => m.Card.Id == id) == 2, "surviving damage summons another Patron");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Summon(p, id); Summon(p, id);
            Play(g, p, "BRM_005");
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 4, "two Patrons that survive each summon one");
            var dead = p.BoardZone.First(); dead.Damage = 2; int n = p.BoardZone.Count; Play(g, p, "CS2_008", dead);
            Core2026Proof.Check(dead.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == n - 1, "lethal damage summons nothing");
            StandardSetsProof.ParkBoard(p); var s = Summon(p, id); s.Silence(); Play(g, p, "CS2_008", s);
            Core2026Proof.Check(p.BoardZone.Count == 1, "Silenced Patron summons nothing");
            var shield = Summon(p, id); shield.HasDivineShield = true; Play(g, p, "CS2_008", shield);
            Core2026Proof.Check(p.BoardZone.Count == 2, "absorbed damage is not survived damage");
            End(g);
            var theirs = Summon(p.Opponent, id); Play(g, p.Opponent, "CS2_008", theirs);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 2, "opponent Patron summons on its own side");
        });
        Blocked("BRM_022", (g, p, id) => {
            var egg = (Minion)Play(g, p, id);
            Play(g, p, "CS2_008", egg);
            var whelp = p.BoardZone.Single(m => m.Id != egg.Id);
            Core2026Proof.Check(whelp.AttackDamage == 2 && whelp.Health == 1 && whelp.Card.IsRace(Race.DRAGON), "damage summons a 2/1 Dragon Whelp: " + whelp.Card.Id);
            Core2026Proof.Check(egg.Zone.Type == Zone.PLAY && egg.Damage == 1, "the Egg survives 1 damage");
            Play(g, p, "CS2_008", egg);
            Core2026Proof.Check(egg.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 2 && p.BoardZone.Count(m => m.Card.Id == whelp.Card.Id) == 2, "lethal damage still summons a Whelp");
            Core2026Proof.Check(egg.Card.ATK == 0 && egg.Card.Health == 2, "Dragon Egg is 0/2");
            End(g);
            var theirs = Summon(p.Opponent, id); Play(g, p.Opponent, "CS2_008", theirs);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 2, "opponent Egg summons on its own side");
        });
        if (BlockedFailures.Count > 0) throw new Exception(string.Join("\n", BlockedFailures));
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    /// <summary>The seat that does not own the cards sees none of their identities (nor does the owner, for deck cards).</summary>
    private static void HiddenFromOpponent(Game g, Controller owner, IEnumerable<int> hiddenIds, string what)
    {
        var ids = hiddenIds.ToArray();
        Core2026Proof.Check(ids.Length > 0, what + ": the fixture hides at least one card");
        var viewer = HistoryFairOriginProof.FairGame(FairBotState.Create(g, owner.Opponent.PlayerId, 97, DeckStyle.Value));
        foreach (int id in ids)
            Core2026Proof.Check(!viewer.IdEntityDic.TryGetValue(id, out var actor) || actor is Unknown, what + " stays hidden from the other seat: " + id);
    }

    private static void EmptyHand(Controller c)
    {
        foreach (var x in c.HandZone.ToArray()) { c.HandZone.Remove(x); c.SetasideZone.Add(x); }
    }

    private static void PlayChoice(Game g, Controller p, IPlayable card, int choice)
    {
        p.BaseMana = 10; p.UsedMana = 0;
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.ChooseOne == choice);
        Core2026Proof.Check(task != null && g.Process(task), "choose-one play " + choice);
    }

    /// <summary>The same play on the live game, on an exact clone and on a snapshot restore ends in the same state.</summary>
    private static void Twin(Game g, Controller p, IPlayable card, ICharacter target, string what)
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

    private static readonly List<string> BlockedFailures = new();

    private static void Blocked(string id, Action<Game, Controller, string> scenario)
    {
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
            }
            catch (Exception error) { BlockedFailures.Add(id + " seed " + seed + ": " + error.Message); return; }
        }
        Console.WriteLine("PASS blocked-standard " + id);
    }
}
