using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using L = LocationProofs;

internal static class EruptingVolcanoProof
{
    // Tier D: pinned 37.0.0.254477 CATA_584 text. Cooldown/charge reuse pinned pilot rules:
    // docs/design/rules-research.md L1-L3 (tier A Cora; tier B builds 176014/195635).
    // Enemy damage taken: hero damage plus Yeti damage plus 1 per dead Wisp.
    private static int EnemyDamage(Controller enemy, Minion yeti, Minion wisp) =>
        enemy.Hero.Damage + yeti.Damage + (wisp.Zone.Type == Zone.GRAVEYARD ? 1 : 0);

    internal static void StandardCase() => StandardSetsProof.Case("CATA_584", (g, p, id) => {
        L.Check(Cards.FromId(id).ConsumesHistory && Cards.FromId("CORE_CS2_029")[GameTag.SPELL_SCHOOL] == 2
            && Cards.FromId("CORE_CS2_024")[GameTag.SPELL_SCHOOL] == 3, "history consumer; fixture Fire and Frost spells");
        var enemy = p.Opponent;
        var yeti = Summon(enemy, "CS2_182"); var wisp = Summon(enemy, "CS2_231");
        var friend = Summon(p, "CS2_182");
        Summon(p, "CS2_142");
        L.Check(p.CurrentSpellPower == 1, "fixture: friendly Spell Damage +1");
        var volcano = L.Play(p, id);
        L.Check(volcano.Durability == 3 && LocationRules.CanActivate(p, volcano, null), "fresh 3-durability location usable untargeted");

        var full = g.CloneExact(history: true);
        while (!full.Player1.BoardZone.IsFull) Summon(full.Player1, "CS2_182");
        var fullVolcano = (BoardObject)full.IdEntityDic[volcano.Id];
        L.Check(LocationRules.CanActivate(full.Player1, fullVolcano, null), "full physical board does not block damage activation");
        L.Use(fullVolcano);
        L.Check(full.Player1.BoardZone.SlotCount == 7 && EnemyDamage(full.Player1.Opponent,
            (Minion)full.IdEntityDic[yeti.Id], (Minion)full.IdEntityDic[wisp.Id]) == 3, "full board still deals exactly three");

        var automatic = g.CloneExact(history: true);
        var autoSpell = Entity.FromCard(automatic.Player1, Cards.FromId("CORE_CS2_029"));
        new EffectContext(automatic, automatic.Player1.Id, volcano.Id).CastSpell(autoSpell.Id, HistoryCastKind.Effect,
            automatic.Player1.Opponent.Hero.Id);
        automatic.DeathProcessingAndAuraUpdate();
        int autoBase = automatic.Player1.Opponent.Hero.Damage;
        L.Use((BoardObject)automatic.IdEntityDic[volcano.Id]);
        L.Check(EnemyDamage(automatic.Player1.Opponent, (Minion)automatic.IdEntityDic[yeti.Id],
            (Minion)automatic.IdEntityDic[wisp.Id]) - autoBase == 3, "automatic Fire cast is not a Fire spell play");

        var opposing = g.CloneExact(history: true);
        L.End(opposing);
        Play(opposing, opposing.Player2, "CORE_CS2_029", opposing.Player1.Hero);
        L.End(opposing);
        L.Use((BoardObject)opposing.IdEntityDic[volcano.Id]);
        L.Check(EnemyDamage(opposing.Player2, (Minion)opposing.IdEntityDic[yeti.Id],
            (Minion)opposing.IdEntityDic[wisp.Id]) == 3, "opponent's Fire play never grants the bonus");

        var plain = g.CloneExact(history: true); var plainOwner = plain.Player1;
        var plainVolcano = (BoardObject)plain.IdEntityDic[volcano.Id];
        L.Use(plainVolcano);
        var pe = plainOwner.Opponent; var py = (Minion)plain.IdEntityDic[yeti.Id]; var pw = (Minion)plain.IdEntityDic[wisp.Id];
        L.Check(EnemyDamage(pe, py, pw) == 3, $"three damage split among enemies without Spell Damage: {EnemyDamage(pe, py, pw)}");
        L.Check(pw.Damage <= 1 && ((Minion)plain.IdEntityDic[friend.Id]).Damage == 0 && plainOwner.Hero.Damage == 0, "dead Wisp not retargeted; friendly side untouched");
        L.Check(plainVolcano.Durability == 2 && !LocationRules.CanActivate(plainOwner, plainVolcano, null), "one charge spent and cooldown");

        var frost = g.CloneExact(history: true);
        Play(frost, frost.Player1, "CORE_CS2_024", frost.Player1.Opponent.Hero);
        int frostBase = frost.Player1.Opponent.Hero.Damage;
        L.Use((BoardObject)frost.IdEntityDic[volcano.Id]);
        L.Check(EnemyDamage(frost.Player1.Opponent, (Minion)frost.IdEntityDic[yeti.Id], (Minion)frost.IdEntityDic[wisp.Id]) - frostBase == 3,
            "non-Fire spell this turn: three");

        var twin = g.CloneExact(history: true);
        Play(g, p, "CORE_CS2_029", enemy.Hero);
        Play(twin, twin.Player1, "CORE_CS2_029", twin.Player1.Opponent.Hero);
        int fireBase = enemy.Hero.Damage;
        L.Use(volcano);
        L.Use((BoardObject)twin.IdEntityDic[volcano.Id]);
        L.Check(EnemyDamage(enemy, yeti, wisp) - fireBase == 6, "Fire spell this turn: six");
        L.Check(enemy.Hero.Damage == twin.Player1.Opponent.Hero.Damage && yeti.Damage == ((Minion)twin.IdEntityDic[yeti.Id]).Damage,
            "random split deterministic for the same seed and state");

        L.End(g); L.End(g);
        L.Check(LocationRules.CanActivate(p, volcano, null) == false, "location skips its owner's next turn");
        L.End(g); L.End(g);
        int before = EnemyDamage(enemy, yeti, wisp);
        L.Use(volcano);
        L.Check(EnemyDamage(enemy, yeti, wisp) - before == 3 && volcano.Durability == 1 && volcano.Zone == p.BoardZone,
            "Fire spell from an earlier turn ignored: three");
        L.End(g); L.End(g); L.End(g); L.End(g);
        before = EnemyDamage(enemy, yeti, wisp);
        L.Use(volcano);
        L.Check(EnemyDamage(enemy, yeti, wisp) - before == 3 && volcano.Zone.Type == Zone.GRAVEYARD && !p.BoardZone.HasLocations,
            "final charge resolves then the location dies");
    });
}
