using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
internal static partial class B14H4IdentityProof
{
    [GroupProof("b14-h4-identity")]
    internal static void Mutations()
    {
        var cases = new (string Id, string Mutated, Action<Game,Controller,string> Test)[] {
            ("CORE_CS2_023","CORE_CS2_023",ArcaneIntellect),
            ("CORE_BOT_256","CORE_BOT_256",Astromancer),
            ("EDR_469","EDR_469e",SlumberingSprite),
            ("EDR_270","EDR_270",HornOfPlenty),
            ("EDR_233","EDR_233a",SpiritsOfTheForest),
            ("EDR_262","EDR_262",SpiritBond),
            ("EDR_263","EDR_263a",GraceOfTheGreatwolf),
            ("END_005","END_005",Unpinned_BygoneEchoes),
            ("BE_780","BE_780",Pinned_UldirQuarantine),
            ("BE_781","BE_781",Unpinned_FetidDevourer),
            ("CATA_140","CATA_140",MerithraOfTheDream),
            ("JAIL_200","JAIL_200",Unpinned_Scullery)
        };
        foreach(var item in cases)
        {
            // Run the same fixture successfully first: an unrelated setup error cannot count as mutation detection.
            var baseline=DeathKnightC1Proof.NewGame(1,CardClass.ROGUE);
            item.Test(baseline,baseline.Player1,item.Id);
            var definition=Cards.FromId(item.Mutated); var original=definition.Power;
            var mutated=new Power { Trigger=original.Trigger,LocationActivation=original.LocationActivation };
            if(item.Id=="EDR_469") mutated=new Power { Trigger=new SabberStoneCore.Triggers.Trigger(TriggerType.INSPIRE) {
                TriggerSource=TriggerSource.FRIENDLY,SingleTask=new CustomTask((g,c,s,t,stack)=>{}) } };
            if(item.Id=="BE_781") mutated=new Power { PowerTask=original.PowerTask };
            if(item.Id=="BE_780") mutated=new Power { Trigger=original.Trigger,LocationActivation=new LocationActivation { RequiresFreeSlot=true,Task=new CustomTask((g,c,s,t,stack)=>{}) } };
            if(item.Id=="CATA_140") mutated=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                bool ready=g.HistoryFacts.Query.ManaSpentWhileHolding(s.Id,c.Id)>=25;
                var filter=SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Single(d=>d.CardId=="CATA_140").Pools.Single().Filter;
                var chosen=OfflineCardPools.Candidates(g,c.HeroClass,filter,s.Card.AssetId).FirstOrDefault();
                int slots=c.HandZone.FreeSpace;
                for(int i=0;i<slots && chosen!=null;i++)
                {
                    var dragon=Generic.DrawCard(c,chosen);
                    if(ready && dragon.Zone==c.HandZone) dragon.Cost=1;
                }
            }) };
            bool caught=false;
            try
            {
                definition.Power=mutated;
                var game=DeathKnightC1Proof.NewGame(1,CardClass.ROGUE);
                try { item.Test(game,game.Player1,item.Id); }
                catch(Exception) { caught=true; }
            }
            finally { definition.Power=original; }
            Core2026Proof.Check(caught,"behavior mutation escaped: "+item.Id);
            Console.WriteLine("PASS h4 mutation detected: "+item.Id);
        }
    }
}
