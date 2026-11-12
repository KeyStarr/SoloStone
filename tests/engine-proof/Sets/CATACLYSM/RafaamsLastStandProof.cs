using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RafaamsLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_498", (g, p, id) => {
        var spell = Add(p, id);
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "pinned displayed base damage two");
        End(g); Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "upgrades at own end turn");
        End(g); Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "opponent turn does not upgrade");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.IdEntityDic[spell.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "upgrade state belongs to clone entity");
        var enemies = new[] { Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182") };
        var friend = Summon(p, "CS2_182"); Summon(p, "CS2_052");
        PlayEntity(g, p, spell);
        Core2026Proof.Check(enemies.Count(m => m.Damage == 4) == 2 && enemies.Count(m => m.Damage == 0) == 1 && friend.Damage == 0 && p.Opponent.Hero.Damage == 0, "two distinct enemy minions take upgraded spell damage");
        End(g); Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "played spell no longer upgrades"); End(g);
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var only = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(only.Damage == 2, "one enemy hit once for base damage");
        ClearBoard(g, p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "empty enemy board safe");
        var deck = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone); End(g);
        Core2026Proof.Check(deck[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "deck spell never upgrades");
    });
}
