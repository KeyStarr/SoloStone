using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-be", ProofKind.PoweredUp)]
    private static void RunBe()
    {
        var ids = new[] { "BE_169", "BE_171", "BE_303", "BE_559", "BE_560", "BE_564", "BE_877",
            "CATA_111", "CATA_554", "CATA_132", "CATA_308", "CATA_557", "MEND_041" };
        Check(ids.All(id => PoweredUp.HasCondition(Cards.FromId(id))),
            "missing BE/Cataclysm conditions: " + string.Join(",", ids.Where(id => !PoweredUp.HasCondition(Cards.FromId(id)))));

        Scenario("BE_169", (f, card, met) => {
            f.Flush(); f.Expected(card, 0, "Mantid unarmed");
            if (met)
            {
                f.Process(PlayCardTask.Any(f.Player, f.Draw("CS2_106")));
                f.Expected(card, 1, "Mantid lights in weapon equip flush");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var minion = (Minion)card;
            Check(minion.AttackDamage == (minion.IsRush ? 4 : 3)
                && minion.Health == (minion.IsRush ? 4 : 3), "Mantid conditional +1/+1 and Rush");
            return minion.IsRush;
        });

        foreach (string id in new[] { "BE_171", "BE_303" })
            Scenario(id, (f, card, met) => {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("GAME_005")));
                f.Expected(card, 0, id + " schoolless spell does not qualify");
                for (int i = 0; i < 2; i++)
                {
                    f.Mana(10);
                    f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("BE_842"), f.Game.Player2.Hero));
                    Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, id + " exactly one held school");
                    f.Expected(card, 0, id + " repeated school does not qualify");
                }
                if (met)
                {
                    f.Mana(10);
                    f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CORE_CS2_029"), f.Game.Player2.Hero));
                    f.Expected(card, 1, id + " lights in second-school flush");
                }
                f.Game.Player2.Hero.Damage = 0;
                f.Summon("CS2_200", f.Game.Player2);
            }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
                if (card.Card.Id == "BE_171")
                    return f.Player.BoardZone.Count(m => m.Card.Id == card.Card.Id) == 2;
                int damage = f.Game.Player2.BoardZone.Single().Damage;
                Check(f.Game.Player2.Hero.Damage == damage && (damage == 0 || damage == 4),
                    "Scholar damage reaches all enemies");
                return damage == 4;
            });

        Scenario("BE_559", (f, card, met) => {
            f.Summon("CS2_200", f.Game.Player2);
            f.Flush(); f.Expected(card, 0, "Show of Force before armor gain");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("EX1_606")));
                f.Expected(card, 1, "Show of Force lights in armor spell flush");
                f.Player.Hero.Armor = 0;
                f.Flush(); f.Expected(card, 1, "lost armor still qualifies");
            }
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.BoardZone.Single()), (f, card) => {
            var target = (Minion)f.Game.IdEntityDic.Values.Single(e => e.Card.Id == "CS2_200"
                && e.Controller == f.Game.Player2 && (e.Zone == f.Game.Player2.BoardZone || e.Zone == f.Game.Player2.GraveyardZone));
            Check(target.Damage == (target.Zone.Type == EngineZone.GRAVEYARD ? 0 : 3), "Show of Force destroys or deals three");
            return target.Zone.Type == EngineZone.GRAVEYARD;
        });

        Scenario("BE_560", (f, card, met) => {
            f.Player.Hero.Armor = 9;
            f.Flush(); f.Expected(card, 0, "Brawler at nine Armor");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("EX1_606")));
                f.Expected(card, 1, "Brawler lights in armor spell flush");
                f.Player.Hero.Armor = 10;
                f.Flush(); f.Expected(card, 1, "Brawler at exact threshold");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var minion = (Minion)card;
            Check(minion.Health == minion.AttackDamage && (minion.Health == 6 || minion.Health == 10),
                "Brawler conditional +4/+4");
            return minion.AttackDamage == 10;
        });

        foreach (string id in new[] { "BE_564", "BE_877" })
            Scenario(id, (f, card, met) => {
                var victim = f.Summon("CS2_231", met ? f.Player : f.Game.Player2);
                f.Flush(); f.Expected(card, 0, id + " before death");
                if (id == "BE_877") ExpectedBeCataclysmCost(f, card, 2);
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), victim));
                f.Expected(card, met ? 1 : 0, id + " friendly-only death flush");
                Check(victim.Zone.Type == EngineZone.GRAVEYARD, "death enabler resolved");
                if (id == "BE_877") ExpectedBeCataclysmCost(f, card, met ? 0 : 2);
            }, (f, card) => card is Minion ? PlayCardTask.Minion(f.Player, card)
                : PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero), (f, card) => {
                if (card is Minion)
                {
                    Check(f.Game.Player2.Hero.Damage == 0 || f.Game.Player2.Hero.Damage == 7, "Terror attacks sole enemy");
                    return f.Game.Player2.Hero.Damage == 7;
                }
                Check(f.Game.Player2.Hero.Damage == 2 && (f.Player.UsedMana == 0 || f.Player.UsedMana == 2),
                    "Cavern always deals two, pays discounted or base cost");
                return f.Player.UsedMana == 0;
            });
    }

    private static void ExpectedBeCataclysmCost(Fixture f, IPlayable card, int cost)
    {
        int printed = -1;
        foreach (var entry in f.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (entity?.Entity == card.Id && entity.Tags.Any(t => t.Name == (int)GameTag.COST))
                printed = entity.Tags.Last(t => t.Name == (int)GameTag.COST).Value;
            if (entry.HasTagChange && entry.TagChange.Entity == card.Id && entry.TagChange.Tag == (int)GameTag.COST)
                printed = entry.TagChange.Value;
        }
        Check(card.Cost == cost && card[GameTag.COST] == cost && printed == cost,
            card.Card.Id + " engine/printed COST expected " + cost + ", got " + card.Cost + "/" + printed);
    }

    private static void RunBeCataclysmConditions()
    {
        Scenario("CATA_111", (f, card, met) => {
            f.Flush(); f.Expected(card, 0, "Broodmother cannot count itself as held Dragon");
            if (met)
            {
                f.Draw("CATA_111"); f.Flush();
                f.Expected(card, 1, "Broodmother lights in Dragon draw flush");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            Check(f.Player.UsedMana == 1 || f.Player.UsedMana == 3, "Broodmother refreshes exactly two Mana");
            return f.Player.UsedMana == 1;
        });

        Scenario("CATA_554", (f, card, met) => {
            f.Summon("CS2_182", f.Game.Player2); f.Summon("CS2_182", f.Game.Player2);
            f.Flush(); f.Expected(card, 0, "Earthen Roar without Dragon");
            if (met)
            {
                f.Draw("CATA_111"); f.Flush();
                f.Expected(card, 1, "Earthen Roar lights in Dragon draw flush");
            }
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.BoardZone.First()), (f, card) => {
            var first = f.Game.Player2.BoardZone.First(); var second = f.Game.Player2.BoardZone.Last();
            Check(first.Health == 1, "Earthen Roar always sets first target Health");
            if (f.Player.Choice != null)
            {
                Check(f.Player.Choice.Choices.SequenceEqual(new[] { second.Id }), "Dragon offers only second enemy");
                var decision = f.Game.GetPendingDecisions().Single();
                Check(f.Game.TryAnswer(new DecisionAnswer(decision.Id, f.Player.Id, new[] { second.Id }), out var error),
                    "Earthen Roar selection failed: " + error);
                f.Flush();
            }
            Check(f.Player.Choice == null && (second.Health == 1 || second.Health == 5), "Earthen Roar second target resolves");
            return second.Health == 1;
        });

        Scenario("CATA_132", (f, card, met) => {
            f.Flush(); f.Expected(card, 0, "Broodwatcher before spending");
            f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
            f.Expected(card, 0, "Broodwatcher four Mana is not ready");
            Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "Broodwatcher four remaining");
            if (met)
            {
                f.Mana(10);
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
                f.Expected(card, 1, "Broodwatcher lights in eighth-Mana flush");
                Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "Broodwatcher Ready display");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            int board = f.Player.BoardZone.Count(m => m.Card.Id == "CATA_132t");
            int hand = f.Player.HandZone.Count(m => m.Card.Id == "CATA_132t");
            Check((board == 2 && hand == 0) || (board == 0 && hand == 2),
                "Broodwatcher own four-Mana payment cannot unlock ready summons");
            return board == 2;
        });

        Scenario("CATA_308", (f, card, met) => {
            f.Summon("CS2_182", f.Game.Player2);
            f.Flush(); f.Expected(card, 0, "Medivh without controlled Legendary");
            ExpectedBeCataclysmCost(f, card, 5);
            if (met)
            {
                f.Process(PlayCardTask.Minion(f.Player, f.Draw("EX1_572")));
                f.Expected(card, 1, "Medivh lights in Legendary play flush");
                ExpectedBeCataclysmCost(f, card, 1);
            }
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            Check(f.Game.Player2.BoardZone.Single().Damage == 4 && (f.Player.UsedMana == 1 || f.Player.UsedMana == 5),
                "Medivh deals four and pays discounted or base cost");
            return f.Player.UsedMana == 1;
        });

        Scenario("CATA_557", (f, card, met) => {
            f.Summon("CS2_200", f.Game.Player2);
            f.Flush(); f.Expected(card, 0, "Sylvanas before another copy");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CATA_557"), f.Player.Hero));
                f.Expected(card, 1, "Sylvanas lights in first-copy flush");
            }
        }, (f, card) => PlayCardTask.Any(f.Player, card,
            f.Player.Hero.Damage == 3 ? null : f.Game.Player2.BoardZone.Single()), (f, card) => {
            Check(f.Game.Player2.BoardZone.Single().Damage == 3, "Sylvanas minion takes three in both modes");
            Check(f.Game.Player2.Hero.Damage == 0 || f.Game.Player2.Hero.Damage == 3, "Sylvanas conditional all-enemy damage");
            return f.Game.Player2.Hero.Damage == 3;
        });

        Scenario("MEND_041", (f, card, met) => {
            f.Process(PlayCardTask.Minion(f.Player, f.Draw("CS2_231")));
            f.Expected(card, 1, "current-turn minion does not change previous-turn condition");
            f.Process(EndTurnTask.Any(f.Player));
            f.Process(EndTurnTask.Any(f.Game.Player2));
            f.Expected(card, 0, "previous own-turn minion clears Wildspeaker");
            if (met)
            {
                f.Process(PlayCardTask.Spell(f.Player, f.Draw("GAME_005")));
                f.Expected(card, 0, "spell does not erase previous-turn minion");
                f.Process(EndTurnTask.Any(f.Player));
                f.Process(EndTurnTask.Any(f.Game.Player2));
                f.Expected(card, 1, "Wildspeaker lights in turn-start flush after no-minion turn");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            Check(f.Player.UsedMana == 2 || f.Player.UsedMana == 5, "Wildspeaker refreshes exactly three Mana");
            return f.Player.UsedMana == 2;
        });

        var bounced = new Fixture();
        var wildspeaker = bounced.Draw("MEND_041");
        bounced.Process(PlayCardTask.Minion(bounced.Player, wildspeaker));
        bounced.Mana(10);
        bounced.Process(PlayCardTask.SpellTarget(bounced.Player, bounced.Draw("EX1_144"), (Minion)wildspeaker));
        bounced.Process(EndTurnTask.Any(bounced.Player));
        bounced.Process(EndTurnTask.Any(bounced.Game.Player2));
        bounced.Expected(wildspeaker, 0, "bounced Wildspeaker still played a minion last turn");
        bounced.Mana(10);
        bounced.Process(PlayCardTask.Minion(bounced.Player, wildspeaker));
        Check(bounced.Player.UsedMana == 3, "bounced Wildspeaker keeps prior-turn play and Shadowstep cost");

        var replay = new Fixture();
        var triumph = replay.Draw("CATA_557");
        replay.Process(PlayCardTask.SpellTarget(replay.Player, triumph, replay.Player.Hero));
        replay.Player.HandZone.Add(replay.Player.GraveyardZone.Remove(triumph));
        replay.Flush();
        replay.Expected(triumph, 1, "prior committed occurrence survives returning same spell to hand");
        replay.Mana(10);
        replay.Process(PlayCardTask.Spell(replay.Player, triumph));
        Check(replay.Game.Player2.Hero.Damage == 3, "current play excluded, prior occurrence still unlocks all enemies");
    }
}
