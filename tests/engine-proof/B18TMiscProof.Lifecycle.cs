using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static partial class B18TMiscProof
{
    private static void Assert(bool value,string message) => Core2026Proof.Check(value,message);
    private static void Unpinned_InfinityLifecycle()
    {
        for(int seed=1;seed<=4;seed++) {
            var g=DiscoverRealProof.New(seed); var p=g.Player1;
            var hand=new[] { Add(p,"CS2_200"),Add(p,"CS2_182"),Add(p,"CS2_231") };
            var rng=g.Random.Clone(); int index=rng.Next(3);
            var host=Summon(p,"END_018"); InfinityRules.MarkCost(host);
            Assert(hand[index].Cost==int.MaxValue && hand.Where((c,i)=>i!=index).All(c=>c.Cost==c.Card.Cost),"seeded ordered own-hand pick");
            var target=(Playable)hand[index];
            var copy=Generic.Copy(p,host,target,Zone.HAND);
            var copiedHost=(Minion)Generic.Copy(p,host,host,Zone.PLAY,preserveBoardMinionState:true);
            g.DeathProcessingAndAuraUpdate();
            target.Cost=4;
            Assert(target.Cost==4,"finite SET overrides attached Infinity");
            DiscoverRealProof.Saved(g,next=>{
                Kill(next,(Minion)next.IdEntityDic[copiedHost.Id]);
                Assert(next.IdEntityDic[target.Id].Cost==4 && next.IdEntityDic[copy.Id].Cost==int.MaxValue,"copied host restores original only; later SET survives");
                Kill(next,(Minion)next.IdEntityDic[host.Id]);
                Assert(next.IdEntityDic[target.Id].Cost==4,"second restoration no-op");
            });
        }
        foreach(bool silence in new[] { false,true }) {
            var g=DiscoverRealProof.New();var p=g.Player1;
            var target=Add(p,"CS2_200");var first=(Minion)Play(g,p,"END_018");var second=(Minion)Play(g,p,"END_018");
            Assert(target.Cost==int.MaxValue,"two marks coexist");
            DiscoverRealProof.Saved(g,next=>{
                var a=(Minion)next.IdEntityDic[first.Id]; if(silence) a.Silence();
                Kill(next,a);Assert(next.IdEntityDic[target.Id].Cost==int.MaxValue,"other mark or silenced source persists");
                Kill(next,(Minion)next.IdEntityDic[second.Id]);
                Assert(next.IdEntityDic[target.Id].Cost==(silence?int.MaxValue:6),"remove only owned application");
            });
        }
        {
            var g=DiscoverRealProof.New();var p=g.Player1;
            var target=Add(p,"CS2_200");var host=(Minion)Play(g,p,"END_018");
            p.SetasideZone.Add(p.HandZone.RemoveForTrade(target)); Generic.AddHandPhase(p,p.SetasideZone.Remove(target));
            Kill(g,host); Assert(target.Cost==int.MaxValue,"old hand residence never restored");
        }
        {
            var g=DiscoverRealProof.New();var p=g.Player1;var host=Summon(p,"END_018");
            InfinityRules.MarkCost(host);Assert(PayloadRules.Entries(host).Length==0,"empty hand has no substitute mark");
        }
    }
    private static void Unpinned_InfinityAuras()
    {
        var g=DiscoverRealProof.New();var p=g.Player1;
        var aura=Summon(p,"EX1_608");var card=Add(p,"CS2_029");g.AuraUpdate();
        var host=(Minion)Play(g,p,"END_018");
        Assert(card.Cost==int.MaxValue-1,"external spell discount remains outside Infinity");
        CostRules.AddModifier((Playable)card,EffectOperator.SUB,1);
        DiscoverRealProof.Saved(g,next=>{
            Kill(next,(Minion)next.IdEntityDic[host.Id]);Assert(next.IdEntityDic[card.Id].Cost==2,"restored permanent value excludes aura");
            Kill(next,(Minion)next.IdEntityDic[aura.Id]);Assert(next.IdEntityDic[card.Id].Cost==3,"aura removal exposes same permanent state");
        });
        var signed=DiscoverRealProof.New();var owner=signed.Player1;var target=Add(owner,"CS2_200");
        CostRules.AddModifier((Playable)target,EffectOperator.SUB,8);
        var acolyte=(Minion)Play(signed,owner,"END_018");Kill(signed,acolyte);
        CostRules.AddModifier((Playable)target,EffectOperator.ADD,3);Assert(target.Cost==1,"signed permanent input survives mark/removal");
        CostRules.RestorePrintedCost((Playable)target);Assert(target.Cost==6,"printed reset supersedes retired journal");
    }
    private static void PinnedRule_PassiveLifecycle()
    {
        var g=DiscoverRealProof.New();var p=g.Player1;var source=Summon(p,"BE_004");
        var definition=Card.CreateAssembledCard(Cards.FromId("CATA_133"),Cards.FromId("CATA_133"),Cards.FromId("CATA_999"));
        var power=BasicHeroPowerRules.InstallPassive(p,source,definition);
        Assert(p.Options().All(t=>t.PlayerTaskType!=SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_POWER),"passive power absent from legal options");
        Assert(power.IsPassiveHeroPower && !power.IsPlayableByPlayer && p.Hero.CantBeTargetedBySpells,"passive not clickable; keyword affects hero");
        DiscoverRealProof.Saved(g,next=>{
            End(next);Assert(next.Player2.Hero.Health==26,"owner end composed payload");
            End(next);Assert(next.Player2.Hero.Health==26,"opponent end excluded");
            new ReplaceHeroPower(Cards.FromId(BasicHeroPowerRules.PowerId(next.Player1.HeroClass)))
                .Process(next,next.Player1,next.IdEntityDic[source.Id],null);
            Assert(!next.Player1.Hero.CantBeTargetedBySpells,"replacement removes keyword grant");
            End(next);Assert(next.Player2.Hero.Health==26,"replacement detaches listener");
        });
        {
            // "Assembled with {0}" names the half that lent the keyword through a stored copy, retired with the grant.
            var named=DiscoverRealProof.New();var c=named.Player1;var rajh=Summon(c,"BE_004");
            var shield=Card.CreateAssembledCard(Cards.FromId("TIME_100"),Cards.FromId("TIME_100"),Cards.FromId("CATA_999"));
            var installed=BasicHeroPowerRules.InstallPassive(c,rajh,shield);
            var grant=c.Hero.AppliedEnchantments.Single(e=>e.Card.Id=="BE_044e");
            var half=PayloadRules.Entries(grant,1).Single();
            Assert(half.Card.AssetId==installed.Card[AssembleRules.FirstHalf] && grant[PayloadRules.ReferenceEntity]==half.Id
                && ((PayloadPolicy)half[PayloadRules.Policy]).HasFlag(PayloadPolicy.ShowOwner) && !((PayloadPolicy)half[PayloadRules.Policy]).HasFlag(PayloadPolicy.ShowAll),
                "Assembled grant stores the keyword's half, owner-only");
            foreach(var next in new[]{named.CloneExact(true),Game.LoadSnapshot(named.SaveSnapshot())})
            {
                var cloned=next.Player1.Hero.AppliedEnchantments.Single(e=>e.Card.Id=="BE_044e");
                Assert(cloned[PayloadRules.ReferenceEntity]==half.Id && PayloadRules.Entries(cloned,1).Single().Id==half.Id,"clone/load keep the Assembled half");
            }
            new ReplaceHeroPower(Cards.FromId(BasicHeroPowerRules.PowerId(c.HeroClass))).Process(named,c,rajh,null);
            Assert(PayloadRules.Entries(grant,1).Length==0,"replacing the passive retires its stored half");
        }
        var swap=DiscoverRealProof.New();var owner=swap.Player1;var maker=Summon(owner,"BE_004");
        var saved=BasicHeroPowerRules.InstallPassive(owner,maker,definition);
        BasicHeroPowerRules.SwapForTwoUses(owner,maker);Assert(!owner.Hero.CantBeTargetedBySpells,"temporary swap detaches keyword");
        BasicHeroPowerRules.AfterUse(owner.Hero.HeroPower);BasicHeroPowerRules.AfterUse(owner.Hero.HeroPower);
        Assert(owner.Hero.HeroPower.Id==saved.Id && owner.Hero.CantBeTargetedBySpells,"temporary return reactivates passive");
        End(swap);Assert(owner.Opponent.Hero.Health==26,"returned listener runs exactly once");
        foreach(bool history in new[]{false,true}) {
            var shield=new Game(new SabberStoneCore.Config.GameConfig { RandomSeed=1,StartPlayer=1,Shuffle=false,SkipMulligan=true,History=history,Logging=false,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            shield.StartGame();var c=shield.Player1;var rajh=Summon(c,"BE_004");
            var shieldDefinition=Card.CreateAssembledCard(Cards.FromId("TIME_100"),Cards.FromId("TIME_100"),Cards.FromId("CATA_999"));
            BasicHeroPowerRules.InstallPassive(c,rajh,shieldDefinition);
            Assert(c.Hero.HasDivineShield,"Divine Shield belongs to the hero");
            new ReplaceHeroPower(Cards.FromId(BasicHeroPowerRules.PowerId(c.HeroClass))).Process(shield,c,rajh,null);
            Assert(!c.Hero.HasDivineShield,"passive Shield cleanup works without presentation history");
            string before=HsSim.Canonical.State(shield);
            foreach (var copy in new[] { shield.CloneExact(history), Game.LoadSnapshot(shield.SaveSnapshot()) })
                Assert(HsSim.Canonical.State(copy)==before && !copy.Player1.Hero.HasDivineShield,
                    "inactive passive survives clone/load without cleanup or state mutation");
        }
    }
}
