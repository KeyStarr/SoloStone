using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Issues #178 (temporary Mana above maximum, Blizzard patch 29.0 notes), #180 (Garona Halforcen cuts remaining Health) and
// #182 (Lingering Spirit's excess healing counts healing bonuses). Each case is pinned in the rules research of 2026-11-18.
internal static class ManaAndHealRulesProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, message);
    [GroupProof("mana-and-heal-rules")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { CoinAtFullCrystals, TemporaryStacksAboveMaximum, TemporaryAboveRaisedMaximum, TemporaryWhileOverloaded,
            TemporaryManaSpentFirst, GaronaHalvesRemainingHealth, GaronaOddHealthAndNoLlane, SpiritExcessIncludesBonus, SpiritExcessWithDoubling,
            SpiritAtFullHealth, SpiritWithConversion, NextTurnManaAboveMaximum, WildGrowthTemporaryManaDoesNotCountAsCrystal, WildGrowthAtTenCrystals, GaronaIsNotDamage })
            try { test(); } catch (Exception e) { failures.Add(test.Method.Name + ": " + e.Message); }
        Verify(failures.Count == 0, string.Join("; ", failures));
        Console.WriteLine("PASS mana and heal rules: temporary Mana above maximum; Garona remaining Health; Lingering Spirit excess with bonus");
    }
    private static void PlayNoReset(Game g, Controller p, string id)
    {
        var card = Add(p, id);
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
        Verify(task != null && g.Process(task), "play " + id);
    }
    private static (Game, Controller) Start()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        return (g, p);
    }
    private static void CoinAtFullCrystals()
    {
        var (g, p) = Start();
        PlayNoReset(g, p, "GAME_005");
        Verify(p.BaseMana == 10 && p.TemporaryMana == 1 && p.RemainingMana == 11, $"Coin at 10/10 gives 11/10, got {p.RemainingMana}/{p.BaseMana} temp {p.TemporaryMana}");
    }
    private static void TemporaryStacksAboveMaximum()
    {
        // Unpinned: the 29.0 notes name no bound on temporary Mana, so it is not capped.
        var (g, p) = Start();
        PlayNoReset(g, p, "GAME_005"); PlayNoReset(g, p, "EX1_169");
        Verify(p.TemporaryMana == 2 && p.RemainingMana == 12, $"Coin then Innervate at 10/10 gives 12, got {p.RemainingMana}");
    }
    private static void TemporaryAboveRaisedMaximum()
    {
        var (g, p) = Start();
        p[GameTag.MAXRESOURCES] = 15; p.BaseMana = 15;
        PlayNoReset(g, p, "GAME_005");
        Verify(p.RemainingMana == 16, $"Coin at 15/15 gives 16, got {p.RemainingMana}");
    }
    private static void TemporaryWhileOverloaded()
    {
        var (g, p) = Start();
        p.OverloadLocked = 2;
        PlayNoReset(g, p, "GAME_005"); PlayNoReset(g, p, "EX1_169"); PlayNoReset(g, p, "GAME_005");
        Verify(p.TemporaryMana == 3 && p.RemainingMana == 11, $"overloaded 10/10 (8 usable) with Coin, Innervate, Coin has 11, got {p.RemainingMana} temp {p.TemporaryMana}");
    }
    private static void TemporaryManaSpentFirst()
    {
        var (g, p) = Start();
        PlayNoReset(g, p, "GAME_005");
        var card = Add(p, "CS2_182"); // 4 Mana
        var task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
        Verify(g.Process(task) && p.TemporaryMana == 0 && p.UsedMana == 3 && p.RemainingMana == 7, $"temporary Mana spent first, got temp {p.TemporaryMana} used {p.UsedMana}");
    }
    private static (Game, Controller, Controller) Garona(int damage, int armor, bool llane)
    {
        var (g, p) = Start(); var e = p.Opponent;
        e.Hero.Damage = damage; e.Hero.Armor = armor;
        if (llane) Add(e, "TIME_875t");
        Play(g, p, "TIME_875");
        g.DeathProcessingAndAuraUpdate();
        return (g, p, e);
    }
    private static void GaronaHalvesRemainingHealth()
    {
        var (g, p, e) = Garona(8, 5, true); // 22 of 30 remaining, 5 armor
        Verify(e.Hero.Health == 11 && e.Hero.BaseHealth == 30 && e.Hero.Armor == 5 && e.Hero.Damage == 19,
            $"22 remaining becomes 11, max 30, armor 5, got {e.Hero.Health}/{e.Hero.BaseHealth} armor {e.Hero.Armor}");
        Verify(!e.HandZone.Any(c => c.Card.Id == "TIME_875t"), "Llane destroyed");
    }
    private static void GaronaOddHealthAndNoLlane()
    {
        // Odd Health rounds the remaining Health down (single-sourced: the removed amount rounds up).
        var (g, p, e) = Garona(15, 0, true);
        Verify(e.Hero.Health == 7 && e.Hero.BaseHealth == 30, $"15 remaining becomes 7, got {e.Hero.Health}/{e.Hero.BaseHealth}");
        (g, p, e) = Garona(8, 5, false);
        Verify(e.Hero.Health == 22 && e.Hero.Armor == 5, "no Llane in hand: nothing changes");
    }
    // Lingering Spirit: an enemy hero is the only enemy, so the excess lands there.
    private static int Excess(int heroDamage, Action<Controller> setup)
    {
        var (g, p) = Start();
        setup(p);
        p.Hero.Damage = heroDamage;
        var spirit = Summon(p, "CAP_803");
        Kill(g, spirit);
        g.DeathProcessingAndAuraUpdate();
        return p.Opponent.Hero.Damage;
    }
    private static void SpiritExcessIncludesBonus()
    {
        int dmg = Excess(1, p => p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 2);
        Verify(dmg == 4, $"hero 1 damaged, +2 healing bonus: 5 proposed, 1 restored, 4 excess; got {dmg}");
    }
    private static void SpiritExcessWithDoubling()
    {
        int dmg = Excess(2, p => { p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 2; p.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] = 1; });
        Verify(dmg == 8, $"(3+2)*2 = 10 proposed, 2 restored, 8 excess; got {dmg}");
    }
    private static void SpiritAtFullHealth()
    {
        int dmg = Excess(0, p => p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 2);
        Verify(dmg == 5, $"full health with +2 bonus: all 5 proposed is excess; got {dmg}");
    }
    private static void SpiritWithConversion()
    {
        var (g, p) = Start();
        p.RestoreToDamage = true; p[GameTag.OUTGOING_HEALING_ADJUSTMENT] = 2;
        var spirit = Summon(p, "CAP_803");
        Kill(g, spirit); g.DeathProcessingAndAuraUpdate();
        Verify(p.Hero.Damage == 5 && p.Opponent.Hero.Damage == 0, $"converted healing: hero takes 5, no excess; got {p.Hero.Damage}/{p.Opponent.Hero.Damage}");
    }
    // Emberscarred Whelp (FIR_927) and other next-turn temporary Mana: the 29.0 note covers all temporary Mana.
    private static void NextTurnManaAboveMaximum()
    {
        foreach (int start in new[] { 9, 10 })
        {
            var (g, p) = Start();
            p.BaseMana = start; p.UsedMana = 0;
            SabberStoneCore.Actions.TemporaryRules.NextTurnMana(p);
            End(g); End(g);
            Verify(p.BaseMana == 10 && p.TemporaryMana == 1 && p.RemainingMana == 11,
                $"next-turn temporary Mana at base {start} gives 11/10, got {p.RemainingMana}/{p.BaseMana} temp {p.TemporaryMana}");
        }
    }
    // Excess Mana depends on permanent crystals only ("if you have 10 Mana Crystals").
    private static (bool excess, int baseMana) WildGrowth(int baseMana, int temporary)
    {
        var (g, p) = Start();
        p.BaseMana = baseMana; p.UsedMana = 0; p.TemporaryMana = temporary;
        PlayNoReset(g, p, "CS2_013");
        return (p.HandZone.Any(c => c.Card.Id == "CS2_013t"), p.BaseMana);
    }
    private static void WildGrowthTemporaryManaDoesNotCountAsCrystal()
    {
        foreach (int temporary in new[] { 3, 4 }) // 4 leaves exactly 10 available after paying for Wild Growth (9 + 4 - 3)
        {
            var (excess, baseMana) = WildGrowth(9, temporary);
            Verify(!excess && baseMana == 10, $"base 9 + {temporary} temporary: Wild Growth gives the 10th crystal and no Excess Mana, got excess={excess} base={baseMana}");
        }
    }
    private static void WildGrowthAtTenCrystals()
    {
        var (excess, baseMana) = WildGrowth(10, 0);
        Verify(excess && baseMana == 10, $"base 10: Excess Mana card, no new crystal, got excess={excess} base={baseMana}");
    }
    private static void GaronaIsNotDamage()
    {
        var (g, p) = Start(); var e = p.Opponent;
        e.Hero.Damage = 8; e.Hero.Armor = 5; Add(e, "TIME_875t");
        int damageFacts = g.HistoryFacts.Query.Damage().Count(), cursor = g.PowerHistory.Full.Count;
        Play(g, p, "TIME_875"); g.DeathProcessingAndAuraUpdate();
        Verify(e.Hero.Health == 11, "Garona resolved");
        Verify(g.HistoryFacts.Query.Damage().Count() == damageFacts, "no damage fact");
        Verify(!g.PowerHistory.Full.Skip(cursor).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Any(t => t.EntityId == e.Hero.Id
            && (t.Tag == GameTag.PREDAMAGE)), "no PREDAMAGE tag change on the hero");
    }
}
