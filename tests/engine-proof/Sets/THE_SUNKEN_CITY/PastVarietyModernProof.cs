using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class PastVarietyModernProof
{
    internal static readonly string[] Ids = { "YOD_010", "TSC_059", "NX2_022", "ONY_021", "ETC_373", "TSC_656", "SCH_609", "DEEP_028" };
    private static readonly string[] Tokens = { "ONY_001t", "ETC_373a", "ETC_373b", "ETC_373t", "ETC_373be", "TSC_656t", "SCH_609e", "DEEP_028t" };
    [GroupProof("pastvar-modern")]
    internal static void Facility()
    {
        ExportAndScope();
        SummonPlayability();
        Unpinned_DormantExclusions();
        PoolsAndGeneration();
        Unpinned_MentorCasts();
        AlternateAndPrivacy();
        RewindState();
    }
    [GroupProof("pastvar-modern-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach(var id in Ids) StandardSetsProof.Case(id, (g,p,card) => {
            // The accepted catalog may not yet include this module. Its pinned definition is already identical.
            var generated = Definition(g,p,card);
            Core2026Proof.Check(generated == Cards.FromId(card), "generated modern definition is pinned module " + card);
            if (generated.Type == CardType.MINION) Mech(g,p,card);
            else foreach(int branch in card == "ETC_373" ? new[]{1,2} : new[]{0}) {
                SpellCase(g, card, false, branch);
            }
        });
        // Each seat drives the ordinary play path, independently of the Standard aggregate fixture.
        foreach(var id in Ids) {
            var g=Fresh(19); Core2026Proof.End(g);
            if(Cards.FromId(id).Type==CardType.MINION) Mech(g,g.CurrentPlayer,id);
            else SpellCase(g,id,false,id=="ETC_373"?2:0);
        }
    }
    private static Game Fresh(int seed=11, OfflinePoolManifest pools=null)
    {
        var g=PastLegacyQualificationProof.New(seed);
        if(pools!=null) g.RestoreOfflinePools(pools);
        g.Player1.Hero[GameTag.CLASS]=(int)CardClass.DRUID;
        g.Player2.Hero[GameTag.CLASS]=(int)CardClass.PALADIN;
        return g;
    }
    private static Card Definition(Game g, Controller p, string id) =>
        OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(scope:PoolScope.Past),0).SingleOrDefault(c=>c.Id==id) ?? Cards.FromId(id);
    private static void Resume(Game g, Action<Game> continuation)
    {
        Core2026Proof.Check(Canonical.State(g)==Canonical.State(g.CloneExact(history:true)),"modern exact clone");
        Core2026Proof.Check(Canonical.State(g)==Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools)),"modern snapshot");
        foreach(var fork in new[]{g.CloneExact(history:true),Game.LoadSnapshotWithPools(g.SaveSnapshot(),g.OfflinePools),g}) continuation(fork);
    }
    private static bool Answer(Game g,int selected)
    {
        var d=g.GetPendingDecisions().Single();
        bool accepted=g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{selected}),out var error);
        Core2026Proof.Check(accepted,"modern decision answer: "+error);return accepted;
    }
    private static void Play(Game g, Controller p, IPlayable card, int branch=0)
    {
        p.BaseMana=10;p.UsedMana=0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card,chooseOne:branch)),"modern real play "+card.Card.Id);
    }
    private static void SummonPlayability()
    {
        var g=Fresh();var p=g.Player1;p.BaseMana=10;
        for(int i=0;i<7;i++)Core2026Proof.Summon(p,"CS2_182");
        var scale=Core2026Proof.Add(p,"ONY_021");var drum=Core2026Proof.Add(p,"ETC_373");
        Core2026Proof.Check(!p.Options().Any(t=>t.Source?.Id==scale.Id),"Scale summon-only spell unavailable on full board");
        Core2026Proof.Check(!p.Options().Any(t=>t.Source?.Id==drum.Id&&t.ChooseOne==1)
            &&p.Options().Any(t=>t.Source?.Id==drum.Id&&t.ChooseOne==2),"full board hides Drum summon branch, keeps buff branch clickable");
        Resume(g,next=> {
            var owner=next.CurrentPlayer;
            foreach(string id in new[]{"ONY_021","ETC_373"}) {
                var spell=(Spell)Entity.FromCard(owner,Definition(next,owner,id));
                Generic.CastAutomaticSpell(owner,next,spell,null,id=="ETC_373"?1:0);
                next.ProcessTasks();next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(owner.BoardZone.Count==7&&owner.BoardZone.All(m=>m.Card.Id=="CS2_182"&&m.AttackDamage==4&&m.Health==5),"full-board automatic summon cast does not add, replace or buff minions "+id);
                Core2026Proof.Check(next.HistoryFacts.Query.Casts().Any(c=>c.EntityId==spell.Id),"automatic failed summon still records its cast");
            }
        });
    }
    private static void ExportAndScope()
    {
        using var f=File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");using var gz=new GZipStream(f,CompressionMode.Decompress);using var doc=JsonDocument.Parse(gz);
        var rows=doc.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c=>c.GetProperty("cardId").GetString());
        var policy=OfflinePoolPolicy.Load();
        foreach(string id in Ids.Concat(Tokens)) {
            var card=Cards.FromId(id);var row=rows[id];
            Core2026Proof.Check(card.Implemented && card.ExcludeFromLegacyRandomPools && EffectModules.ByDbfId[card.AssetId]==id,"modern registered "+id);
            Core2026Proof.Check(card.AssetId==row.GetProperty("dbfId").GetInt32() && card.Cost==row.GetProperty("cost").GetInt32()
                && card.ATK==row.GetProperty("atk").GetInt32() && card.Health==row.GetProperty("health").GetInt32() && card.Text==row.GetProperty("text").GetString(),"modern pinned identity "+id);
            foreach(var tag in row.GetProperty("tags").EnumerateObject()) Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name))==tag.Value.GetInt32(),"modern pinned tag "+id+":"+tag.Name);
            Core2026Proof.Check(!Cards.IsFormatLegal(card,FormatType.FT_STANDARD) && !LocalShop.IsStandardCard(card) && !policy.IsPackEligible(card),"historical card excluded from Standard and packs "+id);
        }
        foreach(string id in Ids) {
            Core2026Proof.Check(!LocalDecks.All.Any(d=>d.CardIds.Contains(id)) && !LocalShop.StandardCollectionCards.Any(c=>c.Id==id) && !LocalCardCatalog.Supported.Any(c=>c.Id==id),"historical card excluded from collection/shop catalog "+id);
            Core2026Proof.Check(!FairPackRules.Eligible(FairPackRules.Standard,FairPackRules.RuleVersion).Any(c=>c.Id==id),"historical card excluded from fair packs "+id);
            Core2026Proof.Check(!OfflinePoolManifest.Installed.Standard.Any(c=>c.CardId==id),"historical module never Standard pool "+id);
        }
        Console.WriteLine("PASS modern exact export: eight cards/eight ancillary identities; Standard/collection/shop/pack exclusions");
    }
    private static void Mech(Game g, Controller p, string id)
    {
        var other=Core2026Proof.Summon(p,"YOD_010");var enemy=Core2026Proof.Summon(p.Opponent,"YOD_010");var plain=Core2026Proof.Summon(p,"CS2_182");
        var held=Generic.DrawCard(p,Definition(g,p,"YOD_010"));
        var source=(Minion)Generic.DrawCard(p,Definition(g,p,id));Play(g,p,source);
        if(id=="YOD_010") {
            Resume(g,next=> {
                var body=(Minion)next.IdEntityDic[source.Id];Core2026Proof.Kill(next,body);
                var reborn=body.Controller.BoardZone.Single(m=>m.Id!=other.Id && m.Card.Id==id);
                Core2026Proof.Check(reborn.AttackDamage==2 && reborn.Health==1 && reborn[GameTag.REBORN]==0,"Shotbot real Reborn once, 2/1");
                Core2026Proof.Kill(next,reborn);Core2026Proof.Check(body.Controller.BoardZone.All(m=>m.Id==other.Id||m.Id==plain.Id),"Reborn spent");
            });
            var silenced=Core2026Proof.Summon(p,id);silenced.Silence();g.DeathProcessingAndAuraUpdate();Core2026Proof.Kill(g,silenced);
            Core2026Proof.Check(p.BoardZone.Count==2,"Silenced Shotbot never Reborn");
        }
        if(id=="TSC_059") {
            Core2026Proof.Check(other.HasDivineShield&&other.HasTaunt&&!source.HasDivineShield&&!source.HasTaunt&&!enemy.HasTaunt&&!plain.HasTaunt,"Bubblebot other friendly Mechs only");
            Core2026Proof.Check(!((Minion)held).HasTaunt,"Bubblebot excludes hand");
            Resume(g,next=> {
                var recipient=(Minion)next.IdEntityDic[other.Id];var body=(Minion)next.IdEntityDic[source.Id];
                body.Silence();Core2026Proof.Kill(next,body);
                Core2026Proof.Check(recipient.HasDivineShield&&recipient.HasTaunt,"Bubblebot buff outlives source");
                Core2026Proof.Check(!Core2026Proof.Summon(recipient.Controller,id).HasTaunt,"summon does not run Battlecry");
                recipient.Silence();next.DeathProcessingAndAuraUpdate();Core2026Proof.Check(!recipient.HasDivineShield&&!recipient.HasTaunt,"recipient Silence removes keywords");
            });
        }
        if(id=="NX2_022") {
            Core2026Proof.Check(source.HasWindfury && source[GameTag.RUSH]==1,"Goldwing holding Mech grants Windfury with printed Rush");
            Resume(g,next=> {
                var body=(Minion)next.IdEntityDic[source.Id];body.Silence();next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(!body.HasWindfury&&body[GameTag.RUSH]==0,"Goldwing Silence removes keywords");
            });
            foreach(var hand in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(hand));
            var solo=(Minion)Generic.DrawCard(p,Definition(g,p,id));Play(g,p,solo);
            Core2026Proof.Check(!solo.HasWindfury,"Goldwing cannot count itself; board and opponent Mechs do not qualify");
            Core2026Proof.Check(!Core2026Proof.Summon(p,id).HasWindfury,"Goldwing summon never runs Battlecry");
        }
    }
}
