using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class GeneratedDefinitionProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    [GroupProof("b18-t-generated")]
    internal static void Run()
    {
        PotionRecipes();
        CreationLifecycle();
        CompositeCost();
        MixedAuraAndTargeting();
        Unpinned_ComponentTargetingAndFizzle();
        ComposedExpiryAndRewind();
        CompositionChannels();
        Unpinned_NefariusDragonUndeadComposition();
        CoinPolicyAndOpening();
        SeededPotion();
        OldDefinitionBytes();
        Console.WriteLine("PASS generated definitions: recipes, copy/clone/snapshot/Rewind, triggers, Silence, coin policy, RNG");
    }

    private static void PotionRecipes()
    {
        foreach (string first in AyaLotusKingpin.Ingredients)
        foreach (string second in AyaLotusKingpin.Ingredients.Where(id => id != first))
        {
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            var dead = Summon(p,"CS2_231"); Kill(g,dead);
            var definition = GeneratedDefinitionRules.Potion(Cards.FromId(first), Cards.FromId(second));
            var potion = Generic.DrawCard(p,definition);
            Check(definition.Id == "JAIL_504t3p" && definition.Cost == 1 && definition.Power.PowerTask != null,
                "executable exact potion identity");
            Check(potion[GameTag.MODULAR_ENTITY_PART_1] == Cards.FromId(first).AssetId
                && potion[GameTag.MODULAR_ENTITY_PART_2] == Cards.FromId(second).AssetId, "ordered ingredient definitions");
            var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            var clone = g.CloneExact(history:true);
            int armor = p.Hero.Armor, hp = p.Opponent.Hero.Health;
            foreach (var next in new[] {g,clone,loaded})
                PlayEntity(next,next.Player1,next.IdEntityDic[potion.Id],definition.MustHaveTargetToPlay ? next.Player2.Hero : null);
            Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
                "every ordered potion pair survives clone/snapshot and executes identically");
            if (first == "CFM_621t3" || second == "CFM_621t3") Check(p.Hero.Armor == armor+4,"Stonescale effect executes");
            if (definition.MustHaveTargetToPlay) Check(p.Opponent.Hero.Health == hp-3,"Heart of Fire targets once");
            if (first == "CFM_621t5" || second == "CFM_621t5") Check(enemy.IsFrozen,"Icecap freezes enemy minion");
        }
    }

    private static void CreationLifecycle()
    {
        var g=ImbueProof.Make(CardClass.SHAMAN);var p=g.Player1;
        var definition=GeneratedDefinitionRules.Creation(Cards.FromId("NEW1_009"),Cards.FromId("EX1_575"));
        var body=(Minion)Generic.DrawCard(p,definition); PlayEntity(g,p,body);
        var damaged=Summon(p,"CS2_182"); damaged.Damage=2;
        int hand=p.HandZone.Count;
        var clone=g.CloneExact(history:true);var loaded=Game.LoadSnapshot(g.SaveSnapshot());
        foreach(var next in new[]{g,clone,loaded}) {
            var owner=next.Player1;var minion=(Minion)next.IdEntityDic[body.Id];
            End(next);
            Check(owner.HandZone.Count==hand+1 && ((Minion)next.IdEntityDic[damaged.Id]).Damage==1,
                "both independent end triggers resolve in selected order: hand="+owner.HandZone.Count+" expected="+(hand+1)+" damage="+((Minion)next.IdEntityDic[damaged.Id]).Damage+" trigger="+minion.ActivatedTrigger?.GetType().Name);
            int count=owner.HandZone.Count;End(next);
            Check(owner.HandZone.Count==count+1 && ((Minion)next.IdEntityDic[damaged.Id]).Damage==1,"opponent turn causes only ordinary start-of-turn draw");
            var copy=Generic.Copy(owner,owner.Hero,minion,Zone.HAND);
            Check(copy.Card.ATK==definition.ATK && copy.Card.Health==definition.Health,"copy retains intrinsic sum");
            minion.Silence();next.DeathProcessingAndAuraUpdate();
            Check(minion.BaseHealth==definition.Health,"Silence preserves composed baseline");
            count=owner.HandZone.Count;End(next);Check(owner.HandZone.Count==count,"Silence removes both triggers");
            End(next);Generic.ReturnToHandBlock(owner,minion);PlayEntity(next,owner,minion);Generic.ReturnToHandBlock(owner,minion);
            count=owner.HandZone.Count;End(next);Check(owner.HandZone.Count==count,"leave play removes both triggers");
        }
        Check(Canonical.State(g)==Canonical.State(clone) && Canonical.State(g)==Canonical.State(loaded),"multi-trigger exact future");
        var baseline=g.SaveSnapshot();g.RestoreRewindBaseline(baseline);
        Check(g.SaveSnapshot().SequenceEqual(baseline),"composed state restores through Rewind codec");
        var keyword=GeneratedDefinitionRules.Creation(Cards.FromId("EX1_012"),Cards.FromId("EX1_012"));
        Check(keyword.SpellPower==2 && keyword.Deathrattle,"numeric Spell Damage adds; boolean Deathrattle coalesces");
    }

    private static void Unpinned_NefariusDragonUndeadComposition()
    {
        var game=ImbueProof.Make(CardClass.DEATHKNIGHT);var p=game.Player1;
        var dragons=VictorNefarius.Candidates(game,p,true);var undead=VictorNefarius.Candidates(game,p,false);
        Console.WriteLine("Nefarius buckets: Dragon="+dragons.Length+" Undead="+undead.Length);
        for(int budget=0;budget<=10;budget++) Console.WriteLine("Undead budget "+budget+"="+VictorNefarius.Candidates(game,p,false,budget).Length);
        Check(dragons.Any(c=>c.Cost>5),"approved Dragon budget permits printed costs above five");
        foreach(var first in dragons)
        foreach(var second in undead.Where(c=>c.Cost<=10-first.Cost)) {
            var card=GeneratedDefinitionRules.Creation(first,second);
            Check(card.Cost==first.Cost+second.Cost && card.ATK==first.ATK+second.ATK && card.Health==first.Health+second.Health,
                "sum printed components, never live auras");
            Check(card.IsRace(Race.DRAGON) && card.IsRace(Race.UNDEAD),"intrinsic dual type");
            Check(card.IsRace(Race.ALL)==(first.IsRace(Race.ALL)||second.IsRace(Race.ALL)),"All propagates exactly");
            var bytes=SnapshotCodec.EncodeDefinition(card);var restored=SnapshotCodec.DecodeDefinition(bytes);
            Check(SnapshotCodec.EncodeDefinition(restored)==bytes,"every admitted pair round-trips detached history definition");
        }
    }

    private static void CompositionChannels()
    {
        var g=ImbueProof.Make(CardClass.DEATHKNIGHT);var p=g.Player1;
        var other=Summon(p,"CS2_182");var enemy=Summon(p.Opponent,"CS2_182");
        var definition=GeneratedDefinitionRules.Creation(Cards.FromId("END_010"),Cards.FromId("CORE_EX1_012"));
        var body=(Minion)Generic.DrawCard(p,definition);PlayEntity(g,p,body);
        Check(p.Choice?.Choices.Count==2,"composed Choose One payload opens its native choices");
        foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var owner=next.Player1;
            B18GeneratedProof.Answer(next,owner.Choice.Choices.Single(id=>next.IdEntityDic[id].Card.Id=="END_010a"));
            Check(((Minion)next.IdEntityDic[other.Id]).AttackDamage==1 && ((Minion)next.IdEntityDic[enemy.Id]).AttackDamage==1,
                "selected component affects both sides");
            var actual=(Minion)next.IdEntityDic[body.Id];
            Check(actual.AttackDamage==definition.ATK && actual.SpellPower==1,"composition itself excluded; second component remains functional");
            int hand=owner.HandZone.Count;Kill(next,actual);Check(owner.HandZone.Count==hand+1,"second component Deathrattle survives first component choice");
        }
    }

    private static void CompositeCost()
    {
        var g=ImbueProof.Make(CardClass.DEATHKNIGHT);var p=g.Player1;
        var card=GeneratedDefinitionRules.Creation(Cards.FromId("END_033"),Cards.FromId("JAIL_204"));
        var creation=Generic.DrawCard(p,card);
        Check(creation.Cost==2,"both adaptive component costs apply in selection order");
        Summon(p,"CS2_231");Add(p,"EX1_043");g.AuraUpdate();
        Check(creation.Cost==card.Cost-3,"failed setter leaves preceding conditional reduction");
        foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var actual=next.IdEntityDic[creation.Id];
            Check(actual.Cost==card.Cost-3,"composite hand costs survive clone and snapshot");
            PlayEntity(next,next.Player1,actual);
            ((Minion)actual).Silence();next.DeathProcessingAndAuraUpdate();
            Check(actual.Cost==card.Cost,"Silence clears both hand adaptive costs");
        }
    }

    private static void MixedAuraAndTargeting()
    {
        var g=ImbueProof.Make(CardClass.SHAMAN);var p=g.Player1;
        var ally=Summon(p,"CS2_182");Add(p,"EX1_043");
        var definition=GeneratedDefinitionRules.Creation(Cards.FromId("END_033"),Cards.FromId("CS2_122"));
        var body=Generic.DrawCard(p,definition);
        Check(body.Cost==definition.Cost-3 && ally.AttackDamage==ally.Card.ATK,"hand costs do not activate board aura");
        PlayEntity(g,p,body);
        Check(ally.AttackDamage==ally.Card.ATK+1,"board aura activates after hand cost retires");
        foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var actual=(Minion)next.IdEntityDic[body.Id];var friend=(Minion)next.IdEntityDic[ally.Id];
            Check(friend.AttackDamage==friend.Card.ATK+1,"mixed aura survives clone/load without doubling");
            actual.Silence();next.DeathProcessingAndAuraUpdate();
            Check(friend.AttackDamage==friend.Card.ATK,"Silence removes composed board layer");
        }
        var targetGame=ImbueProof.Make(CardClass.MAGE);var owner=targetGame.Player1;
        var targeted=GeneratedDefinitionRules.Creation(Cards.FromId("CS2_189"),Cards.FromId("EX1_015"));
        var shooter=Generic.DrawCard(owner,targeted);
        Check(shooter.ValidPlayTargets.Contains(owner.Opponent.Hero),"merged target remains selectable");
        foreach(var next in new[]{targetGame,targetGame.CloneExact(history:true),Game.LoadSnapshot(targetGame.SaveSnapshot())}) {
            int hand=next.Player1.HandZone.Count,hp=next.Player2.Hero.Health;
            PlayEntity(next,next.Player1,next.IdEntityDic[shooter.Id],next.Player2.Hero);
            Check(next.Player2.Hero.Health==hp-1 && next.Player1.HandZone.Count==hand,"targeted and untargeted Battlecries both execute");
        }
    }

    private static void Unpinned_ComponentTargetingAndFizzle()
    {
        // Intersection rejects the empty-board hero; unchecked dispatch buffs heroes/non-Undead.
        foreach (bool reverse in new[] { false, true })
        foreach (string targetKind in new[] { "hero", "friendly-undead", "enemy-undead", "friendly-other" })
        {
            var g = ImbueProof.Make(CardClass.DEATHKNIGHT); var p = g.Player1;
            var wyrm = Cards.FromId("TLC_600"); var sidekick = Cards.FromId("RLK_958");
            Check(VictorNefarius.Candidates(g, p, true).Contains(wyrm)
                && VictorNefarius.Candidates(g, p, false, 2).Contains(sidekick), "review pair admitted within Cost budget");
            ICharacter target = targetKind == "hero" ? p.Opponent.Hero
                : Summon(targetKind == "enemy-undead" ? p.Opponent : p,
                    targetKind == "friendly-other" ? "CS2_182" : "RLK_958");
            if (target is Minion minion) minion[GameTag.HEALTH] = 10;
            var card = Generic.DrawCard(p, GeneratedDefinitionRules.Creation(reverse ? sidekick : wyrm, reverse ? wyrm : sidekick));
            Check(card.Cost == 9 && card.ValidPlayTargets.Contains(target),
                "optional component cannot veto another component's legal target: " + targetKind);
            int health = target.Health, attack = target.AttackDamage;
            var copy = Generic.Copy(p, p.Hero, card, Zone.HAND);
            var baseline = g.SaveSnapshot();
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(baseline) })
            {
                var actual = (ICharacter)next.IdEntityDic[target.Id];
                PlayEntity(next, next.Player1, next.IdEntityDic[card.Id], actual);
                Check(actual.Health == health - 5 && next.Player1.Hero.Armor == 5, "Wyrm damage and armor both resolve");
                Check(actual.AttackDamage == attack + (targetKind == "friendly-undead" ? 2 : 0)
                    && (actual.AppliedEnchantments?.Count(e => e.Card.Id == "RLK_958e") ?? 0) == (targetKind == "friendly-undead" ? 1 : 0),
                    "Sidekick independently validates friendly Undead; invalid payload fizzles");
                Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect, "composition completes without stalled decision");
                next.RestoreRewindBaseline(baseline);
                var restoredCopy = next.IdEntityDic[copy.Id];
                Check(restoredCopy.ValidPlayTargets.Contains((ICharacter)next.IdEntityDic[target.Id]), "copied definition retains target union after Rewind");
            }
        }
        var optional = ImbueProof.Make(CardClass.DEATHKNIGHT);
        var body = Generic.DrawCard(optional.Player1, GeneratedDefinitionRules.Creation(Cards.FromId("EX1_043"), Cards.FromId("RLK_958")));
        Check(!body.ValidPlayTargets.Any() && body.IsValidPlayTarget(null), "optional-only empty target list allows untargeted play");
        PlayEntity(optional, optional.Player1, body);
        Check(!(((Minion)body).AppliedEnchantments?.Any(e => e.Card.Id == "RLK_958e") ?? false), "newly summoned Creation is not a substitute target");
    }

    private static void ComposedExpiryAndRewind()
    {
        var g=ImbueProof.Make(CardClass.DEATHKNIGHT);var p=g.Player1;
        Play(g,p,"EX1_043");End(g);End(g);
        var definition=GeneratedDefinitionRules.Creation(Cards.FromId("TLC_243"),Cards.FromId("CORE_ULD_723"));
        var body=(Minion)Generic.DrawCard(p,definition);PlayEntity(g,p,body);
        Check(body.Card[(GameTag)4028]==1 && body.IsImmune && body.IsRush && body.HasWindfury,"composed Kindred preserves conditional temporary ability");
        foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
            var actual=(Minion)next.IdEntityDic[body.Id];End(next);
            Check(!actual.IsImmune && actual.HasWindfury && actual[GameTag.REBORN]==1,"temporary component expires without erasing intrinsic abilities");
        }
        var rewind=ImbueProof.Make(CardClass.DEATHKNIGHT);
        var drawn=Generic.DrawCard(rewind.Player1,GeneratedDefinitionRules.Creation(Cards.FromId("TIME_003"),Cards.FromId("CORE_EX1_012")));
        PlayEntity(rewind,rewind.Player1,drawn);
        Check(rewind.Rewind!=null,"composed Rewind opens a real transaction");
        foreach(var next in new[]{rewind,rewind.CloneExact(history:true),Game.LoadSnapshot(rewind.SaveSnapshot())}) {
            B18GeneratedProof.Answer(next,next.Rewind.RewindEntityId);
            if(next.Rewind!=null) B18GeneratedProof.Answer(next,next.Rewind.KeepEntityId);
            Check(next.Rewind==null && next.Player1.Choice==null && next.IdEntityDic[drawn.Id].Card[GameTag.MODULAR_ENTITY_PART_1]==Cards.FromId("TIME_003").AssetId,
                "retry and keep preserve generated definition and finish decision");
        }
        Check(Canonical.State(rewind).Contains("CATA_470t1"),"Rewind retains Creation identity");
    }

    private static void CoinPolicyAndOpening()
    {
        foreach(int seat in new[]{1,2}) {
            var g=ImbueProof.Make(CardClass.ROGUE);var p=seat==1?g.Player1:g.Player2;
            var coin=Add(p,"GAME_005");var opposing=Add(p.Opponent,"GAME_005");
            p[PayloadRules.CoinReplacement]=127153;PayloadRules.ReplaceCoin(coin);
            Check(coin.Card.Id=="JAIL_504t2" && opposing.Card.Id=="GAME_005","existing hand upgrades only owning Coins");
            foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=seat==1?next.Player1:next.Player2;
                Check(Add(owner,"GAME_005").Card.Id=="JAIL_504t2","future acquired Coin upgrades after restore");
                Check(Add(owner.Opponent,"GAME_005").Card.Id=="GAME_005","opponent policy isolated");
                End(next);Check(Add(owner,"GAME_005").Card.Id=="JAIL_504t2","replacement persists across turns");
            }
        }
        foreach(var seats in new[]{(true,false),(false,true),(true,true),(false,false)}) {
            var a=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList();var b=a.ToList();
            if(seats.Item1)a[0]=Cards.FromId("JAIL_504");if(seats.Item2)b[0]=Cards.FromId("JAIL_504");
            var g=new Game(new GameConfig{RandomSeed=17,StartPlayer=-1,Shuffle=false,SkipMulligan=true,
                Player1HeroClass=CardClass.ROGUE,Player2HeroClass=CardClass.ROGUE,Player1Deck=a,Player2Deck=b,History=true});g.StartGame();
            if(seats.Item1!=seats.Item2)Check(g.FirstPlayer==(seats.Item1?g.Player2:g.Player1),"single Aya chooses second before opening");
            Check(g.FirstPlayer.HandZone.All(c=>c.Card.Id!="GAME_005") && g.FirstPlayer.Opponent.HandZone.Count(c=>c.Card.Id=="GAME_005")==1,
                "one ordinary second-player Coin only");
        }
    }
    private static void SeededPotion()
    {
        var g=ImbueProof.Make(CardClass.ROGUE);var p=g.Player1;var oracle=g.CloneExact(history:true);
        var candidates=AyaLotusKingpin.Ingredients.ToList();var expected=new List<string>();
        for(int i=0;i<2;i++){int pick=oracle.Random.Next(candidates.Count);expected.Add(candidates[pick]);candidates.RemoveAt(pick);}
        Play(g,p,"JAIL_504t3");var potion=p.HandZone.Single(c=>c.Card.Id=="JAIL_504t3p");
        Check(potion[GameTag.MODULAR_ENTITY_PART_1]==Cards.FromId(expected[0]).AssetId && potion[GameTag.MODULAR_ENTITY_PART_2]==Cards.FromId(expected[1]).AssetId,
            "seeded order-sensitive distinct potion ingredients");
        Check(g.Random.CaptureState().Equals(oracle.Random.CaptureState()),"two ingredient RNG draws only");
    }
    private static void OldDefinitionBytes()
    {
        foreach(var file in Directory.GetFiles("tests/engine-proof/definition-fixtures","*.b64")) {
            var bytes=File.ReadAllText(file).Trim();var card=SnapshotCodec.DecodeDefinition(bytes);
            Check(card!=null,"historical detached definition still decodes: "+file);
            Check(SnapshotCodec.EncodeDefinition(card)==bytes,"historical detached definition re-encodes byte-identically: "+file);
        }
    }
}
