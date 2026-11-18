using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m3e: Grand Tournament AT_088..AT_110 (Neutral minions and the Paladin Tuskarr Jouster).
/// Registered (literal WildSetsProof.Case, listed in wild-y0b-m3e.txt): every id of the lane except AT_101.
/// BLOCKED-STANDARD, written but never registered: AT_101 Pit Fighter (data/pools/pool-eligibility-v1.json). Its script is
/// untouched; run it with WILD_Y0B_M3E_BLOCKED=1.
/// Amount trap: Frigid Snobold's SPELLPOWER tag is stored as 1 by the client and is 1 on the card (Spell Damage +1).
/// Inspire is proven as a facility in WildY0bM3AProof.InspireMechanic; the Inspire cards here prove their own effect and the
/// boundaries every Inspire card shares (opponent's Hero Power, once per use, Silence). Joust is proven in WildY0bM3CProof (AT_048);
/// Tuskarr Jouster and Armored Warhorse prove their own numbers and outcomes. FORGETFUL (Mogor's Champion) uses SpecificTask.Forgetful.
/// </summary>
internal static class WildY0bM3EProof
{
    private static bool BlockedRun => Environment.GetEnvironmentVariable("WILD_Y0B_M3E_BLOCKED") == "1";
    private const string Yeti = "CS2_182", Wisp = "CS2_231", Thalnos = "EX1_012", Frostbolt = "CS2_024", Fireball = "CS2_029",
        Taunter = "CS2_179", Sneak = "EX1_010", Annoy = "GVG_085", Voidwalker = "CS2_065", FlameImp = "EX1_319",
        Fireblast = "CS2_034", Dagger = "CS2_083b", SteadyShot = "DS1h_292", Reinforce = "CS2_101", Recruit = "CS2_101t";

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("wild-y0b-m3e-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        if (BlockedRun) Blocked();

        WildSetsProof.Case("AT_088", (g, p, id) => {
            var opp = p.Opponent;
            var champ = (Minion)Play(g, p, id);
            Check(champ.Card.Cost == 6 && champ.AttackDamage == 8 && champ.Health == 5, "Mogor's Champion is an 8/5 for 6");
            Check(champ.IsExhausted && !champ.HasCharge && !champ.HasTaunt, "no Charge or Taunt");
            End(g); End(g);
            for (int i = 0; i < 20; i++) { opp.Hero.Damage = 0; champ.Damage = 0; Strike(g, champ, opp.Hero); Check(opp.Hero.Damage == 8, "the hero is the only enemy: the attack stays"); }
            var a = Dummy(opp); var b = Dummy(opp); var c = Dummy(opp);
            var (intended, others) = Trials(g, 100, a, new ICharacter[] { b, c, opp.Hero }, () => Strike(g, champ, a));
            Check(intended >= 30 && intended <= 70, "about half the attacks reach the intended target: " + intended);
            Check(others.All(x => x >= 3), "every other enemy, hero included, is a candidate: " + string.Join("/", others));
            var (hi, ho) = Trials(g, 100, opp.Hero, new ICharacter[] { a, b, c }, () => Strike(g, champ, opp.Hero));
            Check(hi >= 30 && hi <= 70 && ho.All(x => x >= 3), "hero as the intended target: " + hi + "/" + string.Join("/", ho));
            Park(opp);
            var taunt = Dummy(opp, Taunter); var plain = Dummy(opp); var sneaky = Dummy(opp, Sneak); sneaky[GameTag.ATK] = 0;
            Check(taunt.HasTaunt && sneaky.HasStealth, "fixture");
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == champ.Id && t.Target?.Id == plain.Id), "Taunt still restricts the player's choice");
            var (ti, to) = Trials(g, 100, taunt, new ICharacter[] { plain, sneaky, opp.Hero }, () => Strike(g, champ, taunt));
            Check(ti >= 30 && ti <= 70 && to.All(x => x >= 3), "Taunt and Stealth do not limit the wrong target: " + ti + "/" + string.Join("/", to));
            int died = 0, popped = 0;
            for (int i = 0; i < 60; i++)
            {
                Park(opp); champ.Damage = 0; opp.Hero.Damage = 0;
                var dd = Dummy(opp); var weak = Summon(opp, Wisp); var shield = Dummy(opp); shield.HasDivineShield = true;
                Strike(g, champ, dd);
                if (weak.Zone.Type == Zone.GRAVEYARD) died++;
                if (!shield.HasDivineShield) popped++;
                NoCombatFlags(g, "after wrong target " + i);
            }
            Check(died >= 3 && popped >= 3, "a wrong target can die or lose its Divine Shield: " + died + "/" + popped);
            Park(opp); opp.Hero.Damage = 0;
            var x1 = Dummy(opp); var x2 = Dummy(opp); var x3 = Dummy(opp);
            var spell = Add(p, Fireball);
            PlayEntity(g, p, spell, x1);
            Check(x1.Damage == 6 && x2.Damage == 0 && x3.Damage == 0 && opp.Hero.Damage == 0, "a spell keeps its target: " + x1.Damage + "/" + x2.Damage + "/" + x3.Damage + "/" + opp.Hero.Damage);
            x1.Damage = 0;
            Resume(g, next => {
                var nc = (Minion)next.IdEntityDic[champ.Id];
                var na = (ICharacter)next.IdEntityDic[x1.Id];
                var nb = (ICharacter)next.IdEntityDic[x2.Id]; var nd = (ICharacter)next.IdEntityDic[x3.Id];
                var (ci, co) = Trials(next, 100, na, new ICharacter[] { nb, nd, next.Player2.Hero }, () => Strike(next, nc, na));
                Check(ci >= 30 && ci <= 70 && co.All(x => x >= 3), "Forgetful continues on a clone and snapshot: " + ci + "/" + string.Join("/", co));
            });
            // the opponent's Champion misdirects against my side
            End(g);
            Park(p); Park(opp);
            var m1 = Dummy(p); var m2 = Dummy(p);
            var theirs = Summon(opp, id); theirs.IsExhausted = false;
            var (oi, oo) = Trials(g, 100, m1, new ICharacter[] { m2, p.Hero }, () => Strike(g, theirs, m1));
            Check(oi >= 30 && oi <= 70 && oo.All(x => x >= 10), "the opponent's Champion misdirects against my side: " + oi + "/" + string.Join("/", oo));
            End(g);
            // Silence removes Forgetful; death leaves nothing behind
            Park(p); var champ2 = Summon(p, id);
            champ2.Silence(); g.DeathProcessingAndAuraUpdate();
            Park(opp);
            var s1 = Dummy(opp); var s2 = Dummy(opp);
            for (int i = 0; i < 40; i++)
            {
                s1.Damage = 0; s2.Damage = 0; opp.Hero.Damage = 0;
                Strike(g, champ2, s1);
                Check(s1.Damage == 8 && s2.Damage == 0 && opp.Hero.Damage == 0, "a Silenced Champion always hits its target");
            }
            Kill(g, champ2);
            Check(champ2.Zone.Type == Zone.GRAVEYARD, "dead Champion");
            var fresh = (Minion)Play(g, p, id);
            Check(fresh.AttackDamage == 8 && fresh.Health == 5, "a new Champion is forgetful again");
        });

        WildSetsProof.Held("AT_089", (g, p, id) => {
            var opp = p.Opponent;
            var lt = (Minion)Play(g, p, id);
            Check(lt.Card.Cost == 2 && lt.AttackDamage == 3 && lt.Health == 2 && lt.IsRace(Race.UNDEAD), "Boneguard Lieutenant is a 3/2 Undead for 2");
            Use(g, p);
            Check(lt.AttackDamage == 3 && lt.Health == 3, "Inspire: +1 Health only: " + lt.AttackDamage + "/" + lt.Health);
            Check(!CanUse(p), "one Inspire per Hero Power use");
            var late = (Minion)Play(g, p, id);
            Check(late.Health == 2, "a Lieutenant played afterwards does not trigger retroactively");
            End(g);
            var theirs = (Minion)Play(g, opp, id);
            Use(g, opp, p.Hero);
            Check(theirs.Health == 3 && lt.Health == 3 && late.Health == 2, "each side's Inspire follows its own Hero Power only");
            End(g);
            Use(g, p);
            Check(lt.Health == 4 && late.Health == 3, "Inspire stacks on later uses: " + lt.Health + "/" + late.Health);
            lt.Damage = 3;
            Check(lt.Health == 1, "damage applies on top of the gain");
            Resume(g, next => {
                End(next); End(next);
                var n = (Minion)next.IdEntityDic[late.Id]; Use(next, next.Player1);
                Check(n.Health == 4, "Inspire continues on a clone and snapshot");
            });
            lt.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(lt.Health == 1 && lt.Damage == 1 && lt.AttackDamage == 3 && lt.Card.Health == 2, "Silence removes the gain; the current Health is capped by the new maximum of 2: " + lt.Health);
            Heal(g, lt, 5);
            Check(lt.Health == 2, "its maximum Health is back to 2: " + lt.Health);
            End(g); End(g);
            Use(g, p);
            Check(lt.Health == 2 && late.Health == 4, "a Silenced Lieutenant no longer Inspires, the other one does");
            Kill(g, late);
            Check(late.Zone.Type == Zone.GRAVEYARD, "dead Lieutenant");
        });

        WildSetsProof.Case("AT_090", (g, p, id) => {
            var opp = p.Opponent;
            var friend = Summon(p, Yeti); var foe = Summon(opp, Yeti);
            var mukla = (Minion)Play(g, p, id);
            Check(mukla.Card.Cost == 5 && mukla.AttackDamage == 5 && mukla.Health == 3 && mukla.IsRace(Race.BEAST), "Mukla's Champion is a 5/3 Beast for 5");
            Check(friend.AttackDamage == 4 && friend.Health == 5, "no Battlecry effect");
            Use(g, p);
            Check(friend.AttackDamage == 5 && friend.Health == 6, "Inspire: other friendly minions get +1/+1: " + friend.AttackDamage + "/" + friend.Health);
            Check(mukla.AttackDamage == 5 && mukla.Health == 3, "not the Champion itself");
            Check(foe.AttackDamage == 4 && foe.Health == 5, "not enemy minions");
            Check(!CanUse(p), "one Inspire per use");
            var late = Summon(p, Wisp);
            Check(late.AttackDamage == 1 && late.Health == 1, "a minion summoned afterwards has no buff");
            End(g);
            Use(g, opp, p.Hero);
            Check(friend.AttackDamage == 5 && late.AttackDamage == 1, "the opponent's Hero Power does not Inspire");
            End(g);
            Use(g, p);
            Check(friend.AttackDamage == 6 && friend.Health == 7 && late.AttackDamage == 2 && late.Health == 2, "Inspire stacks and reaches later minions: " + friend.AttackDamage + "/" + friend.Health);
            var second = (Minion)Play(g, p, id);
            Check(mukla.AttackDamage == 5, "an unbuffed second Champion does not trigger by itself");
            Resume(g, next => {
                End(next); End(next);
                var n = (Minion)next.IdEntityDic[friend.Id]; Use(next, next.Player1);
                Check(n.AttackDamage == 8 && n.Health == 9, "two Champions both Inspire on a clone: " + n.AttackDamage);
            });
            End(g); End(g);
            Use(g, p);
            Check(friend.AttackDamage == 8 && mukla.AttackDamage == 6 && second.AttackDamage == 6, "each Champion buffs the other: " + friend.AttackDamage + "/" + mukla.AttackDamage + "/" + second.AttackDamage);
            friend.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(friend.AttackDamage == 4 && friend.Health == 5, "Silence removes the buffs from a minion");
            mukla.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(mukla.AttackDamage == 5 && mukla.Health == 3, "Silence removes what the other Champion gave it");
            End(g); End(g);
            Use(g, p);
            Check(friend.AttackDamage == 5 && mukla.AttackDamage == 6 && second.AttackDamage == 6, "a Silenced Champion no longer Inspires (the other one still does): " + friend.AttackDamage + "/" + mukla.AttackDamage + "/" + second.AttackDamage);
            Kill(g, second);
            Check(second.Zone.Type == Zone.GRAVEYARD, "dead Champion");
        });

        WildSetsProof.Case("AT_091", (g, p, id) => {
            var opp = p.Opponent;
            var medic = (Minion)Play(g, p, id);
            Check(medic.Card.Cost == 4 && medic.AttackDamage == 1 && medic.Health == 8, "Tournament Medic is a 1/8 for 4");
            p.Hero.Damage = 10; opp.Hero.Damage = 10;
            Use(g, p);
            Check(p.Hero.Damage == 8 && opp.Hero.Damage == 10, "Inspire restores 2 to my hero only: " + p.Hero.Damage + "/" + opp.Hero.Damage);
            Check(!CanUse(p), "one Inspire per use");
            End(g);
            Use(g, opp, opp.Hero);
            Check(p.Hero.Damage == 8, "the opponent's Hero Power does not Inspire");
            End(g);
            p.Hero.Damage = 1;
            Use(g, p);
            Check(p.Hero.Damage == 0, "no healing beyond full Health");
            var damaged = Summon(p, Yeti); damaged.Damage = 3;
            End(g); End(g);
            p.Hero.Damage = 5;
            var second = (Minion)Play(g, p, id);
            Use(g, p);
            Check(p.Hero.Damage == 1 && damaged.Damage == 3, "two Medics restore 4, minions are not healed: " + p.Hero.Damage);
            Resume(g, next => {
                End(next); End(next);
                next.Player1.Hero.Damage = 9; Use(next, next.Player1);
                Check(next.Player1.Hero.Damage == 5, "Inspire continues on a clone and snapshot");
            });
            medic.Silence(); g.DeathProcessingAndAuraUpdate();
            End(g); End(g);
            p.Hero.Damage = 6; Use(g, p);
            Check(p.Hero.Damage == 4, "Silence removes one Medic, the other still heals");
            Kill(g, second);
            End(g); End(g);
            p.Hero.Damage = 6; Use(g, p);
            Check(p.Hero.Damage == 6, "a dead Medic heals nothing");
        });

        WildSetsProof.Case("AT_092", (g, p, id) => {
            var held = (Minion)Add(p, id);
            Check(held.Card.Cost == 3 && held.AttackDamage == 5 && held.Health == 2 && held.IsRace(Race.ELEMENTAL), "Ice Rager is a 5/2 Elemental for 3");
            int hand = p.HandZone.Count, foeHand = p.Opponent.HandZone.Count;
            PlayEntity(g, p, held);
            Check(p.HandZone.Count == hand - 1 && p.Opponent.HandZone.Count == foeHand && p.BoardZone.Count == 1, "no effect besides the body");
            Check(!held.HasTaunt && !held.HasCharge && !held.HasStealth && !held.HasDivineShield && !held.HasWindfury && !held.HasDeathrattle && held.IsExhausted, "vanilla");
            Check(p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 0, "nothing was damaged");
            held.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(held.AttackDamage == 5 && held.Health == 2, "Silence changes nothing");
            var foe = Summon(p.Opponent, Yeti);
            End(g); End(g);
            Attack(g, held, foe);
            Check(foe.Damage == 5 && held.Damage == 4 && held.Zone.Type == Zone.GRAVEYARD, "5 Attack kills a Yeti, 2 Health does not survive");
            Resume(g, next => { var m = (Minion)Play(next, next.Player1, id); Check(m.AttackDamage == 5 && m.Health == 2, "plays the same on a clone"); });
        });

        WildSetsProof.Case("AT_093", (g, p, id) => {
            var opp = p.Opponent;
            var target = Summon(opp, Yeti); target[GameTag.HEALTH] = 60;
            Play(g, p, Frostbolt, target);
            Check(target.Damage == 3, "Frostbolt deals 3 without Spell Damage");
            target.Damage = 0;
            var snobold = (Minion)Play(g, p, id);
            Check(snobold.Card.Cost == 4 && snobold.AttackDamage == 2 && snobold.Health == 6, "Frigid Snobold is a 2/6 for 4");
            Check(p.CurrentSpellPower == 1 && opp.CurrentSpellPower == 0, "Spell Damage +1 for its owner only: " + p.CurrentSpellPower);
            Play(g, p, Frostbolt, target);
            Check(target.Damage == 4, "Frostbolt deals 4 with Snobold: " + target.Damage);
            Play(g, p, Fireball, opp.Hero);
            Check(opp.Hero.Damage == 7, "Fireball deals 7");
            Use(g, p);
            Check(p.Hero.Weapon != null && p.Hero.Weapon.AttackDamage == 1, "Hero Power is untouched: Dagger Mastery equips a 1/2");
            var two = (Minion)Play(g, p, id);
            Check(p.CurrentSpellPower == 2, "two Snobolds stack");
            target.Damage = 0;
            Play(g, p, Frostbolt, target);
            Check(target.Damage == 5, "Frostbolt deals 5 with two Snobolds");
            End(g);
            Check(p.CurrentSpellPower == 2 && opp.CurrentSpellPower == 0, "the bonus stays with the owner on the opponent's turn");
            var mine = Summon(p, Yeti); mine[GameTag.HEALTH] = 60;
            Play(g, opp, Frostbolt, mine);
            Check(mine.Damage == 3, "the opponent's spell gets no bonus from my Snobolds: " + mine.Damage);
            Use(g, opp, mine);
            Check(mine.Damage == 4, "Fireblast is not a spell: 1 damage");
            var theirs = Summon(opp, id);
            Check(opp.CurrentSpellPower == 1, "their own Snobold");
            mine.Damage = 0; opp.BaseMana = 10; opp.UsedMana = 0;
            Play(g, opp, Frostbolt, mine);
            Check(mine.Damage == 4, "their Frostbolt deals 4 now");
            Resume(g, next => {
                Check(next.Player1.CurrentSpellPower == 2 && next.Player2.CurrentSpellPower == 1, "Spell Damage holds on a clone and snapshot");
            });
            snobold.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(p.CurrentSpellPower == 1, "Silence removes Spell Damage");
            Kill(g, two);
            Check(p.CurrentSpellPower == 0, "death removes Spell Damage");
            Kill(g, theirs);
            Check(opp.CurrentSpellPower == 0, "their Snobold dies as well");
        });

        WildSetsProof.Case("AT_094", (g, p, id) => {
            var opp = p.Opponent;
            var juggler = (Minion)Play(g, p, id);
            Check(juggler.Card.Cost == 2 && juggler.AttackDamage == 2 && juggler.Health == 3 && juggler.IsRace(Race.DRAENEI), "Flame Juggler is a 2/3 Draenei for 2");
            Check(opp.Hero.Damage == 1 && p.Hero.Damage == 0 && juggler.Damage == 0, "the only enemy is the hero: 1 damage");
            Park(opp); opp.Hero.Damage = 0;
            var a = Dummy(opp); var b = Dummy(opp); var sneaky = Dummy(opp, Sneak); sneaky[GameTag.HEALTH] = 20;
            var mine = Summon(p, Yeti);
            var hits = new int[4];
            for (int i = 0; i < 120; i++)
            {
                a.Damage = 0; b.Damage = 0; sneaky.Damage = 0; opp.Hero.Damage = 0; mine.Damage = 0; p.Hero.Damage = 0;
                var j = (Minion)Play(g, p, id);
                var sum = a.Damage + b.Damage + sneaky.Damage + opp.Hero.Damage;
                Check(sum == 1 && mine.Damage == 0 && p.Hero.Damage == 0 && j.Damage == 0, "exactly 1 damage, to an enemy only: " + sum);
                if (a.Damage > 0) hits[0]++; if (b.Damage > 0) hits[1]++; if (sneaky.Damage > 0) hits[2]++; if (opp.Hero.Damage > 0) hits[3]++;
                Park(p); mine = Summon(p, Yeti);
            }
            Check(hits.All(x => x >= 10), "every enemy character is a candidate, a Stealth minion and the hero included: " + string.Join("/", hits));
            // lethal and Divine Shield
            Park(opp); opp.Hero.Damage = 0; var weak = Summon(opp, Wisp);
            Play(g, p, id);
            Check(weak.Zone.Type == Zone.GRAVEYARD || opp.Hero.Damage == 1, "a 1-Health enemy can die to it, or the hero is hit");
            var popped = 0; int dead = 0; Minion shield;
            for (int i = 0; i < 40; i++)
            {
                Park(opp); Park(p); shield = Summon(opp, Wisp); shield.HasDivineShield = true; opp.Hero.Damage = 0;
                Play(g, p, id);
                if (!shield.HasDivineShield && shield.Zone.Type == Zone.PLAY) popped++;
                if (shield.Zone.Type != Zone.PLAY) dead++;
                Check(shield.Zone.Type == Zone.PLAY, "a shielded Wisp never dies to 1 damage");
            }
            Check(popped >= 5 && dead == 0, "Divine Shield absorbs the damage: " + popped);
            Park(opp); opp.Hero.Damage = 0;
            Summon(p, Thalnos);
            Play(g, p, id);
            Check(opp.Hero.Damage == 1, "Spell Damage does not change a Battlecry: " + opp.Hero.Damage);
            Park(p); Park(opp);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Play(g, p, id);
            Check(p.BoardZone.Count == 7 && opp.Hero.Damage == 2, "a full board still deals the damage");
            Resume(g, next => {
                var o = next.Player2; o.Hero.Damage = 0; Park(o); Park(next.Player1);
                Play(next, next.Player1, id);
                Check(o.Hero.Damage == 1, "Battlecry on a clone");
            });
            // the opponent's Juggler hits my side
            End(g); Park(p);
            var mm = Dummy(p); var m2 = Dummy(p); int mineHits = 0, heroHits = 0;
            for (int i = 0; i < 40; i++)
            {
                mm.Damage = 0; m2.Damage = 0; p.Hero.Damage = 0; Park(opp);
                var f = Summon(opp, Yeti); Play(g, opp, id);
                Check(mm.Damage + m2.Damage + p.Hero.Damage == 1 && opp.Hero.Damage == 2, "the opponent's Juggler damages my side only");
                if (p.Hero.Damage > 0) heroHits++; else mineHits++;
            }
            Check(heroHits >= 3 && mineHits >= 10, "both my hero and my minions are candidates: " + heroHits + "/" + mineHits);
        });

        WildSetsProof.Case("AT_095", (g, p, id) => {
            var opp = p.Opponent;
            var knight = (Minion)Play(g, p, id);
            Check(knight.Card.Cost == 3 && knight.AttackDamage == 2 && knight.Health == 2, "Silent Knight is a 2/2 for 3");
            Check(knight.HasStealth && knight.HasDivineShield && !knight.HasTaunt, "Stealth and Divine Shield");
            End(g);
            var attacker = Summon(opp, Yeti); attacker.IsExhausted = false;
            Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == knight.Id), "an enemy cannot attack a Stealthed minion");
            var held = Add(opp, Frostbolt);
            Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id && t.Target?.Id == knight.Id), "an enemy spell cannot target it");
            Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == knight.Id), "nor the enemy Hero Power");
            Use(g, opp, p.Hero);
            Check(knight.Damage == 0 && p.Hero.Damage == 1, "Fireblast goes to the hero instead");
            End(g);
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[knight.Id];
                Check(n.HasStealth && n.HasDivineShield, "both keywords hold on a clone and snapshot");
            });
            var foe = Summon(opp, Yeti); foe[GameTag.ATK] = 3; foe[GameTag.HEALTH] = 10;
            End(g); End(g);
            Attack(g, knight, foe);
            Check(!knight.HasStealth && !knight.HasDivineShield && knight.Damage == 0 && foe.Damage == 2, "attacking breaks Stealth, the Shield absorbs the counter-attack");
            End(g);
            Check(opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == knight.Id) || foe.IsExhausted, "visible once it attacked");
            End(g);
            var second = (Minion)Play(g, p, id);
            second.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(!second.HasStealth && !second.HasDivineShield, "Silence removes Stealth and Divine Shield");
            var third = (Minion)Play(g, p, id);
            Kill(g, third);
            Check(third.Zone.Type == Zone.GRAVEYARD, "dead Knight");
        });

        WildSetsProof.Case("AT_096", (g, p, id) => {
            var opp = p.Opponent;
            Check(Add(p, id).Cost == 5, "Clockwork Knight costs 5");
            ParkHand(p);
            var held = Add(p, id);
            var yeti = Summon(p, Yeti); var enemyMech = Summon(opp, Annoy);
            var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(options.Length > 0 && options.All(t => t.Target == null), "no friendly Mech: playable without a target");
            var knight = (Minion)held; PlayEntity(g, p, held);
            Check(knight.AttackDamage == 5 && knight.Health == 5 && knight.IsRace(Race.MECHANICAL) && yeti.AttackDamage == 4 && enemyMech.AttackDamage == 1, "a 5/5 Mech, nothing buffed");
            var mech = Summon(p, Annoy);
            var second = Add(p, id);
            options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == second.Id).ToArray();
            var targets = options.Select(t => t.Target?.Id).ToHashSet();
            Check(targets.SetEquals(new int?[] { mech.Id, knight.Id }), "friendly Mechs only (the first Knight is one too), a target is required: " + options.Length);
            PlayEntity(g, p, second, mech);
            Check(mech.AttackDamage == 2 && mech.Health == 3 && knight.AttackDamage == 5 && enemyMech.AttackDamage == 1, "+1/+1 on the chosen Mech: " + mech.AttackDamage + "/" + mech.Health);
            var secondKnight = (Minion)second;
            Check(secondKnight.AttackDamage == 5, "not on itself");
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[mech.Id];
                Check(n.AttackDamage == 2, "the buff holds on a clone");
                var again = Add(next.Player1, id);
                PlayEntity(next, next.Player1, again, n);
                Check(n.AttackDamage == 3 && n.Health == 4, "and a second Knight stacks on a clone");
            });
            mech.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(mech.AttackDamage == 1 && mech.Health == 2 && !mech.HasTaunt, "Silence removes the buff");
            Kill(g, mech);
            var third = Add(p, id);
            options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == third.Id).ToArray();
            Check(options.Length > 0 && options.All(t => t.Target != null && (t.Target.Id == knight.Id || t.Target.Id == secondKnight.Id)), "a dead Mech is no target; Knights are Mechs");
            // a Mech that cannot be targeted
            Park(p); var stealthMech = Summon(p, Annoy);
            var onlyEnemy = Summon(opp, Annoy);
            var lone = Add(p, id);
            options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == lone.Id).ToArray();
            Check(options.Length > 0 && options.All(t => t.Target?.Id == stealthMech.Id), "only the friendly Mech is offered");
            Check(!options.Any(t => t.Target?.Id == onlyEnemy.Id), "never an enemy Mech");
        });

        WildSetsProof.Case("AT_097", (g, p, id) => {
            var opp = p.Opponent;
            var attendee = (Minion)Play(g, p, id);
            Check(attendee.Card.Cost == 1 && attendee.AttackDamage == 2 && attendee.Health == 1 && attendee.HasTaunt, "Tournament Attendee is a 2/1 Taunt for 1");
            var other = Summon(p, Yeti);
            End(g);
            var foe = Summon(opp, Yeti); foe.IsExhausted = false;
            var attacks = opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == foe.Id).Select(t => t.Target.Id).ToHashSet();
            Check(attacks.SetEquals(new[] { attendee.Id }), "Taunt: the only legal attack target");
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[attendee.Id];
                Check(n.HasTaunt, "Taunt holds on a clone and snapshot");
            });
            attendee.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(!attendee.HasTaunt, "Silence removes Taunt");
            attacks = opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == foe.Id).Select(t => t.Target.Id).ToHashSet();
            Check(attacks.Contains(other.Id) && attacks.Contains(p.Hero.Id), "free choice without Taunt");
            End(g);
            var second = (Minion)Play(g, p, id);
            Attack(g, second, foe);
            Check(second.Zone.Type == Zone.GRAVEYARD && foe.Damage == 2, "a 2/1 trades down");
        });

        WildSetsProof.Case("AT_098", (g, p, id) => {
            var opp = p.Opponent;
            Check(p.Hero.HeroPower.Card.Id == Dagger && opp.Hero.HeroPower.Card.Id == Fireblast, "fixture: Dagger Mastery against Fireblast");
            var spelleater = (Minion)Play(g, p, id);
            Check(spelleater.Card.Cost == 6 && spelleater.AttackDamage == 6 && spelleater.Health == 5, "Sideshow Spelleater is a 6/5 for 6");
            var copy = p.Hero.HeroPower;
            Check(copy.Card.Id == Fireblast && copy != opp.Hero.HeroPower && copy.Controller == p, "my Hero Power is now a copy of Fireblast");
            Check(opp.Hero.HeroPower.Card.Id == Fireblast && opp.Hero.HeroPower.Controller == opp, "the opponent keeps theirs");
            Check(copy.Cost == 2, "the copy keeps its cost");
            Use(g, p, opp.Hero);
            Check(opp.Hero.Damage == 1 && !CanUse(p), "the copy works, once");
            Check(!opp.Hero.HeroPower.IsExhausted, "using the copy does not exhaust the original");
            Check(p.Hero.Weapon == null, "no Dagger was equipped");
            End(g);
            // the other direction: the opponent copies Dagger Mastery... mine is Fireblast now, so they copy Fireblast
            var theirs = (Minion)Play(g, opp, id);
            Check(opp.Hero.HeroPower.Card.Id == Fireblast && opp.Hero.HeroPower != copy, "a copy of the same power: a new entity");
            SetPower(p, Reinforce);
            var again = (Minion)Play(g, opp, id);
            Check(opp.Hero.HeroPower.Card.Id == Reinforce, "the opponent copies Reinforce");
            Use(g, opp);
            Check(opp.BoardZone.Any(m => m.Card.Id == Recruit), "and can use it");
            End(g);
            Check(p.Hero.HeroPower.Card.Id == Reinforce, "my own power is unchanged by their copy");
            // Hero Power used earlier this turn: the copy is fresh
            SetPower(opp, SteadyShot);
            Use(g, p);
            Check(!CanUse(p), "fixture: my power is used up");
            var later = (Minion)Play(g, p, id);
            Check(p.Hero.HeroPower.Card.Id == SteadyShot, "a copy of Steady Shot");
            Check(CanUse(p), "the copy is not exhausted");
            Use(g, p);
            Check(opp.Hero.Damage == 3, "Steady Shot hits the enemy hero for 2: " + opp.Hero.Damage);
            Resume(g, next => {
                var owner = next.Player1;
                Check(owner.Hero.HeroPower.Card.Id == SteadyShot && owner.Hero.HeroPower.Controller == owner, "the copy is kept on a clone and snapshot");
            });
            spelleater.Silence(); g.DeathProcessingAndAuraUpdate();
            Kill(g, later);
            Check(p.Hero.HeroPower.Card.Id == SteadyShot, "Silence and death do not undo the copy");
        });

        WildSetsProof.Case("AT_099", (g, p, id) => {
            var opp = p.Opponent;
            var rider = (Minion)Play(g, p, id);
            Check(rider.Card.Cost == 6 && rider.AttackDamage == 3 && rider.Health == 5, "Kodorider is a 3/5 for 6");
            Check(p.BoardZone.Count == 1, "no Battlecry");
            var right = Summon(p, Wisp);
            Use(g, p);
            Check(p.BoardZone.Count == 3, "Inspire summons one minion: " + p.BoardZone.Count);
            var kodo = p.BoardZone[rider.ZonePosition + 1];
            Check(kodo.Card.Id == "AT_099t" && kodo.AttackDamage == 3 && kodo.Health == 5 && kodo.IsRace(Race.BEAST), "a 3/5 War Kodo to the right of Kodorider: " + kodo.Card.Id + " " + kodo.AttackDamage + "/" + kodo.Health);
            Check(p.BoardZone[rider.ZonePosition + 2].Id == right.Id, "the Wisp was pushed right");
            Check(!CanUse(p) && opp.BoardZone.Count == 0, "one Inspire per use, nothing for the opponent");
            End(g);
            Use(g, opp, p.Hero);
            Check(p.BoardZone.Count == 3, "the opponent's Hero Power does not Inspire");
            End(g);
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[rider.Id]; Use(next, next.Player1);
                Check(next.Player1.BoardZone.Count == 4 && next.Player1.BoardZone[n.ZonePosition + 1].Card.Id == "AT_099t", "Inspire on a clone");
            });
            Park(p);
            var full = (Minion)Play(g, p, id);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Check(p.BoardZone.Count == 7, "fixture: full board");
            Use(g, p);
            Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id != "AT_099t"), "a full board: no Kodo");
            End(g); End(g);
            Kill(g, p.BoardZone.Last());
            Use(g, p);
            Check(p.BoardZone.Count(m => m.Card.Id == "AT_099t") == 1, "with room again, a Kodo comes");
            full.Silence(); g.DeathProcessingAndAuraUpdate();
            Kill(g, p.BoardZone.Last());
            End(g); End(g);
            Use(g, p);
            Check(p.BoardZone.Count(m => m.Card.Id == "AT_099t") == 1, "a Silenced Kodorider no longer Inspires");
            Kill(g, full);
            Check(full.Zone.Type == Zone.GRAVEYARD, "dead Kodorider");
        });

        WildSetsProof.Case("AT_100", (g, p, id) => {
            var opp = p.Opponent;
            var regent = (Minion)Play(g, p, id);
            Check(regent.Card.Cost == 3 && regent.AttackDamage == 3 && regent.Health == 3, "Silver Hand Regent is a 3/3 for 3");
            Check(p.BoardZone.Count == 1, "no Battlecry");
            var right = Summon(p, Wisp);
            Use(g, p);
            var token = p.BoardZone[regent.ZonePosition + 1];
            Check(p.BoardZone.Count == 3 && token.Card.Id == Recruit && token.AttackDamage == 1 && token.Health == 1, "Inspire: a 1/1 Silver Hand Recruit to the right: " + token.Card.Id);
            Check(p.BoardZone[regent.ZonePosition + 2].Id == right.Id, "the Wisp was pushed right");
            Check(!CanUse(p), "one Inspire per use");
            End(g);
            Use(g, opp, p.Hero);
            Check(p.BoardZone.Count == 3 && opp.BoardZone.Count == 0, "the opponent's Hero Power does not Inspire");
            End(g);
            Resume(g, next => {
                Use(next, next.Player1);
                Check(next.Player1.BoardZone.Count(m => m.Card.Id == Recruit) == 2, "Inspire on a clone");
            });
            Park(p);
            var full = (Minion)Play(g, p, id);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Use(g, p);
            Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id != Recruit), "a full board: no Recruit");
            End(g); End(g);
            Kill(g, p.BoardZone.Last());
            Use(g, p);
            Check(p.BoardZone.Count(m => m.Card.Id == Recruit) == 1, "room again: a Recruit comes");
            full.Silence(); g.DeathProcessingAndAuraUpdate();
            Kill(g, p.BoardZone.Last());
            int recruits = p.BoardZone.Count(m => m.Card.Id == Recruit);
            End(g); End(g);
            Use(g, p);
            Check(p.BoardZone.Count(m => m.Card.Id == Recruit) == recruits, "a Silenced Regent no longer Inspires");
            Kill(g, full);
            Check(full.Zone.Type == Zone.GRAVEYARD, "dead Regent");
        });

        WildSetsProof.Case("AT_102", (g, p, id) => {
            var held = (Minion)Add(p, id);
            Check(held.Card.Cost == 7 && held.AttackDamage == 5 && held.Health == 9 && held.IsRace(Race.BEAST), "Captured Jormungar is a 5/9 Beast for 7");
            PlayEntity(g, p, held);
            Check(!held.HasTaunt && !held.HasCharge && !held.HasStealth && !held.HasDivineShield && !held.HasWindfury && !held.HasDeathrattle && held.IsExhausted, "vanilla");
            Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 0 && p.Opponent.Hero.Damage == 0, "no effect besides the body");
            held.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(held.AttackDamage == 5 && held.Health == 9, "Silence changes nothing");
            var foe = Summon(p.Opponent, Yeti);
            End(g); End(g);
            Attack(g, held, foe);
            Check(foe.Zone.Type == Zone.GRAVEYARD && held.Damage == 4 && held.Zone.Type == Zone.PLAY, "5 Attack kills a Yeti, 9 Health survives it");
            Resume(g, next => { var m = (Minion)Play(next, next.Player1, id); Check(m.AttackDamage == 5 && m.Health == 9, "plays the same on a clone"); });
        });

        WildSetsProof.Case("AT_103", (g, p, id) => {
            var opp = p.Opponent;
            var friend = Summon(p, Yeti); var foe = Summon(opp, Yeti); var sneaky = Summon(opp, Sneak);
            var held = Add(p, id);
            Check(held.Cost == 9, "North Sea Kraken costs 9");
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.Target?.Id).ToHashSet();
            Check(targets.SetEquals(new int?[] { friend.Id, foe.Id, p.Hero.Id, opp.Hero.Id }), "any character except itself and a Stealthed enemy minion, a target is required: " + targets.Count);
            Check(!targets.Contains(sneaky.Id) && !targets.Contains(null), "no Stealth target, no untargeted play");
            var kraken = (Minion)held;
            PlayEntity(g, p, held, foe);
            Check(kraken.AttackDamage == 9 && kraken.Health == 7 && foe.Damage == 4 && friend.Damage == 0, "a 9/7: Battlecry deals 4 to the target");
            Summon(p, Thalnos);
            var more = Add(p, id); PlayEntity(g, p, more, opp.Hero);
            Check(opp.Hero.Damage == 4, "Spell Damage does not change a Battlecry: " + opp.Hero.Damage);
            Park(p); Summon(p, Thalnos);
            var third = Add(p, id); PlayEntity(g, p, third, p.Hero);
            Check(p.Hero.Damage == 4, "it can hit my own hero");
            Park(p);
            friend = Summon(p, Yeti);
            var fourth = Add(p, id); PlayEntity(g, p, fourth, friend);
            Check(friend.Damage == 4 && friend.Zone.Type == Zone.PLAY, "or a friendly minion: 4 damage on a 5-Health Yeti");
            Park(p); friend = Summon(p, Yeti);
            var fodder = Summon(p, Wisp); var fifthCard = Add(p, id); PlayEntity(g, p, fifthCard, fodder);
            Check(fodder.Zone.Type == Zone.GRAVEYARD, "a 1-Health minion dies");
            Park(p); Park(opp);
            var shielded = Summon(opp, Yeti); shielded.HasDivineShield = true;
            var fifth = Add(p, id); PlayEntity(g, p, fifth, shielded);
            Check(!shielded.HasDivineShield && shielded.Damage == 0, "Divine Shield absorbs the damage");
            Park(opp); Park(p);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            var last = Add(p, id);
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == last.Id), "a full board cannot play it");
            Park(p);
            Resume(g, next => {
                var owner = next.Player1;
                var k = Add(owner, id); PlayEntity(next, owner, k, next.Player2.Hero);
                Check(next.Player2.Hero.Damage == 8, "Battlecry on a clone");
            });
        });

        WildSetsProof.Case("AT_104", (g, p, id) => {
            var opp = p.Opponent;
            var held = Add(p, id);
            Check(held.Cost == 5, "Tuskarr Jouster costs 5");
            ParkHand(p);
            var hurt = p.Hero;
            SetDeck(p, Yeti); SetDeck(opp, Wisp);
            hurt.Damage = 25; opp.Hero.Damage = 25; int mine = p.DeckZone.Count, theirs = opp.DeckZone.Count;
            var jouster = (Minion)Play(g, p, id);
            Check(jouster.AttackDamage == 5 && jouster.Health == 5, "a 5/5");
            Check(hurt.Damage == 18 && opp.Hero.Damage == 25, "a costlier minion restores 7 to my hero only: " + hurt.Damage);
            Check(p.DeckZone.Count == mine && opp.DeckZone.Count == theirs && p.HandZone.Count == 0 && opp.HandZone.Count == 0, "Reveal moves and draws nothing");
            Park(p); SetDeck(p, Yeti); SetDeck(opp, Yeti); hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 25, "a tie restores nothing");
            Park(p); SetDeck(p, Wisp); SetDeck(opp, Yeti); hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 25, "a cheaper minion restores nothing");
            Park(p); SetDeck(p, Yeti); SetDeck(opp, Frostbolt, Frostbolt); hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 18, "an opponent deck without minions loses the Joust");
            Park(p); SetDeck(p, Frostbolt); SetDeck(opp, Wisp); hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 25, "no minion in my deck: nothing");
            Park(p); SetDeck(p); SetDeck(opp); hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 25, "empty decks: nothing");
            var outcomes = new HashSet<int>();
            for (int i = 0; i < 24; i++)
            {
                Park(p); SetDeck(p, Yeti, Wisp, FlameImp); SetDeck(opp, FlameImp);
                hurt.Damage = 25; Play(g, p, id);
                outcomes.Add(25 - hurt.Damage);
            }
            Check(outcomes.SetEquals(new[] { 0, 7 }), "the Joust is random: " + string.Join(",", outcomes));
            Park(p); hurt.Damage = 3; SetDeck(p, Yeti); SetDeck(opp, Wisp); Play(g, p, id);
            Check(hurt.Damage == 0, "no healing beyond full Health");
            Park(p); Summon(p, Thalnos);
            hurt.Damage = 25; Play(g, p, id);
            Check(hurt.Damage == 18, "Spell Damage does not change the restore amount");
            Park(p);
            // unrevealed deck cards stay hidden
            SetDeck(p, Yeti); SetDeck(opp, Wisp, Fireball);
            var secret = opp.DeckZone.Last();
            Park(p); Play(g, p, id);
            HiddenSeat(g, p.PlayerId, new[] { secret }, "my view of the opponent's deck after the Joust");
            // the opponent's Jouster heals the opponent's hero
            End(g);
            SetDeck(opp, Yeti); SetDeck(p, Wisp);
            opp.Hero.Damage = 25; hurt.Damage = 5;
            Park(opp); Play(g, opp, id);
            Check(opp.Hero.Damage == 18 && hurt.Damage == 5, "the opponent's Jouster heals the opponent's hero");
            Resume(g, next => {
                var owner = next.Player2;
                SetDeck(owner, Yeti); SetDeck(next.Player1, Wisp); Park(owner);
                owner.Hero.Damage = 25; Play(next, owner, id);
                Check(owner.Hero.Damage == 18, "Joust on a clone");
            });
        });

        WildSetsProof.Held("AT_105", (g, p, id) => {
            var opp = p.Opponent;
            var held = (Minion)Add(p, id);
            Check(held.Cost == 1 && held.AttackDamage == 2 && held.Health == 4 && held.IsRace(Race.UNDEAD), "Injured Kvaldir is a 2/4 Undead for 1");
            PlayEntity(g, p, held);
            Check(held.Zone.Type == Zone.PLAY && held.Damage == 3 && held.Health == 1, "Battlecry: 3 damage to this minion: " + held.Damage);
            Check(p.Hero.Damage == 0 && opp.Hero.Damage == 0 && p.BoardZone.Count == 1, "nothing else is damaged");
            Summon(p, Thalnos);
            var second = (Minion)Play(g, p, id);
            Check(second.Damage == 3 && second.Health == 1, "Spell Damage does not change a Battlecry");
            var buffed = (Minion)Add(p, id); buffed.BaseHealth = 3;
            PlayEntity(g, p, buffed);
            Check(buffed.Zone.Type == Zone.GRAVEYARD, "a Kvaldir with 3 Health dies to its own Battlecry");
            var big = (Minion)Add(p, id); big.BaseHealth = 6;
            PlayEntity(g, p, big);
            Check(big.Zone.Type == Zone.PLAY && big.Damage == 3 && big.Health == 3, "buffed Health survives: " + big.Health);
            var other = Summon(p, Yeti);
            Check(other.Damage == 0, "friendly minions are untouched");
            held.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(held.Damage == 3 && held.Health == 1, "Silence leaves the damage");
            Resume(g, next => {
                var m = (Minion)Play(next, next.Player1, id);
                Check(m.Damage == 3 && m.Health == 1, "Battlecry on a clone");
            });
            Heal(g, held, 3);
            Check(held.Damage == 0 && held.Health == 4, "healing restores it");
        });

        WildSetsProof.Case("AT_106", (g, p, id) => {
            var opp = p.Opponent;
            Check(Add(p, id).Cost == 3, "Light's Champion costs 3");
            ParkHand(p);
            var held = Add(p, id);
            var yeti = Summon(p, Yeti);
            var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(options.Length > 0 && options.All(t => t.Target == null), "no Demon: playable without a target");
            var champion = (Minion)held; PlayEntity(g, p, held);
            Check(champion.AttackDamage == 4 && champion.Health == 3 && yeti.Zone.Type == Zone.PLAY, "a 4/3");
            var friendDemon = Summon(p, Voidwalker); var enemyDemon = Summon(opp, FlameImp); var enemyYeti = Summon(opp, Yeti);
            var buffed = Summon(opp, Voidwalker); buffed[GameTag.ATK] = 7;
            var second = Add(p, id);
            options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == second.Id).ToArray();
            var targets = options.Select(t => t.Target?.Id).ToHashSet();
            Check(targets.SetEquals(new int?[] { friendDemon.Id, enemyDemon.Id, buffed.Id }), "any Demon, friendly or enemy, and nothing else; a target is required: " + options.Length);
            Check(friendDemon.HasTaunt && buffed.HasTaunt && buffed.AttackDamage == 7, "fixture");
            PlayEntity(g, p, second, buffed);
            Check(!buffed.HasTaunt && buffed.AttackDamage == 1 && buffed.Zone.Type == Zone.PLAY, "Silenced: Taunt and the buff are gone");
            var third = Add(p, id); PlayEntity(g, p, third, friendDemon);
            Check(!friendDemon.HasTaunt, "a friendly Demon can be Silenced");
            Resume(g, next => {
                var d = (Minion)next.IdEntityDic[enemyDemon.Id];
                d.HasDivineShield = true;
                var c = Add(next.Player1, id); PlayEntity(next, next.Player1, c, d);
                Check(!d.HasDivineShield, "Silence on a clone");
            });
            Check(enemyYeti.AttackDamage == 4 && yeti.AttackDamage == 4, "non-Demons are untouched");
            Park(opp); var hiding = Summon(opp, FlameImp); hiding.HasStealth = true;
            var fourth = Add(p, id);
            options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == fourth.Id && t.Target?.Id == hiding.Id).ToArray();
            Check(options.Length == 0, "a Stealthed enemy Demon is not a target");
        });

        WildSetsProof.Case("AT_108", (g, p, id) => {
            var opp = p.Opponent;
            Check(Add(p, id).Cost == 4, "Armored Warhorse costs 4");
            ParkHand(p);
            SetDeck(p, Yeti); SetDeck(opp, Wisp);
            var horse = (Minion)Play(g, p, id);
            Check(horse.AttackDamage == 5 && horse.Health == 3 && horse.IsRace(Race.BEAST), "Armored Warhorse is a 5/3 Beast for 4");
            Check(horse.HasCharge && !horse.IsExhausted, "a costlier minion: Charge");
            Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == horse.Id && t.Target?.Id == opp.Hero.Id), "and it can attack at once");
            Check(p.DeckZone.Count == 1 && opp.DeckZone.Count == 1 && p.HandZone.Count == 0, "Reveal moves and draws nothing");
            SetDeck(p, Yeti); SetDeck(opp, Yeti);
            var tie = (Minion)Play(g, p, id);
            Check(!tie.HasCharge && tie.IsExhausted, "a tie: no Charge");
            SetDeck(p, Wisp); SetDeck(opp, Yeti);
            var lower = (Minion)Play(g, p, id);
            Check(!lower.HasCharge, "a cheaper minion: no Charge");
            SetDeck(p, Yeti); SetDeck(opp, Frostbolt);
            var none = (Minion)Play(g, p, id);
            Check(none.HasCharge, "an opponent deck without minions loses the Joust");
            SetDeck(p, Frostbolt); SetDeck(opp, Wisp);
            var nothing = (Minion)Play(g, p, id);
            Check(!nothing.HasCharge, "no minion in my deck: no Charge");
            SetDeck(p); SetDeck(opp);
            var empty = (Minion)Play(g, p, id);
            Check(!empty.HasCharge, "empty decks: no Charge");
            var outcomes = new HashSet<bool>();
            for (int i = 0; i < 24; i++)
            {
                Park(p);
                SetDeck(p, Yeti, Wisp, FlameImp); SetDeck(opp, FlameImp);
                outcomes.Add(((Minion)Play(g, p, id)).HasCharge);
            }
            Check(outcomes.Count == 2, "the Joust is random: " + string.Join(",", outcomes));
            Park(p);
            SetDeck(p, Yeti); SetDeck(opp, Wisp);
            var charged = (Minion)Play(g, p, id);
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[charged.Id];
                Check(n.HasCharge && !n.IsExhausted, "Charge holds on a clone and snapshot");
                SetDeck(next.Player1, Yeti); SetDeck(next.Player2, Wisp);
                var again = (Minion)Play(next, next.Player1, id);
                Check(again.HasCharge, "Joust on a clone");
            });
            charged.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(!charged.HasCharge && charged.AttackDamage == 5, "Silence removes Charge");
            Attack(g, charged, opp.Hero);
            Check(opp.Hero.Damage == 5, "it attacked once on the play turn before Silence only because it was ready");
            End(g);
            SetDeck(opp, Wisp); SetDeck(p, Yeti);
            var theirs = (Minion)Play(g, opp, id);
            Check(!theirs.HasCharge, "the opponent's Warhorse loses the Joust against my costlier minion");
            Park(opp); SetDeck(opp, Yeti); SetDeck(p, Frostbolt);
            var theirs2 = (Minion)Play(g, opp, id);
            Check(theirs2.HasCharge && opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == theirs2.Id), "the opponent's Warhorse wins and charges");
        });

        WildSetsProof.Case("AT_109", (g, p, id) => {
            var opp = p.Opponent;
            var watchman = (Minion)Play(g, p, id);
            Check(watchman.Card.Cost == 2 && watchman.AttackDamage == 2 && watchman.Health == 4, "Argent Watchman is a 2/4 for 2");
            Use(g, p);
            Check(!CanAttack(p, watchman, opp.Hero), "Inspire does not cure summoning sickness");
            End(g); End(g);
            Check(!CanAttack(p, watchman, opp.Hero), "Can't attack by default");
            Use(g, p);
            Check(CanAttack(p, watchman, opp.Hero), "Inspire: can attack as normal this turn");
            var foe = Summon(opp, Yeti);
            Check(CanAttack(p, watchman, foe), "any legal target");
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[watchman.Id];
                Check(CanAttack(next.Player1, n, next.Player2.Hero), "the permission holds on a clone and snapshot");
            });
            Attack(g, watchman, opp.Hero);
            Check(opp.Hero.Damage == 2, "it attacks for 2");
            Check(!CanAttack(p, watchman, opp.Hero), "only once");
            End(g);
            Use(g, opp, p.Hero);
            End(g);
            Check(!CanAttack(p, watchman, opp.Hero), "the permission ended with the turn and the opponent's Hero Power gives none");
            End(g); End(g);
            Use(g, p);
            Check(CanAttack(p, watchman, opp.Hero), "Inspire works again each turn");
            var second = (Minion)Play(g, p, id);
            Check(!CanAttack(p, second, opp.Hero) && CanAttack(p, watchman, opp.Hero), "a second Watchman played afterwards has nothing yet");
            End(g); End(g);
            watchman.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(CanAttack(p, watchman, opp.Hero), "Silence removes the restriction: it attacks as normal");
            Check(!CanAttack(p, second, opp.Hero), "the other Watchman is unaffected");
            Use(g, p);
            Check(CanAttack(p, second, opp.Hero), "the other Watchman still Inspires");
            Kill(g, second);
            Check(second.Zone.Type == Zone.GRAVEYARD, "dead Watchman");
        });

        WildSetsProof.Case("AT_110", (g, p, id) => {
            var opp = p.Opponent;
            var manager = (Minion)Play(g, p, id);
            Check(manager.Card.Cost == 3 && manager.AttackDamage == 2 && manager.Health == 5, "Coliseum Manager is a 2/5 for 3");
            manager.Damage = 2;
            var buddy = Summon(p, Yeti);
            Use(g, p);
            Check(manager.Zone.Type == Zone.HAND && p.BoardZone.Count == 1 && p.BoardZone[0].Id == buddy.Id, "Inspire returns it to my hand");
            Check(manager.Cost == 3 && manager.Damage == 0 && manager.AttackDamage == 2 && manager.Health == 5, "as a fresh card: " + manager.Damage);
            Check(!CanUse(p), "one Inspire per use");
            ParkHand(p);
            End(g);
            var theirs = (Minion)Play(g, opp, id);
            Use(g, opp, p.Hero);
            Check(theirs.Zone.Type == Zone.HAND && theirs.Controller == opp, "the opponent's Manager returns to the opponent's hand");
            ParkHand(opp);
            End(g);
            var m2 = (Minion)Play(g, p, id);
            End(g);
            Use(g, opp, p.Hero);
            Check(m2.Zone.Type == Zone.PLAY, "the opponent's Hero Power does not Inspire");
            End(g);
            Resume(g, next => {
                Use(next, next.Player1);
                Check(((Minion)next.IdEntityDic[m2.Id]).Zone.Type == Zone.HAND, "Inspire on a clone and snapshot");
            });
            // a full hand
            for (int i = p.HandZone.Count; i < 10; i++) Add(p, Yeti);
            Check(p.HandZone.Count == 10, "fixture: full hand");
            Use(g, p);
            Check(m2.Zone.Type != Zone.HAND && m2.Zone.Type != Zone.PLAY && p.HandZone.Count == 10, "a full hand: the Manager is destroyed instead: " + m2.Zone.Type);
            // Silence removes the Inspire
            End(g); End(g);
            ParkHand(p);
            var silenced = (Minion)Play(g, p, id);
            silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            Use(g, p);
            Check(silenced.Zone.Type == Zone.PLAY, "a Silenced Manager stays");
            Kill(g, silenced);
            Check(silenced.Zone.Type == Zone.GRAVEYARD, "dead Manager");
            ParkHand(p);
            // a replayed Manager is fresh
            End(g); End(g);
            var fresh = (Minion)Play(g, p, id);
            fresh.Damage = 1;
            Use(g, p);
            var returned = p.HandZone.Single(c => c.Card.Id == id);
            Check(returned.Cost == 3 && ((Minion)returned).Damage == 0, "the returned card is as new");
            End(g); End(g);
            PlayEntity(g, p, returned);
            Use(g, p);
            Check(p.HandZone.Count(c => c.Card.Id == id) == 1, "and Inspires again after being replayed");
        });

    }

    // ---- blocked cards: Standard uses them (pool eligibility); never registered ---------------------------------------

    private static void Blocked()
    {
        Unregistered("AT_101", (g, p, id) => {
            var held = (Minion)Add(p, id);
            Check(held.Card.Cost == 5 && held.AttackDamage == 5 && held.Health == 6, "Pit Fighter is a 5/6 for 5");
            PlayEntity(g, p, held);
            Check(!held.HasTaunt && !held.HasCharge && !held.HasStealth && !held.HasDivineShield && !held.HasWindfury && !held.HasDeathrattle && held.IsExhausted, "vanilla");
            Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 0, "no effect besides the body");
            held.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(held.AttackDamage == 5 && held.Health == 6, "Silence changes nothing");
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

    private static bool CanAttack(Controller c, Minion m, ICharacter target) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == target.Id);

    private static void SetPower(Controller c, string id)
    {
        var power = (HeroPower)Entity.FromCard(c, Cards.FromId(id));
        c.SetasideZone.Add(c.Hero.HeroPower);
        c.Hero.HeroPower = power;
        BasicHeroPowerRules.EnrollPowerAuras(c, power);
        c.Game.AuraUpdate();
    }

    private static void Park(Controller c) => StandardSetsProof.ParkBoard(c);

    private static void ParkHand(Controller p) { foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); } }

    private static void Kill(Game g, Minion minion)
    {
        Core2026Proof.Kill(g, minion);
        g.DeathProcessingAndAuraUpdate();
    }

    private static void Heal(Game g, ICharacter target, int n) { target.TakeHeal(target, n); g.DeathProcessingAndAuraUpdate(); }

    private static Minion Dummy(Controller c, string id = Yeti) { var m = Summon(c, id); m[GameTag.ATK] = 0; m[GameTag.HEALTH] = 20; return m; }

    private static void Strike(Game g, Minion attacker, ICharacter target)
    {
        attacker.NumAttacksThisTurn = 0;
        Attack(g, attacker, target);
    }

    /// <summary>No character may keep a combat flag once an attack is over (a redirected attack must not leave the intended target "defending").</summary>
    private static void NoCombatFlags(Game g, string what)
    {
        foreach (var entity in g.IdEntityDic.Values.OfType<ICharacter>())
            Check(!entity.IsAttacking && !entity.IsDefending, what + ": no stale combat flag on " + entity.Card.Id);
    }

    /// <summary>Runs <paramref name="n"/> attacks; returns how often the intended target and each other candidate lost health.</summary>
    private static (int Intended, int[] Others) Trials(Game g, int n, ICharacter intended, ICharacter[] others, Action swing)
    {
        int hitIntended = 0; var hitOthers = new int[others.Length];
        for (int i = 0; i < n; i++)
        {
            intended.Damage = 0; foreach (var o in others) o.Damage = 0;
            swing();
            Check(intended.Damage > 0 ^ others.Any(o => o.Damage > 0), "an attack lands on exactly one of the candidates");
            if (intended.Damage > 0) hitIntended++;
            for (int k = 0; k < others.Length; k++) if (others[k].Damage > 0) hitOthers[k]++;
            NoCombatFlags(g, "after attack " + i);
        }
        intended.Damage = 0; foreach (var o in others) o.Damage = 0;
        return (hitIntended, hitOthers);
    }

    /// <summary>No card of <paramref name="hidden"/> sits in a hidden zone (hand, deck, set aside, secret) of either player in the fair view of seat <paramref name="viewer"/>.</summary>
    private static void HiddenSeat(Game g, int viewer, IEnumerable<IPlayable> hidden, string why)
    {
        var ids = hidden.Select(e => e.Card.Id).ToHashSet();
        var before = Canonical.State(g); string history = g.HistoryFacts.CanonicalText(); var random = g.Random.CaptureState();
        var fair = FairBotState.Create(g, viewer, 83, DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        Check(!view.IdEntityDic.Values.Any(e => (e.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE or Zone.SECRET) && e.Controller.PlayerId != viewer && ids.Contains(e.Card.Id)),
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
