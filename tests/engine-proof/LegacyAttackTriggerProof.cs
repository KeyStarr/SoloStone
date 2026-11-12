using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class LegacyAttackTriggerProof
{
    [GroupProof("legacy-attack-trigger-effects")]
    internal static void Run()
    {
        foreach (string id in new[] { "CS2_097", "EX1_130", "EX1_289", "EX1_363e", "EX1_411", "EX1_594", "tt_010" })
        foreach (bool positive in new[] { false, true })
        {
            var g = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1, SkipMulligan = true, History = true,
                Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            g.StartGame(); var p = g.Player1;
            if (id is "CS2_097" or "EX1_411")
            {
                var weapon = (Weapon)Play(g, p, id); p.Hero.Damage = 10;
                if (id == "CS2_097")
                {
                    if (positive) Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Truesilver hero attack");
                    else Attack(g, Summon(p, "CS2_182"), p.Opponent.Hero);
                    Core2026Proof.Check(p.Hero.Damage == (positive ? 8 : 10), "Truesilver heals only its attacking hero");
                }
                else
                {
                    var target = positive ? (ICharacter)Summon(p.Opponent, "CS2_182") : p.Opponent.Hero;
                    Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, target)), "Gorehowl attack");
                    Core2026Proof.Check(positive ? p.Hero.Weapon == weapon && weapon.AttackDamage == 6 && weapon.Durability == 1 : p.Hero.Weapon == null,
                        "Gorehowl loses Attack only against minions, durability against hero");
                }
            }
            else if (id == "EX1_363e")
            {
                var host = Summon(p, "CS2_182"); Play(g, p, "EX1_363", host); int hand = p.HandZone.Count;
                Attack(g, positive ? host : Summon(p, "CS2_182"), p.Opponent.Hero);
                Core2026Proof.Check(p.HandZone.Count == hand + (positive ? 1 : 0), "Wisdom draws only for enchanted attacker");
            }
            else
            {
                var secret = Play(g, p, id); var defender = Summon(p, "CS2_182");
                if (!positive && id == "EX1_130") Attack(g, Summon(p, "CS2_182"), p.Opponent.Hero);
                else
                {
                    End(g);
                    if (id == "tt_010")
                    {
                        Play(g, p.Opponent, positive ? "CS2_008" : "GAME_005", positive ? defender : null);
                        Core2026Proof.Check(defender.Damage == 0 && (!positive || p.BoardZone.Any(m => m.Card.Id == "tt_010a" && m.Damage == 1)), "Spellbender redirects targeted spell to token");
                    }
                    else if (id == "EX1_594" && !positive)
                    {
                        Play(g, p.Opponent, "CS2_082"); Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)), "hero is not Vaporize source");
                    }
                    else
                    {
                        var attacker = Summon(p.Opponent, "CS2_182");
                        Attack(g, attacker, positive ? p.Hero : defender);
                        if (positive && id is "EX1_130" or "EX1_594") Core2026Proof.Check(p.Hero.Damage == 0, "attack prevented before hero damage");
                        if (positive && id == "EX1_594") Core2026Proof.Check(attacker.Zone == p.Opponent.GraveyardZone, "Vaporize destroys attacker");
                        if (id == "EX1_289") Core2026Proof.Check(p.Hero.Armor == (positive ? 4 : 0), "Ice Barrier armor applied only when hero attacked");
                    }
                }
                Core2026Proof.Check((secret.Zone == p.GraveyardZone) == positive, "secret reveals only for matching event: " + id);
            }
            Console.WriteLine($"LEGACY-ATTACK {id} {(positive ? "positive" : "negative")} PASS");
        }
    }
}
