using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1BEProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("BE_287", Unpinned_Ghuun);
        StandardSetsProof.Case("BE_997", Unpinned_ProtoPandaren);
    }
    private static void Unpinned_Ghuun(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g,p,id);
        Core2026Proof.Check(body.Health == 7 && p.Opponent.Hero.Health == 24, "first G'huun excludes own runes and steals six");
        var repeat=g.HistoryFacts.Query.CompletedPlays().Where(f=>f.ControllerId==p.Id).ToArray();
        Core2026Proof.Check(repeat.Sum(f=>f.Definition.Materialize()[GameTag.COST_BLOOD])==3,"own three runes enter after play finishes");
        body.ActivateTask(PowerActivation.POWER);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.Health==16 && p.Opponent.Hero.Health==15,"later repeated Battlecry steals nine");
        var choices=new HashSet<int>();
        foreach(int seed in new[]{1,123456789,2147483646})
        foreach(bool reverse in new[]{false,true})
        {
            var tie=PlayerDecisionProof.Fixture(seed);var owner=tie.Player1;
            var a=Summon(owner.Opponent,reverse?"CS2_120":"CS2_182");
            var b=Summon(owner.Opponent,reverse?"CS2_182":"CS2_120");
            a.Health=20;b.Health=20;owner.Opponent.Hero.Health=20;
            if(reverse)for(int i=0;i<7;i++)tie.Random.Next(3);
            var rng=tie.Random.Clone();int chosen=rng.Next(3);choices.Add(chosen);
            var stolen=(Minion)Play(tie,owner,id);var victims=new ICharacter[]{a,b,owner.Opponent.Hero};
            Core2026Proof.Check(stolen.Health==7 && victims.Select(v=>v.Health).SequenceEqual(Enumerable.Range(0,3).Select(i=>i==chosen?14:20)),
                "seeded/reordered lowest-Health tie; reselect current lowest after each unit");
        }
        Core2026Proof.Check(choices.Any(i=>i!=0),"tie vectors reject always-first selection");
        var damaged=HistoryFactsProof.Fixture();var target=Summon(damaged.Player2,"CS2_182");target.BaseHealth=8;target.Damage=7;
        int hits=damaged.HistoryFacts.Query.Damage().Count();
        Play(damaged,damaged.Player1,id);
        Core2026Proof.Check(target.BaseHealth==7 && target.Damage==7 && target.Zone==damaged.Player2.GraveyardZone
            && damaged.HistoryFacts.Query.Damage().Count()==hits,"1/8 damaged target becomes 0/7 without damage events");
    }
    private static void Unpinned_ProtoPandaren(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g,p,id);
        var pool=OfflineCardPools.Candidates(g,p.HeroClass,SabberStoneCore.CardSets.Client.BEHistoryExtPools.Spell(),132135);
        var rng=g.Random.Clone();string expected=pool[rng.Next(pool.Count)].Id;
        var entry = Add(p,"CS2_231");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 1
            && p.HandZone.Last().Card.Id == expected && p.HandZone.Last().Card.Id == p.Opponent.HandZone.Last().Card.Id, "one shared spell roll, own additions excluded");
        body.Silence(); Add(p,"CS2_231");
        Core2026Proof.Check(p.Opponent.HandZone.Count == 1, "silenced Proto does not trigger");
        var chain=HistoryFactsProof.Fixture();var owner=chain.Player1;
        var first=(Minion)Play(chain,owner,id);var second=(Minion)Play(chain,owner,id);
        Add(owner,"CS2_231");
        Core2026Proof.Check(owner.HandZone.IsFull && owner.Opponent.HandZone.IsFull,"two instances trigger each other's additions until both hands full");
        Core2026Proof.Check(chain.HistoryFacts.Query.HandEntries().Where(f=>f.SourceId==first.Id||f.SourceId==second.Id)
            .Select(f=>f.SourceId).Distinct().Count()==2,"per-instance causal exclusion permits other Proto");
        Generic.ReturnToHandBlock(owner,first);chain.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(first.Zone==owner.GraveyardZone,"full-hand bounce does not produce successful hand entry");
    }
}
