using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class MoltenGoldProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_801", (g, p, id) => {
        var token = Cards.FromId("JAIL_801t");
        Core2026Proof.Check(token.Implemented && token.AssetId == 126408 && token.Type == CardType.MINION && token.Cost == 3 && token.ATK == 3 && token.Health == 3 &&
            token.IsRace(Race.ELEMENTAL) && token.Text == "<b>Battlecry:</b> Deal 4 damage.", "exact exported token closure");
        Summon(p, "CS2_052");
        var spell = Add(p, id); int damage = p.Opponent.Hero.Damage;
        PlayEntity(g, p, spell, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 5 && spell is Spell && spell.Zone == p.GraveyardZone, "base spell uses Spell Damage");
        var held = Add(p, id); int entity = held.Id, slot = held.ZonePosition;
        Play(g, p, "GAME_005"); Play(g, p, "GAME_005"); Play(g, p, "GAME_005");
        var upgraded = g.IdEntityDic[entity];
        Core2026Proof.Check(upgraded is Minion && upgraded.Card == token && upgraded.ZonePosition == slot &&
            upgraded[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && held is Spell && !ReferenceEquals(held, upgraded),
            HandProgressRulesProof.Leans + ": registry target replaces stale CLR reference at same id/slot");
        PlayEntity(g, p, upgraded, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 9 && ((Minion)upgraded).AttackDamage == 3 && ((Minion)upgraded).BaseHealth == 3,
            "target Battlecry fixed four ignores Spell Damage; no upgrade-time Battlecry");
    });
}
