using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MendProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_302", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); friend.Damage = 4; enemy.Damage = 3;
        var spell = Add(p, id); TargetRules(g, p, id, enemy, friend, false);
        int deck = p.DeckZone.Count; PlayEntity(g, p, spell, friend);
        Core2026Proof.Check(friend.Health == 5 && friend.Damage == 0 && p.DeckZone.Count == deck - 1 && enemy.Damage == 3, "fully heals friendly minion and draws");
        deck = p.DeckZone.Count; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 5 && p.DeckZone.Count == deck - 1, "enemy minion fully healed");
        deck = p.DeckZone.Count; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Health == 5 && p.DeckZone.Count == deck - 1, "undamaged target still draws");
        Summon(p, "EX1_591"); friend.Damage = 2; deck = p.DeckZone.Count; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD && p.DeckZone.Count == deck - 1, "converted maximum-Health healing kills damaged 3/5 and still draws");
        enemy.BaseHealth = 8; deck = p.DeckZone.Count; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.DeckZone.Count == deck - 1, "converted healing kills full-health buffed 4/8 using current maximum Health");
        var shield = Summon(p.Opponent, "CS2_182"); shield.Damage = 2; shield.HasDivineShield = true;
        deck = p.DeckZone.Count; Play(g, p, id, shield);
        Core2026Proof.Check(shield.Zone.Type == Zone.PLAY && shield.Health == 3 && !shield.HasDivineShield && p.DeckZone.Count == deck - 1, "Divine Shield prevents converted healing damage; still draws");
        shield.IsImmune = true; deck = p.DeckZone.Count;
        var source = Add(p, id); source.Card.Power.PowerTask.Process(g, p, source, shield, new TaskStack());
        Core2026Proof.Check(shield.Zone.Type == Zone.PLAY && shield.Health == 3 && p.DeckZone.Count == deck - 1, "Immune target prevents converted healing damage; still draws");
    });
}
