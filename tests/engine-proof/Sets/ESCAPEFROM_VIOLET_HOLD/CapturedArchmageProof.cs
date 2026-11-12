using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CapturedArchmageProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_974", (g, p, id) => {
        var held = Add(p, id);
        var deck = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(deck);
        var board = (Minion)Play(g, p, id);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && deck[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0
            && board[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "progress starts at zero in all zones");
        var discarded = Add(p, id); Generic.DiscardBlock(p, discarded);
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var burned = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(burned); Generic.Draw(p);
        Core2026Proof.Check(discarded.Zone == p.GraveyardZone && burned.Zone == p.GraveyardZone
            && !discarded.ToBeDestroyed && !burned.ToBeDestroyed, "real discard and burn do not mark deaths");
        foreach (var card in p.HandZone.Where(c => c.Id != held.Id).ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        var enemyCopy = Summon(p.Opponent, id); enemyCopy.Silence(); Kill(g, enemyCopy);
        Kill(g, Summon(p, "CS2_182"));
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && deck[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
            "burns, discards, enemy deaths and other identities excluded");
        for (int count = 1; count <= 3; count++) {
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == count
                && deck[GameTag.TAG_SCRIPT_DATA_NUM_1] == count && board[GameTag.TAG_SCRIPT_DATA_NUM_1] == count,
                "first three deaths cannot cast despite burned/discarded copies; progress visible in all zones");
        }
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[board.Id]);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 0 && clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 4
            && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "fourth death does not count itself; clone progress independent");
        Kill(g, board);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4,
            "fourth death still cannot cast; own death excluded");
        var late = Add(p, id); var lateDeck = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(lateDeck);
        Core2026Proof.Check(late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && lateDeck[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4,
            "new hand and deck copies initialize from prior deaths");
        PlayEntity(g, p, held); Kill(g, (Minion)held);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && p.GraveyardZone.Count(c => c.Card.Id == "CS2_029") == 1,
            "fifth death casts actual Fireball at sole enemy");
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "silence removes qualifying Deathrattle");
        var enemy = Summon(p.Opponent, "CS2_200"); var spellDamage = Summon(p, "EX1_012");
        int heroBefore = p.Opponent.Hero.Damage;
        Kill(g, Summon(p, id));
        Core2026Proof.Check(p.Opponent.Hero.Damage - heroBefore + enemy.Damage == 7 && enemy.Damage is 0 or 7
            && spellDamage.Damage == 0 && p.Hero.Damage == 0, "random enemy only; cast benefits from Spell Damage");
        ClearBoard(g, p.Opponent);
        End(g); End(g);
        Core2026Proof.Check(late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "progress remains capped and persists across turns");
    });
}
