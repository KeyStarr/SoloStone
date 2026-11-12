using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B14F3PastPoolsProof
{
    [GroupProof("b14-f3-past-pools-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("END_027", Wings);
        StandardSetsProof.Case("TIME_013", Unpinned_FarseerWoPastNatureClassScope);
        StandardSetsProof.Case("TIME_016", Neon);
        StandardSetsProof.Case("TIME_444", Glaive);
        StandardSetsProof.Case("TIME_446", Pinned_EternalHoldAnyIsClassNotFormat);
        StandardSetsProof.Case("TIME_704", Unpinned_MentorPupilCapture);
        StandardSetsProof.Case("TIME_707", Alternate);
        StandardSetsProof.Case("TIME_711", Flashback);
        StandardSetsProof.Case("TIME_857", Alter);
    }
    private static IPlayable Choose(Game g, Controller p, int index = 0)
    {
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3, "three distinct historical offers");
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Select(e => e.Card.Id).Distinct().Count() == 3, "Discover has distinct definitions");
        var selected = offers[index];
        var forks=new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())};
        var decision=g.GetPendingDecisions().Single(d=>d.ControllerId==p.Id);
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{selected.Id}),out var error), "historical selection accepted: "+error);
        foreach(var fork in forks)
        {
            var owner=p==g.Player1 ? fork.Player1 : fork.Player2;
            var saved=fork.GetPendingDecisions().Single(d=>d.ControllerId==owner.Id);
            Core2026Proof.Check(fork.TryAnswer(new DecisionAnswer(saved.Id,saved.ControllerId,new[]{selected.Id}),out error),"saved historical selection resumes: "+error);
            Core2026Proof.Check(HsSim.Canonical.State(g)==HsSim.Canonical.State(fork),"same selection outcome after clone/snapshot");
        }
        return selected;
    }
    private static void Wings(Game g, Controller p, string id)
    {
        Play(g,p,id); var selected = Choose(g,p);
        Core2026Proof.Check(selected is Minion && selected.Card.IsRace(Race.DRAGON) && DarkGifts.Has(selected), "past Dragon retains displayed Dark Gift");
        Core2026Proof.Check(g.OfflinePools.Past.Any(m => m.CardId == selected.Card.Id), "Wild-exclusive Dragon");
    }
    private static void Unpinned_FarseerWoPastNatureClassScope(Game g, Controller p, string id)
    {
        var wo = (Minion)Play(g,p,id);
        Core2026Proof.Check(wo.CantBeTargetedBySpells && wo.CantBeTargetedByHeroPowers, "Wo is Elusive");
        Play(g,p,"GAME_005"); var first = Choose(g,p);
        Core2026Proof.Check(first.Card.Class == CardClass.SHAMAN, "empty Rogue Nature bucket falls back to Shaman");
        End(g); Play(g,p.Opponent,"GAME_005");
        Core2026Proof.Check(p.Choice==null,"opponent spell does not trigger Wo"); End(g);
        p.Hero.Card = Cards.FromId("HERO_06");
        Play(g,p,"GAME_005"); var choice = p.Choice;
        Core2026Proof.Check(choice.Choices.All(i => g.IdEntityDic[i].Card.Class == CardClass.DRUID), "current hero class before printed Shaman");
        new SilenceTask(EntityType.TARGET).Process(g,p,wo,wo);
        Choose(g,p);
        Play(g,p,"GAME_005"); Core2026Proof.Check(p.Choice == null, "Silence stops later triggers, not started choice");
        Kill(g,wo); Play(g,p,"GAME_005"); Core2026Proof.Check(p.Choice == null, "leave-play stops trigger");
    }
    private static void Neon(Game g, Controller p, string id)
    {
        Play(g,p,id); var chosen = Choose(g,p);
        Core2026Proof.Check(chosen.Card.Class == CardClass.PALADIN && chosen.Card.IsRace(Race.MECHANICAL)
            && ((Minion)chosen).AttackDamage == chosen.Card.ATK+5 && ((Minion)chosen).Health == chosen.Card.Health+5, "Paladin-only past Mech +5/+5");
        while(p.HandZone.Count<9) Add(p,"CS2_182");
        Play(g,p,id); Add(p,"CS2_182");
        var burned=Choose(g,p);
        Core2026Proof.Check(p.HandZone.Count==10 && burned.Zone!=p.HandZone,"full-hand selected Mech burns once");
    }
    private static void Glaive(Game g, Controller p, string id)
    {
        Play(g,p,id); Play(g,p,"CS2_106");
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.IsRace(Race.DEMON)
            && g.OfflinePools.Past.Any(m=>m.CardId==p.HandZone[0].Card.Id), "destroyed Glaive gives past Demon");
    }
    private static void Pinned_EternalHoldAnyIsClassNotFormat(Game g, Controller p, string id)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var location = LocationProofs.Play(p,id); LocationProofs.Use(location);
        Core2026Proof.Check(!g.IdEntityDic.Values.OfType<Enchantment>().Any(e=>e.Card.Id=="TIME_446e"),"next-minion rule follows the Discover");
        Core2026Proof.Check(p.Choice.Choices.All(i=>g.OfflinePools.Standard.Any(m=>m.CardId==g.IdEntityDic[i].Card.Id)),"any class does not bypass Standard format");
        var demon = Choose(g,p);
        Core2026Proof.Check(demon.Card.Cost>=5 && demon.Card.IsRace(Race.DEMON) && demon.Cost==1,
            "any-class Standard Demon and next-minion SET-1");
        var other = Add(p,"CS2_182"); Core2026Proof.Check(other.Cost==1,"discount applies to next minion, independently of choice");
        PlayEntity(g,p,other); Core2026Proof.Check(demon.Cost==demon.Card.Cost,"one-use rule consumed");
        bool offeredWild=false;
        for(int seed=1;seed<=12;seed++)
        {
            var wild=PastLegacyQualificationProof.New(seed);var owner=wild.Player1;
            owner.BaseMana=10;
            var hold=LocationProofs.Play(owner,id);LocationProofs.Use(hold);
            offeredWild|=owner.Choice.Choices.Any(i=>wild.OfflinePools.Past.Any(m=>m.CardId==wild.IdEntityDic[i].Card.Id));
            var chosen=Choose(wild,owner);
            Core2026Proof.Check(chosen.Cost==chosen.Card.Cost && !wild.IdEntityDic.Values.OfType<Enchantment>().Any(e=>e.Card.Id=="TIME_446e"),
                "minion remaining in deck prevents next-minion rule");
        }
        Core2026Proof.Check(offeredWild,"Wild format includes qualified historical Demons");
    }
    private static void Unpinned_MentorPupilCapture(Game g, Controller p, string id)
    {
        Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==1 && p.HandZone[0].Card.Id=="TIME_704t", "only Pupil enters hand before teaching");
        var pupil = p.HandZone[0]; var selected=Choose(g,p);
        Core2026Proof.Check(p.HandZone.Count==1 && PayloadRules.Entries(pupil,1).Single().Card.Id==selected.Card.Id,
            "chosen expensive Druid spell stored on Pupil, no spell hand addition");
        var copy=Generic.Copy(p,pupil,pupil,Zone.HAND);
        Core2026Proof.Check(PayloadRules.Entries(copy,1).Single().Card.Id==selected.Card.Id, "Pupil copy retains independent taught definition");
        PlayEntity(g,p,pupil);
        Core2026Proof.Check(g.HistoryFacts.Query.Casts().Any(c=>c.DbfId==selected.Card.AssetId), "Pupil automatically casts taught spell");
    }
    private static void Alternate(Game g, Controller p, string id)
    {
        Add(p,"CS2_182"); Add(p,"CS2_182"); var old=p.HandZone.Concat(p.DeckZone).Select(e=>e.Id).ToHashSet();
        int deck=p.DeckZone.Count; Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==2 && p.DeckZone.Count==deck,"replacement preserves hand/deck counts");
        Core2026Proof.Check(p.HandZone.Concat(p.DeckZone).All(e=>!old.Contains(e.Id) && e.Card.ChooseOne && e.Cost==Math.Max(0,e.Card.Cost-1)),
            "fresh past Choose One cards discounted once");
    }
    private static void Flashback(Game g, Controller p, string id)
    {
        Play(g,p,id); Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.All(e=>e.AttackDamage==e.Card.ATK),"two past 1-Cost summons without Combo");
        StandardSetsProof.ParkBoard(p); Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.All(e=>e.AttackDamage==e.Card.ATK+1),"Combo grants +1 Attack");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); Play(g,p,id); Core2026Proof.Check(p.BoardZone.SlotCount==7,"full-board summons fizzle");
    }
    private static void Alter(Game g, Controller p, string id)
    {
        p.Hero.Card=Cards.FromId("HERO_08"); Play(g,p,id); Choose(g,p,1);
        Core2026Proof.Check(p.Choice!=null,"second Arcane Discover opens after first answer");
        Choose(g,p,2);
        Core2026Proof.Check(p.HandZone.Count==2 && p.HandZone.All(e=>e.Cost==Math.Max(0,e.Card.Cost-2)),"two historical Arcane spells cost 2 less");
    }
}
