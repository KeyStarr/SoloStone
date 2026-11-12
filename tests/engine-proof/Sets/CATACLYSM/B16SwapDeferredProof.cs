using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class B16SwapDeferredCataclysmProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_550", Unpinned_MagmawRefill);
    private static void Unpinned_MagmawRefill(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_550t") == 6,
            "Magmaw fills free physical slots");
        Core2026Proof.Check(body[GameTag.TAG_SCRIPT_DATA_NUM_1] == 93, "successful summons debit exported remaining counter");
        var victim = p.BoardZone.First(m => m.Id != body.Id);
        var survivors = p.BoardZone.Where(m => m.Id != victim.Id).ToArray();
        var expected = g.CloneExact(history: true);
        int recipient = survivors[expected.Random.Next(survivors.Length)].Id;
        var attacks = survivors.ToDictionary(m => m.Id, m => m.AttackDamage);
        DiscoverRealProof.Saved(g, next => {
            Kill(next, (Minion)next.IdEntityDic[victim.Id]);
            Core2026Proof.Check(next.Player1.BoardZone.Count == 7 && next.IdEntityDic[body.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 92,
                "Magmaw retains and refills leftovers on original/clone/load");
            foreach (var minion in survivors)
                Core2026Proof.Check(((Minion)next.IdEntityDic[minion.Id]).AttackDamage == attacks[minion.Id] + (minion.Id == recipient ? 2 : 0),
                    "seeded Deathrattle buffs only pre-refill survivors, exactly once");
            Core2026Proof.Check(next.Random.CaptureState().Equals(expected.Random.CaptureState()), "one seeded Body pick, no refill RNG");
        });
        Exhaust(g, body);
        Lifetimes(g.Seed);
        Competition(g.Seed);
        CopyAndControl(g.Seed);
        Dormancy(g.Seed);
        var untouchable = DiscoverRealProof.New(g.Seed);
        var portal = Summon(untouchable.Player1, "UNG_829t2");
        var appendage = Summon(untouchable.Player1, "CATA_550t");
        var rng = untouchable.Random.CaptureState();
        Kill(untouchable, appendage);
        Core2026Proof.Check(portal.AttackDamage == portal.Card.ATK && untouchable.Random.CaptureState().Equals(rng),
            "Body ignores untouchable permanents; no eligible minion means no buff and no RNG");
    }

    private static void Exhaust(Game g, Minion body)
    {
        var p = body.Controller;
        while (body[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0)
        {
            foreach (var minion in p.BoardZone.Where(m => m.Id != body.Id).ToArray()) minion.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();
        }
        foreach (var minion in p.BoardZone.Where(m => m.Id != body.Id).ToArray()) minion.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.GraveyardZone.Count(c => c.Card.Id == "CATA_550t") == 99,
            "finite budget totals exactly 99 successful appendages; exhausted body never refills");
    }

    private static void Lifetimes(long seed)
    {
        foreach (string mode in new[] { "silence", "bounce", "death", "setaside" })
        {
            var game = DiscoverRealProof.New(seed); var owner = game.Player1;
            var blockers = Enumerable.Range(0, 5).Select(_ => Summon(owner, "CS2_182")).ToArray();
            var body = Summon(owner, "CATA_550");
            Core2026Proof.Check(owner.BoardZone.Count(m => m.Card.Id == "CATA_550t") == 1
                && body[GameTag.TAG_SCRIPT_DATA_NUM_1] == 98, "nearly full board retains 98 leftovers");
            DiscoverRealProof.Saved(game, next => {
                var source = (Minion)next.IdEntityDic[body.Id]; var p = source.Controller;
                if (mode == "silence") source.Silence();
                if (mode == "bounce") Core2026Proof.Check(Generic.ReturnToHandBlock(p, source), "bounce source");
                if (mode == "death") source.ToBeDestroyed = true;
                if (mode == "setaside") p.SetasideZone.Add(p.BoardZone.Remove(source));
                next.IdEntityDic[blockers[0].Id].ToBeDestroyed = true;
                next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CATA_550t") == 1,
                    "source Silence/departure/simultaneous death cancels pending refills: " + mode);
            });
        }
    }

    private static void Competition(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        var first = Summon(p, "CATA_550");
        var removed = p.BoardZone.First(m => m.Id != first.Id);
        p.SetasideZone.Add(p.BoardZone.Remove(removed));
        var second = Summon(p, "CATA_550");
        var victim = p.BoardZone.First(m => m.Id != first.Id && m.Id != second.Id);
        DiscoverRealProof.Saved(g, next => {
            Kill(next, (Minion)next.IdEntityDic[victim.Id]);
            Core2026Proof.Check(next.IdEntityDic[first.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 92
                && next.IdEntityDic[second.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 99,
                "competing Magmaws refill in play order after all Deathrattles");
        });
    }

    private static void CopyAndControl(long seed)
    {
        var g = DiscoverRealProof.New(seed); var p = g.Player1;
        var body = Summon(p, "CATA_550");
        var copy = (Minion)Generic.Copy(p.Opponent, p.Opponent.Hero, body, Zone.PLAY);
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 87 && body[GameTag.TAG_SCRIPT_DATA_NUM_1] == 93,
            "exact board copy has independent remaining budget, never fresh 99");
        copy.Silence();
        Kill(g, p.Opponent.BoardZone.First(m => m.Id != copy.Id));
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 6 && p.BoardZone.Count == 7,
            "silencing opponent copy cannot cancel original refill state");
        StandardSetsProof.ParkBoard(p.Opponent);
        new ControlTask(EntityType.TARGET, opposite: true).Process(g, p, p.Hero, body);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Controller == p.Opponent && body[GameTag.TAG_SCRIPT_DATA_NUM_1] == 87
            && p.Opponent.BoardZone.Count == 7, "living controlled body refills only new controller's board");
    }

    private static void Dormancy(long seed)
    {
        var g = DiscoverRealProof.New(seed); var body = Summon(g.Player1, "CATA_550");
        Core2026Proof.Check(DormantRules.Make(body, 1, "TIME_046e", g.Player1.Hero), "external Dormant applied");
        g.DeathProcessingAndAuraUpdate();
        DiscoverRealProof.Saved(g, next => {
            var source = (Minion)next.IdEntityDic[body.Id];
            Kill(next, next.Player1.BoardZone.First(m => m.Id != source.Id));
            Core2026Proof.Check(next.Player1.BoardZone.Count == 6 && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 93,
                "Dormant suppresses continuous refill without consuming remainder");
            Core2026Proof.Check(DormantRules.Awaken(source), "body awakens"); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(next.Player1.BoardZone.Count == 7 && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 92,
                "awake body resumes its existing remainder at next checkpoint");
        });
    }
}
