using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class PayloadRulesProof
{
    internal static void Run()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var host = Summon(p, "CS2_182");
        var stored = Entity.FromCard(p, Cards.FromId("CS2_182"), new EntityData {
            [PayloadRules.Owner] = host.Id, [PayloadRules.Key] = 7, [PayloadRules.Policy] = 3, [PayloadRules.Stored] = 1
        }, zone: p.SetasideZone);
        ((Minion)stored).AttackDamage = 9;
        var copy = Generic.Copy(p, p.Hero, host, Zone.PLAY);
        var entries = g.IdEntityDic.Values.Where(e => e[PayloadRules.Owner] == copy.Id && e[PayloadRules.Key] == 7).ToArray();
        Core2026Proof.Check(entries.Length == 1 && entries[0].Id != stored.Id && ((Minion)entries[0]).AttackDamage == 9,
            "gameplay copy retains independent stored payload");
        g.DeathProcessingAndAuraUpdate();
        var boundary = g.SaveSnapshot();
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(boundary) })
        {
            var child = PayloadRules.Entries(branch.IdEntityDic[copy.Id], 7).Single();
            ((Minion)child).AttackDamage = 13;
            Core2026Proof.Check(((Minion)stored).AttackDamage == 9 && !ReferenceEquals(child, entries[0]), "clone/snapshot own payload entities");
            ((Minion)branch.IdEntityDic[copy.Id]).Silence();
            Core2026Proof.Check(PayloadRules.Entries(branch.IdEntityDic[copy.Id]).Length == 0
                && PayloadRules.Entries(branch.IdEntityDic[host.Id]).Length == 1, "Silence removes selected host payload only");
        }
        Core2026Proof.Check(boundary.SequenceEqual(g.SaveSnapshot()), "branch changes leave source snapshot unchanged");
        var hand = Add(p, "CS2_029");
        var timer = PayloadRules.StoreValue(hand, 2, 3, PayloadPolicy.Copy | PayloadPolicy.HandExit);
        p.SetasideZone.Add(p.HandZone.Remove(hand));
        Core2026Proof.Check(PayloadRules.Entries(hand).Length == 0 && PayloadRules.IsStored(timer), "hand expiry retires entry without leaking its old identity");
        Generic.ChangeEntityBlock(p, host, Cards.FromId("CS2_120"), true);
        Core2026Proof.Check(PayloadRules.Entries(host).Length == 0, "transform clears payload");
        bool cyclic = false;
        try { PayloadRules.Store(copy, 1, copy); } catch (ArgumentException) { cyclic = true; }
        Core2026Proof.Check(cyclic, "cyclic capture rejected");
        var ordinary = Add(p, "CS2_029");
        var ordinaryCopy = Generic.Copy(p, p.Hero, ordinary, Zone.HAND);
        Core2026Proof.Check(!ordinaryCopy.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_1)
            && !ordinaryCopy.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_6), "plain hand copies gain no synthetic script defaults");
        var rewind = HeroDivineShieldProof.NewGame(true); var rewindHost = Summon(rewind.Player1, "CS2_182");
        var rewindEntry = PayloadRules.StoreCard(rewindHost, 1, Cards.FromId("CS2_120")); rewindEntry[PayloadRules.Value] = 5;
        Play(rewind, rewind.Player1, "TIME_001");
        foreach (var branch in new[] { rewind.CloneExact(true), Game.LoadSnapshot(rewind.SaveSnapshot()), rewind })
        {
            branch.IdEntityDic[rewindEntry.Id][PayloadRules.Value] = 99;
            RewindTestHelpers.AnswerRewind(branch);
            Core2026Proof.Check(PayloadRules.Entries(branch.IdEntityDic[rewindHost.Id], 1).Single()[PayloadRules.Value] == 5,
                "Rewind restores payload from attempt checkpoint after clone/load");
            RewindTestHelpers.KeepIfOffered(branch);
        }
        PayloadTransformCopyProof.Run();
        Console.WriteLine("PASS stored payload: independent gameplay copy, clone/snapshot, Silence, transform, hand expiry, cycle guard");
    }
}
