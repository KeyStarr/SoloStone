using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class PoweredUpGlowTests
{
    // Every live card registration must be classified in the powered-up-inventory TSVs, so a conditional card cannot
    // silently lack its hand glow (2026-10-21 playtest: CORE_KAR_062, Netherspite Historian, never glowed; legacy Core,
    // RebalancedCore2026 and Reprints were not inventoried). G = glow predicate in PoweredUp, K = Kindred (4028), O =
    // Outcast, C = Combo, N = no pre-play condition. Each G needs a true/false outcome scenario.
    private static void InventoryIsComplete()
    {
        var policy = OfflinePoolPolicy.Load();
        var live = LocalCardCatalog.Supported.Select(c => c.Id)
            .Concat(LocalCardCatalog.SupplementalGeneratedIds).Concat(LocalCardCatalog.LegacyGeneratedPublicIds)
            .Concat(new[] { "CORE_SW_066", "CORE_CS2_032", "TIME_045", "TLC_101", "TLC_101e", "TLC_226", "DINO_435" })
            .Concat(StandardSets.CollectibleIds).Concat(EffectModules.ByDbfId.Values)
            .Concat(RebalancedCore2026.ByDbfId.Keys.Select(id => Cards.FromAssetId(id).Id))
            .Concat(Reprints.ByDbfId.Keys.Select(id => Cards.FromAssetId(id).Id))
            .Concat(policy.Members.Select(c => c.CardId)).Concat(policy.Dependencies.Select(c => c.CardId))
            .ToHashSet(StringComparer.Ordinal);
        var rows = new[] { "tests/native-match/powered-up-inventory.tsv" }
            .Concat(Directory.GetFiles("tests/native-match/powered-up-inventory", "*.tsv").OrderBy(path => path, StringComparer.Ordinal))
            .SelectMany(path => File.ReadAllLines(path).Skip(1)).Select(line => line.Split('\t')).ToArray();
        Check(rows.All(row => row.Length == 5 && "GKOCN".Contains(row[1], StringComparison.Ordinal)
            && row[1].Length == 1 && !string.IsNullOrWhiteSpace(row[2]) && row[3] is "existing" or "legacy"), "invalid inventory row");
        var classified = rows.Select(row => row[0]).ToHashSet(StringComparer.Ordinal);
        Check(rows.Length == classified.Count, "duplicate inventory identity");
        Check(live.SetEquals(classified), "unclassified live registrations: " + string.Join(", ", live.Except(classified))
            + "; stale inventory: " + string.Join(", ", classified.Except(live)));
        foreach (var row in rows)
        {
            var card = Cards.FromId(row[0]);
            Check(card.Name == row[4], "inventory identity changed: " + row[0]);
            bool valid = row[1] switch {
                "G" => PoweredUp.RegisteredIds.Contains(card.Id),
                "K" => card[(GameTag)4028] == 1,
                "O" => card[GameTag.OUTCAST] == 1,
                "C" => card.Combo,
                _ => !PoweredUp.HasCondition(card) && !card.Combo
            };
            Check(valid, "inventory classification disagrees with engine: " + row[0] + "=" + row[1]);
        }
        var glowIds = rows.Where(row => row[1] == "G").Select(row => row[0]).ToArray();
        Check(glowIds.All(OutcomeCases.Contains), "each G classification needs a true/false outcome scenario: "
            + string.Join(", ", glowIds.Except(OutcomeCases)));
        Console.WriteLine("PASS PoweredUp inventory: " + rows.Length + " live identities classified; "
            + glowIds.Length + " conditional outcomes covered");
    }

    // The first conditional legacy, Core 2026 and reprint cards fixed after that playtest (see InventoryIsComplete
    // above).
    [GroupProof("powered-up-legacy-core", ProofKind.PoweredUp)]
    private static void RunLegacyCore()
    {
        foreach (string id in new[] { "CORE_BRM_013", "CORE_KAR_062", "CORE_LOE_039", "CORE_TRL_111" })
            Check(StandardSets.CollectibleIds.Contains(id) && PoweredUp.RegisteredIds.Contains(id),
                id + " Standard registration retains its shared glow predicate");
        foreach (string id in new[] { "CORE_KAR_062", "CORE_LOE_039" })
            Scenario(id, (f, card, met) => {
                // Enemy tribes never satisfy the friendly hand/board conditions.
                f.Summon(id == "CORE_KAR_062" ? "EX1_561" : "EX1_556", f.Game.Player2);
                if (!met) return; // Historian alone and Gorillabot alone must stay dark.
                if (id == "CORE_KAR_062") f.Draw("EX1_561");
                else f.Summon("EX1_556", f.Player);
            }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card), (f, card) => {
                bool discovered = f.Player.Choice != null;
                if (discovered)
                {
                    f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices.First()));
                    Check(f.Player.HandZone.Any(p => p[GameTag.DISPLAYED_CREATOR] == card.Id), card.Card.Id + " Discover adds a card");
                }
                return discovered;
            });

        Scenario("CORE_TRL_111", (f, card, met) => {
            f.Summon("CS2_120", f.Game.Player2);
            f.Summon(met ? "CS2_120" : "CS2_182", f.Player);
        },
            (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
                Check(f.Player.Hero.Weapon == card, "Hatchet equipped");
                Check(((Weapon)card).Durability == 2 || ((Weapon)card).Durability == 3, "Hatchet durability");
                return ((Weapon)card).Durability == 3;
            });

        int draws = 0;
        Scenario("CORE_BRM_013", (f, card, met) => {
            if (!met) f.Draw("CS2_231");
            draws = f.Player.NumCardsDrawnThisTurn;
        }, (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero), (f, card) => {
            Check(f.Game.Player2.Hero.Damage == 3, "Quick Shot always deals three");
            int drawn = f.Player.NumCardsDrawnThisTurn - draws;
            Check(drawn == 0 || drawn == 1, "Quick Shot draws at most one");
            return drawn == 1;
        });

        Scenario("EX1_085", (f, card, met) => {
            for (int i = 0; i < (met ? 4 : 3); i++) f.Summon("CS2_182", f.Game.Player2);
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => {
                Check(f.Game.Player2.BoardZone.Count == 3 && (f.Player.BoardZone.Count == 1 || f.Player.BoardZone.Count == 2),
                    "Mind Control Tech steals exactly one at four, none at three");
                return f.Player.BoardZone.Any(m => m.Card.Id == "CS2_182");
            });

        Scenario("EX1_408", (f, card, met) => f.Player.Hero.Damage = met ? 18 : 17,
            (f, card) => PlayCardTask.SpellTarget(f.Player, card, f.Game.Player2.Hero), (f, card) => {
                Check(f.Game.Player2.Hero.Damage == 4 || f.Game.Player2.Hero.Damage == 6, "Mortal Strike damage");
                return f.Game.Player2.Hero.Damage == 6;
            });

        Scenario("EX1_409", (f, card, met) => {
            if (met) f.Process(PlayCardTask.Any(f.Player, f.Draw("CS2_106")));
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            var weapon = f.Player.Hero.Weapon;
            Check(weapon != null && weapon.Durability == 3
                && ((weapon.Card.Id == "EX1_409t" && weapon.AttackDamage == 1)
                    || (weapon.Card.Id == "CS2_106" && weapon.AttackDamage == 4)), "Upgrade equips 1/3 or buffs to 4/3");
            return weapon.Card.Id == "CS2_106";
        });

        Scenario("EX1_625", (f, card, met) => {
            if (met) f.Process(PlayCardTask.Spell(f.Player, f.Draw("EX1_625")));
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            string power = f.Player.Hero.HeroPower.Card.Id;
            Check(power == "EX1_625t" || power == "EX1_625t2", "Shadowform replaces power");
            return power == "EX1_625t2";
        });

        // Preserve the legacy special case and known third-cast downgrade; do not repair rules in glow work.
        foreach (string initialPower in new[] { "ICC_830p", "EX1_625t2" })
        {
            var f = new Fixture();
            var card = f.Draw("EX1_625");
            new ReplaceHeroPower(Cards.FromId(initialPower)).Process(f.Game, f.Player, card, null);
            f.Flush();
            bool met = initialPower == "ICC_830p";
            f.Expected(card, met ? 1 : 0, "Shadowform legacy starting power " + initialPower);
            f.Process(PlayCardTask.Spell(f.Player, card));
            f.Expected(card, 0, "Shadowform legacy branch reset");
            Check(f.Player.Hero.HeroPower.Card.Id == (met ? "EX1_625t2" : "EX1_625t"),
                "Shadowform legacy outcome disagrees with glow for " + initialPower);
        }
    }
}
