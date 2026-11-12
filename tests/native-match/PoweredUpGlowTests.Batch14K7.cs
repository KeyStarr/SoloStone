using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k7-dh-druid", ProofKind.PoweredUp)]
    private static void RunBatch14K7()
    {
        Scenario("CATA_131", (f, card, met) => {
            if (met)
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card),
            (f, card) => {
                Check(card.Zone == f.Player.BoardZone && f.Player.RemainingMana == 8 + f.Player.TemporaryMana,
                    "Felwood Treant pays two Mana and grants one usable crystal");
                return f.Player.TemporaryMana == 0;
            });
    }

    [GroupProof("native-b14-k7-dh-druid", ProofKind.Native)]
    private static void OmenPayloadStaysPrivate()
    {
        var f = new Fixture();
        var omen = (Minion)f.Draw("EDR_421");
        f.Process(PlayCardTask.Minion(f.Player, omen));
        var target = f.Summon("CS2_182", f.Game.Player2);
        f.Process(MinionAttackTask.Any(f.Player, omen, target));
        var stored = PayloadRules.Entries(omen).Select(e => e.Id).ToArray();
        Check(stored.Length == 1 && omen[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "Omen creates one private progress actor");
        Check(!f.Entries.Any(e => e.HasFullEntity && stored.Contains(e.FullEntity.Entity) && e.FullEntity.Name != ""
            || e.HasShowEntity && stored.Contains(e.ShowEntity.Entity) && e.ShowEntity.Name != ""
            || e.HasChangeEntity && stored.Contains(e.ChangeEntity.Entity) && e.ChangeEntity.Name != ""),
            "private Omen progress actor never reveals identity to client");
        Check(!f.Entries.Any(e => e.HasTagChange && PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),
            "payload bookkeeping stays internal");
    }
}
