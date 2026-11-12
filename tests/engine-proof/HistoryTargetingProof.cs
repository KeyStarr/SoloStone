using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HistoryTargetingProof
{
    private static void Check(bool value,string message) => Core2026Proof.Check(value,"history targeting: "+message);
    private static EffectContext Context(Game g,IPlayable source) => new EffectContext(g,source.Controller.Id,source.Id);
    private static void Drawn()
    {
        foreach(bool indexed in new[]{false,true})
        foreach(bool noLegal in new[]{false,true})
        {
            var g=HistoryFactsProof.Fixture();var p=g.Player1;
            var elusive=Summon(p.Opponent,"CS2_231"); elusive[GameTag.CANT_BE_TARGETED_BY_SPELLS]=1;
            var stealth=Summon(p.Opponent,"CS2_231"); stealth.HasStealth=true;
            var legal=noLegal?null:Summon(p.Opponent,"CS2_231");
            var card=Cards.FromId("CS2_076").Clone();card.Tags[GameTag.CASTSWHENDRAWN]=1;
            card.Power=new Power {PowerTask=card.Power.PowerTask,TopdeckTask=new CustomTask((game,controller,source,t,stack)=>{
                var ctx=new EffectContext(game,controller.Id,source.Id);
                var chosen=ctx.RandomLegalTarget(source.Id);
                if(chosen.HasValue) Generic.CastSpell(controller,game,(Spell)source,(ICharacter)game.IdEntityDic[chosen.Value],0);
            })};
            var spell=Entity.FromCard(p,card);p.DeckZone.Add(spell);
            var rng=g.Random.Clone();
            if(indexed) Generic.Draw(p,p.DeckZone.Count-1); else Generic.Draw(p,spell);
            g.DeathProcessingAndAuraUpdate();
            var casts=g.HistoryFacts.Query.Casts().Where(f=>f.EntityId==spell.Id).ToArray();
            Check(casts.Length==2 && casts.All(f=>f.CasterKind==HistoryCastKind.Drawn)
                && !g.HistoryFacts.Query.Plays().Any(),"cast-when-drawn retains one Drawn invocation in both draw overloads");
            Check(g.HistoryFacts.Query.Deaths().Count()==(noLegal?0:1)
                && (noLegal || g.HistoryFacts.Query.Deaths().Single().EntityId==legal.Id),"TopdeckTask legal target or fizzle");
            if(noLegal) Check(g.Random.Next()==rng.Next(),"drawn fizzle consumes no RNG");
        }
    }
    internal static void Run()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        var friend=Summon(p,"CS2_231");
        var elusive=Summon(p.Opponent,"CS2_231"); elusive[GameTag.CANT_BE_TARGETED_BY_SPELLS]=1;
        var stealth=Summon(p.Opponent,"CS2_231"); stealth.HasStealth=true;
        var immune=Summon(p.Opponent,"CS2_231"); immune.IsImmune=true;
        var first=Summon(p.Opponent,"CS2_182");var second=Summon(p.Opponent,"CS2_182");
        var spell=(Spell)Entity.FromCard(p,Cards.FromId("CS2_076"));
        int[] candidates={friend.Id,p.Hero.Id,p.Opponent.Hero.Id,elusive.Id,stealth.Id,immune.Id,first.Id,second.Id};
        var selections=new HashSet<int>();
        foreach(int seed in Enumerable.Range(1,32))
        {
            g.SetRandomSeed(seed);var a=g.CloneExact();var b=g.CloneExact();
            for(int i=0;i<4;i++)
            {
                int? picked=Context(a,a.IdEntityDic[spell.Id]).RandomLegalTarget(spell.Id);
                int? same=Context(b,b.IdEntityDic[spell.Id]).RandomLegalTarget(spell.Id,candidates);
                Check(picked==first.Id || picked==second.Id,"Elusive, Stealth, Immune, friendly and hero targets excluded");
                Check(picked==same,"seeded candidates and default targeting agree"); selections.Add(picked.Value);
            }
        }
        Check(selections.SetEquals(new[]{first.Id,second.Id}),"game RNG reaches both legal candidates");
        var bytes=g.SaveSnapshot();var clone=g.CloneForSimulation(101);var sibling=g.CloneForSimulation(101);
        Check(Context(clone,clone.IdEntityDic[spell.Id]).RandomLegalTarget(spell.Id)
            ==Context(sibling,sibling.IdEntityDic[spell.Id]).RandomLegalTarget(spell.Id),"simulation seeds deterministic");
        Check(bytes.SequenceEqual(g.SaveSnapshot()),"clone selection cannot mutate authoritative RNG/history/tags");
        var pickedTarget=Context(g,spell).RandomLegalTarget(spell.Id);
        new PlayTask(PlayType.SPELL,EntityType.TARGET).Process(g,p,p.Hero,g.IdEntityDic[pickedTarget.Value],
            new TaskStack {Playables=new List<IPlayable>{spell}});
        g.DeathProcessingAndAuraUpdate();
        Check(g.HistoryFacts.Query.Casts().Count(f=>f.EntityId==spell.Id)==2
            && g.HistoryFacts.Query.Deaths().Single().EntityId==pickedTarget,"explicit-target PlayTask uses helper without duplicate casts");
        var repeated=(Spell)Entity.FromCard(p,Cards.FromId("CS2_076"));
        var repeatedTarget=Context(g,repeated).RandomLegalTarget(repeated.Id);
        Context(g,repeated).CastSpell(repeated.Id,HistoryCastKind.Repeat,repeatedTarget);
        g.DeathProcessingAndAuraUpdate();
        Check(g.HistoryFacts.Query.Casts().Count(f=>f.EntityId==repeated.Id && f.CasterKind==HistoryCastKind.Repeat)==2
            && !g.HistoryFacts.Query.Plays().Any(),"automatic recast retains Repeat and is not a play");
        var next=(Spell)Entity.FromCard(p,Cards.FromId("CS2_076"));
        var before=g.SaveSnapshot();int casts=g.HistoryFacts.Query.Casts().Count();
        var target=Context(g,next).RandomLegalTarget(next.Id);
        if(target.HasValue) Context(g,next).CastSpell(next.Id,HistoryCastKind.Repeat,target);
        Check(target==null && casts==g.HistoryFacts.Query.Casts().Count() && before.SequenceEqual(g.SaveSnapshot()),
            "no legal target fizzles without RNG, history or task effects");
        // Nontargeting effects supply their own enemy/minion subset and still apply source-specific legality.
        var effect=(Spell)Entity.FromCard(p,Cards.FromId("GAME_005"));
        Check(Context(g,effect).RandomLegalTarget(effect.Id,p.Opponent.BoardZone.Select(m=>m.Id))==null,
            "random enemy/minion effect excludes protected enemies");
        friend.HasStealth=true;friend.IsImmune=true;
        var blessing=(Spell)Entity.FromCard(p,Cards.FromId("CS2_009"));
        Check(Context(g,blessing).RandomLegalTarget(blessing.Id)==friend.Id,"friendly Stealth/Immune remain legal");
        friend[GameTag.CANT_BE_TARGETED_BY_SPELLS]=1;
        Check(Context(g,blessing).RandomLegalTarget(blessing.Id)==null,"friendly Elusive excluded");
        Drawn();
        Console.WriteLine("PASS history targeting: legal filters, 32 seeds, clone RNG isolation, TopdeckTask/PlayTask/recast, random effects, fizzle");
    }
}
