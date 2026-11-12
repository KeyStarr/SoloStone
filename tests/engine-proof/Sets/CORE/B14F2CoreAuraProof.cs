using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2CoreAuraProof
{
    internal static void Run() => StandardSetsProof.Case("TTN_851", Resistance);
    private static void Resistance(Game g, SabberStoneCore.Model.Entities.Controller p, string id)
    {
        var enemySpell = Add(p.Opponent, "CS2_029"); var ownSpell = Add(p, "CS2_029");
        var aura = Play(g, p, id);
        Core2026Proof.Check(enemySpell.Cost == 5 && ownSpell.Cost == 4, "enemy spells only cost one more");
        End(g);
        Core2026Proof.Check(enemySpell.Cost == 5 && aura[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "owner end preserves enemy clock");
        End(g);
        Core2026Proof.Check(enemySpell.Cost == 5 && aura[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "first enemy end ticks");
        End(g); End(g);
        Core2026Proof.Check(enemySpell.Cost == 4 && Active(p).Length == 0, "second enemy end expires and removes cost aura");
    }
}
