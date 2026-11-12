using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class AncientPterrordaxProof
{
    // Tier E Hearthstone Hungary reveal + tier D export; owner assumption: data/rules-evidence/misc-cards/pterrordax-option-ids.yaml.
    internal static void Run() => StandardSetsProof.Case("TLC_246", Unpinned_AncientPterrordaxOptions);
    private static void Unpinned_AncientPterrordaxOptions(Game g, Controller p, string id)
    {
        string[] options = { "UNG_999t10", "UNG_999t5", "UNG_999t7" };
        for (int i = 0; i < 3; i++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            var source = (Minion)Play(g, p, id); AncientStegodonProof.Options(g, p, options);
            int pick = p.Choice.Choices.ElementAt(i);
            Resume(g, next => AncientStegodonProof.Answer(next, pick));
            Core2026Proof.Check(source.HasStealth == (i == 0) && source.CantBeTargetedBySpells == (i == 1)
                && source.CantBeTargetedByHeroPowers == (i == 1) && source.HasWindfury == (i == 2), "only selected keyword applied");
            Resume(g, next => {
                End(next); var m = (Minion)next.IdEntityDic[source.Id];
                Core2026Proof.Check(m.HasStealth == (i == 0), "Stealth lasts through opponent turn");
                End(next);
                Core2026Proof.Check(!m.HasStealth && m.CantBeTargetedBySpells == (i == 1) && m.HasWindfury == (i == 2), "only Stealth expires at next owner start");
            });
            source.Silence();
            Core2026Proof.Check(!source.HasStealth && !source.CantBeTargetedBySpells && !source.HasWindfury, "silence removes chosen keyword");
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var dead = (Minion)Play(g, p, id); AncientStegodonProof.Answer(g, p.Choice.Choices.First()); Kill(g, dead); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "expiry safe after source leaves play");
    }
}
