using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    private static string[] B18Beasts(bool activeOnly = false) => Cards.All.Where(c=>DeckbuildingRules.Eligible("JAIL_831",c,FormatType.FT_STANDARD)
            && (!activeOnly || OfflinePoolPolicy.Load().AllowsCard(c)))
        .OrderBy(c=>c.Id,StringComparer.Ordinal).Take(3).Select(c=>c.Id).ToArray();
    [GroupProof("native-b18-o-deck", ProofKind.Native)]
    private static void B18ODeckNative()
    {
        B18ProfileAndNativeSave();
        foreach(bool enemy in new[]{false,true})
        {
            var f=new Fixture();var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(f.Player));
            var p=enemy?g.Player2:g.Player1;p.BaseMana=10;p.UsedMana=0;
            var slots=B18Beasts().Select(id=>PayloadRules.StoreCard(p,125998,Cards.FromId(id),0)).ToArray();
            var king=Generic.DrawCard(p,Cards.FromId("JAIL_831"));
            f.Process(PlayCardTask.Any(p,king));
            var offers=p.Choice.Choices.ToArray();
            f.Process(ChooseTask.Pick(p,offers[1]));
            var reward=p.HandZone.Single(c=>c.Card.Id==slots[1].Card.Id);
            Check(reward.Cost==Math.Max(0,reward.Card.Cost-3),"native choice returns discounted Beast");
            SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("JAIL_397"),zone:p.DeckZone);
            PayloadRules.StoreCard(p,126621,Cards.FromId("EX1_015"),0);
            typeof(DeckbuildingRules).GetMethod("Prepare",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{g});
            f.Flush();
            var hidden=slots.Select(c=>c.Id).Concat(offers).Concat(p.DeckZone.Select(c=>c.Id)).Append(reward.Id).ToArray();
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if(enemy) Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent sideboard/offers/generated hand/deck identities stay private");
            Check(actors.All(e=>e.Tags.All(t=>!PayloadRules.IsInternalTag((GameTag)t.Name))),"no payload tags in native entities");
            Check(f.Entries.Where(e=>e.HasTagChange).All(e=>!PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),"no payload tag deltas");
        }
        Console.WriteLine("PASS B18 deck native privacy, current-schema sideboards and 40-card save/reload");
    }
    private static CollectionDeck B18Deck(LocalCustomDeck saved, IEnumerable<string> ids)
    {
        var deck=new CollectionDeck { ID=saved.Id,Name=saved.Name,HeroCardID=saved.Hero,
            Type=PegasusShared.DeckType.NORMAL_DECK,FormatType=NativeUIProbe.LocalDeckFormat,
            SourceType=PegasusShared.DeckSourceType.DECK_SOURCE_TYPE_NORMAL };
        foreach(var group in ids.GroupBy(id=>id))
        {
            var slot=new CollectionDeckSlot { CardID=group.Key };slot.SetCount(group.Count(),TAG_PREMIUM.NORMAL);deck.GetSlots().Add(slot);
        }
        return deck;
    }
    // Native constructors require Unity DefLoader; seed their ordinary collection-slot state for the shared save boundary.
    private static void B18SideboardFixture(CollectionDeck parent, IEnumerable<LocalSideboard> rows)
    {
        foreach(var row in rows)
        {
            var side=(SideboardDeck)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SideboardDeck));
            var empty=new CollectionDeck();
            for(var type=typeof(CollectionDeck);type!=null;type=type.BaseType)
                foreach(var field in type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                    field.SetValue(side,field.GetValue(empty));
            typeof(SideboardDeck).GetField("<MainDeck>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(side,parent);
            foreach(string id in row.Cards)
            {
                var slot=new CollectionDeckSlot { CardID=id };slot.SetCount(1,TAG_PREMIUM.NORMAL);side.GetSlots().Add(slot);
            }
            parent.GetAllSideboards().Add(row.Source,side);
        }
    }
    private static void B18ProfileAndNativeSave()
    {
        B18RecipeNativeSave();
        string dir=Path.Combine(Path.GetTempPath(),"b18-o-deck-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"profile.xml");long rafaamId,kingId,beatrixId;string[] forty;
        try
        {
            using(var profile=new LocalProfile(path))
            {
                // No virtual ownership (owner 2026-10-30): the fixture owns one real Rafaam; companions follow it.
                Check(!profile.Collection.ContainsKey("TIME_005"),"a fresh profile does not own Timethief Rafaam");
                profile.CommitChange(next=>{next.Collection.Add(new LocalCardCount{Id="TIME_005",Count=1,Acquired=1});return true;});
                var draft=profile.CreateDraft("Forty", "HERO_07");rafaamId=draft.Id;
                var filler=profile.Collection.Where(row=>!FabledRules.IsCompanion(row.Key) && !FabledRules.IsFabled(row.Key))
                    .Select(row=>new { Card=Cards.FromId(row.Key),row.Value })
                    .Where(row=>LocalDecks.IsAllowedInDeck(row.Card,CardClass.WARLOCK))
                    .OrderBy(row=>row.Card.Id,StringComparer.Ordinal)
                    .SelectMany(row=>Enumerable.Repeat(row.Card.Id,Math.Min(row.Value,LocalEconomy.PlayableCap(row.Card)))).Take(30).ToArray();
                forty=FabledRules.ExpandForDeckbuilding(new[]{"TIME_005"}).Concat(filler).ToArray();
                Check(forty.Length==40,"legal owned Fabled fixture has forty cards");
                var owned=profile.Collection.ToDictionary(row=>row.Key,row=>row.Value);
                NativeUIProjection.ValidateSelection("custom",draft.Hero,forty,owned);
                Check(NativeUIProjection.ValidateDraftProjection(draft.Id,draft.Name,draft.Hero,forty,draft.Name,draft.Hero,forty,owned),
                    "native forty-card selection/completeness includes companions");
                Check(FabledRules.Bundle("TIME_005").All(id=>NativeUIProjection.ForcesTestPlayability(true,id)),"Fabled companions available to native editor");
                var working=B18Deck(draft,forty);var prior=B18Deck(draft,Array.Empty<string>());
                var saved=NativeUIProbe.CommitLocalDraft(profile,working,prior);
                Check(LocalDecks.Custom(saved.Name,saved.Hero,saved.Cards).Build().Count==40,"40 accepted by native save, custom validator, engine builder");
                byte[] before=File.ReadAllBytes(path);
                foreach(var ids in new[]{forty.Append("CS2_182").ToArray(),filler.Append("CS2_182").ToArray()})
                {
                    bool rejected=false;
                    try { NativeUIProbe.CommitLocalDraft(profile,B18Deck(draft,ids),prior); } catch(InvalidOperationException) {rejected=true;}
                    Check(rejected && File.ReadAllBytes(path).SequenceEqual(before),"41 Fabled / ordinary 31 rejected atomically");
                }
                var king=profile.CreateDraft("Contraband","HERO_05");kingId=king.Id;
                // BE choices still exercise the native proof above; player saves enforce the active meta.
                before=File.ReadAllBytes(path);
                var inactive=B18Deck(king,new[]{"JAIL_831"});
                B18SideboardFixture(inactive,new[]{new LocalSideboard {Source="JAIL_831",Cards=B18Beasts().ToList()}});
                bool inactiveRejected=false;
                try {NativeUIProbe.CommitLocalDraft(profile,inactive,B18Deck(king,Array.Empty<string>()));}
                catch(InvalidOperationException) {inactiveRejected=true;}
                Check(inactiveRejected && File.ReadAllBytes(path).SequenceEqual(before),"inactive sideboard native save rejected atomically");
                var rows=new[]{new LocalSideboard {Source="JAIL_831",Cards=B18Beasts(activeOnly:true).ToList()}};
                var kingDeck=B18Deck(king,new[]{"JAIL_831"});
                B18SideboardFixture(kingDeck,rows);
                var persisted=NativeUIProbe.CommitLocalDraft(profile,kingDeck,B18Deck(king,Array.Empty<string>()));
                Check(persisted.Sideboards.Single().Cards.SequenceEqual(rows[0].Cards),"native sideboard slots persist outside main slots");
                var reload=B18Deck(persisted,persisted.Cards);B18SideboardFixture(reload,persisted.Sideboards);
                Check(NativeUIProbe.SameSideboards(kingDeck,reload),"native sideboard projection round trip");
                var beatrix=profile.CreateDraft("Beatrix","HERO_04");beatrixId=beatrix.Id;
                var main=profile.Collection.Where(row=>row.Key!="EX1_015" && !FabledRules.IsFabled(row.Key) && !FabledRules.IsCompanion(row.Key) && !DeckbuildingRules.HasSideboard(row.Key))
                    .Select(row=>new {Card=Cards.FromId(row.Key),row.Value}).Where(row=>LocalDecks.IsAllowedInDeck(row.Card,CardClass.PALADIN))
                    .OrderBy(row=>row.Card.Id,StringComparer.Ordinal).SelectMany(row=>Enumerable.Repeat(row.Card.Id,Math.Min(row.Value,LocalEconomy.PlayableCap(row.Card))))
                    .Take(29).Prepend("JAIL_397").ToArray();
                var beatrixDeck=B18Deck(beatrix,main);
                B18SideboardFixture(beatrixDeck,new[]{new LocalSideboard {Source="JAIL_397",Cards=new List<string>{"EX1_015"}}});
                var savedBeatrix=NativeUIProbe.CommitLocalDraft(profile,beatrixDeck,B18Deck(beatrix,Array.Empty<string>()));
                var local=LocalDecks.Custom(savedBeatrix.Name,savedBeatrix.Hero,savedBeatrix.Cards).WithSideboards(savedBeatrix.Sideboards,true);
                var match=new NativeMatch((id,message)=>{},local,"mage",19);match.Start();
                Check(match.EngineStartCount==40 && match.Game.Player1.DeckCards.Count==30,"native startup preserves selected Beatrix expansion and submitted thirty");
                before=File.ReadAllBytes(path);
                var invalid=B18Deck(beatrix,new[]{"JAIL_397","EX1_015"});
                B18SideboardFixture(invalid,savedBeatrix.Sideboards);
                bool failed=false;try {NativeUIProbe.CommitLocalDraft(profile,invalid,beatrixDeck);}catch(InvalidOperationException){failed=true;}
                Check(failed && File.ReadAllBytes(path).SequenceEqual(before),"main copy exclusion leaves persisted selections unchanged");
            }
            using(var profile=new LocalProfile(path))
            {
                Check(profile.GetDraft(rafaamId).Cards.SequenceEqual(forty),"40-card profile restart preserves all companions");
                Check(profile.GetDraft(kingId).Sideboards.Single().Cards.SequenceEqual(B18Beasts(activeOnly:true)),"sideboard profile restart preserves order");
                Check(profile.GetDraft(beatrixId).Sideboards.Single().Cards.SequenceEqual(new[]{"EX1_015"}),"Beatrix selected identity survives restart");
            }
        }
        finally { Directory.Delete(dir,true); }
    }
    [GroupProof("powered-up-b18-o-deck", ProofKind.PoweredUp)]
    private static void B18ODeckGlow()
    {
        var f=new Fixture();
        foreach(var id in new[]{"JAIL_397","JAIL_831"}) Check(!PoweredUp.IsMet(f.Draw(id)),"unconditional card has no glow "+id);
    }
}
