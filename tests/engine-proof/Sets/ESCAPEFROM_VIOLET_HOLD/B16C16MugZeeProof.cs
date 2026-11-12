using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16C16MugZeeProof
{
    [GroupProof("b16-c16-mugzee-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_800", Unpinned_OriginalDeck);
        Unpinned_ZeeHintPlayByPlay();
        Unpinned_FifthPlayAndReplacement();
        Unpinned_ReinstallAndClock();
    }

    // Owner report 2026-11-03: hint read "(2 left!)" before the 8th minion and "(3 left!)" after. JAIL_800hp2 uses the
    // client's ENTITY_TAG_THRESHOLD builder (2459=2 -> SDN1, 2460=5): text = "(5 - SDN1 left!)", "(Ready!)" at SDN1 >= 5.
    // So SDN1 must be progress (plays since the last doubling), not minions remaining. Replays that builder here.
    private static string ClientHint(Entity power)
    {
        var card = power.Card;
        Core2026Proof.Check(card[(GameTag)2459]==2 && card[(GameTag)2460]==5, "Zee printed threshold metadata (SDN1, 5)");
        int counter=power[(GameTag)card[(GameTag)2459]], threshold=card[(GameTag)2460];
        var v=card.Text.Split('@');
        Core2026Proof.Check(v.Length==3,"Zee text has left and Ready alternates");
        return v[0]+(counter>=threshold ? v[2] : string.Format(v[1],threshold-counter));
    }
    private static void Unpinned_ZeeHintPlayByPlay()
    {
        var deck=Enumerable.Repeat(Cards.FromId("CS2_182"),29).Append(Cards.FromId("JAIL_800")).ToList();
        foreach (int seat in new[]{1,2})
        {
            var g=new Game(new GameConfig {RandomSeed=1,StartPlayer=1,Shuffle=false,SkipMulligan=true,History=true,
                Logging=false,FormatType=FormatType.FT_STANDARD,Player1Deck=deck,Player2Deck=deck,
                Player1HeroClass=CardClass.SHAMAN,Player2HeroClass=CardClass.SHAMAN});
            g.StartGame();
            if (seat==2) End(g);
            var p=seat==1?g.Player1:g.Player2; var other=p.Opponent;
            Core2026Proof.Check(g.CurrentPlayer==p,"hint fixture drives the intended seat");
            PoolPilotProof.EmptyHand(p);
            // Minion plays in order; "SPELL" is a generated Moonfire after the 4th (never counts); Ultraxion is play 8 (Herald token).
            var plays=new[]{"CS2_231","CS2_231","CS2_231","CS2_231","EX1_015","EX1_015","CS2_231","CATA_497","CS2_231","EX1_015","CS2_231","CS2_231"};
            for (int i=0;i<=plays.Length;i++)
            {
                int played=i;
                var power=p.HeroPowers.Single(h=>h.Card.Id=="JAIL_800hp2");
                string hint=ClientHint(power);
                Core2026Proof.Check(power[GameTag.TAG_SCRIPT_DATA_NUM_1]==played%5,$"seat {seat}: SDN1 after {played} minion plays");
                Core2026Proof.Check(hint.Contains($"({5-played%5} left!)") && !hint.Contains("Ready"),$"seat {seat}: hint after {played} plays: {hint}");
                Core2026Proof.Check(other.HeroPowers.Single(h=>h.Card.Id=="JAIL_800hp2")[GameTag.TAG_SCRIPT_DATA_NUM_1]==0,"opponent Zee progress untouched");
                if (i==plays.Length) break;
                if (i==4)
                {
                    int before=p.HandZone.Count; Play(g,p,"CS2_008",other.Hero);
                    Core2026Proof.Check(power[GameTag.TAG_SCRIPT_DATA_NUM_1]==4 && p.HandZone.Count==before,"generated spell leaves Zee progress at 4");
                }
                StandardSetsProof.ParkBoard(p);
                int deckBefore=p.DeckZone.Count, board=p.BoardZone.Count;
                Play(g,p,plays[i]);
                bool fifth=(i+1)%5==0;
                if (plays[i]=="EX1_015")
                    Core2026Proof.Check(p.DeckZone.Count==deckBefore-(fifth?2:1),$"seat {seat}: Battlecry doubled exactly on play {i+1}");
                if (plays[i]=="CATA_497")
                    Core2026Proof.Check(p.BoardZone.Count==board+2,"Ultraxion Herald token is summoned, not played");
                Core2026Proof.Check(g.HistoryFacts.Query.Plays().Count(f=>f.ControllerId==p.Id && f.CardType==CardType.MINION)==i+1,"Herald token and spell are not minion plays");
            }
        }
    }
    private static void Unpinned_ReinstallAndClock()
    {
        foreach (int seat in new[]{1,2})
        {
            var g=B15DrawReplacementProof.NewGame();
            var p=seat==1?g.Player1:g.Player2;
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("JAIL_800hp1")).Process(g,p,p.Hero,null);
            Core2026Proof.Check(p.Hero.HeroPower[(GameTag)4414]==1,"early Mug installation retains native lock in both seats");
            for(int i=0;i<6;i++) End(g);
            var card=Add(p,"CS2_182"); g.AuraUpdate();
            Core2026Proof.Check(card.Cost==2,"Mug uses accumulated personal turns in both seats");
            Core2026Proof.Check(p.Hero.HeroPower[(GameTag)4414]==0,"Mug clears native lock at unlock in both seats");
            foreach(var branch in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot()),g})
            {
                var owner=seat==1?branch.Player1:branch.Player2;
                Core2026Proof.Check(owner.Hero.HeroPower[(GameTag)4414]==0,"unlocked native presentation survives clone/load");
                new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("CS2_034")).Process(branch,owner,owner.Hero,null);
                Core2026Proof.Check(branch.IdEntityDic[card.Id].Cost==4,"replacing Mug removes discount immediately");
                new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("JAIL_800hp1")).Process(branch,owner,owner.Hero,null);
                Core2026Proof.Check(branch.IdEntityDic[card.Id].Cost==2
                    && owner.AppliedEnchantments.Count(e=>e.Card.Id=="JAIL_800e")==1,"reinstall resumes once, no stacked aura");
                Core2026Proof.Check(owner.Hero.HeroPower[(GameTag)4414]==0
                    && owner.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]==0,"late Mug reinstall clears native lock and countdown in both seats");
            }
        }
    }

    private static void Unpinned_FifthPlayAndReplacement()
    {
        var deck=Enumerable.Repeat(Cards.FromId("CS2_182"),29).Append(Cards.FromId("JAIL_800")).ToList();
        var g=new Game(new GameConfig {RandomSeed=1,StartPlayer=1,Shuffle=false,SkipMulligan=true,History=true,
            Logging=false,FormatType=FormatType.FT_STANDARD,Player1Deck=deck,Player2Deck=deck});
        g.StartGame(); var p=g.Player1;
        PoolPilotProof.EmptyHand(p);
        Core2026Proof.Check(p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]==0,"Zee initially zero plays of five (client prints 5 left)");
        for(int i=0;i<4;i++) Play(g,p,"CS2_231");
        Core2026Proof.Check(p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]==4,"Zee progress before fifth play (client prints 1 left)");
        B15QuestClosureProof.RewindState(g);
        p=g.Player1;
        Core2026Proof.Check(p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]==4,"Rewind preserves Zee play progress");
        foreach(var branch in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var owner=branch.Player1; int before=owner.DeckZone.Count;
            Play(branch,owner,"EX1_015");
            Core2026Proof.Check(owner.DeckZone.Count==before-2,"Unpinned_Zee fifth minion doubles Battlecry across clone/load");
            StandardSetsProof.ParkBoard(owner);
            before=owner.DeckZone.Count; Play(branch,owner,"EX1_015");
            Core2026Proof.Check(owner.DeckZone.Count==before-1,"sixth minion normal Battlecry");
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("CS2_034")).Process(branch,owner,owner.Hero,null);
            for(int i=0;i<3;i++) Play(branch,owner,"CS2_231");
            before=owner.DeckZone.Count; Play(branch,owner,"EX1_015");
            Core2026Proof.Check(owner.DeckZone.Count==before-1,"replaced Zee stops on tenth play");
        }
    }
    private static void Unpinned_OriginalDeck(Game g, Controller p, string id)
    {
        foreach(string filler in new[]{"CS2_182","CS2_029","CS2_106"})
        {
            var deck=Enumerable.Repeat(Cards.FromId(filler),29).Append(Cards.FromId(id)).ToList();
            var game=new Game(new GameConfig { RandomSeed=1,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_STANDARD,Player1Deck=deck,Player2Deck=deck });
            game.StartGame();
            foreach(var owner in new[]{game.Player1,game.Player2})
            {
                Core2026Proof.Check(owner.HeroPowers.Any(h=>h.Card.Id=="JAIL_800hp1")== (filler!="CS2_182"),"Mug original-deck predicate");
                Core2026Proof.Check(owner.HeroPowers.Any(h=>h.Card.Id=="JAIL_800hp2")== (filler!="CS2_029"),"Zee independent original-deck predicate");
                Core2026Proof.Check(owner.HeroPowers.All(h=>h.IsPassiveHeroPower),"installed powers passive");
                owner.BaseMana = 10;
                Core2026Proof.Check(owner.Options().All(task => task.PlayerTaskType != SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_POWER), "passive powers never appear as legal actions");
            }
            if (filler == "CS2_029")
            {
                for(int turn=0;turn<6;turn++)
                {
                    var owner=game.CurrentPlayer;
                    PoolPilotProof.EmptyHand(owner);
                    var minion=Add(owner,"CS2_182"); game.AuraUpdate();
                    int turns=game.HistoryFacts.Query.TurnsTaken(owner.Id);
                    Core2026Proof.Check(minion.Cost==(turns>=3?2:4),"Mug unlock uses personal turns in both seats");
                    Core2026Proof.Check(owner.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1]==Math.Max(0,3-turns),"Mug exported countdown reflects personal clock");
                    Core2026Proof.Check(owner.Hero.HeroPower[(GameTag)4414]==(turns>=3?0:1),"Mug native lock follows personal clock in both seats");
                    PlayEntity(game,owner,minion);
                    var second=Add(owner,"CS2_182"); game.AuraUpdate();
                    Core2026Proof.Check(second.Cost==4,"Mug only first minion each turn");
                    End(game);
                }
            }
        }
    }
}
