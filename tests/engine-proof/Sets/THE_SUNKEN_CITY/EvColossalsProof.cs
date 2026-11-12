using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static PoolPilotProof;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static partial class EvColossalsProof
{
    internal static readonly string[] Ids = { "TSC_026", "TSC_029", "TSC_030", "TSC_216", "TSC_937", "TSC_639", "TSC_950", "TSC_219", "TSC_962", "TSC_660", "TID_711", "TID_712", "NX2_037" };
    [GroupProof("ev-colossals")]
    internal static void Facility() { foreach (var id in Ids) Core2026Proof.Check(Cards.FromId(id)?.Implemented == true, "missing historical Colossal " + id); ExportedTokens(); Unpinned_Lord(); Unpinned_StatePrivacyAndRewind(); }
    [GroupProof("ev-colossals-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (var id in Ids) StandardSetsProof.Case(id, Unpinned_Body);
        var args=System.Environment.GetCommandLineArgs();int evidence=System.Array.IndexOf(args,"--evidence");
        if(evidence>=0) PoolProofEvidence.Write(args[evidence+1]);
    }
    private static void Unpinned_Body(Game g, Controller p, string id)
    {
        Core2026Proof.Check(!Cards.IsFormatLegal(Cards.FromId(id), SabberStoneCore.Enums.FormatType.FT_STANDARD), "historical body is not Standard-deckable");
        string[] expectedOffers=null;
        if(id=="TSC_660") {
            var candidates=OfflineCardPools.Candidates(g,p.HeroClass,NellieTheGreatThresher.Pirates(),74894).ToList();var rng=g.Random.Clone();var chosen=new System.Collections.Generic.List<string>();
            while(candidates.Count>0 && chosen.Count<3){int index=rng.Next(candidates.Count);chosen.Add(candidates[index].Id);candidates.RemoveAt(index);}expectedOffers=chosen.ToArray();
        }
        var body = (Minion)Play(g,p,id);
        if(expectedOffers!=null)Core2026Proof.Check((p.Choice?.Choices.Select(n=>g.IdEntityDic[n].Card.Id).ToArray()??System.Array.Empty<string>()).SequenceEqual(expectedOffers),"Nellie seeded ordered distinct offers");
        if (id != "TSC_660") Core2026Proof.Check(p.Choice == null, "no unexpected body decision");
        switch (id) {
            case "TSC_026": Unpinned_Colaque(g,p,body); break;
            case "TSC_029": Unpinned_Gaia(g,p,body); break;
            case "TSC_030": Unpinned_Leviathan(g,p,body); break;
            case "TSC_216": Unpinned_Behemoth(g,p,body); break;
            case "TSC_937": Unpinned_Crabatoa(g,p,body); break;
            case "TSC_639": Unpinned_Glugg(g,p,body); break;
            case "TSC_950": Unpinned_Hydralodon(g,p,body); break;
            case "TSC_219": Unpinned_Xhilag(g,p,body); break;
            case "TSC_962": Unpinned_Gigafin(g,p,body); break;
            case "TSC_660": Unpinned_Nellie(g,p,body); break;
            case "TID_711": Unpinned_Ozumat(g,p,body); break;
            case "TID_712": Unpinned_Neptulon(g,p,body); break;
            case "NX2_037": EvColossalsSindragosaProof.Unpinned_Freeze(g,p,body); break;
        }
        // Every body must also summon (without Battlecry), truncate and never refill ordinary missing limbs.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        for (int i=0;i<6;i++) Summon(p,"CS2_182");
        var crowded = Summon(p,id);
        Core2026Proof.Check(p.BoardZone.Count==7 && !SabberStoneCore.Actions.ColossalRules.AppendagesOf(crowded).Any(), "full board drops every appendage");
        Kill(g,p.BoardZone.First(m=>m!=crowded));
        Core2026Proof.Check(p.BoardZone.Count==6, "ordinary Colossal never refills discarded limbs");
        StandardSetsProof.ParkBoard(p);
        for(int i=0;i<4;i++) Summon(p,"CS2_182");
        var partial=Summon(p,id); int expected=Math.Min(Cards.FromId(id).ColossalAppendageIds.Count,2);
        Core2026Proof.Check(SabberStoneCore.Actions.ColossalRules.AppendagesOf(partial).Count()==expected,"partial board creates declared prefix only");
        var limbs=SabberStoneCore.Actions.ColossalRules.AppendagesOf(partial).Select(m=>m.Id).ToArray();
        partial.Silence();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(limbs.All(n=>g.IdEntityDic[n].Zone==p.BoardZone),"Silence leaves independent limbs");
        Kill(g,partial);
        Core2026Proof.Check(limbs.All(n=>g.IdEntityDic[n].Zone==p.BoardZone),"body death leaves independent limbs");
    }
}
