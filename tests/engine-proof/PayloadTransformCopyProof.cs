using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class PayloadTransformCopyProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var proof in new Action[] { Unpinned_FacelessWarden, Unpinned_FacelessEgg, Unpinned_FacelessJar, JarDiscardIsNotDeath, Unpinned_FacelessIsorath })
            try { proof(); } catch (Exception e) { failures.Add(proof.Method.Name + ": " + e.Message); }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS transform payload: actual Faceless Warden, Egg, Jar, Iso'rath; clone/snapshot; independent Silence");
    }

    private static void Unpinned_FacelessWarden()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var prisoner = Summon(p.Opponent, "CS2_182");
        var warden = (Minion)Play(g, p, "TIME_442", prisoner);
        var copy = (Minion)Play(g, p, "EX1_564", warden);
        Kill(g, copy);
        Core2026Proof.Check(!prisoner.IsDormant && warden.Zone == p.BoardZone,
            "Faceless Warden releases original prisoner while original lives");
    }

    private static void Unpinned_FacelessEgg()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var dragon = Summon(p, "EX1_043"); dragon.AttackDamage = 9; dragon.BaseHealth = 10; dragon.Damage = 2;
        var location = LocationProofs.Play(p, "EDR_454"); LocationProofs.Use(location, dragon);
        var egg = p.BoardZone.Single(m => m.Card.Id == "EDR_454t");
        var copy = (Minion)Play(g, p, "EX1_564", egg);
        dragon.AttackDamage = 20; Kill(g, dragon);
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            Kill(branch, (Minion)branch.IdEntityDic[copy.Id]);
            Core2026Proof.Check(branch.Player1.BoardZone.Any(m => m.Card.Id == "EX1_043" && m.AttackDamage == 9 && m.BaseHealth == 10 && m.Damage == 2),
                "Faceless Egg hatches captured Dragon after source death and clone/snapshot");
            var original = (Minion)branch.IdEntityDic[egg.Id]; original.Silence(); Kill(branch, original);
            Core2026Proof.Check(branch.Player1.BoardZone.Count(m => m.Card.Id == "EX1_043") == 1,
                "Silence of original Egg does not change transformed copy's payload");
        }
    }

    private static void Unpinned_FacelessJar()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        Add(p, "CS2_182"); Play(g, p, "TLC_841");
        var jar = (Minion)p.HandZone.Single(); PlayEntity(g, p, jar);
        var copy = (Minion)Play(g, p, "EX1_564", jar);
        var actor = copy.AppliedEnchantments.Single(e => e.Card.Id == "TLC_841e");
        Core2026Proof.Check(PayloadRules.Entries(actor).SingleOrDefault()?.Card.Id == "CS2_182"
            && copy[GameTag.TAG_SCRIPT_DATA_NUM_6] == Cards.FromId("CS2_182").AssetId,
            "Faceless Jar copies removable enchantment payload and sixth display slot");
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            ((Minion)branch.IdEntityDic[jar.Id]).Silence();
            Kill(branch, (Minion)branch.IdEntityDic[copy.Id]);
            Core2026Proof.Check(branch.Player1.BoardZone.Count(m => m.Card.Id == "CS2_182") == 1,
                "Faceless Jar independently releases contents after original Silence and clone/snapshot");
        }
    }

    // Toru's jar Deathrattle is granted by TLC_841e: discarding the jar is not a death, so nothing is released
    // (transport standard-batch-1 34-vs-25 s4: Bygone Doomspeaker discard summoned the bottled minion).
    private static void JarDiscardIsNotDeath()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        Add(p, "CS2_182"); Add(p, "CS2_182"); Play(g, p, "TLC_841");
        var jars = p.HandZone.OfType<Minion>().ToArray(); int board = p.BoardZone.Count;
        Generic.DiscardBlock(p, jars[0]);
        Core2026Proof.Check(jars[0].Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == board && g.HistoryFacts.EvidenceValid,
            "discarded jar releases nothing and keeps evidence valid (" + p.BoardZone.Count + "/" + g.HistoryFacts.EvidenceFailure + ")");
        PlayEntity(g, p, jars[1]); Kill(g, jars[1]);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 1 && g.HistoryFacts.EvidenceValid,
            "a jar that dies on the board still releases its minion");
    }

    private static void Unpinned_FacelessIsorath()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        foreach (var held in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(held));
        Add(p.Opponent, "CS2_029").Cost = 2;
        ((Minion)Add(p.Opponent, "CS2_182")).AttackDamage = 8;
        var source = (Minion)Play(g, p, "CATA_481"); DormantRules.Awaken(source);
        var copy = (Minion)Play(g, p, "EX1_564", source);
        Kill(g, copy);
        Core2026Proof.Check(p.Opponent.HandZone.Count == 2 && p.Opponent.HandZone.Single(c => c.Card.Id == "CS2_029").Cost == 2
            && ((Minion)p.Opponent.HandZone.Single(c => c.Card.Id == "CS2_182")).AttackDamage == 8,
            "Faceless Iso'rath returns modified cards to recorded recipient");
        Kill(g, source);
        Core2026Proof.Check(p.Opponent.HandZone.Count == 4, "original Iso'rath retains independent return recipe");
    }
}
