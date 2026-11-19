using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

// Owner ruling 2026-11-18: only a real hit is DAMAGE and only a real heal is HEAL; Reborn's 1-Health return and every other
// non-damage Health change (set-Health, halving) is labelled apart (REBORN, HEALTH_SET). The engine's damage / heal meta tells them apart.
internal static partial class Program
{
    private static string Dump(List<string> lines) => "\n" + string.Join("\n", lines);

    private static void HealthLabelsRealDamageAndHeal()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1;
        var yeti = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        var archer = EngineEntity.FromCard(player, Cards.FromId("CS2_189"), zone: player.HandZone);
        var touch = EngineEntity.FromCard(player, Cards.FromId("CS2_007"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, archer, yeti);
        PlayAndFlush(match, player, touch, yeti);
        Check(Kind(lines, "DAMAGE").Any(l => l.Contains("target=CS2_182#" + yeti.Id) && l.Contains("amount=1") && l.Contains("hp=5->4")), "real hit stays DAMAGE" + Dump(lines));
        Check(Kind(lines, "HEAL").Any(l => l.Contains("target=CS2_182#" + yeti.Id) && l.Contains("amount=1") && l.Contains("hp=4->5")), "real heal stays HEAL" + Dump(lines));
        Check(!Kind(lines, "HEALTH_SET").Any() && !Kind(lines, "REBORN").Any(), "no health-set label for real hits" + Dump(lines));
    }

    private static void HealthLabelsHuntersMarkIsHealthSet()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1;
        var yeti = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        ((ICharacter)yeti).Damage = 2;
        var mark = EngineEntity.FromCard(player, Cards.FromId("CS2_084"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, mark, yeti);
        Check(!Kind(lines, "DAMAGE").Any() && !Kind(lines, "HEAL").Any(), "Hunter's Mark is neither damage nor heal" + Dump(lines));
        Check(Kind(lines, "HEALTH_SET").Any(l => l.Contains("target=CS2_182#" + yeti.Id) && l.Contains("hp=3->1") && l.Contains("by=CS2_084#" + mark.Id)), "Hunter's Mark is HEALTH_SET 3->1" + Dump(lines));
    }

    private static void HealthLabelsHalvingIsHealthSet()
    {
        // Garona Halforcen halves the enemy hero's remaining Health: since PR 203 it adds DAMAGE inside the battlecry block
        // without a damage meta, so the rule (no hit meta, no heal meta) labels it HEALTH_SET, never DAMAGE or a STATS change.
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        var llane = EngineEntity.FromCard(enemy, Cards.FromId("TIME_875t"), zone: enemy.HandZone);
        var garona = EngineEntity.FromCard(player, Cards.FromId("TIME_875"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        int before = enemy.Hero.Health;
        PlayAndFlush(match, player, garona);
        Check(enemy.Hero.Health == before / 2, "Garona halved the hero: " + before + " -> " + enemy.Hero.Health);
        Check(!Kind(lines, "DAMAGE").Any() && !Kind(lines, "HEAL").Any(), "halving is neither damage nor heal" + Dump(lines));
        Check(Kind(lines, "HEALTH_SET").Any(l => l.Contains("target=HERO_08#") && l.Contains("hp=" + before + "->" + before / 2) && l.Contains("by=TIME_875#" + garona.Id)), "halving is HEALTH_SET " + before + "->" + before / 2 + Dump(lines));
        Check(!Kind(lines, "STATS").Any(l => l.Contains("target=HERO_08#")), "halving keeps max Health, so no STATS line on the hero" + Dump(lines));
    }

    private static void HealthLabelsDamageWriteWithoutHitIsHealthSet()
    {
        // What Garona Halforcen does after PR 203: it adds DAMAGE to the hero. A DAMAGE write with no damage event in front
        // of it is not a hit, so it reads HEALTH_SET, never DAMAGE.
        var (match, _, lines) = LoggedBoard();
        var enemy = match.Game.Player2;
        enemy.Hero.Damage = 15;
        FlushHistory(match);
        Check(Kind(lines, "HEALTH_SET").Any(l => l.Contains("target=HERO_08#") && l.Contains("hp=30->15")), "bare DAMAGE write is HEALTH_SET" + Dump(lines));
        Check(!Kind(lines, "DAMAGE").Any(), "bare DAMAGE write is not damage" + Dump(lines));
        enemy.Hero.Damage = 5;
        FlushHistory(match);
        Check(Kind(lines, "HEALTH_SET").Any(l => l.Contains("hp=15->25")), "bare DAMAGE drop is HEALTH_SET, not HEAL" + Dump(lines));
        Check(!Kind(lines, "HEAL").Any(), "bare DAMAGE drop is not a heal" + Dump(lines));
    }

    private static void HealthLabelsRebornIsReborn()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        var yeti = EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        yeti[GameTag.REBORN] = 1;
        var fireball = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, fireball, yeti);
        var returned = player.BoardZone.Single(m => m.Card.Id == "CS2_182");
        Check(((ICharacter)returned).Health == 1 && returned.Id != yeti.Id, "Reborn returned a 1-Health copy");
        Check(Kind(lines, "REBORN").Any(l => l.Contains("target=CS2_182#" + returned.Id) && l.Contains("hp=5->1")), "Reborn return is REBORN" + Dump(lines));
        Check(!Kind(lines, "DAMAGE").Any(l => l.Contains("#" + returned.Id)), "returned copy took no damage" + Dump(lines));
    }

