using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m3c: Grand Tournament AT_045..AT_065 (Shaman, Priest, Hunter, Warrior, Druid Aviana).
/// Registered (literal WildSetsProof.Case, listed in wild-y0b-m3c.txt): AT_045 .. AT_050, AT_054, AT_056 .. AT_061,
/// AT_063, AT_063t, AT_065.
/// HELD (amount rule, written but never registered): AT_053 Ancestral Knowledge: the base row stores Overload 2, the 37.0 text says (1);
/// the generator refuses an identity row whose amount differs from the text, so the card needs an engine change first.
/// BLOCKED-STANDARD, written but never registered: AT_051 (data/pools/pool-eligibility-v1.json), AT_052, AT_055, AT_062, AT_064
/// (CORE_ copy groups in plugin/LocalCardCatalog.cs and data/progression/core-2026-unlocks.json). Their scripts are untouched;
/// the proofs assert what the 37.0 text says and print any identity drift the base card still has. Run them with
/// WILD_Y0B_M3C_BLOCKED=1.
/// Joust (Reveal a minion in each deck) is proven through AT_048 (Healing Wave) and AT_058 (King's Elekk); Inspire through
/// AT_049 and AT_059 (the mechanic itself is proven in lane m3a, WildY0bM3AProof.InspireMechanic).
/// </summary>
internal static class WildY0bM3CProof
{
    private static bool BlockedRun => Environment.GetEnvironmentVariable("WILD_Y0B_M3C_BLOCKED") == "1";
    private const string Yeti = "CS2_182", Wisp = "CS2_231", Raptor = "CS2_172", Thalnos = "EX1_012", Frostbolt = "CS2_024",
        Fireball = "CS2_029", Footman = "CS1_042", Searing = "CS2_050", Wrath = "CS2_052", Stoneclaw = "CS2_051",
        Healing = "NEW1_009", ArcaneShot = "DS1_185", Freezing = "EX1_611";

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("wild-y0b-m3c-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        if (BlockedRun) Blocked();

        WildSetsProof.Case("AT_045", (g, p, id) => {
            var yeti = Add(p, Yeti); var wisp = Add(p, Wisp); var bolt = Add(p, Frostbolt); var foeYeti = Add(p.Opponent, Yeti);
            Check(yeti.Cost == 4 && wisp.Cost == 0 && bolt.Cost == 2 && foeYeti.Cost == 4, "no discount without Aviana");
            var aviana = (Minion)Play(g, p, id);
            Check(p.BoardZone.Count == 1 && aviana.AttackDamage == 5 && aviana.Health == 5, "Aviana is a 5/5");
            Check(yeti.Cost == 1 && wisp.Cost == 1, "hand minions cost (1), also a Wisp that cost 0: " + yeti.Cost + "/" + wisp.Cost);
            Check(bolt.Cost == 2, "a spell keeps its cost");
            Check(foeYeti.Cost == 4, "the opponent's hand is untouched");
            var spare = Add(p, id);
            g.DeathProcessingAndAuraUpdate();
            Check(spare.Cost == 1, "a second Aviana in hand costs (1) as well");
            PlayEntity(g, p, yeti);
            Check(p.UsedMana == 1 && yeti.Zone.Type == Zone.PLAY, "the discounted minion is paid for with 1 Mana");
            End(g); End(g);
            var drawn = p.HandZone.Last();
            Check(drawn.Card.Id == Yeti && drawn.Cost == 1, "a minion drawn at the start of my turn costs (1) too: " + drawn.Card.Id + "/" + drawn.Cost);
            var second = Summon(p, id);
            Check(wisp.Cost == 1 && drawn.Cost == 1, "two Avianas do not stack");
            var foe = Summon(p.Opponent, id);
            Check(foeYeti.Cost == 1 && wisp.Cost == 1, "the opponent's Aviana discounts only the opponent's hand");
            Kill(g, foe);
            Check(foeYeti.Cost == 4, "the opponent's discount ends with their Aviana");
            Resume(g, next => {
                var mine = next.Player1;
                Check(((IPlayable)next.IdEntityDic[wisp.Id]).Cost == 1, "the discount holds on a clone");
                Kill(next, (Minion)next.IdEntityDic[aviana.Id]); Kill(next, (Minion)next.IdEntityDic[second.Id]);
                Check(((IPlayable)next.IdEntityDic[wisp.Id]).Cost == 0 && ((IPlayable)next.IdEntityDic[drawn.Id]).Cost == 4,
                    "the discount ends when the last Aviana leaves play, on a clone");
            });
            aviana.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(wisp.Cost == 1, "one silenced Aviana: the other still discounts");
            second.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(wisp.Cost == 0 && drawn.Cost == 4 && spare.Cost == 9, "Silence removes the discount: " + wisp.Cost + "/" + drawn.Cost + "/" + spare.Cost);
            var third = Summon(p, id);
            Kill(g, third);
            Check(wisp.Cost == 0, "death removes the discount");
        });

        WildSetsProof.Case("AT_046", (g, p, id) => {
            var seen = new HashSet<string>();
            var basic = Cards.BasicTotems.Select(c => c.Id).ToHashSet();
            for (int i = 0; i < 28; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var left = Summon(p, Yeti);
                var tuskarr = (Minion)Play(g, p, id);
                Check(tuskarr.AttackDamage == 3 && tuskarr.Health == 2, "Tuskarr Totemic is a 3/2");
                Check(p.BoardZone.Count == 3, "one Totem joins: " + p.BoardZone.Count);
                var totem = p.BoardZone.Single(m => m.Card.Id != id && m.Card.Id != Yeti);
                Check(totem.IsRace(Race.TOTEM) && basic.Contains(totem.Card.Id), "the summoned minion is a basic Totem: " + totem.Card.Id);
                Check(totem.ZonePosition == tuskarr.ZonePosition + 1, "the Totem appears right of Tuskarr: " + tuskarr.ZonePosition + "/" + totem.ZonePosition + "/" + left.ZonePosition);
                seen.Add(totem.Card.Id);
            }
            Check(seen.Count == 4 && seen.SetEquals(basic), "all four basic Totems are possible: " + string.Join(",", seen));
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Play(g, p, id);
            Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id == Yeti || m.Card.Id == id), "a full board leaves no room for the Totem");
            Check(p.Opponent.BoardZone.Count == 0, "nothing is summoned for the opponent");
            StandardSetsProof.ParkBoard(p);
            Resume(g, next => {
                Play(next, next.Player1, id);
                Check(next.Player1.BoardZone.Count == 2 && next.Player1.BoardZone.Any(m => m.IsRace(Race.TOTEM)), "Battlecry on a clone summons a Totem");
            });
        });

        WildSetsProof.Case("AT_047", (g, p, id) => {
            var plain = (Minion)Play(g, p, id);
            Check(plain.AttackDamage == 4 && plain.Health == 5, "Draenei Totemcarver is a 4/5 and gains nothing without Totems: " + plain.AttackDamage + "/" + plain.Health);
            StandardSetsProof.ParkBoard(p);
            Summon(p, Yeti); Summon(p, Raptor);
            var s1 = Summon(p, Searing); Summon(p, Wrath);
            Summon(p.Opponent, Searing); Summon(p.Opponent, Stoneclaw);
            var carver = (Minion)Play(g, p, id);
            Check(carver.AttackDamage == 6 && carver.Health == 7, "+1/+1 per friendly Totem, not per other minion or enemy Totem: " + carver.AttackDamage + "/" + carver.Health);
            s1.Silence(); g.DeathProcessingAndAuraUpdate();
            var late = Summon(p, Stoneclaw);
            Check(carver.AttackDamage == 6 && carver.Health == 7, "later Totems and Silenced Totems change nothing afterwards");
            var again = (Minion)Play(g, p, id);
            Check(again.AttackDamage == 7 && again.Health == 8, "a Silenced Totem still counts, and a Draenei is no Totem: " + again.AttackDamage + "/" + again.Health);
            carver.Damage = 2;
            Check(carver.Health == 5, "damage applies on top of the gain");
            carver.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(carver.AttackDamage == 4 && carver.Health == 5 && carver.Damage == 0, "Silence removes the gain; the damage is capped by the new maximum: " + carver.AttackDamage + "/" + carver.Health + " dmg " + carver.Damage);
            Kill(g, late);
            Check(late.Zone.Type == Zone.GRAVEYARD, "dead Totem");
            Kill(g, (Minion)p.BoardZone.First(m => m.Card.Id == Searing));
            var last = (Minion)Play(g, p, id);
            Check(last.AttackDamage == 5 && last.Health == 6, "dead Totems no longer count, only Wrath of Air is left: " + last.AttackDamage + "/" + last.Health);
            Resume(g, next => {
                var n = (Minion)Play(next, next.Player1, id);
                Check(n.AttackDamage == 5 && n.Health == 6, "Battlecry on a clone counts the same Totems");
            });
        });

        WildSetsProof.Case("AT_048", (g, p, id) => {
            var hurt = p.Hero; var foe = p.Opponent.Hero;
            Check(Add(p, id).Cost == 3, "Healing Wave costs 3");
            ParkHand(p);
            // Joust won: 16.
            SetDeck(p, Yeti); SetDeck(p.Opponent, Wisp);
            hurt.Damage = 25; int mineBefore = p.DeckZone.Count, theirsBefore = p.Opponent.DeckZone.Count;
            Play(g, p, id, hurt);
            Check(hurt.Damage == 9, "a costlier minion restores 16: " + hurt.Damage);
            Check(p.DeckZone.Count == mineBefore && p.Opponent.DeckZone.Count == theirsBefore, "the revealed minions stay in their decks");
            Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 0, "Reveal draws nothing");
            // Tie: 8.
            SetDeck(p, Yeti); SetDeck(p.Opponent, Yeti);
            hurt.Damage = 25;
            Play(g, p, id, hurt);
            Check(hurt.Damage == 17, "a tie restores only 8: " + hurt.Damage);
            // Lower: 8.
            SetDeck(p, Wisp); SetDeck(p.Opponent, Yeti);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 17, "a cheaper minion restores only 8");
            // Opponent deck without minions: any minion of mine wins.
            SetDeck(p, Wisp); SetDeck(p.Opponent, Frostbolt, Frostbolt);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 9, "an opponent deck without minions loses the Joust: " + hurt.Damage);
            // My deck without minions: lose; both without: lose.
            SetDeck(p, Frostbolt); SetDeck(p.Opponent, Wisp);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 17, "no minion in my deck restores only 8");
            SetDeck(p, Frostbolt); SetDeck(p.Opponent, Frostbolt);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 17, "no minion in either deck restores only 8");
            SetDeck(p); SetDeck(p.Opponent);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 17, "empty decks restore only 8");
            // Random reveal: mine {Yeti, Wisp, Raptor} against theirs {Raptor}: only the Yeti wins.
            var outcomes = new HashSet<int>();
            for (int i = 0; i < 24; i++)
            {
                SetDeck(p, Yeti, Wisp, Raptor); SetDeck(p.Opponent, Raptor);
                hurt.Damage = 25; Play(g, p, id, hurt);
                outcomes.Add(25 - hurt.Damage);
                Check(hurt.Damage == 9 || hurt.Damage == 17, "only 8 or 16 are possible");
            }
            Check(outcomes.Count == 2, "the Joust is random: " + string.Join(",", outcomes));
            // Targets: enemy hero, minions, no overheal, spell damage does not matter.
            SetDeck(p, Yeti); SetDeck(p.Opponent, Wisp);
            foe.Damage = 3; Play(g, p, id, foe);
            Check(foe.Damage == 0, "healing never goes above full Health");
            var minion = Summon(p, Yeti); minion.Damage = 4;
            Play(g, p, id, minion);
            Check(minion.Damage == 0, "any character can be healed");
            Summon(p, Thalnos);
            hurt.Damage = 25; Play(g, p, id, hurt);
            Check(hurt.Damage == 9, "Spell Damage does not change the restore amount");
            var held = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(targets.Length > 0 && targets.All(t => t.Target != null), "Healing Wave needs a target and has legal ones");
            Resume(g, next => {
                var owner = next.Player1;
                SetDeck(owner, Yeti); SetDeck(owner.Opponent, Wisp);
                owner.Hero.Damage = 25; Play(next, owner, id, owner.Hero);
                Check(owner.Hero.Damage == 9, "Joust on a clone");
            });
        });

        WildSetsProof.Case("AT_049", (g, p, id) => {
            var totem = Summon(p, Searing); var other = Summon(p, Yeti); var foeTotem = Summon(p.Opponent, Searing);
            var valiant = (Minion)Play(g, p, id);
            Check(valiant.AttackDamage == 3 && valiant.Health == 6, "Thunder Bluff Valiant is a 3/6");
            Check(totem.AttackDamage == 3 && other.AttackDamage == 4 && foeTotem.AttackDamage == 1 && valiant.AttackDamage == 3,
                "Battlecry gives friendly Totems +2 Attack only: " + totem.AttackDamage + "/" + other.AttackDamage + "/" + foeTotem.AttackDamage);
            Use(g, p);
            Check(totem.AttackDamage == 5 && other.AttackDamage == 4 && foeTotem.AttackDamage == 1, "Inspire gives Totems +2 again: " + totem.AttackDamage);
            Check(!CanUse(p), "one Inspire per Hero Power use");
            var late = Summon(p, Stoneclaw);
            End(g); End(g);
            Use(g, p);
            Check(totem.AttackDamage == 7 && late.AttackDamage == 2, "Inspire reaches Totems summoned since, and stacks: " + totem.AttackDamage + "/" + late.AttackDamage);
            End(g);
            Use(g, p.Opponent, p.Hero);
            Check(totem.AttackDamage == 7, "the opponent's Hero Power does not Inspire for me");
            End(g);
            totem.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(totem.AttackDamage == 1, "Silence removes the Attack bonus from a Totem");
            Resume(g, next => {
                var owner = next.Player1;
                End(next); End(next);
                Use(next, owner);
                Check(((Minion)next.IdEntityDic[late.Id]).AttackDamage == 4, "Inspire continues on a clone");
            });
            valiant.Silence(); g.DeathProcessingAndAuraUpdate();
            End(g); End(g);
            int before = late.AttackDamage; Use(g, p);
            Check(late.AttackDamage == before, "a Silenced Valiant no longer Inspires");
            StandardSetsProof.ParkBoard(p);
            var lone = (Minion)Play(g, p, id);
            Check(lone.AttackDamage == 3 && p.BoardZone.Count == 1, "Battlecry with no Totem does nothing");
            var foeValiant = Summon(p.Opponent, id);
            var mine = Summon(p, Searing);
            End(g);
            Use(g, p.Opponent, p.Hero);
            Check(mine.AttackDamage == 1 && foeTotem.AttackDamage == 3, "an enemy Valiant Inspires for the enemy side only: " + mine.AttackDamage + "/" + foeTotem.AttackDamage);
        });

        WildSetsProof.Case("AT_050", (g, p, id) => {
            Check(Add(p, id).Cost == 3, "Charged Hammer costs 3");
            var yeti = Summon(p.Opponent, Yeti); yeti.BaseHealth = 40;
            Play(g, p, id);
            var weapon = p.Hero.Weapon;
            Check(weapon != null && weapon.AttackDamage == 2 && weapon.Durability == 3, "Charged Hammer is a 2/3 weapon: " + weapon?.AttackDamage + "/" + weapon?.Durability);
            var original = p.Hero.HeroPower.Card.Id;
            AttackHero(g, p, yeti);
            Check(p.Hero.Weapon != null && p.Hero.Weapon.Durability == 2 && yeti.Damage == 2, "an attack costs 1 Durability");
            Check(p.Hero.HeroPower.Card.Id == original, "the Hero Power only changes when the weapon dies");
            Resume(g, next => {
                var w = next.Player1.Hero.Weapon;
                w.ToBeDestroyed = true; next.DeathProcessingAndAuraUpdate();
                Check(next.Player1.Hero.HeroPower.Card.Id == "AT_050t", "Deathrattle on a clone replaces the Hero Power");
            });
            p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Check(p.Hero.Weapon == null && p.Hero.HeroPower.Card.Id == "AT_050t", "when the weapon dies the Hero Power becomes Lightning Jolt");
            Check(p.Opponent.Hero.HeroPower.Card.Id != "AT_050t", "the opponent's Hero Power is untouched");
            End(g); End(g);
            var jolt = p.Hero.HeroPower;
            Check(jolt.Cost == 2, "Lightning Jolt costs 2");
            var noTarget = p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target == null);
            Check(!noTarget, "Lightning Jolt needs a target");
            Use(g, p, p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 2, "Lightning Jolt deals 2 damage: " + p.Opponent.Hero.Damage);
            End(g); End(g);
            Use(g, p, yeti);
            Check(yeti.Damage == 2 + 2, "Lightning Jolt hits a minion for 2: " + yeti.Damage);
            // A Charged Hammer that is silenced can't be: weapons have no Silence. A destroyed weapon with the Hammer already gone changes nothing.
            Play(g, p, id);
            p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Check(p.Hero.HeroPower.Card.Id == "AT_050t", "a second Hammer leaves Lightning Jolt in place");
            // Replaced by a new weapon: the Hammer is destroyed, so Deathrattle fires on a Hammer that was replaced too.
            SetPower(p, "CS2_083b");
            Play(g, p, id);
            Play(g, p, "CS2_106");
            Check(p.Hero.Weapon.Card.Id == "CS2_106" && p.Hero.HeroPower.Card.Id == "AT_050t", "replacing the Hammer with another weapon triggers its Deathrattle");
            // The opponent's own Hammer changes only the opponent's Hero Power.
            End(g);
            var theirs = p.Opponent;
            Check(theirs.Hero.HeroPower.Card.Id == "CS2_034", "the opponent still has Fireblast");
            Play(g, theirs, id);
            theirs.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Check(theirs.Hero.HeroPower.Card.Id == "AT_050t" && p.Hero.HeroPower.Card.Id == "AT_050t", "an enemy Hammer replaces the enemy Hero Power");
        });

        WildSetsProof.Held("AT_054", (g, p, id) => {
            ParkHand(p);
            SetDeck(p, Yeti, Raptor, Frostbolt, Wisp);
            var handYeti = Add(p, Yeti); var handWisp = Add(p, Wisp); var handSpell = Add(p, Frostbolt);
            var foeHand = Add(p.Opponent, Yeti); var foeDeck = p.Opponent.DeckZone.First();
            var mistcaller = (Minion)Play(g, p, id);
            Check(mistcaller.AttackDamage == 4 && mistcaller.Health == 4, "The Mistcaller itself stays a 4/4");
            var yeti = (Minion)handYeti; var wisp = (Minion)handWisp;
            Check(yeti.AttackDamage == 5 && yeti.Health == 6 && wisp.AttackDamage == 2 && wisp.Health == 2, "minions in hand get +1/+1: " + yeti.AttackDamage + "/" + yeti.Health);
            Check(handSpell.Cost == 2 && handSpell.Zone.Type == Zone.HAND, "spells in hand are untouched");
            var deckMinions = p.DeckZone.Where(c => c.Card.Type == CardType.MINION).Cast<Minion>().ToArray();
            Check(deckMinions.Length == 3 && deckMinions.All(m => m.AttackDamage == m.Card.ATK + 1 && m.Health == m.Card.Health + 1), "minions in the deck get +1/+1");
            Check(p.DeckZone.Where(c => c.Card.Type == CardType.SPELL).All(c => c.Cost == 2), "spells in the deck are untouched");
            Check(((Minion)foeHand).AttackDamage == 4 && ((Minion)foeDeck).AttackDamage == 4 && p.Opponent.DeckZone.Cast<Minion>().All(m => m.AttackDamage == 4 && m.Health == 5),
                "the opponent's hand and deck are untouched");
            var played = (Minion)Play(g, p, Yeti);
            Check(played.AttackDamage == 4, "a fresh Yeti is a plain 4/5");
            PlayEntity(g, p, yeti);
            Check(yeti.Zone.Type == Zone.PLAY && yeti.AttackDamage == 5 && yeti.Health == 6, "the buff stays after playing");
            yeti.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(yeti.AttackDamage == 4 && yeti.Health == 5, "Silence removes the buff");
            var drawn = Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
            Check(drawn == null || drawn is not Minion dm || dm.AttackDamage == dm.Card.ATK + 1, "a drawn deck minion keeps the buff");
            var second = (Minion)Play(g, p, id);
            Check(wisp.AttackDamage == 3 && wisp.Health == 3, "a second Mistcaller stacks +1/+1: " + wisp.AttackDamage);
            var inHand = Add(p, id);
            var third = (Minion)Play(g, p, id);
            Check(((Minion)inHand).AttackDamage == 5 && ((Minion)inHand).Health == 5, "another Mistcaller in hand is buffed too");
            ParkHand(p); SetDeck(p);
            Play(g, p, id);
            Check(p.HandZone.Count == 0 && p.DeckZone.Count == 0, "no minions in hand or deck: nothing happens");
            SetDeck(p, Raptor); ParkHand(p);
            var held = Add(p, Yeti);
            Resume(g, next => {
                var owner = next.Player1;
                Play(next, owner, id);
                var n = (Minion)next.IdEntityDic[held.Id];
                Check(n.AttackDamage == 5 && ((Minion)owner.DeckZone.Single()).AttackDamage == 4, "Battlecry on a clone buffs hand and deck");
            });
            HiddenSeat(g, p.Opponent.PlayerId, new IPlayable[] { }, "Mistcaller changes no card identity");
        });

        WildSetsProof.Case("AT_056", (g, p, id) => {
            var left = Summon(p.Opponent, Wisp); var middle = Summon(p.Opponent, Yeti); var right = Summon(p.Opponent, Raptor);
            var farRight = Summon(p.Opponent, Yeti);
            var spell = Add(p, id);
            var tasks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
            Check(spell.Cost == 3 && tasks.Length == 4 && tasks.All(t => t.Target is Minion), "Powershot costs 3 and targets minions only: " + tasks.Length);
            PlayEntity(g, p, spell, middle);
            Check(left.Zone.Type == Zone.GRAVEYARD && middle.Damage == 2 && right.Damage == 2 && farRight.Damage == 0, "the target and both neighbours take 2: "
                + middle.Damage + "/" + right.Damage + "/" + farRight.Damage);
            Check(p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 0, "heroes never take damage");
            var end = Summon(p.Opponent, Yeti);
            Play(g, p, id, end);
            Check(end.Damage == 2 && farRight.Damage == 2, "an end minion hits only one neighbour: " + end.Damage + "/" + farRight.Damage);
            Check(right.Damage == 2, "a minion two places away is safe");
            StandardSetsProof.ParkBoard(p.Opponent);
            var only = Summon(p.Opponent, Yeti);
            Play(g, p, id, only);
            Check(only.Damage == 2, "a lone minion takes 2");
            // Spell Damage, Divine Shield and friendly minions.
            StandardSetsProof.ParkBoard(p);
            var a = Summon(p, Yeti); var shielded = Summon(p, Yeti); shielded.HasDivineShield = true; var c = Summon(p, Yeti);
            Summon(p, Thalnos);
            Play(g, p, id, shielded);
            Check(a.Damage == 3 && c.Damage == 3 && !shielded.HasDivineShield && shielded.Damage == 0, "Spell Damage applies, Divine Shield absorbs, and friends are hit too");
            // Killing the target does not stop the neighbours.
            var weak = Summon(p.Opponent, Wisp); var neighbour = Summon(p.Opponent, Yeti);
            var ahead = Summon(p.Opponent, Wisp);
            Play(g, p, id, weak);
            Check(weak.Zone.Type == Zone.GRAVEYARD && neighbour.Damage == 3 && ahead.Zone.Type == Zone.PLAY, "a dying target still damages the minion beside it");
            // Stealth minions cannot be targeted but are still hit as neighbours.
            var stealth = Summon(p.Opponent, Yeti); stealth.HasStealth = true;
            var shown = Summon(p.Opponent, Yeti);
            var again = Add(p, id);
            Check(!p.Options().Any(t => t.Source?.Id == again.Id && t.Target?.Id == stealth.Id), "a Stealthed enemy cannot be targeted");
            int before = stealth.Damage;
            PlayEntity(g, p, again, shown);
            Check(stealth.Damage == before + 3, "a Stealthed neighbour is still damaged");
            Resume(g, next => {
                var owner = next.Player1;
                var t = (Minion)next.IdEntityDic[shown.Id];
                int d = t.Damage; Play(next, owner, id, t);
                Check(t.Damage == d + 3, "Powershot on a clone");
            });
        });

        WildSetsProof.Case("AT_057", (g, p, id) => {
            var beast = Summon(p, Raptor); var human = Summon(p, Yeti); var foeBeast = Summon(p.Opponent, Raptor);
            var held = Add(p, id);
            var tasks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(held.Cost == 3 && tasks.Length > 0 && tasks.All(t => t.Target == null || t.Target.Id == beast.Id),
                "Stablemaster targets only my Beast: " + string.Join(",", tasks.Select(t => t.Target?.Card.Id)));
            var stealthBeast = Summon(p, Raptor); stealthBeast.HasStealth = true;
            Check(p.Options().Any(t => t.Source?.Id == held.Id && t.Target?.Id == stealthBeast.Id), "a friendly Stealth Beast can be chosen");
            Kill(g, stealthBeast);
            PlayEntity(g, p, held, beast);
            var maker = (Minion)held;
            Check(maker.AttackDamage == 4 && maker.Health == 2, "Stablemaster is a 4/2");
            Check(beast.IsImmune && !human.IsImmune && !foeBeast.IsImmune, "only the chosen Beast is Immune");
            Play(g, p, Frostbolt, beast);
            Check(beast.Damage == 0, "Immune takes no damage");
            beast.HasDivineShield = true; Play(g, p, Frostbolt, beast);
            Check(beast.HasDivineShield && beast.Damage == 0, "Immune does not pop Divine Shield");
            beast.HasDivineShield = false;
            Resume(g, next => {
                Check(((Minion)next.IdEntityDic[beast.Id]).IsImmune, "Immunity survives a clone");
                End(next);
                Check(!((Minion)next.IdEntityDic[beast.Id]).IsImmune, "Immunity ends with the turn, on a clone");
            });
            End(g);
            Check(!beast.IsImmune, "Immune lasts this turn only");
            Play(g, p.Opponent, Frostbolt, beast);
            Check(beast.Damage == 2 || beast.Zone.Type == Zone.GRAVEYARD, "after the turn the Beast takes damage: " + beast.Damage);
            End(g);
            // No Beast: still playable without a target.
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Summon(p, Yeti);
            var empty = Add(p, id);
            p.BaseMana = 10; p.UsedMana = 0;
            var untargeted = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == empty.Id);
            Check(untargeted != null && untargeted.Target == null && g.Process(untargeted), "with no friendly Beast Stablemaster plays without a target");
            // Silence removes the Immunity.
            var silenceMe = Summon(p, Raptor);
            Play(g, p, id, silenceMe);
            silenceMe.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(!silenceMe.IsImmune, "Silence removes the Immunity");
            Play(g, p, Frostbolt, silenceMe);
            Check(silenceMe.Zone.Type == Zone.GRAVEYARD, "a Silenced Beast takes damage again");
            // A Beast on the enemy side may not be chosen.
            var enemy = Summon(p.Opponent, Raptor);
            var last = Add(p, id);
            Check(!p.Options().Any(t => t.Source?.Id == last.Id && t.Target?.Id == enemy.Id), "an enemy Beast cannot be chosen");
        });

        WildSetsProof.Case("AT_058", (g, p, id) => {
            // Mine wins: draw exactly that minion.
            SetDeck(p, Yeti, Frostbolt); SetDeck(p.Opponent, Wisp);
            ParkHand(p);
            var revealed = p.DeckZone.Single(c => c.Card.Id == Yeti);
            int oppDeck = p.Opponent.DeckZone.Count, oppHand = p.Opponent.HandZone.Count;
            var elekk = (Minion)Play(g, p, id);
            Check(elekk.AttackDamage == 3 && elekk.Health == 2 && elekk.IsRace(Race.BEAST), "King's Elekk is a 3/2 Beast");
            Check(revealed.Zone.Type == Zone.HAND && p.HandZone.Count == 1 && p.DeckZone.Count == 1, "the costlier revealed minion is drawn");
            Check(p.Opponent.DeckZone.Count == oppDeck && p.Opponent.HandZone.Count == oppHand, "the opponent draws nothing and keeps the deck");
            SetDeck(p, "EX1_008"); SetDeck(p.Opponent, Wisp); ParkHand(p);
            var squire = p.DeckZone.Single();
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(squire.Zone.Type == Zone.HAND, "the revealed Squire is drawn");
            HiddenSeat(g, p.Opponent.PlayerId, new[] { squire }, "a drawn minion");
            // Tie and loss: no draw.
            SetDeck(p, Yeti); SetDeck(p.Opponent, Yeti); ParkHand(p);
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(p.HandZone.Count == 0 && p.DeckZone.Count == 1, "a tie draws nothing");
            SetDeck(p, Raptor); SetDeck(p.Opponent, Yeti); ParkHand(p);
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(p.HandZone.Count == 0 && p.DeckZone.Count == 1, "a cheaper minion draws nothing");
            // Opponent without a minion: draw mine. Mine without a minion: nothing.
            SetDeck(p, Raptor); SetDeck(p.Opponent, Frostbolt); ParkHand(p);
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(p.HandZone.Count == 1, "an opponent deck without minions loses the Joust");
            SetDeck(p, Frostbolt); SetDeck(p.Opponent, Wisp); ParkHand(p);
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(p.HandZone.Count == 0 && p.DeckZone.Count == 1, "no minion in my deck draws nothing");
            SetDeck(p); SetDeck(p.Opponent); ParkHand(p);
            StandardSetsProof.ParkBoard(p); Play(g, p, id);
            Check(p.HandZone.Count == 0, "empty decks draw nothing and do not hurt: " + p.Hero.Damage);
            // Random reveal: only the Yeti (4) beats the Raptor (2); Wisp (0) and Raptor (2, tie) never draw.
            var drawn = new HashSet<string>(); int draws = 0;
            for (int i = 0; i < 40; i++)
            {
                SetDeck(p, Yeti, Wisp, Raptor); SetDeck(p.Opponent, Raptor); ParkHand(p);
                StandardSetsProof.ParkBoard(p); Play(g, p, id);
                foreach (var c in p.HandZone.ToArray()) { drawn.Add(c.Card.Id); draws++; }
                Check(p.HandZone.Count <= 1 && p.DeckZone.Count + p.HandZone.Count == 3, "at most one card is drawn");
            }
            Check(drawn.SetEquals(new[] { Yeti }) && draws > 3 && draws < 40, "only the Yeti is ever drawn, sometimes: " + string.Join(",", drawn) + " " + draws);
            // Two mine against two theirs: reveal each side separately.
            var results = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                SetDeck(p, Yeti, Wisp); SetDeck(p.Opponent, Wisp, Raptor); ParkHand(p);
                StandardSetsProof.ParkBoard(p); Play(g, p, id);
                results.Add(string.Join("+", p.HandZone.Select(c => c.Card.Id)));
            }
            Check(results.Contains("") && results.Contains(Yeti) && results.Count == 2, "both sides reveal at random: [" + string.Join("|", results) + "]");
            Resume(g, next => {
                var owner = next.Player1;
                SetDeck(owner, Yeti); SetDeck(owner.Opponent, Wisp); ParkHand(owner);
                StandardSetsProof.ParkBoard(owner); Play(next, owner, id);
                Check(owner.HandZone.Count == 1 && owner.HandZone[0].Card.Id == Yeti, "Battlecry on a clone");
            });
        });

        WildSetsProof.Held("AT_059", "held: FX-MISSING live (inspire)", (g, p, id) => {
            var archer = Summon(p, id);
            Check(archer.AttackDamage == 2 && archer.Health == 1, "Brave Archer is a 2/1");
            Check(Add(p, id).Cost == 1, "Brave Archer costs 1");
            ParkHand(p);
            Use(g, p);
            Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0, "Inspire with an empty hand deals 2 to the enemy hero");
            Check(!CanUse(p), "one Inspire per Hero Power use");
            End(g); End(g);
            var held = Add(p, Wisp);
            Use(g, p);
            Check(p.Opponent.Hero.Damage == 2, "no Inspire damage while I hold a card");
            End(g); End(g);
            ParkHand(p);
            p.Opponent.Hero.Armor = 1;
            Use(g, p);
            Check(p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 3, "damage goes through Armor first: " + p.Opponent.Hero.Armor + "/" + p.Opponent.Hero.Damage);
            End(g); End(g); ParkHand(p);
            var second = Summon(p, id);
            Use(g, p);
            Check(p.Opponent.Hero.Damage == 7, "two Brave Archers Inspire twice: " + p.Opponent.Hero.Damage);
            End(g); End(g); ParkHand(p);
            archer.Silence(); g.DeathProcessingAndAuraUpdate();
            Resume(g, next => {
                Use(next, next.Player1);
                Check(next.Player2.Hero.Damage == 9, "Inspire on a clone: " + next.Player2.Hero.Damage);
            });
            Use(g, p);
            Check(p.Opponent.Hero.Damage == 9, "Silence removes one Inspire");
            // The enemy Hero Power does not Inspire mine; their own Archer works on their turn with their empty hand.
            End(g);
            Use(g, p.Opponent, p.Hero);
            Check(p.Opponent.Hero.Damage == 9, "the enemy Hero Power does not Inspire for me");
            ParkHand(p.Opponent);
            var foeArcher = Summon(p.Opponent, id);
            End(g); End(g);
            ParkHand(p.Opponent);
            int myHurt = p.Hero.Damage;
            Use(g, p.Opponent, p.Opponent.Hero);
            Check(p.Hero.Damage == myHurt + 2, "an enemy Archer hits my hero when their hand is empty: " + (p.Hero.Damage - myHurt));
        });

        WildSetsProof.Case("AT_060", (g, p, id) => {
            ParkHand(p);
            var trap = Add(p, id);
            Check(trap.Cost == 2, "Bear Trap costs 2");
            PlayEntity(g, p, trap);
            Check(p.SecretZone.Count == 1, "Bear Trap is a Secret");
            var guard = Summon(p, Wisp);
            var raider = Summon(p.Opponent, Yeti);
            End(g);
            // An attack on a minion does not trigger it.
            Attack(g, raider, guard);
            Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0, "an attack on a minion does not spring it");
            raider.IsExhausted = false;
            Resume(g, next => {
                var r = (Minion)next.IdEntityDic[raider.Id];
                var attack = next.Player2.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == r.Id && t.Target?.Id == next.Player1.Hero.Id);
                Check(next.Process(attack), "attack accepted on a clone");
                Check(next.Player1.BoardZone.Count == 1 && next.Player1.BoardZone[0].AttackDamage == 3 && next.Player1.BoardZone[0].Health == 3 && next.Player1.BoardZone[0].HasTaunt
                    && next.Player1.SecretZone.Count == 0, "the trap springs on a clone");
            });
            Attack(g, raider, p.Hero);
            Check(p.Hero.Damage == 4, "the attack still hits the hero for 4: " + p.Hero.Damage);
            Check(p.SecretZone.Count == 0 && p.BoardZone.Count == 1, "a Bear is summoned and the Secret is spent");
            var bear = p.BoardZone[0];
            Check(bear.AttackDamage == 3 && bear.Health == 3 && bear.HasTaunt, "a 3/3 Bear with Taunt");
            Check(p.Opponent.BoardZone.Count == 1 && raider.Zone.Type == Zone.PLAY, "nothing happens to the attacker");
            raider.IsExhausted = false;
            Check(!p.Opponent.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == p.Hero.Id), "the Taunt Bear now guards the hero");
            // Full board: the Secret cannot summon, so it stays set.
            End(g);
            StandardSetsProof.ParkBoard(p); ParkHand(p);
            var again = Add(p, id);
            PlayEntity(g, p, again);
            for (int i = 0; i < 7; i++) Summon(p, Wisp);
            End(g);
            Attack(g, raider, p.Hero);
            Check(p.BoardZone.Count == 7 && p.SecretZone.Count == 1, "a full board: no Bear and the Secret stays: " + p.BoardZone.Count + "/" + p.SecretZone.Count);
            // The opposite seat: an enemy Bear Trap answers my attack on their hero.
            StandardSetsProof.ParkBoard(p.Opponent); ParkHand(p.Opponent);
            var theirs = Add(p.Opponent, id);
            PlayEntity(g, p.Opponent, theirs);
            End(g);
            StandardSetsProof.ParkBoard(p);
            var mine = Summon(p, Yeti);
            Check(p.Opponent.SecretZone.Count == 1, "the enemy Secret is set");
            Attack(g, mine, p.Opponent.Hero);
            Check(p.Opponent.BoardZone.Count == 1 && p.Opponent.BoardZone[0].HasTaunt && p.Opponent.SecretZone.Count == 0, "an enemy Bear Trap answers my attack on their hero");
            Check(p.Opponent.Hero.Damage == 4, "the attack still lands: " + p.Opponent.Hero.Damage);
        });

        WildSetsProof.Case("AT_061", (g, p, id) => {
            ParkHand(p);
            var held = Add(p, id);
            Check(held.Cost == 0, "Lock and Load costs 0");
            var foe = p.Opponent.Hero; foe.Armor = 500;
            PlayEntity(g, p, held);
            Check(p.HandZone.Count == 0, "casting Lock and Load itself gives nothing");
            Play(g, p, Frostbolt, foe);
            Check(p.HandZone.Count == 1, "each spell cast this turn gives a card: " + p.HandZone.Count);
            var first = p.HandZone[0];
            Check(first.Card.Class == CardClass.HUNTER, "the card is a Hunter card: " + first.Card.Id);
            HiddenFrom(g, p, new[] { first }, "a created Hunter card");
            ParkHand(p);
            var spare = Add(p, id);
            PlayEntity(g, p, spare);
            Check(p.HandZone.Count == 1, "a second Lock and Load is itself a cast spell, the first one reacts: " + p.HandZone.Count);
            ParkHand(p);
            Play(g, p, Frostbolt, foe);
            Check(p.HandZone.Count == 2, "two Lock and Loads give two cards per spell: " + p.HandZone.Count);
            ParkHand(p);
            Play(g, p, Freezing);
            Check(p.HandZone.Count == 2, "casting a Secret is casting a spell too: " + p.HandZone.Count);
            ParkHand(p);
            Play(g, p, Thalnos);
            Check(p.HandZone.Count == 0, "casting no spell (a minion) gives nothing");
            Resume(g, next => {
                var owner = next.Player1; ParkHand(owner);
                Play(next, owner, Frostbolt, next.Player2.Hero);
                Check(owner.HandZone.Count == 2, "the effect continues on a clone");
            });
            End(g);
            Play(g, p.Opponent, Frostbolt, p.Opponent.Hero);
            Check(p.HandZone.Count == 0, "the opponent's spell on their own turn gives me nothing: " + string.Join(",", p.HandZone.Select(c => c.Card.Id)));
            // Forced on a separate game (the turn check is bypassed, so its history is not reused): the opponent plays a spell DURING my turn,
            // while my Lock and Load is active. It only counts my own spells (TriggerSource.FRIENDLY; ALL would hand me a card).
            var f = Fixture(11); var fp = f.Player1; ParkHand(fp);
            PlayEntity(f, fp, Add(fp, id));
            var theirSpell = (Spell)Add(fp.Opponent, Frostbolt);
            fp.Opponent.BaseMana = 10; fp.Opponent.UsedMana = 0; fp.Opponent.Hero.Armor = 500;
            f.CurrentPlayer = fp.Opponent;
            Check(Generic.PlayCard(fp.Opponent, theirSpell, fp.Opponent.Hero), "the opponent plays a spell during my turn");
            f.CurrentPlayer = fp;
            f.DeathProcessingAndAuraUpdate();
            Check(fp.HandZone.Count == 0, "the opponent's spell during my turn gives me nothing: " + string.Join(",", fp.HandZone.Select(c => c.Card.Id)));
            Play(f, fp, Frostbolt, fp.Opponent.Hero);
            Check(fp.HandZone.Count == 1, "my own spell in the same turn still gives a card: " + fp.HandZone.Count);
            ParkHand(p);
            End(g);
            int held2 = p.HandZone.Count;
            Play(g, p, Frostbolt, foe);
            Check(p.HandZone.Count == held2, "the effect ended with the turn: " + held2 + "/" + p.HandZone.Count);
            // Random: many distinct Hunter cards, all collectible, of more than one type.
            var ids = new HashSet<string>(); var types = new HashSet<CardType>();
            for (int i = 0; i < 32; i++)
            {
                SetDeck(p, Yeti, Yeti, Yeti); SetDeck(p.Opponent, Yeti, Yeti, Yeti);
                ParkHand(p); foe.Damage = 0;
                Play(g, p, id);
                Play(g, p, Frostbolt, foe);
                Check(p.HandZone.Count == 1, "iteration " + i + " hand: " + p.HandZone.Count);
                var c = p.HandZone.Single();
                Check(c.Card.Class == CardClass.HUNTER && c.Card.Collectible, "a collectible Hunter card: " + c.Card.Id);
                ids.Add(c.Card.Id); types.Add(c.Card.Type);
                End(g); End(g);
            }
            Check(ids.Count >= 10 && types.Count >= 2, "the card is random: " + ids.Count + " ids, " + types.Count + " types");
            // Full hand: two Lock and Loads give two cards, but only one fits.
            ParkHand(p); foe.Damage = 0;
            Play(g, p, id); Play(g, p, id);
            ParkHand(p);
            for (int i = 0; i < 9; i++) Add(p, Wisp);
            var last = Add(p, Frostbolt);
            Check(p.HandZone.Count == 10, "ten cards in hand before the cast");
            PlayEntity(g, p, last, foe);
            Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id != Wisp) == 1, "the second card burns on a full hand: " + p.HandZone.Count);
        });

        WildSetsProof.Case("AT_063", (g, p, id) => {
            var acid = (Minion)Play(g, p, id);
            Check(acid.AttackDamage == 4 && acid.Health == 2 && acid.IsRace(Race.BEAST), "Acidmaw is a 4/2 Beast");
            Check(Add(p, id).Cost == 3, "Acidmaw costs 3");
            var foe = Summon(p.Opponent, Yeti); var friend = Summon(p, Yeti); var foeWisp = Summon(p.Opponent, Wisp);
            Play(g, p, Frostbolt, foe);
            Check(foe.Zone.Type == Zone.GRAVEYARD, "an enemy minion that takes damage is destroyed");
            Play(g, p, Frostbolt, friend);
            Check(friend.Zone.Type == Zone.PLAY && friend.Damage == 3, "a friendly minion that takes damage survives");
            acid.Damage = 1;
            g.DeathProcessingAndAuraUpdate();
            Check(acid.Zone.Type == Zone.PLAY, "Acidmaw itself is not destroyed by damage");
            var shielded = Summon(p.Opponent, Yeti); shielded.HasDivineShield = true;
            Play(g, p, Frostbolt, shielded);
            Check(shielded.Zone.Type == Zone.PLAY && shielded.Damage == 0 && !shielded.HasDivineShield, "no damage taken, no destruction");
            Play(g, p, ArcaneShot, p.Opponent.Hero);
            Check(g.State != State.COMPLETE && p.Opponent.Hero.Damage == 2, "heroes are not minions");
            var healed = Summon(p.Opponent, Yeti);
            healed.Damage = 0;
            Check(healed.Zone.Type == Zone.PLAY, "an undamaged enemy minion lives");
            var dr = Summon(p.Opponent, "EX1_012");
            Play(g, p, Fireball, dr);
            Check(dr.Zone.Type == Zone.GRAVEYARD, "a Deathrattle minion dies as usual");
            // Combat: damage dealt by an attack counts too.
            Summon(p, Thalnos);
            var wall = Summon(p.Opponent, Yeti);
            var brute = Summon(p, Wisp);
            Attack(g, brute, wall);
            Check(wall.Zone.Type == Zone.GRAVEYARD, "an enemy minion damaged in combat is destroyed");
            // Acidmaw's own attack.
            var anotherFoe = Summon(p.Opponent, Stoneclaw);
            acid.Damage = 0; Attack(g, acid, anotherFoe);
            Check(anotherFoe.Zone.Type == Zone.GRAVEYARD, "an enemy minion hit by Acidmaw dies");
            Resume(g, next => {
                var foeNow = Summon(next.Player2, Yeti);
                Play(next, next.Player1, Frostbolt, foeNow);
                Check(foeNow.Zone.Type == Zone.GRAVEYARD, "Acidmaw on a clone");
            });
            // Silence ends it; the enemy's Acidmaw works against me.
            acid.Silence(); g.DeathProcessingAndAuraUpdate();
            var survivor = Summon(p.Opponent, Yeti);
            Play(g, p, Frostbolt, survivor);
            Check(survivor.Zone.Type == Zone.PLAY, "Silence ends Acidmaw's effect");
            var theirs = Summon(p.Opponent, id);
            var myBig = Summon(p, Yeti);
            Play(g, p, Frostbolt, myBig);
            Check(myBig.Zone.Type == Zone.GRAVEYARD, "an enemy Acidmaw destroys my damaged minions");
            var theirYeti = Summon(p.Opponent, Yeti);
            Play(g, p, Frostbolt, theirYeti);
            Check(theirYeti.Zone.Type == Zone.PLAY && theirYeti.Damage > 0, "and its own side is safe");
        });

        WildSetsProof.Case("AT_063t", (g, p, id) => {
            var dread = (Minion)Play(g, p, id);
            Check(dread.AttackDamage == 4 && dread.Health == 2 && dread.IsRace(Race.BEAST), "Dreadscale is a 4/2 Beast");
            Check(Add(p, id).Cost == 3, "Dreadscale costs 3");
            var friend = Summon(p, Yeti);
            var foeA = Summon(p.Opponent, Yeti); var foeB = Summon(p.Opponent, Wisp); var foeC = Summon(p.Opponent, Yeti);
            foeC.HasDivineShield = true;
            var foeStealth = Summon(p.Opponent, Yeti); foeStealth.HasStealth = true;
            End(g);
            Check(p.Opponent.Hero.Damage == 1, "the enemy hero takes 1: " + p.Opponent.Hero.Damage);
            Check(foeA.Damage == 1 && foeB.Zone.Type == Zone.GRAVEYARD && !foeC.HasDivineShield && foeC.Damage == 0 && foeStealth.Damage == 1,
                "every enemy minion takes 1 (Divine Shield absorbs, Stealth is no shelter)");
            Check(friend.Damage == 0 && dread.Damage == 0 && p.Hero.Damage == 0, "my side takes nothing");
            End(g);
            Check(foeA.Damage == 1 && p.Opponent.Hero.Damage == 1, "it does not trigger at the end of the opponent's turn");
            Resume(g, next => {
                End(next);
                Check(next.Player2.Hero.Damage == 2, "it triggers on a clone");
            });
            End(g);
            Check(p.Opponent.Hero.Damage == 2, "it triggers every one of my turns");
            // Armor takes the hit, then Silence.
            p.Opponent.Hero.Armor = 5;
            End(g); End(g);
            Check(p.Opponent.Hero.Armor == 4 && p.Opponent.Hero.Damage == 2, "Armor is damaged first");
            dread.Silence(); g.DeathProcessingAndAuraUpdate();
            End(g); End(g);
            Check(p.Opponent.Hero.Armor == 4 && foeA.Damage == 3, "Silence stops it");
            // An enemy Dreadscale hits me at the end of their turn.
            End(g);
            StandardSetsProof.ParkBoard(p.Opponent);
            var mine = Summon(p, Yeti);
            var theirs = Summon(p.Opponent, id);
            End(g);
            Check(p.Hero.Damage == 0 && mine.Damage == 0, "an enemy Dreadscale does not act on my turn end");
            End(g);
            Check(p.Hero.Damage == 1 && mine.Damage == 1 && theirs.Damage == 0 && p.Opponent.Hero.Damage == 2, "an enemy Dreadscale hits my side at the end of their turn");
            Kill(g, theirs);
            End(g); End(g);
            Check(p.Hero.Damage == 1, "dead Dreadscale does nothing");
        });

        WildSetsProof.Case("AT_065", (g, p, id) => {
            var plain = Add(p, id);
            Check(plain.Cost == 3, "King's Defender costs 3");
            PlayEntity(g, p, plain);
            var weapon = p.Hero.Weapon;
            Check(weapon.AttackDamage == 3 && weapon.Durability == 2, "without Taunt it is a 3/2: " + weapon.AttackDamage + "/" + weapon.Durability);
            var foeTaunt = Summon(p.Opponent, Footman);
            var plainMinion = Summon(p, Yeti);
            Play(g, p, id);
            Check(p.Hero.Weapon.Durability == 2, "the opponent's Taunt and my plain minion do not count");
            var guard = Summon(p, Footman);
            Play(g, p, id);
            Check(p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 3, "a friendly Taunt minion gives +1 Durability: " + p.Hero.Weapon.Durability);
            guard.Silence(); g.DeathProcessingAndAuraUpdate();
            Play(g, p, id);
            Check(p.Hero.Weapon.Durability == 2, "a Silenced Taunt minion does not count");
            var granted = Summon(p, Yeti); granted.HasTaunt = true;
            Play(g, p, id);
            Check(p.Hero.Weapon.Durability == 3, "Taunt from any source counts");
            AttackHero(g, p, foeTaunt);
            Check(p.Hero.Weapon.Durability == 2, "the extra Durability is real");
            Resume(g, next => {
                var owner = next.Player1;
                Play(next, owner, id);
                Check(owner.Hero.Weapon.Durability == 3, "Battlecry on a clone");
            });
            Kill(g, granted);
            Play(g, p, id);
            Check(p.Hero.Weapon.Durability == 2, "a dead Taunt minion does not count");
            var stealthTaunt = Summon(p, Footman); stealthTaunt.HasStealth = true;
            Play(g, p, id);
            Check(p.Hero.Weapon.Durability == 3, "a Stealthed Taunt minion counts");
            var worn = p.Hero.Weapon;
            AttackHero(g, p, p.Opponent.Hero); AttackHero(g, p, p.Opponent.Hero); AttackHero(g, p, p.Opponent.Hero);
            g.DeathProcessingAndAuraUpdate();
            Check(p.Hero.Weapon == null || p.Hero.Weapon != worn, "a 3-Durability weapon is used up by three hits");
        });
    }

    // ---- BLOCKED-STANDARD proofs (never registered) ------------------------------------------------------------------

    private static void Blocked()
    {
        Unregistered("AT_053", (g, p, id) => {
            Check(Add(p, id).Cost == 2, "Ancestral Knowledge costs 2");
            SetDeck(p, Yeti, Raptor, Wisp);
            ParkHand(p);
            var spell = Add(p, id);
            PlayEntity(g, p, spell);
            Check(p.HandZone.Count == 2 && p.DeckZone.Count == 1, "draws 2 cards: hand " + p.HandZone.Count + " deck " + p.DeckZone.Count);
            Check(p.OverloadOwed == 1 && p.Opponent.OverloadOwed == 0, "Overload (1) for the caster only");
            End(g);
            Check(p.OverloadOwed == 1 && p.OverloadLocked == 0, "the Overload waits for my next turn");
            End(g);
            Check(p.OverloadLocked == 1 && p.RemainingMana == p.BaseMana - 1, "one Mana Crystal is locked next turn: " + p.RemainingMana + "/" + p.BaseMana);
            End(g); End(g);
            Check(p.OverloadLocked == 0, "the lock lasts one turn only");
            // A deck of one card: the second draw is fatigue (1 damage).
            SetDeck(p, Yeti); ParkHand(p);
            int hp = p.Hero.Damage;
            Play(g, p, id);
            Check(p.HandZone.Count == 1 && p.Hero.Damage > hp, "an empty deck on the second draw is fatigue: " + (p.Hero.Damage - hp));
            // Full hand: drawn cards burn.
            SetDeck(p, Yeti, Yeti, Yeti); ParkHand(p);
            for (int i = 0; i < 9; i++) Add(p, Wisp);
            Play(g, p, id);
            Check(p.HandZone.Count == 10 && p.DeckZone.Count == 1, "a full hand burns the second card: hand " + p.HandZone.Count + " deck " + p.DeckZone.Count);
            Check(p.OverloadOwed == 2, "Overload stacks per cast: " + p.OverloadOwed);
            SetDeck(p); SetDeck(p.Opponent, Yeti, Yeti); ParkHand(p);
            int owed = p.OverloadOwed;
            Resume(g, next => {
                var c = next.Player1;
                Play(next, c, id);
                Check(c.OverloadOwed == owed + 1, "Overload on a clone");
            });
        });
        Unregistered("AT_051", (g, p, id) => {
            var spell = Add(p, id);
            var seen = new HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
                var a = Summon(p, Yeti); var b = Summon(p.Opponent, Yeti); var c = Summon(p.Opponent, Yeti); a.BaseHealth = 20; b.BaseHealth = 20; c.BaseHealth = 20;
                p.OverloadOwed = 0;
                Play(g, p, id);
                foreach (var m in new[] { a, b, c }) { Check(m.Damage == 4 || m.Damage == 5, "4-5 damage: " + m.Damage); seen.Add(m.Damage); }
                Check(p.OverloadOwed == 2, "Overload (2): " + p.OverloadOwed);
            }
            Check(seen.Count == 2, "both 4 and 5 occur");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Summon(p, Thalnos);
            var big = Summon(p.Opponent, Yeti); big.BaseHealth = 20;
            Play(g, p, id);
            Check(big.Damage == 5 || big.Damage == 6, "Spell Damage adds to it: " + big.Damage);
            Check(p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 0, "heroes are untouched");
        });
        Unregistered("AT_052", (g, p, id) => {
            var golem = (Minion)Play(g, p, id);
            Check(golem.AttackDamage == 3 && golem.Health == 4 && golem.IsRace(Race.TOTEM), "Totem Golem is a 3/4 Totem");
            Check(p.OverloadOwed == 1, "Overload (1): " + p.OverloadOwed);
            End(g); End(g);
            Check(p.OverloadLocked == 1, "one crystal locked next turn");
        });
        Unregistered("AT_055", (g, p, id) => {
            p.Hero.Damage = 12;
            Play(g, p, id, p.Hero);
            Check(p.Hero.Damage == 7, "Flash Heal restores 5: " + p.Hero.Damage);
            Play(g, p, id, p.Hero);
            Check(p.Hero.Damage == 2, "restores 5 again");
            Play(g, p, id, p.Hero);
            Check(p.Hero.Damage == 0, "no overheal");
            var m = Summon(p, Yeti); m.Damage = 3;
            Play(g, p, id, m);
            Check(m.Damage == 0, "restores minions");
            var foe = Summon(p.Opponent, Yeti); foe.Damage = 4;
            Play(g, p, id, foe);
            Check(foe.Damage == 0, "any character can be targeted");
        });
        Unregistered("AT_062", (g, p, id) => {
            var spell = Add(p, id);
            PlayEntity(g, p, spell);
            Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Card.Id == "FP1_011" && m.AttackDamage == 1 && m.Health == 1), "three 1/1 Webspinners");
            ParkHand(p);
            var beasts = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var spinner = p.BoardZone.First();
                Kill(g, spinner);
                Check(p.HandZone.Count == i + 1 && p.HandZone.All(c => c.Card.Races.Contains(Race.BEAST)), "each Webspinner death gets a random Beast");
            }
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Play(g, p, id);
            Check(p.BoardZone.Count == 7, "one free slot summons one Webspinner");
            var full = Add(p, id);
            Check(!p.Options().Any(t => t.Source?.Id == full.Id), "a full board cannot play it");
        });
        Unregistered("AT_064", (g, p, id) => {
            var foe = Summon(p.Opponent, Yeti);
            Play(g, p, id, foe);
            Check(foe.Damage == 3 && p.Hero.Armor == 3, "Bash deals 3 and gains 3 Armor");
            var wisp = Summon(p.Opponent, Wisp);
            Play(g, p, id, wisp);
            Check(wisp.Zone.Type == Zone.GRAVEYARD && p.Hero.Armor == 6, "Armor is gained even if the target dies");
            Summon(p, Thalnos);
            Play(g, p, id, p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 4 && p.Hero.Armor == 9, "Spell Damage applies to the damage only: " + p.Opponent.Hero.Damage + "/" + p.Hero.Armor);
            var shield = Summon(p.Opponent, Yeti); shield.HasDivineShield = true;
            Play(g, p, id, shield);
            Check(!shield.HasDivineShield && p.Hero.Armor == 12, "Divine Shield absorbs, Armor still comes");
        });
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------

    private static Game Fixture(int seed)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId(Yeti), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId(Yeti), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
            owner.BaseMana = 10;
        }
        return game;
    }

    /// <summary>Runs a scenario for a card the suite must not register or change; prints PASS or FAIL, never throws. Also prints
    /// any identity drift the base card still has against the 37.0 export.</summary>
    private static void Unregistered(string id, Action<Game, Controller, string> scenario, string reason = "BLOCKED-STANDARD")
    {
        try
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = Fixture(seed);
                scenario(g, g.Player1, id);
                Check(Canonical.State(g) == Canonical.State(g.CloneExact(history: true)), id + " exact clone");
                Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)), id + " snapshot");
            }
            Console.WriteLine("PASS (unregistered, " + reason + ") " + id);
        }
        catch (Exception error) { Console.WriteLine("FAIL (unregistered, " + reason + ") " + id + ": " + error.Message); }
        var data = ClientExport.TryCards();
        if (data != null && data.TryGetValue(id, out var row))
        {
            var card = Cards.FromId(id);
            var drift = new List<string>();
            if (card.Cost != row.GetProperty("cost").GetInt32()) drift.Add("cost " + card.Cost + ">" + row.GetProperty("cost").GetInt32());
            if (card.ATK != row.GetProperty("atk").GetInt32() || card.Health != row.GetProperty("health").GetInt32()) drift.Add("stats");
            if (card.Text != row.GetProperty("text").GetString()) drift.Add("text");
            Console.WriteLine("identity drift (unregistered) " + id + ": " + (drift.Count == 0 ? "none" : string.Join(",", drift)));
        }
    }

    private static void SetDeck(Controller c, params string[] ids)
    {
        foreach (var card in c.DeckZone.ToArray()) { c.DeckZone.Remove(card); c.SetasideZone.Add(card); }
        foreach (var cardId in ids) Entity.FromCard(c, Cards.FromId(cardId), zone: c.DeckZone);
    }

    private static void Use(Game g, Controller c, ICharacter target = null)
    {
        c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
        Check(g.Process(HeroPowerTask.Any(c, target)), "hero power accepted for seat " + c.PlayerId + " (current " + c.Game.CurrentPlayer.PlayerId + ")");
    }

    private static bool CanUse(Controller c) => c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static void SetPower(Controller c, string id)
    {
        var power = (HeroPower)Entity.FromCard(c, Cards.FromId(id));
        c.SetasideZone.Add(c.Hero.HeroPower);
        c.Hero.HeroPower = power;
        BasicHeroPowerRules.EnrollPowerAuras(c, power);
        c.Game.AuraUpdate();
    }

    private static void AttackHero(Game g, Controller c, ICharacter target)
    {
        c.Hero.NumAttacksThisTurn = 0; c.Hero.IsExhausted = false;
        var task = c.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == target.Id);
        Check(task != null && g.Process(task), "hero attack accepted");
    }

    private static void Kill(Game g, Minion minion)
    {
        Core2026Proof.Kill(g, minion);
        g.DeathProcessingAndAuraUpdate();
    }

    private static void ParkHand(Controller p) { foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); } }

    /// <summary>No card of <paramref name="hidden"/> identifies itself in the fair view of the opponent of <paramref name="owner"/>.</summary>
    private static void HiddenFrom(Game g, Controller owner, IEnumerable<IPlayable> hidden, string why) => HiddenSeat(g, owner.Opponent.PlayerId, hidden, why);

    /// <summary>No card of <paramref name="hidden"/> sits in a hidden zone (hand, deck, set aside, secret) of either player in the fair view of seat <paramref name="viewer"/>.</summary>
    private static void HiddenSeat(Game g, int viewer, IEnumerable<IPlayable> hidden, string why)
    {
        var ids = hidden.Select(e => e.Card.Id).ToHashSet();
        var before = Canonical.State(g); string history = g.HistoryFacts.CanonicalText(); var random = g.Random.CaptureState();
        var fair = FairBotState.Create(g, viewer, 83, DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        Check(!view.IdEntityDic.Values.Any(e => (e.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE or Zone.SECRET) && ids.Contains(e.Card.Id)),
            why + ": identity visible in the fair view");
        Check(before == Canonical.State(g) && history == g.HistoryFacts.CanonicalText() && random.Equals(g.Random.CaptureState()),
            why + ": the projection leaves the game untouched");
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        Game[] copies;
        try { copies = new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) }; }
        catch (Exception error) { throw new Exception("clone/snapshot inside the scenario: " + error.Message, error); }
        foreach (var next in copies)
        {
            Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }
}
