using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class PaladinAurasProof
{
    internal static void NonFirstPick(Game g, int count)
    {
        for (int i = 0; i < 32; i++)
        {
            if (g.Random.Clone().Next(count) != 0) return;
            g.Random.Next();
        }
        throw new Exception("seeded random fixture failed to reach a nonfirst choice");
    }
    internal static Spell[] Active(Controller p) => p.SecretZone.Where(s => s.Card[(GameTag)3429] == 1).ToArray();
    internal static Spell Install(Controller p, string id)
    {
        var rules = typeof(Spell).Assembly.GetType("SabberStoneCore.Actions.AuraSpellRules");
        Core2026Proof.Check(rules != null, "shared timed Aura facility missing");
        var actor = (Spell)Entity.FromCard(p, Cards.FromId(id));
        return (Spell)rules.GetMethod("Install").Invoke(null, new object[] { actor });
    }

    [GroupProof("b14-f2-paladin-auras")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var first = Install(p, "TIME_700");
            var second = Install(p, "TIME_700");
            Core2026Proof.Check(Active(p).Length == 2 && p.SecretZone.OccupiedSecretSlots == 0,
                "duplicate independent public Auras do not occupy Secret slots");
            var publicView = g.CloneExact(history);
            publicView.ProjectHistoryForFairView(2);
            Core2026Proof.Check(!publicView.HistoryFacts.Query.StartedInDeck(first.Id, p.Id),
                "opponent Aura remains public in fair-search history");
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == 2 && first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                "both owner-end actors fire and tick once");
            End(g);
            Core2026Proof.Check(first[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "enemy end cannot consume owner clock");
            foreach (var branch in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = branch.Player1;
                End(branch); End(branch); End(branch);
                Core2026Proof.Check(Active(owner).Length == 0 && owner.BoardZone.Count == 6,
                    "clone/load retains independent timers and final effects precede expiry");
                Core2026Proof.Check(branch.IdEntityDic[first.Id].Zone == owner.GraveyardZone,
                    "expired actor leaves play");
            }
            var leave = HeroDivineShieldProof.NewGame(history);
            var actor = Install(leave.Player1, "TIME_700");
            leave.Player1.SetasideZone.Add(leave.Player1.SecretZone.Remove(actor));
            End(leave);
            Core2026Proof.Check(leave.Player1.BoardZone.Count == 0 && Active(leave.Player1).Length == 0,
                "leave-play detaches Aura listener");
            var resistance = Install(leave.Player1, "TTN_851");
            var taxed = Add(leave.Player2, "CS2_029");
            leave.AuraUpdate();
            Core2026Proof.Check(taxed.Cost == 5, "active Resistance taxes enemy spell");
            leave.Player1.SetasideZone.Add(leave.Player1.SecretZone.Remove(resistance));
            leave.AuraUpdate();
            Core2026Proof.Check(taxed.Cost == 4, "leave-play removes continuous cost effect");
        }
        FullBoardExpiry();
        Unpinned_InstanceExtension();
        RecastAndRewind();
        Console.WriteLine("PASS timed Paladin Aura facility: instances, clocks, expiry, leave-play, clone/load");
    }

    private static void FullBoardExpiry()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        Install(p, "TIME_700");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        for (int i = 0; i < 3; i++) { End(g); End(g); }
        Core2026Proof.Check(Active(p).Length == 0 && p.BoardZone.All(m => m.Card.Id == "CS2_231"),
            "failed summons still spend every duration tick");
        Kill(g, p.BoardZone.First()); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 6, "no deferred summon after expiry");
    }

    private static void Unpinned_InstanceExtension()
    {
        foreach (string id in new[] { "TIME_700", "TTN_851", "END_011" })
        {
            var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
            var held = (Spell)Add(p, id);
            AuraSpellRules.Extend(held, 1);
            End(g); End(g);
            Core2026Proof.Check(held[GameTag.QUEST_PROGRESS] == 0, "held duration never ticks");
            foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = branch.Player1;
                var extended = (Spell)branch.IdEntityDic[held.Id];
                PlayEntity(branch, owner, extended);
                Core2026Proof.Check(AuraSpellRules.Remaining(extended) == (id == "TTN_851" ? 3 : 4),
                    "instance bonus survives hand delay and clone/load");
                var normal = Install(owner, id);
                Core2026Proof.Check(AuraSpellRules.Remaining(normal) == (id == "TTN_851" ? 2 : 3),
                    "ordinary same-identity Aura receives no bonus");
                for (int turn = 0; turn < 8; turn++) End(branch);
                Core2026Proof.Check(Active(owner).Length == 0, "extended clock expires on its normal side");
            }
        }
    }

    private static void RecastAndRewind()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var first = Install(p, "TIME_700");
        End(g); End(g);
        Generic.CastSpell(p, g, first, null, 0);
        Core2026Proof.Check(Active(p).Length == 2 && AuraSpellRules.Remaining(first) == 2
            && Active(p).Any(a => a.Id != first.Id && AuraSpellRules.Remaining(a) == 3),
            "recasting active actor installs independent lifetime without refreshing old one");
        Play(g, p, "TIME_001");
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var actor = (Spell)branch.IdEntityDic[first.Id];
            AuraSpellRules.Extend(actor, 2);
            RewindTestHelpers.AnswerRewind(branch);
            Core2026Proof.Check(AuraSpellRules.Remaining((Spell)branch.IdEntityDic[first.Id]) == 2,
                "Rewind restores independent Aura counter after clone/load");
            RewindTestHelpers.KeepIfOffered(branch);
        }
    }
}
