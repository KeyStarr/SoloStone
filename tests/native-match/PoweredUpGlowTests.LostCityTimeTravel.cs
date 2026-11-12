using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    private static void LostTimeClearDeck(Controller player)
    {
        foreach (var item in player.DeckZone.ToArray())
            player.SetasideZone.Add(player.DeckZone.Remove(item));
    }

    private static IPlayable LostTimeDeck(Controller player, string id) =>
        SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(id), zone: player.DeckZone);

    private static void LostTimeCost(Fixture f, IPlayable card, int cost)
    {
        f.Game.AuraUpdate();
        f.Flush();
        Check(card.Cost == cost && card[GameTag.COST] == cost
            && f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.COST] == cost,
            card.Card.Id + " engine/printed/transport cost must be " + cost);
    }

    private static void LostTimeDragon(Fixture f, IPlayable card, bool met)
    {
        f.Summon("CORE_EX1_043", f.Player);
        Generic.DrawCard(f.Game.Player2, Cards.FromId("CORE_EX1_043"));
        f.Flush();
        f.Expected(card, 0, card.Card.Id + " source/board/opponent Dragons do not qualify");
        if (!met) return;
        f.Draw("CORE_EX1_043");
        f.Game.AuraUpdate();
        f.Flush();
        f.Expected(card, 1, card.Card.Id + " lights in Dragon draw flush");
    }

    [GroupProof("powered-up-lost-city", ProofKind.PoweredUp)]
    private static void RunLostCity()
    {
        Scenario("TLC_110", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, "CS2_171");
            LostTimeDeck(f.Player, "CS2_171");
            LostTimeDeck(f.Player, "CS2_182");
            f.Summon("CS2_182", f.Player);
            f.Flush();
            f.Expected(card, 0, "Esho mixed deck");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("CS2_023")));
                f.Expected(card, 1, "Esho lights after drawing deck's mismatched type");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => f.Player.BoardZone.First(m => m.Id != card.Id).AttackDamage == 6);

        Scenario("TLC_365", (f, card, met) => {
            f.Summon("CS2_200", f.Game.Player2);
            LostTimeCost(f, card, 3);
            f.Expected(card, 0, "Scuffle before Discover");
            if (met)
                Check(new EffectContext(f.Game, f.Player.Id, f.Player.Hero.Id)
                    .Discover(new[] { Cards.FromId("GAME_005") }), "Discover fixture must open");
            else
                Generic.CreateChoiceCards(f.Player, f.Player.Hero, null, ChoiceType.GENERAL,
                    ChoiceAction.HAND, new[] { Cards.FromId("GAME_005") }, null);
            f.Flush();
            f.Expected(card, 0, "pending Discover does not light Scuffle");
            f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices[0]));
            f.Expected(card, met ? 1 : 0, "only semantic Discover lights Scuffle in choice flush");
            LostTimeCost(f, card, met ? 0 : 3);
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.BoardZone.Single()),
            (f, card) => {
                Check(f.Game.Player2.BoardZone.Single().Damage == 3, "Scuffle base damage");
                return f.Player.UsedMana == 0;
            });

        Scenario("TLC_819", (f, card, met) => {
            LostTimeCost(f, card, 6);
            f.Process(PlayCardTask.Spell(f.Player, f.Draw("CORE_CS2_093")));
            f.Expected(card, 0, "Holy alone does not light Siren");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CORE_EX1_302"),
                    f.Summon("CS2_200", f.Game.Player2)));
                f.Expected(card, 1, "Siren lights in Shadow cast flush");
            }
            LostTimeCost(f, card, met ? 1 : 6);
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => f.Player.UsedMana == 1);

        Scenario("TLC_987", (f, card, met) => {
            f.Summon("CS2_200", f.Game.Player2);
            f.Flush();
            f.Expected(card, 0, "Assistant before Quest");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("TLC_426")));
                f.Expected(card, 1, "Assistant lights in Quest play flush");
            }
        }, (f, card) => f.Player.Options().First(t => t.Source?.Id == card.Id),
            (f, card) => f.Game.Player2.BoardZone.Single().Damage == 3);

        LostTimeKindred();
    }

    private static void LostTimeKindred()
    {
        foreach (string id in new[] { "DINO_138", "DINO_413", "TLC_243", "TLC_366", "TLC_429",
            "TLC_432", "TLC_463", "TLC_600", "TLC_829", "TLC_903" })
        {
            IPlayable tutor = null, ownDiscard = null;
            int opponentHandBefore = 0;
            Scenario(id, (f, card, met) => {
                f.Flush();
                f.Expected(card, 0, id + " before previous-turn match");
                if (met)
                {
                    f.Process(PlayCardTask.Minion(f.Player, (Minion)f.Draw("GIL_681")));
                    f.Expected(card, 0, id + " same-turn All minion insufficient");
                    f.Process(EndTurnTask.Any(f.Player));
                    f.Expected(card, 0, id + " hidden on opponent turn");
                    f.Process(EndTurnTask.Any(f.Game.Player2));
                    f.Expected(card, 1, id + " lights in next own turn flush");
                    foreach (var item in f.Player.BoardZone.ToArray())
                        f.Player.SetasideZone.Add(f.Player.BoardZone.Remove(item));
                    foreach (var item in f.Player.HandZone.Where(p => p.Id != card.Id).ToArray())
                        f.Player.SetasideZone.Add(f.Player.HandZone.Remove(item));
                }
                LostTimeClearDeck(f.Player);
                if (id == "TLC_432") tutor = LostTimeDeck(f.Player, "CORE_EX1_012");
                if (id == "TLC_463")
                {
                    ownDiscard = f.Draw("CS2_029");
                    opponentHandBefore = f.Game.Player2.HandZone.Count;
                    Check(opponentHandBefore > 0, "Razidir opponent must have cards");
                }
                if (id == "DINO_138" || id == "DINO_413" || id == "TLC_829")
                    f.Summon("CS2_200", f.Game.Player2);
                if (id == "TLC_366") LostTimeCost(f, card, met ? 4 : 6);
                if (id == "TLC_600") LostTimeCost(f, card, met ? 6 : 8);
            }, (f, card) => id == "TLC_600"
                ? PlayCardTask.MinionTarget(f.Player, (Minion)card, f.Game.Player2.Hero)
                : id == "TLC_829"
                    ? PlayCardTask.MinionTarget(f.Player, (Minion)card, f.Game.Player2.BoardZone.Single())
                    : PlayCardTask.Minion(f.Player, (Minion)card),
                (f, card) => {
                    switch (id)
                    {
                        case "DINO_138": return f.Game.Player2.BoardZone.Single().Damage == 6;
                        case "DINO_413":
                            Check(f.Game.Player2.BoardZone.Single().Damage == 2, "Stegodon unconditional damage");
                            return f.Game.Player2.BoardZone.Single().IsFrozen;
                        case "TLC_243": return ((Minion)card).IsImmune;
                        case "TLC_366": return f.Player.UsedMana == 4;
                        case "TLC_429": return f.Player.BoardZone.Count(m => m.Card.Id == "TLC_429t") == 2;
                        case "TLC_432":
                            Check(tutor.Zone == f.Player.HandZone, "Raptor must draw eligible Deathrattle");
                            return tutor.Cost == 0;
                        case "TLC_463":
                            bool discardedEnemy = f.Game.Player2.HandZone.Count == opponentHandBefore - 1;
                            Check((ownDiscard.Zone == f.Player.HandZone) == discardedEnemy,
                                "Razidir discards exactly one card from the qualified side");
                            return discardedEnemy;
                        case "TLC_600":
                            Check(f.Player.Hero.Armor == 5 && f.Game.Player2.Hero.Damage == 5, "Wyrm base Battlecry");
                            return f.Player.UsedMana == 6;
                        case "TLC_829": return ((Minion)card).AttackDamage == card.Card.ATK + 6;
                        default: return f.Player.Hero.AttackDamage == 5;
                    }
                });
        }
    }

    [GroupProof("powered-up-time-travel", ProofKind.PoweredUp)]
    private static void RunTimeTravel()
    {
        Scenario("END_019", (f, card, met) => {
            f.Flush();
            f.Expected(card, 0, "Survivor before damage");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Player.Hero));
                f.Expected(card, 1, "Survivor lights in hero damage flush");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => ((Minion)card).AttackDamage == card.Card.ATK + 3);

        Scenario("END_033", (f, card, met) => {
            LostTimeCost(f, card, 7);
            LostTimeDragon(f, card, met);
            LostTimeCost(f, card, met ? 4 : 7);
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => f.Player.UsedMana == 4);

        Scenario("END_035", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, "CS2_182");
            LostTimeDeck(f.Player, "CS2_182");
            f.Flush();
            f.Expected(card, 0, "Omen with cards in deck");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("CS2_023")));
                f.Expected(card, 1, "Omen lights in last draw flush");
            }
        }, (f, card) => PlayCardTask.Spell(f.Player, card),
            (f, card) => f.Game.Player2.GraveyardZone.Count == 5);

        Scenario("TIME_006", LostTimeDragon, (f, card) => PlayCardTask.Spell(f.Player, card),
            (f, card) => f.Player.BoardZone.Count(m => m.Card.Id == "TIME_006t1") == 2);
        Scenario("TIME_062", LostTimeDragon, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => {
                Check(((Minion)card).HasTaunt == ((Minion)card).HasDivineShield, "Keeper grants both keywords");
                return ((Minion)card).HasTaunt;
            });

        Scenario("TIME_213", (f, card, met) => {
            f.Flush();
            f.Expected(card, 0, "Overseer before Nature cast");
            f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw(met ? "EX1_238" : "CS2_029"), f.Game.Player2.Hero));
            f.Expected(card, met ? 1 : 0, "Overseer cast flush");
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, "CS2_182");
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => {
                bool buffed = ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 4;
                Check((f.Player.DeckZone.Count == 0) == buffed, "Overseer buff and draw agree");
                return buffed;
            });

        Scenario("TIME_449", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, "CS2_029");
            LostTimeDeck(f.Player, "CS2_182");
            LostTimeDeck(f.Player, "CS2_182");
            f.Draw("CS2_182");
            f.Flush();
            f.Expected(card, 0, "Legacy with deck minions");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("CS2_023")));
                f.Expected(card, 1, "Legacy lights after last minion draw");
            }
        }, (f, card) => PlayCardTask.Spell(f.Player, card),
            (f, card) => {
                Check(f.Player.Hero.AttackDamage == 4, "Legacy unconditional hero attack");
                return f.Player.HandZone.OfType<Minion>().All(m => m.AttackDamage == 8);
            });

        Scenario("TIME_614", (f, card, met) => {
            f.Summon("CS2_200", f.Game.Player2);
            f.Player.Hero.Damage = 5;
            f.Process(EndTurnTask.Any(f.Player));
            f.Process(EndTurnTask.Any(f.Game.Player2));
            f.Expected(card, 0, "Liferender ignores previous-turn Health change");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_007"), f.Player.Hero));
                f.Expected(card, 1, "Liferender lights in healing flush");
            }
        }, (f, card) => f.Player.Options().First(t => t.Source?.Id == card.Id),
            (f, card) => f.Game.Player2.BoardZone.Single().Damage == 6);

        Scenario("TIME_702", (f, card, met) => {
            f.Summon("CS2_231", f.Player);
            f.Flush();
            f.Expected(card, 0, "summon does not light Ebb and Flow");
            if (met)
            {
                f.Process(PlayCardTask.Minion(f.Player, (Minion)f.Draw("CS2_231")));
                f.Expected(card, 1, "Ebb and Flow lights in minion play flush");
            }
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero),
            (f, card) => {
                Check(f.Game.Player2.Hero.Damage == 3, "Ebb and Flow unconditional damage");
                return f.Player.Hero.Armor == 5;
            });

        Scenario("TIME_703", (f, card, met) => {
            f.Player.Hero.Damage = 14;
            f.Flush();
            f.Expected(card, 0, "Dodo above ten Health");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Player.Hero));
                f.Expected(card, 1, "Dodo lights at exactly ten Health in damage flush");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => {
                int count = f.Player.BoardZone.Count(m => m.Card.Id == card.Card.Id);
                Check(((Minion)card).AttackDamage == (count == 2 ? 10 : 5), "Dodo buff and copy agree");
                return count == 2;
            });

        Scenario("TIME_750", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, "CS2_182");
            f.Draw("CS2_182");
            f.Draw("CS2_032");
            f.Flush();
            f.Expected(card, 0, "cheap minion and expensive spell do not light Strike");
            if (met)
            {
                f.Draw("TIME_856");
                f.Flush();
                f.Expected(card, 1, "Strike lights in five-cost minion draw flush");
            }
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero),
            (f, card) => {
                Check(f.Game.Player2.Hero.Damage == 3, "Strike unconditional damage");
                return f.Player.DeckZone.Count == 0;
            });
    }
}
