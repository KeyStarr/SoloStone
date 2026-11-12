using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    private static void VioletDeck(Fixture f, int count, string id = "CS2_182")
    {
        foreach (var card in f.Player.DeckZone.ToArray())
            f.Player.SetasideZone.Add(f.Player.DeckZone.Remove(card));
        for (int i = 0; i < count; i++) SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId(id), zone: f.Player.DeckZone);
        f.Game.DeathProcessingAndAuraUpdate();
    }

    private static void CorpseFlip(Fixture f, IPlayable card, bool met, int threshold)
    {
        Check(f.Game.EnableCorpses, "native fixture must enable Corpses");
        CorpseRules.Gain(f.Player, threshold - 1);
        f.Flush();
        f.Expected(card, 0, card.Card.Id + " below Corpse threshold");
        if (!met) return;
        var victim = f.Summon("CS2_231", f.Player);
        f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), victim));
        Check(victim.Zone == f.Player.GraveyardZone && CorpseRules.Available(f.Player) == threshold,
            "friendly death reaches exact Corpse threshold");
        f.Expected(card, 1, card.Card.Id + " lights in friendly-death flush");
    }

    [GroupProof("powered-up-violet-hold", ProofKind.PoweredUp)]
    private static void RunVioletHold()
    {
        Scenario("CAP_006", (f, card, met) => {
            var attacker = f.Summon("EX1_008", f.Player);
            attacker.IsExhausted = false;
            attacker.HasStealth = met;
            f.Flush();
            f.Expected(card, 0, "Tricks before attack");
            f.Process(MinionAttackTask.Any(f.Player, attacker, f.Game.Player2.Hero));
            f.Expected(card, met ? 1 : 0, "Tricks in attack flush");
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Player.Hero),
            (f, card) => { Check(f.Player.Hero.Damage == 1 || f.Player.Hero.Damage == 3, "Tricks damage"); return f.Player.Hero.Damage == 3; });

        Scenario("JAIL_035", (f, card, met) => {
            VioletDeck(f, 2);
            f.Flush();
            f.Expected(card, 0, "Sentry with Neutral cards");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("CS2_023")));
                f.Expected(card, 1, "Sentry lights when last Neutral drawn");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => { int count = f.Player.BoardZone.Count(m => m.Card.Id == "JAIL_035"); Check(count == 1 || count == 3, "Sentry copies"); return count == 3; });

        Scenario("JAIL_204", (f, card, met) => {
            var victim = f.Summon("CS2_231", f.Game.Player2);
            f.Game.DeathProcessingAndAuraUpdate();
            f.Flush();
            f.Expected(card, 0, "Prisoner with enemy minion");
            Check(card.Cost == 5, "Prisoner printed cost");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), victim));
                f.Expected(card, 1, "Prisoner lights in last-minion death flush");
            }
            Check(card.Cost == (met ? 2 : 5) && card[GameTag.COST] == card.Cost
                && f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.COST] == card.Cost, "Prisoner projected cost agrees with glow");
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => { Check(card.Zone == f.Player.BoardZone, "Prisoner played"); return f.Player.UsedMana == 2; });

        foreach (string id in new[] { "JAIL_307", "JAIL_456" })
        {
            int drawsBefore = 0;
            Scenario(id, (f, card, met) => {
                VioletDeck(f, 24);
                // Cosmic Manifestations in the middle shuffles exactly one spell.
                f.Draw("CS2_231");
                var shuffle = f.Draw("JAIL_892");
                f.Draw("CS2_231");
                f.Flush();
                f.Expected(card, 0, id + " at 24 cards");
                if (met)
                {
                    f.Process(PlayCardTask.SpellTarget(f.Player, shuffle, f.Game.Player2.Hero));
                    Check(f.Player.DeckZone.Count == 25, "shuffle reaches exact deck boundary");
                    f.Expected(card, 1, id + " lights in shuffle flush");
                }
                if (id == "JAIL_307") Check(card.Cost == (met ? 3 : 5) && card[GameTag.COST] == card.Cost
                    && f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.COST] == card.Cost, "Crowd Control projected cost agrees with glow");
                drawsBefore = f.Player.NumCardsDrawnThisTurn;
            }, (f, card) => card is Minion minion ? PlayCardTask.Minion(f.Player, minion) : PlayCardTask.Spell(f.Player, card),
                (f, card) => {
                    if (id == "JAIL_307") return f.Player.UsedMana == 3;
                    int draws = f.Player.NumCardsDrawnThisTurn - drawsBefore;
                    Check(draws == 0 || draws == 1, "P1CK-P0K3T draws at most one");
                    return draws == 1;
                });
        }

        Scenario("JAIL_445", (f, card, met) => {
            f.Flush();
            f.Expected(card, 0, "Flurry before death");
            var victim = f.Summon("CS2_231", met ? f.Player : f.Game.Player2);
            f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), victim));
            Check(victim.Zone == victim.Controller.GraveyardZone, "Flurry fixture death");
            f.Expected(card, met ? 1 : 0, "Flurry in death flush");
        }, (f, card) => PlayCardTask.Spell(f.Player, card),
            (f, card) => { int damage = f.Game.Player2.Hero.Damage; Check(damage == 3 || damage == 6, "Flurry missile count"); return damage == 6; });

        Scenario("JAIL_806", (f, card, met) => {
            f.Player.DeckCards.Clear();
            f.Player.DeckCards.Add(Cards.FromId(met ? "CS2_182" : "CS2_029"));
            VioletDeck(f, 1, met ? "CS2_029" : "CS2_182");
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card), (f, card) => {
            var result = f.Player.HandZone.Single(p => p[GameTag.DISPLAYED_CREATOR] == card.Id);
            int undiscounted = result.Card.Cost - (result.Card.Id == "JAIL_514" ? f.Player.HandZone.Count : 0);
            Check(result.Cost == undiscounted || result.Cost == Math.Max(0, undiscounted - 5), "Hexmarshal generated spell cost");
            return result.Cost == Math.Max(0, undiscounted - 5);
        });

        bool recipeReady = false;
        Scenario("JAIL_866", (f, card, met) => {
            recipeReady = met;
            VioletDeck(f, 2);
            f.Mana(9);
            f.Flush();
            f.Expected(card, 0, "Recipe below ten Mana");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("CS2_013")));
                f.Expected(card, 1, "Recipe lights in Wild Growth flush");
            }
        }, (f, card) => {
            // Scenario restores spendable Mana; retain the tested maximum-Mana boundary.
            f.Mana(recipeReady ? 10 : 9);
            return PlayCardTask.Spell(f.Player, card);
        }, (f, card) => {
            var drawn = f.Player.HandZone.OfType<Minion>().ToArray();
            Check(drawn.Length == 2 && drawn.All(m => m.AttackDamage == 4 || m.AttackDamage == 7), "Recipe draws two minions");
            return drawn.All(m => m.AttackDamage == 7 && m.Health == 8);
        });

        OutcastGlow("JAIL_892");
    }

    [GroupProof("powered-up-core", ProofKind.PoweredUp)]
    private static void RunCore()
    {
        Scenario("CORE_RLK_814", (f, card, met) => {
            f.Flush();
            f.Expected(card, 0, "Cultist without Shadow spell");
            f.Draw(met ? "CS2_234" : "CS2_029");
            f.Flush();
            f.Expected(card, met ? 1 : 0, "Cultist in draw flush");
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => { var m = (Minion)card; Check(m.AttackDamage == m.Card.ATK || m.AttackDamage == m.Card.ATK + 1, "Cultist buff"); return m.AttackDamage == m.Card.ATK + 1 && m.Health == m.Card.Health + 1; });

        Scenario("CORE_RLK_066", (f, card, met) => CorpseFlip(f, card, met, 1),
            (f, card) => PlayCardTask.Minion(f.Player, (Minion)card), (f, card) => {
                bool discovered = f.Player.Choice != null;
                if (discovered)
                {
                    Check(f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 1, "Hematurge spends one");
                    f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices.First()));
                    Check(f.Player.HandZone.Count == 1, "Hematurge Discover adds card");
                }
                return discovered;
            });

        Scenario("CORE_RLK_505", (f, card, met) => CorpseFlip(f, card, met, 1),
            (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => { Check(f.Game.Player2.Hero.Damage == 0 || f.Game.Player2.Hero.Damage == 2, "Marrow one-Corpse damage"); return f.Game.Player2.Hero.Damage == 2 && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 1; });

        Scenario("CORE_RLK_712", (f, card, met) => {
            f.Draw("CS2_182");
            CorpseFlip(f, card, met, 2);
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            var minion = f.Player.HandZone.OfType<Minion>().Single();
            Check(minion.AttackDamage == 5 || minion.AttackDamage == 6, "Blood Tap baseline or extra buff");
            return minion.AttackDamage == 6 && minion.Health == 7 && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 2;
        });

        Scenario("RLK_707", (f, card, met) => {
            f.Summon("CS2_182", f.Player);
            CorpseFlip(f, card, met, 5);
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            var minion = f.Player.BoardZone.Single();
            Check(minion.AttackDamage == 5 || minion.AttackDamage == 7, "Grave Strength baseline or extra buff");
            return minion.AttackDamage == 7 && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 5;
        });

        OutcastGlow("CORE_BT_480");
        OutcastGlow("CORE_BT_491");
    }

    private static void OutcastGlow(string id)
    {
        foreach (int position in new[] { 0, 1, 2 })
        {
            var f = new Fixture();
            VioletDeck(f, 5);
            IPlayable card = null;
            for (int i = 0; i < 3; i++)
            {
                var drawn = f.Draw(i == position ? id : "CS2_231");
                if (i == position) card = drawn;
            }
            f.Flush();
            f.Expected(card, position == 1 ? 0 : 1, id + " hand position " + position);
            int before = f.Player.DeckZone.Count;
            f.Process(card is Minion minion ? PlayCardTask.Minion(f.Player, minion) : id == "JAIL_892"
                ? PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero) : PlayCardTask.Spell(f.Player, card));
            f.Expected(card, 0, id + " reset after play");
            int bonus = position == 1 ? 0 : 1;
            if (id == "JAIL_892")
                Check(f.Game.Player2.Hero.Damage == 2 + 2 * bonus && f.Player.DeckZone.Count == before + 1 + bonus, "Cosmic Outcast damage and shuffles");
            else Check(f.Player.DeckZone.Count == before - (id == "CORE_BT_491" ? 1 : 0) - bonus, id + " Outcast draws");
        }

        var flip = new Fixture();
        var left = flip.Draw("CS2_231");
        var held = flip.Draw(id);
        flip.Draw("CS2_231");
        flip.Flush();
        flip.Expected(held, 0, id + " initially middle");
        flip.Process(PlayCardTask.Minion(flip.Player, (Minion)left));
        flip.Expected(held, 1, id + " lights in edge-enabling play flush");
    }
}
