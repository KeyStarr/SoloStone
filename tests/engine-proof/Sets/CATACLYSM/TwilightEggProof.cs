using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwilightEggProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_210", (g, p, id) => {
        var hand = Add(p, id); var egg = (Minion)Play(g, p, id);
        Core2026Proof.Check(egg.AttackDamage == 0 && egg.Health == 2 && egg.HasDeathrattle
            && egg[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && egg[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1, "initial displayed Whelp is 2/1");
        End(g);
        Core2026Proof.Check(egg[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && egg[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1, "opponent turn does not grow Whelp");
        End(g);
        Core2026Proof.Check(egg[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && egg[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2
            && hand[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && hand[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1, "own turn grows displayed counters on board only");
        var clone = g.CloneExact(history: true); End(clone); End(clone);
        var copy = (Minion)clone.IdEntityDic[egg.Id];
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && copy[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3
            && egg[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && egg[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "second growth isolated in clone");
        Kill(clone, copy);
        var whelp = clone.Player1.BoardZone.Single();
        Core2026Proof.Check(whelp.Card.Id == "CATA_210t" && whelp.AttackDamage == 4 && whelp.Health == 3 && whelp.Card.IsRace(Race.DRAGON), "death summons exact grown Whelp");
        End(clone); End(clone);
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && whelp.AttackDamage == 4 && whelp.Health == 3, "departed Egg and Whelp no longer grow");
        var silent = g.CloneExact(history: true); var silenced = (Minion)silent.IdEntityDic[egg.Id];
        silenced.Silence(); silent.DeathProcessingAndAuraUpdate();
        int count = silenced[GameTag.TAG_SCRIPT_DATA_NUM_1]; End(silent); End(silent);
        Core2026Proof.Check(silenced[GameTag.TAG_SCRIPT_DATA_NUM_1] == count && !silenced.HasDeathrattle, "Silence stops growth and Deathrattle");
        Kill(silent, silenced); Core2026Proof.Check(silent.Player1.BoardZone.Count == 0, "silenced Egg summons nothing");
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); Kill(g, egg);
        whelp = p.BoardZone.Single(m => m.Card.Id == "CATA_210t");
        Core2026Proof.Check(whelp.AttackDamage == 3 && whelp.Health == 2
            && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { whelp.Id, left.Id, right.Id }), "departed leftmost Egg's slot takes grown token");
        egg = (Minion)Play(g, p, id); Kill(g, egg);
        Core2026Proof.Check(p.BoardZone[0].Card.Id == "CATA_210t" && p.BoardZone[0].AttackDamage == 2 && p.BoardZone[0].Health == 1, "fresh Egg does not inherit another Egg growth");
        ClearBoard(g, p); egg = (Minion)Play(g, p, id);
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        egg.Card.Power.DeathrattleTask.Process(g, p, egg, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && !p.BoardZone.Any(m => m.Card.Id == "CATA_210t"), "full board blocks Whelp");
        Kill(g, egg); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[0].Card.Id == "CATA_210t", "actual death frees the Egg's slot for Whelp");
        var board = DeathrattleSlotBoard(p, () => egg = Summon(p, id)); Kill(g, egg);
        CheckDeathrattleSlot(p, board, p.BoardZone.Where(m => m.Card.Id == "CATA_210t"), "Whelp fills the dying Egg's physical slot");
        ParkBoard(p);
        End(g);
    });
}
