using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    // Every enabling change must appear in the very next real SendHistory projection.
    private static void EmeraldEnable(Fixture f, IPlayable card, bool met, Action enable)
    {
        f.Flush();
        f.Expected(card, 0, card.Card.Id + " before enabling action");
        if (!met) return;
        enable();
        f.Flush();
        f.Expected(card, 1, card.Card.Id + " enabling action flush");
    }

    private static void EmeraldCost(Fixture f, IPlayable card, int expected)
    {
        Check(card.Cost == expected && card[GameTag.COST] == expected, card.Card.Id + " effective cost expected " + expected + ", got " + card.Cost + "/" + card[GameTag.COST]);
        Check(f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.COST] == expected,
            card.Card.Id + " projected cost");
    }

    [GroupProof("powered-up-emerald-dream", ProofKind.PoweredUp)]
    private static void RunEmeraldDream()
    {
        EmeraldDreamsaber();
        EmeraldAessina();
        EmeraldDragons();
        EmeraldSpells();
        EmeraldRunebear();
        EmeraldRuneblade();
        EmeraldHeroPower();
        EmeraldDarkGifts();
        EmeraldFlames();
        EmeraldUniqueCosts();
    }

    private static void EmeraldDreamsaber()
    {
        Scenario("EDR_014", (f, card, met) => {
            var enemy = f.Summon("CS2_182", f.Game.Player2);
            enemy.AttackDamage = 1;
            if (!met) card.Cost = 4;
            EmeraldEnable(f, card, met, () =>
                f.Process(PlayCardTask.Minion(f.Player, f.Draw("FIR_940"))));
            EmeraldCost(f, card, met ? 3 : 4);
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var enemy = f.Game.Player2.BoardZone.Single();
            Check(enemy.Damage == 0 || enemy.Damage == 4, "Dreamsaber attack damage");
            Check(((Minion)card).Damage == (enemy.Damage == 4 ? 1 : 0), "Dreamsaber retaliation");
            Check(f.Player.UsedMana == (enemy.Damage == 4 ? 3 : 4), "Dreamsaber paid current hand cost");
            return enemy.Damage == 4;
        });
    }

    private static void EmeraldAessina()
    {
        Scenario("EDR_430", (f, card, met) => {
            f.Game.Player2.Hero.Armor = 100;
            for (int i = 0; i < 19; i++)
            {
                f.Summon("CS2_231", f.Player).ToBeDestroyed = true;
                f.Game.DeathProcessingAndAuraUpdate();
            }
            // Enemy deaths cannot supply the twentieth friendly death.
            f.Summon("CS2_231", f.Game.Player2).ToBeDestroyed = true;
            f.Game.DeathProcessingAndAuraUpdate();
            var twentieth = f.Summon("CS2_231", f.Player);
            EmeraldEnable(f, card, met, () => f.Process(HeroPowerTask.Any(f.Player, twentieth)));
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            Check(f.Game.Player2.Hero.Armor == 100 || f.Game.Player2.Hero.Armor == 80,
                "Aessina must deal zero or exactly twenty damage");
            return f.Game.Player2.Hero.Armor == 80;
        });
    }

    private static void EmeraldDragons()
    {
        foreach (string id in new[] { "EDR_456", "EDR_457" })
            Scenario(id, (f, card, met) => {
                f.Summon("TIME_056", f.Player);
                EmeraldEnable(f, card, met, () => f.Draw("TIME_056"));
            }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
                if (id == "EDR_457")
                {
                    if (f.Player.Hero.Weapon == null) return false;
                    Check(f.Player.Hero.Weapon.Card.Id == "EDR_457t"
                        && f.Player.Hero.Weapon.AttackDamage == 2 && f.Player.Hero.Weapon.Durability == 2,
                        "Brood Keeper equips its 2/2 weapon");
                    return true;
                }
                if (f.Player.Choice == null) return false;
                int chosen = f.Player.Choice.Choices.First(id =>
                    f.Game.IdEntityDic[f.Game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
                var decision = f.Game.GetPendingDecisions().Single();
                Check(f.Game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { chosen }), out var error),
                    "Darkrider Discover answer: " + error);
                f.Flush();
                Check(f.Player.Choice == null && f.Player.HandZone.Any(p => p.Id == chosen
                    && p.Card.IsRace(Race.DRAGON) && DarkGifts.Has(p)), "Darkrider grants discovered gifted Dragon");
                return true;
            });
    }

    private static void EmeraldSpells()
    {
        foreach (string id in new[] { "EDR_472", "FIR_961" })
            Scenario(id, (f, card, met) => {
                var spell = f.Draw("CS2_029");
                // Expensive minions do not qualify; effective spell cost does.
                f.Draw("EX1_298");
                EmeraldEnable(f, card, met, () => spell.Cost = 5);
            }, (f, card) => id == "EDR_472" && PoweredUp.IsMet(card)
                ? PlayCardTask.MinionTarget(f.Player, card, f.Game.Player2.Hero)
                : PlayCardTask.Minion(f.Player, card), (f, card) => {
                if (id == "EDR_472")
                {
                    Check(f.Game.Player2.Hero.Damage == 0 || f.Game.Player2.Hero.Damage == 3, "Weaver deals three damage");
                    return f.Game.Player2.Hero.Damage == 3;
                }
                var pixie = (Minion)card;
                Check(pixie.HasDivineShield == pixie.HasLifeSteal, "Pixie grants both keywords together");
                return pixie.HasDivineShield && pixie.HasLifeSteal;
            });
    }

    private static void EmeraldRunebear()
    {
        Scenario("EDR_481", (f, card, met) => {
            EmeraldEnable(f, card, met, () =>
                f.Process(PlayCardTask.Minion(f.Player, f.Draw("CORE_CFM_753"))));
            Check(((Minion)card).AttackDamage == (met ? 4 : 3), "Runebear Attack boundary");
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var bears = f.Player.BoardZone.Where(p => p.Card.Id == "EDR_481").ToArray();
            Check(bears.Length == 1 || bears.Length == 2, "Runebear board count");
            if (bears.Length == 2) Check(bears.All(p => p.AttackDamage == 4 && p.Health == 5 && p.HasTaunt),
                "Runebear copy retains hand buff");
            return bears.Length == 2;
        });
    }

    private static void EmeraldRuneblade()
    {
        // Separate single-rune cases catch an accidental AND instead of OR, and self-history counting.
        foreach (var runes in new[] { (Unholy: 1, Blood: 0), (Unholy: 0, Blood: 1), (Unholy: 1, Blood: 1) })
            Scenario("EDR_812", (f, card, met) => {
                EmeraldEnable(f, card, met, () => {
                    var prior = Cards.FromId("CS2_231").Clone();
                    prior.Tags[(GameTag)2198] = runes.Unholy;
                    prior.Tags[(GameTag)2196] = runes.Blood;
                    var played = SabberStoneCore.Actions.Generic.DrawCard(f.Player, prior);
                    f.Process(PlayCardTask.Minion(f.Player, played));
                });
            }, (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
                var weapon = (Weapon)card;
                bool bonus = weapon.AttackDamage != 2 || weapon.Durability != 2;
                Check(weapon.AttackDamage == (bonus ? 2 + runes.Unholy : 2)
                    && weapon.Durability == (bonus ? 2 + runes.Blood : 2), "Runeblade independent rune rewards");
                return bonus;
            });

        var f = new Fixture();
        var blade = f.Draw("EDR_812");
        f.Process(PlayCardTask.Any(f.Player, f.Draw("EDR_812")));
        f.Expected(blade, 1, "prior copy counts as another entity");
        f.Process(PlayCardTask.Minion(f.Player, f.Draw("CS2_231")));
        f.Expected(blade, 0, "only most recent own card counts");
    }

    private static void EmeraldHeroPower()
    {
        Scenario("FIR_777", (f, card, met) =>
            EmeraldEnable(f, card, met, () => f.Process(HeroPowerTask.Any(f.Player, f.Game.Player2.Hero))),
            (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
                var spirit = (Minion)card;
                Check((spirit.AttackDamage == 1 && spirit.Health == 3)
                    || (spirit.AttackDamage == 4 && spirit.Health == 6), "Spirit grants +3/+3");
                return spirit.AttackDamage == 4 && spirit.Health == 6;
            });

        var f = new Fixture();
        var spirit = f.Draw("FIR_777");
        f.Process(HeroPowerTask.Any(f.Player, f.Game.Player2.Hero));
        f.Expected(spirit, 1, "Spirit enabled this turn");
        f.Process(EndTurnTask.Any(f.Player));
        f.Process(EndTurnTask.Any(f.Game.Player2));
        f.Expected(spirit, 0, "last turn Hero Power no longer counts");
    }

    private static void EmeraldDarkGifts()
    {
        foreach (string id in new[] { "FIR_901", "FIR_922", "FIR_956" })
            Scenario(id, (f, card, met) => {
                // A gift on the source itself must not light the two minion consumers.
                if (card is Minion) DarkGifts.Mirror(card, "EDR_100t5", f.Player.Hero);
                var held = f.Draw("CS2_182");
                held[GameTag.HAS_DARK_GIFT] = 1;
                EmeraldEnable(f, card, met, () => DarkGifts.Mirror(held, "EDR_100t5", f.Player.Hero));
            }, (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
                if (id == "FIR_901")
                {
                    int count = f.Player.BoardZone.Count(p => p.Card.Id == "FIR_901t");
                    Check(count == 0 || count == 2, "Matriarch summons two Dragons");
                    return count == 2;
                }
                if (id == "FIR_922")
                {
                    var weapon = (Weapon)card;
                    Check(weapon.Durability == 2 && (weapon.AttackDamage == 1 || weapon.AttackDamage == 4),
                        "Cindersword gains three Attack");
                    return weapon.AttackDamage == 4;
                }
                Check((f.Player.Hero.AttackDamage == 0 && f.Player.Hero.Armor == 0)
                    || (f.Player.Hero.AttackDamage == 3 && f.Player.Hero.Armor == 6), "Turtle grants Attack and Armor");
                return f.Player.Hero.AttackDamage == 3 && f.Player.Hero.Armor == 6;
            });
    }

    private static void EmeraldFlames()
    {
        Scenario("FIR_923", (f, card, met) => {
            f.Summon("EX1_563", f.Game.Player2);
            // Even an eight-cost source cannot satisfy its own holding condition.
            card.Cost = 8;
            var held = f.Draw("EX1_298");
            held.Cost = 7;
            EmeraldEnable(f, card, met, () => held.Cost = 8);
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            int damage = f.Game.Player2.BoardZone.Single().Damage;
            Check(damage == 4 || damage == 8, "Flames deals four or eight damage");
            return damage == 8;
        });
    }

    private static void EmeraldUniqueCosts()
    {
        Scenario("FIR_940", (f, card, met) => {
            f.Draw("CS2_029");
            var duplicate = f.Draw("CS2_029");
            EmeraldEnable(f, card, met, () => f.Process(PlayCardTask.SpellTarget(f.Player, duplicate, f.Game.Player2.Hero)));
            // The remaining card and source share a cost; only OTHER hand cards count.
            card.Cost = 4;
            f.Flush();
            f.Expected(card, met ? 1 : 0, "Flamemancer ignores its own Cost");
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var held = f.Player.HandZone.ToArray();
            Check(held.Length > 0 && held.All(p => p.Cost == 4 || p.Cost == 2), "Flamemancer costs");
            foreach (var other in held) EmeraldCost(f, other, held.Length == 1 ? 2 : 4);
            return held.All(p => p.Cost == 2);
        });

        var f = new Fixture();
        var alone = f.Draw("FIR_940");
        f.Flush();
        f.Expected(alone, 1, "empty other hand vacuously has unique Costs");
        f.Process(PlayCardTask.Minion(f.Player, alone));
        Check(f.Player.HandZone.Count == 0, "Flamemancer safely resolves with no other cards");
    }
}