    // ---- regression net: every real hit and heal keeps its DAMAGE / HEAL label exactly as before (probed identical on main) ----
    private static void Ready(Minion m) { m.IsExhausted = false; m[GameTag.NUM_TURNS_IN_PLAY] = 1; }

    private static void HealthLabelsCombatBothSides()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        var mine = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        var theirs = (Minion)EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        Ready(mine); FlushHistory(match); lines.Clear();
        Check(match.Game.Process(MinionAttackTask.Any(player, mine, theirs)), "attack"); FlushHistory(match);
        Check(Kind(lines, "DAMAGE").Count(l => l.Contains("amount=4") && l.Contains("hp=5->1") && (l.Contains("target=CS2_182#" + mine.Id) || l.Contains("target=CS2_182#" + theirs.Id))) == 2, "both combatants take DAMAGE 4" + Dump(lines));
        Check(!Kind(lines, "HEALTH_SET").Any(), "combat sets nothing" + Dump(lines));
    }

    private static void HealthLabelsAoeArmorAndDivineShield()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        enemy.Hero.Armor = 2;
        var plain = EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        var shield = EngineEntity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        shield[GameTag.DIVINE_SHIELD] = 1;
        var consecration = EngineEntity.FromCard(player, Cards.FromId("CS2_093"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, consecration);
        Check(Kind(lines, "DAMAGE").Any(l => l.Contains("target=CS2_182#" + plain.Id) && l.Contains("amount=2") && l.Contains("hp=5->3")), "AoE damages the minion" + Dump(lines));
        Check(!Kind(lines, "DAMAGE").Any(l => l.Contains("#" + shield.Id)) && !Kind(lines, "HEALTH_SET").Any(), "shield pop and absorbed armor hit are no health line" + Dump(lines));
        Check(Kind(lines, "ARMOR").Any(l => l.Contains("2->0")), "armor absorbs the hero's hit" + Dump(lines));
    }

    private static void HealthLabelsFireballThroughArmor()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        enemy.Hero.Armor = 3;
        var fireball = EngineEntity.FromCard(player, Cards.FromId("CS2_029"), zone: player.HandZone);
        FlushHistory(match); lines.Clear();
        PlayAndFlush(match, player, fireball, enemy.Hero);
        Check(Kind(lines, "DAMAGE").Any(l => l.Contains("target=HERO_08#") && l.Contains("amount=3") && l.Contains("hp=30->27")), "Fireball through 3 armor" + Dump(lines));
        Check(!Kind(lines, "HEALTH_SET").Any(), "no health-set line" + Dump(lines));
    }

    private static void HealthLabelsFatigueTwice()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1;
        foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        FlushHistory(match); lines.Clear();
        Generic.Draw(player); FlushHistory(match);
        Generic.Draw(player); FlushHistory(match);
        var hits = Kind(lines, "DAMAGE");
        Check(hits.Length == 2 && hits[0].Contains("amount=1") && hits[1].Contains("amount=2"), "fatigue 1 then 2 stays DAMAGE" + Dump(lines));
    }

    private static void HealthLabelsLifesteal()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        var mine = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        mine[GameTag.LIFESTEAL] = 1; Ready(mine);
        player.Hero.Damage = 10;
        FlushHistory(match); lines.Clear();
        Check(match.Game.Process(MinionAttackTask.Any(player, mine, enemy.Hero)), "attack"); FlushHistory(match);
        Check(Kind(lines, "DAMAGE").Any(l => l.Contains("target=HERO_08#") && l.Contains("amount=4")), "lifesteal hit is DAMAGE" + Dump(lines));
        Check(Kind(lines, "HEAL").Any(l => l.Contains("target=HERO_06#") && l.Contains("amount=4") && l.Contains("hp=20->24")), "lifesteal heal is HEAL" + Dump(lines));
    }

    // Pins the weapon rule: a swing loses durability through a DAMAGE write with no meta; it is neither damage nor a Health set.
    private static void HealthLabelsWeaponSwingIsDurability()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1; var enemy = match.Game.Player2;
        var axe = EngineEntity.FromCard(player, Cards.FromId("CS2_106"), zone: player.HandZone);
        PlayAndFlush(match, player, axe); lines.Clear();
        Check(match.Game.Process(HeroAttackTask.Any(player, enemy.Hero)), "hero attack"); FlushHistory(match);
        Check(Kind(lines, "DURABILITY").Any(l => l.Contains("target=CS2_106#" + axe.Id) && l.Contains("amount=1") && l.Contains("damage=0->1")), "swing is DURABILITY" + Dump(lines));
        Check(!Kind(lines, "HEALTH_SET").Any() && !Kind(lines, "DAMAGE").Any(l => l.Contains("CS2_106#")), "weapon is not a Health set or damaged" + Dump(lines));
        Check(Kind(lines, "DAMAGE").Any(l => l.Contains("target=HERO_08#") && l.Contains("amount=3")), "the swing's hit on the hero stays DAMAGE" + Dump(lines));
    }

    private static void HealthLabelsLocationUseIsDurability()
    {
        var (match, _, lines) = LoggedBoard();
        var game = match.Game; var player = game.Player1;
        int found = 0;
        foreach (var card in Cards.All.Where(c => c.Type == CardType.LOCATION && c.Id != null && c.Implemented).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var source = (BoardObject)EngineEntity.FromCard(player, Cards.FromId(card.Id), zone: player.BoardZone);
            source[GameTag.EXHAUSTED] = 0; source[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
            FlushHistory(match); lines.Clear();
            var task = player.Options().OfType<ActivateLocationTask>().FirstOrDefault(t => t.Source?.Id == source.Id);
            if (task == null) continue;
            game.Process(task);
            for (int picks = 0; player.Choice != null && picks < 4; picks++) game.Process(ChooseTask.Pick(player, player.Choice.Choices[0]));
            FlushHistory(match);
            var own = lines.Where(l => l.Contains("target=" + card.Id + "#" + source.Id)).ToArray();
            if (own.Any(l => l.Contains(" DURABILITY "))) found++;
            Check(!own.Any(l => l.Contains(" HEALTH_SET ") || l.Contains(" DAMAGE ") || l.Contains(" HEAL ")), card.Id + " use is no health line" + Dump(lines));
            source.Zone.Remove(source);
            if (found >= 2) break;
        }
        Check(found > 0, "at least one location use reads DURABILITY");
    }

    private static void HealthLabelsHealthCostIsHealthCost()
    {
        var (match, _, lines) = LoggedBoard();
        var player = match.Game.Player1;
        var warloc = EngineEntity.FromCard(player, Cards.FromId("CATA_180"), zone: player.HandZone);
        var murloc = EngineEntity.FromCard(player, Cards.FromId("CS2_168"), zone: player.HandZone);
        PlayAndFlush(match, player, warloc); lines.Clear();
        PlayAndFlush(match, player, murloc);
        Check(Kind(lines, "HEALTH_COST").Any(l => l.Contains("target=HERO_06#") && l.Contains("amount=1") && l.Contains("hp=30->29") && l.Contains("by=CS2_168#" + murloc.Id)), "Health cost is HEALTH_COST" + Dump(lines));
        Check(!Kind(lines, "HEALTH_SET").Any() && !Kind(lines, "DAMAGE").Any(l => l.Contains("HERO_06")), "payment is neither set nor damage" + Dump(lines));
    }
}
