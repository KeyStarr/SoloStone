using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class PastLegacyQualificationProof
{
    internal static readonly string[] Ids = { "EX1_563", "BRM_024", "BRM_025", "GVG_058", "GVG_062", "BOT_906",
        "AT_020", "CS2_168", "KAR_300", "EX1_008", "GVG_033", "OG_195", "EX1_183",
        "AT_037", "OG_047", "CS2_039", "CS2_041", "EX1_245", "BOT_453", "GVG_005", "BOT_257",
        "EX1_310", "CS2_064", "DRG_069", "EX1_066", "OG_254", "EX1_048" };

    internal static Game New(int seed=11)
    {
        var g = new Game(new GameConfig { RandomSeed=seed, StartPlayer=1, Shuffle=false, SkipMulligan=true,
            History=true, Logging=false, FormatType=FormatType.FT_WILD, Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
        g.StartGame();
        foreach(var p in new[]{g.Player1,g.Player2})
            foreach(var e in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(e));
        return g;
    }
    [GroupProof("b14-f3-past-legacy")]
    internal static void Run()
    {
        Core2026Proof.Check(Ids.All(id=>Cards.FromId(id).Implemented),"unimplemented qualification candidates: "+string.Join(",",Ids.Where(id=>!Cards.FromId(id).Implemented)));
        var definitions=ClientExport.TryCards();
        if(definitions==null) ClientExport.Skip("Past legacy cards against the client export (printed stats, current spell school)");
        foreach(string id in Ids)
        {
            var card=Cards.FromId(id); int canonicalSchool=card[GameTag.SPELL_SCHOOL];
            Core2026Proof.Check(card.Implemented && card.Collectible,id+" registered legacy identity");
            if(definitions!=null) {
                var data=definitions[id];
                Core2026Proof.Check(card.AssetId==data.GetProperty("dbfId").GetInt32(),id+" pinned identity");
                Core2026Proof.Check(card.Cost==data.GetProperty("cost").GetInt32() && card.ATK==data.GetProperty("atk").GetInt32()
                    && card.Health==data.GetProperty("health").GetInt32(),id+" pinned printed stats");
            }
            foreach(int seed in new[]{1,9})
            foreach(int option in card.ChooseOne ? new[]{1,2} : new[]{0})
            {
                var g=New(seed); var p=g.Player1; var target=Summon(p.Opponent,"CS2_182"); var friend=Summon(p,"CS2_182");
                var extra=Summon(p.Opponent,"CS2_182");
                if(id=="BRM_024") p.Opponent.Hero.Damage=15;
                if(id=="GVG_033") { p.Hero.Damage=9; friend.Damage=2; }
                if(id=="CS2_041") friend.Damage=2;
                if(id=="DRG_069") p.Opponent.Hero.Armor=10;
                if(id=="EX1_310") for(int i=0;i<3;i++) Add(p,"CS2_182");
                if(id=="EX1_066") p.Opponent.Hero.AddWeapon((Weapon)Entity.FromCard(p.Opponent,Cards.FromId("CS2_106")));
                if(id=="OG_254") Entity.FromCard(p.Opponent,Cards.FromId("EX1_287"),zone:p.Opponent.SecretZone);
                ICharacter selected=id switch { "CS2_039" or "CS2_041"=>friend,
                    "AT_037" when option==1=>target, "EX1_245" or "BOT_453" or "EX1_048"=>target, _=>null };
                var admitted=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(scope:PoolScope.Past),0).Single(c=>c.Id==id);
                if(definitions!=null)
                    Core2026Proof.Check(admitted[GameTag.SPELL_SCHOOL]==definitions[id].GetProperty("tags").EnumerateObject()
                        .Where(t=>t.Name=="1635").Select(t=>t.Value.GetInt32()).FirstOrDefault(),id+" generated current spell school");
                var source=Generic.DrawCard(p,admitted); p.BaseMana=10; p.UsedMana=0;
                Core2026Proof.Check(g.Process(PlayCardTask.Any(p,source,selected,chooseOne:option)),id+" generated legacy play accepted");
                if(card.Type==CardType.SPELL)
                    Core2026Proof.Check(g.HistoryFacts.Query.Casts().Any(c=>c.EntityId==source.Id && c.SpellSchool==admitted[GameTag.SPELL_SCHOOL]),id+" cast records current school");
                Core2026Proof.Check(card[GameTag.SPELL_SCHOOL]==canonicalSchool,id+" canonical legacy metadata unchanged");
                switch(id)
                {
                    case "EX1_563": Core2026Proof.Check(p.CurrentSpellPower==5,"Malygos grants 5 Spell Damage"); break;
                    case "BRM_024": Core2026Proof.Check(((Minion)source).AttackDamage==9 && ((Minion)source).Health==9,"Crusher low-Health Battlecry"); break;
                    case "BRM_025": Kill(g,friend); Core2026Proof.Check(Add(p,id).Cost==5,"Volcanic Drake tracks minion deaths this turn"); End(g); Core2026Proof.Check(source.Cost==6,"Volcanic Drake turn expiry"); break;
                    case "GVG_058": case "EX1_008": Core2026Proof.Check(((Minion)source).HasDivineShield,"printed Divine Shield"); break;
                    case "GVG_062":
                        foreach(var fork in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
                        {
                            var guardian=(Minion)fork.IdEntityDic[source.Id]; var mech=Summon(fork.Player1,"GVG_058");
                            Core2026Proof.Check(guardian.HasDivineShield,"Cobalt Guardian shields itself after friendly Mech summon");
                            guardian.HasDivineShield=false; Summon(fork.Player2,"GVG_058");
                            Core2026Proof.Check(!guardian.HasDivineShield,"opponent Mech does not trigger Guardian");
                            new SilenceTask(EntityType.TARGET).Process(fork,fork.Player1,guardian,guardian); Summon(fork.Player1,"GVG_058");
                            Core2026Proof.Check(!guardian.HasDivineShield,"Silenced Guardian does not trigger");
                        }
                        break;
                    case "BOT_906":
                        var host=Summon(p,"GVG_058"); var magnet=Add(p,id); p.UsedMana=0;
                        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,magnet,zonePosition:host.ZonePosition)),"Magnetic play accepted");
                        Core2026Proof.Check(host.AttackDamage==3 && host.Health==5,"Glow-Tron Magnetic merges +1/+3"); break;
                    case "GVG_033": Core2026Proof.Check(p.Hero.Damage==0 && friend.Damage==0,"Tree of Life restores all characters"); break;
                    case "OG_195": Core2026Proof.Check(option==1 ? p.BoardZone.Count==7 : friend.AttackDamage==6 && friend.Health==7,"Wisps both Choose One branches"); break;
                    case "EX1_183": Core2026Proof.Check(friend.AttackDamage==6 && friend.Health==7 && friend.HasTaunt,"Gift of the Wild +2/+2 Taunt"); break;
                    case "AT_037": Core2026Proof.Check(option==1 ? target.Damage==2 : p.BoardZone.Count==3,"Living Roots damage or two Saplings"); break;
                    case "OG_047": Core2026Proof.Check(option==1 ? p.Hero.AttackDamage==4 : p.Hero.Armor==8,"Feral Rage Attack or Armor"); End(g); Core2026Proof.Check(p.Hero.AttackDamage==0,"Feral Rage Attack expires"); break;
                    case "CS2_039": Core2026Proof.Check(friend.HasWindfury,"Windfury granted"); break;
                    case "CS2_041": Core2026Proof.Check(friend.Damage==0 && friend.HasTaunt,"Ancestral Healing heals and grants Taunt"); break;
                    case "EX1_245": Core2026Proof.Check(target.IsSilenced && target.Damage==1,"Earth Shock Silence then damage"); break;
                    case "BOT_453": Core2026Proof.Check(target.Damage==1 && extra.Damage==1,"Shooting Star target and adjacent minion"); break;
                    case "GVG_005": Core2026Proof.Check(p.HandZone.Count==1 && p.HandZone[0].Card.Id==friend.Card.Id,"Echo copies friendly board to hand"); break;
                    case "BOT_257": Core2026Proof.Check(p.DeckZone.All(e=>e.Cost==1),"Pocket Galaxy sets deck minion costs to 1"); break;
                    case "EX1_310": Core2026Proof.Check(((Minion)source).HasCharge && p.HandZone.Count==1 && p.GraveyardZone.Count==2,"Doomguard Charge and two real discards"); break;
                    case "CS2_064": Core2026Proof.Check(target.Damage==1 && friend.Damage==1 && extra.Damage==1 && p.Hero.Damage==1 && p.Opponent.Hero.Damage==1,"Infernal damages every other character"); break;
                    case "DRG_069": Core2026Proof.Check(p.Opponent.Hero.Armor==0,"Platebreaker removes Armor"); break;
                    case "EX1_066": Core2026Proof.Check(p.Opponent.Hero.Weapon==null,"Ooze destroys enemy weapon"); break;
                    case "OG_254": Core2026Proof.Check(p.Opponent.SecretZone.Count==0 && ((Minion)source).AttackDamage==3 && ((Minion)source).Health==5,"Eater removes secret and gains +1/+1"); break;
                    case "EX1_048": Core2026Proof.Check(target.IsSilenced,"Spellbreaker silences chosen minion"); break;
                    default: Core2026Proof.Check(source.Zone==p.BoardZone && ((Minion)source).AttackDamage==card.ATK,"ordinary generated minion play"); break;
                }
                Core2026Proof.Check(Canonical.State(g)==Canonical.State(g.CloneExact(history:true)),id+" exact clone");
                Core2026Proof.Check(Canonical.State(g)==Canonical.State(Game.LoadSnapshot(g.SaveSnapshot())),id+" snapshot");
            }
            PoolProofEvidence.Passed(id,"PastLegacyQualificationProof.Run:"+id);
            Console.WriteLine("PASS qualified Past legacy "+id);
        }
    }
}
