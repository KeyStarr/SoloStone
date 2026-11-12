using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-batch10", ProofKind.PoweredUp)]
    private static void RunBatch10()
    {
        var ids = new[] { "BE_283", "BE_554", "BE_565", "FIR_908", "MEND_043", "TLC_434", "TLC_462",
            "TIME_448", "TIME_609", "TIME_609t1", "TIME_609t2" };
        Check(ids.All(id => PoweredUp.HasCondition(Cards.FromId(id))), "missing batch 10 conditions: "
            + string.Join(", ", ids.Where(id => !PoweredUp.HasCondition(Cards.FromId(id)))));

        Scenario("BE_283", (f, card, met) => {
            f.Summon("CS2_200", f.Player);
            f.Summon("CS2_200", f.Game.Player2);
            f.Player[GameTag.CORPSES] = met ? 1 : 0;
            f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            int spent = f.Player[GameTag.CORPSES_SPENT_THIS_GAME];
            Check(spent == 0 || spent == 1, "Bloodfiend spends available Corpse");
            Check(f.Player.BoardZone.First(m => m.Id != card.Id).Damage == spent
                && f.Game.Player2.BoardZone.Single().Damage == spent && ((Minion)card).Damage == 0,
                "Bloodfiend damage follows spend on both boards, excluding source");
            return spent == 1;
        });

        Scenario("BE_554", (f, card, met) => {
            f.Summon(met ? "BE_730t" : "CS2_182", f.Player);
            f.Summon("BE_730t", f.Game.Player2); // An enemy Mech alone never enables the repeat.
            f.Summon("CS2_182", f.Game.Player2);
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            int damage = f.Game.Player2.Hero.Damage;
            Check((damage == 3 || damage == 6) && f.Game.Player2.BoardZone.Count == 2 - damage / 3,
                "Bombardment destroys one enemy per three-damage burst");
            return damage == 6;
        });

        Scenario("BE_565", (f, card, met) => {
            for (int i = 0; i < (met ? 5 : 4); i++)
            {
                var victim = f.Summon("CS2_231", f.Player);
                new DestroyTask(EntityType.TARGET).Process(f.Game, f.Player, card, victim);
                f.Game.DeathProcessingAndAuraUpdate();
            }
            f.Summon("BE_873t", f.Player);
            Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == (met ? 0 : 1), "Sha'kar ready boundary");
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            bool active = f.Player.AppliedEnchantments?.Any(e => e.Card.Id == "BE_565e") ?? false;
            Check(f.Player.BoardZone.Single(m => m.Card.Id == "BE_873t").HasCharge == active,
                "Sha'kar permanent enchantment grants Rush minion Charge");
            return active;
        });

        Scenario("FIR_908", (f, card, met) => {
            f.Summon("CS2_182", f.Player);
            if (met) f.Process(HeroPowerTask.Any(f.Player, f.Game.Player2.Hero));
        }, (f, card) => f.Player.Options().First(t => t.Source?.Id == card.Id), (f, card) => {
            var target = f.Player.BoardZone.Single(m => m.Id != card.Id);
            Check((target.AttackDamage == 4 && target.Health == 5 && !target.IsRush)
                || (target.AttackDamage == 5 && target.Health == 7 && target.IsRush), "Chameleon +1/+2 and Rush");
            return target.IsRush;
        });

        int draws = 0;
        Scenario("MEND_043", (f, card, met) => {
            f.Process(PlayCardTask.Minion(f.Player, f.Draw("CS2_231")));
            f.Process(EndTurnTask.Any(f.Player));
            f.Process(EndTurnTask.Any(f.Game.Player2));
            if (met)
            {
                f.Process(EndTurnTask.Any(f.Player));
                f.Process(EndTurnTask.Any(f.Game.Player2));
            }
            f.Player.Hero.Armor = 0;
            draws = f.Player.NumCardsDrawnThisTurn;
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            int drawn = f.Player.NumCardsDrawnThisTurn - draws;
            Check((drawn == 1 || drawn == 2) && f.Player.Hero.Armor == drawn * 3,
                "Heartroot Stones repeats both draw and Armor");
            return drawn == 2;
        });

        Scenario("TLC_434", (f, card, met) => {
            f.Player[GameTag.CORPSES] = met ? 5 : 4;
            f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            bool keptAll = f.Player.Choice == null;
            if (!keptAll) f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices.First()));
            int awarded = f.Player.HandZone.Count(p => p[GameTag.DISPLAYED_CREATOR] == card.Id);
            Check(awarded == (keptAll ? 3 : 1)
                && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == (keptAll ? 5 : 0),
                "Paleomancy keeps all three only after spending five Corpses");
            return keptAll;
        });

        Scenario("TLC_462", (f, card, met) => {
            if (met)
            {
                Check(new EffectContext(f.Game, f.Player.Id, f.Player.Hero.Id)
                    .Discover(new[] { Cards.FromId("GAME_005") }), "Artifacts Discover opens");
                f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices.First()));
            }
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            int cost = f.Player.BoardZone.Single().Card.Cost;
            Check(cost == 2 || cost == 4, "Artifacts exact two/four-Cost summon");
            return cost == 4;
        });

        IPlayable held = null;
        Scenario("TIME_448", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            LostTimeDeck(f.Player, met ? "CS2_029" : "CS2_182");
            held = f.Draw("CS2_200");
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            int choices = Batch10AnswerAll(f);
            Check(choices == 2, "Solitude always Discovers twice");
            Check(held.Cost == 4 || held.Cost == 6, "Solitude discount reaches pre-existing minion");
            return held.Cost == 4;
        });

        Scenario("TIME_609", (f, card, met) => {
            if (met) f.Process(PlayCardTask.Minion(f.Player, f.Draw("TIME_609t2")));
            else f.Summon("TIME_609t2", f.Player); // Summoning a partner does not count as playing it.
            f.Summon("CS2_200", f.Game.Player2);
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            int damage = f.Game.Player2.Hero.Damage;
            Check((damage == 2 || damage == 4) && f.Game.Player2.BoardZone.Single().Damage == damage,
                "Sylvanas repeats fixed damage to every enemy");
            return damage == 4;
        });

        Scenario("TIME_609t1", (f, card, met) => {
            if (met) f.Process(PlayCardTask.Minion(f.Player, f.Draw("TIME_609t2")));
            else f.Summon("TIME_609t2", f.Player);
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            int choices = Batch10AnswerAll(f);
            Check((choices == 1 || choices == 2)
                && f.Player.HandZone.Count(p => p[GameTag.DISPLAYED_CREATOR] == card.Id) == choices,
                "Alleria keeps one spell per Discover");
            return choices == 2;
        });

        Minion deck = null;
        Scenario("TIME_609t2", (f, card, met) => {
            LostTimeClearDeck(f.Player);
            deck = (Minion)LostTimeDeck(f.Player, "CS2_182");
            if (met) f.Process(PlayCardTask.Minion(f.Player, f.Draw("TIME_609")));
            else f.Summon("TIME_609", f.Player);
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            Check((deck.AttackDamage == 5 && deck.Health == 6) || (deck.AttackDamage == 6 && deck.Health == 7),
                "Vereesa gives deck minions +1/+1 once or twice");
            return deck.AttackDamage == 6;
        });
        Console.WriteLine("PASS PoweredUp batch 10: 11 conditional cards, true/false glow and outcomes");
    }

    private static int Batch10AnswerAll(Fixture f)
    {
        int count = 0;
        while (f.Player.Choice != null)
        {
            Check(++count <= 3, "bounded sequential Discover choices");
            f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices.First()));
        }
        return count;
    }
}
