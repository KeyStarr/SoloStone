using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class Program
{
    // Raw client tag ids, so the tests pin which tag carries the sleeping state. 1518 is the client's DORMANT tag (the
    // one the native client animates from). 1090 is DORMANT_VISUAL (the old engine called it DORMANT): it is still
    // mirrored at runtime, but no client animation is known to read it.
    private static readonly GameTag Dormant = (GameTag)1518;
    private static readonly GameTag Visual = (GameTag)1090;
    private static int passed, failed;
    private static void Main()
    {
        Run("exact frozen definitions", Definitions);
        Run("occupied slot and full board", Slots);
        Run("sleeping attacker and target rejection", Targets);
        Run("sleeping Taunt cannot block", Taunt);
        Run("mixed board AOE and random exclusion", Effects);
        Run("aura recipient exclusion and wake refresh", AuraRecipient);
        Run("sleeping aura emitter and spell power inactive", AuraEmitter);
        Run("ordinary triggers inactive until wake", Triggers);
        Run("exactly two owner starts and Rush", TimedWake);
        Run("successful owner power and ordinary readiness", PowerWake);
        Run("failed and opponent powers do not wake", InvalidPower);
        Run("clone wake counters isolated", Clone);
        Run("ordered same entity wake history", History);
        Run("gameplay copies preserve awake and sleeping state", GameplayCopy);
        Run("transform away clears dormant state", TransformAway);
        Run("exact Sprite condition helper and supplemental tags", HelperDefinition);
        Run("printed Dormant does not sleep hand or deck", HandDeckDormant);
        Run("Sprite condition lifecycle and transforms", HelperLifecycle);
        Run("Sprite helper copy and clone ownership", HelperOwnership);
        Run("parent and child attachment history order", HelperHistory);
        Run("printed Dormant still publishes board entry", RuntimeEntryHistory);
        Run("nonplay raw Dormant and published identity", NonplayHistory);
        Run("leaving play clears raw Dormant and helper", LeavePlay);
        Console.WriteLine($"Dormant: {passed} passed, {failed} failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static Game Fresh()
    {
        var g = ProofDecks.CreateGame("proof-mage", "proof-warrior", 31);
        g.StartGame(); g.Player1.BaseMana = g.Player2.BaseMana = 10;
        g.Player1.UsedMana = g.Player2.UsedMana = 0; return g;
    }
    private static Minion Hand(Controller c, string id) => (Minion)Generic.DrawCard(c, Cards.FromId(id));
    private static IPlayable Spell(Controller c, string id) => Generic.DrawCard(c, Cards.FromId(id));
    private static Minion Board(Controller c, string id) => (Minion)Entity.FromCard(c, Cards.FromId(id), zone: c.BoardZone);
    private static Minion Play(Game g, string id)
    {
        var m = Hand(g.CurrentPlayer, id);
        Check(g.Process(PlayCardTask.Minion(g.CurrentPlayer, m)), "play rejected " + id); return m;
    }
    private static Minion Sleeping(Controller c, string id)
    {
        // Real entity tags model an asleep ordinary effect; no production test seam.
        return (Minion)Entity.FromCard(c, Cards.FromId(id), new Dictionary<GameTag,int> {
            {Dormant,1}, {Visual,1}, {GameTag.UNTOUCHABLE,1}, {GameTag.TAG_SCRIPT_DATA_NUM_1,1}
        }, zone: c.BoardZone);
    }
    private static void End(Game g) => Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "end rejected");
    private static void Round(Game g) { End(g); End(g); }
    private static void Definitions()
    {
        var v=Cards.FromId("CORE_BT_156"); var s=Cards.FromId("EDR_469");
        Check(v.Implemented && s.Implemented, "Dormant cards not implemented");
        Check(v.AssetId==127593 && v.Cost==2 && v.ATK==3 && v.Health==5 && v[GameTag.CARDRACE]==15 && v.Rush, "Vilefiend metadata");
        Check(s.AssetId==114344 && s.Cost==1 && s.ATK==3 && s.Health==3 && (int)s.Set==1946 && s[GameTag.CARDRACE]==0, "Sprite metadata");
        Check((int)v.Set==1637 && (int)s.Rarity==4, "set or rarity");
    }
    private static void Slots()
    {
        var g=Fresh(); var m=Play(g,"EDR_469");
        Check(g.Player1.BoardZone.Count==1 && g.Player1.BoardZone.GetAll().Length==0, "sleeping slot missing or active");
        for(int i=0;i<6;i++) Board(g.Player1,"CORE_CS2_189");
        var extra=Hand(g.Player1,"CORE_CS2_189");
        Check(g.Player1.BoardZone.IsFull && !g.Process(PlayCardTask.Minion(g.Player1,extra)), "sleeping slot did not fill board");
        Check(m.ZonePosition==0 && m.Zone==g.Player1.BoardZone, "sleeping position lost");
    }
    private static void Targets()
    {
        var g=Fresh();var own=Play(g,"CORE_BT_156");var enemy=Board(g.Player2,"EDR_469");
        own.IsExhausted=false;
        Check(!own.CanAttack && !g.Process(MinionAttackTask.Any(g.Player1,own,g.Player2.Hero)), "sleeper attacked");
        var bolt=Spell(g.Player1,"CORE_CS2_024");var archer=Hand(g.Player1,"CORE_CS2_189");
        Check(!g.Player1.Options().Any(t=>t.Target==own || t.Target==enemy), "sleeping target offered");
        Check(!g.Process(PlayCardTask.SpellTarget(g.Player1,bolt,enemy)), "spell targeted sleeping enemy");
        Check(!g.Process(PlayCardTask.MinionTarget(g.Player1,archer,own)), "battlecry targeted friendly sleeper");
        Check(!g.Process(HeroPowerTask.Any(g.Player1,enemy)), "hero power targeted sleeper");
    }
    private static void Taunt()
    {
        var g=Fresh();var sleeper=Sleeping(g.Player2,"CORE_CS2_179");var attacker=Board(g.Player1,"Core_CS2_200");attacker.IsExhausted=false;
        Check(sleeper.HasTaunt && attacker.ValidAttackTargets.Contains(g.Player2.Hero), "sleeping Taunt blocked face");
        Check(g.Player1.Options().Any(t=>t.Source==attacker && t.Target==g.Player2.Hero), "face option missing");
        Check(!attacker.ValidAttackTargets.Contains(sleeper), "sleeping Taunt attackable");
    }
    private static void Effects()
    {
        var g=Fresh();var sleeper=Board(g.Player2,"CORE_BT_156");var active=Board(g.Player2,"Core_CS2_200");
        Check(g.Process(PlayCardTask.Spell(g.Player1,Spell(g.Player1,"CORE_CS2_028"))), "Blizzard rejected");
        Check(sleeper.Damage==0 && !sleeper.IsFrozen && active.Damage==2 && active.IsFrozen, "AOE affected sleeper");
        g.Player1.UsedMana=0;
        Check(g.Process(PlayCardTask.Spell(g.Player1,Spell(g.Player1,"EX1_277"))), "Missiles rejected");
        Check(sleeper.Damage==0, "random effect affected sleeper");
        for(int i=0;i<20;i++)
        {
            Check(g.Player2.BoardZone.Random==active,"board Random returned sleeper");
            Check(((SabberStoneCore.Model.Zones.Zone<Minion>)g.Player2.BoardZone).Random==active,"generic zone Random returned sleeper");
        }
        Check(g.Player2.BoardZone.CountExceptUntouchables==1, "active count wrong");
    }
    private static void AuraRecipient()
    {
        var g=Fresh();var sleeper=Play(g,"CORE_BT_156");Play(g,"CORE_CS2_222");
        Check(sleeper.AttackDamage==3 && sleeper.Health==5, "aura buffed sleeping recipient");
        Round(g);Round(g);
        Check(sleeper.AttackDamage==4 && sleeper.Health==6, "aura not refreshed on wake");
    }
    private static void AuraEmitter()
    {
        var g=Fresh();var active=Board(g.Player1,"CORE_CS2_189");var champion=Sleeping(g.Player1,"CORE_CS2_222");
        var spellpower=Sleeping(g.Player1,"CS2_142");g.DeathProcessingAndAuraUpdate();
        Check(active.AttackDamage==1 && active.Health==1, "sleeping Champion emitted aura");
        Check(g.Player1.CurrentSpellPower==0, "sleeping minion emitted spellpower");
        Round(g);
        Check(champion[Dormant]==0 && active.AttackDamage==2 && active.Health==2, "awake Champion aura absent");
        Check(spellpower[Dormant]==0 && g.Player1.CurrentSpellPower==1, "awake spellpower absent");
    }
    private static void Triggers()
    {
        var g=Fresh();var frothing=Sleeping(g.Player1,"CORE_EX1_604");var victim=Board(g.Player2,"Core_CS2_200");
        Check(g.Process(HeroPowerTask.Any(g.Player1,victim)),"trigger probe power rejected");
        Check(frothing.AttackDamage==2, "sleeping ordinary damage trigger ran");
        Round(g);
        Check(g.Process(HeroPowerTask.Any(g.Player1,victim)),"awake trigger probe rejected");
        Check(frothing.AttackDamage==3, "awake ordinary trigger absent");
    }
    private static void TimedWake()
    {
        var g=Fresh();var m=Play(g,"CORE_BT_156");var target=Board(g.Player2,"Core_CS2_200");int id=m.Id;
        Check(m[Dormant]==1 && m[Visual]==1 && m[GameTag.UNTOUCHABLE]==1 && m[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"entry tags");
        End(g);Check(m[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"opponent start ticked");
        End(g);Check(m[Dormant]==1 && m[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"first owner start wrong");
        Round(g);Check(m[Dormant]==0 && m.Id==id && m.Zone==g.Player1.BoardZone,"second owner start did not awaken same entity");
        Check(m.CanAttack && m.AttackableByRush && m.ValidAttackTargets.Contains(target) && !m.ValidAttackTargets.Contains(g.Player2.Hero),"wake Rush readiness wrong");
        Round(g);Check(!m.AttackableByRush && m.ValidAttackTargets.Contains(g.Player2.Hero),"Rush restriction did not expire");
    }
    private static void PowerWake()
    {
        var g=Fresh();var m=Play(g,"EDR_469");
        Round(g);Check(m[Dormant]==1,"Sprite woke on owner turn");
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"owner power rejected");
        Check(m[Dormant]==0 && m.IsExhausted && !m.CanAttack && g.Player1.NumMinionsPlayedThisTurn==0,"Sprite wake readiness or replayed play");
        Round(g);Check(m.CanAttack,"Sprite not ready next owner turn");
    }
    private static void InvalidPower()
    {
        var g=Fresh();var m=Play(g,"EDR_469");g.Player1.UsedMana=10;string before=g.Hash();
        Check(!g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)) && m[Dormant]==1 && g.Hash()==before,"failed power changed sleeper");
        End(g);Check(g.Process(HeroPowerTask.Any(g.Player2)) && m[Dormant]==1,"opponent power woke Sprite");
        End(g);Check(!g.Process(HeroPowerTask.Any(g.Player1)) && m[Dormant]==1,"invalid target power woke Sprite");
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)) && m[Dormant]==0,"later valid power did not wake");
    }
    private static void Clone()
    {
        var g=Fresh();var m=Play(g,"CORE_BT_156");Round(g);var clone=g.Clone(resetRandomSeed:false,history:true);
        var copy=(Minion)clone.IdEntityDic[m.Id];Round(clone);
        Check(copy[Dormant]==0 && m[Dormant]==1 && m[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"cloned timer shared");
        Check(g.Player1.BoardZone.GetAll().Length==0 && clone.Player1.BoardZone.GetAll().Length==1,"clone active count shared");
        var sprite=Play(g,"EDR_469");var other=g.Clone(resetRandomSeed:false,history:true);
        Check(other.Process(HeroPowerTask.Any(other.Player1,other.Player2.Hero)),"clone power failed");
        Check(sprite[Dormant]==1 && other.IdEntityDic[sprite.Id][Dormant]==0,"cloned wake watcher shared");
    }
    private static void GameplayCopy()
    {
        var g=Fresh();var m=Play(g,"CORE_BT_156");Round(g);
        var sleep=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,m,Zone.PLAY);
        Check(sleep.Id!=m.Id && sleep[Dormant]==1 && sleep[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,
            "sleeping copy did not preserve remaining owner starts");
        Round(g);Check(m[Dormant]==0 && sleep[Dormant]==0,"copied timer did not wake independently");
        int plays=g.Player1.NumMinionsPlayedThisTurn;
        var awake=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,m,Zone.PLAY);
        Check(awake.Id!=m.Id && awake[Dormant]==0 && !awake.Untouchable && awake.AttackableByRush,
            "awake gameplay copy went dormant");
        Check(g.Player1.NumMinionsPlayedThisTurn==plays,"copy replayed minion");
        var sprite=Play(g,"EDR_469");
        var copy=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,sprite,Zone.PLAY);
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"copy wake power rejected");
        Check(sprite[Dormant]==0 && copy[Dormant]==0 && copy.IsExhausted,"Sprite copy wake watcher absent");
    }
    private static void TransformAway()
    {
        var g=Fresh();var m=Play(g,"CORE_BT_156");
        var transformed=(Minion)Generic.ChangeEntityBlock(g.Player1,m,Cards.FromId("Core_CS2_200"),true);
        Check(transformed.Id==m.Id && transformed[Dormant]==0 && transformed[Visual]==0 &&
            transformed[GameTag.UNTOUCHABLE]==0 && transformed[GameTag.TAG_SCRIPT_DATA_NUM_1]==0 &&
            !transformed.Untouchable && !transformed.AttackableByRush,"transform retained sleeping lifecycle");
        Check(g.Player1.BoardZone.GetAll().Single()==transformed,"transformed minion excluded from active board");
    }
    private static Enchantment Condition(Minion parent) => parent.AppliedEnchantments?.SingleOrDefault(e=>e.Card.Id=="EDR_469e");
    private static void RuntimeEntryHistory()
    {
        var g=Fresh();
        foreach(string id in new[]{"EDR_469","CORE_BT_156"})
        {
            var m=Play(g,id);
            Check(g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t=>t.EntityId==m.Id && t.Tag==Dormant && t.Value==1),
                "printed Dormant suppressed runtime entry: " + id);
        }
    }
    private static void NonplayHistory()
    {
        foreach(string id in new[]{"EDR_469","CORE_BT_156"})
        {
            var g=Fresh();
            foreach(var zone in new SabberStoneCore.Model.Zones.IZone[]{g.Player1.HandZone,g.Player1.DeckZone,g.Player1.SetasideZone,g.Player1.GraveyardZone})
            {
                var m=(Minion)Entity.FromCard(g.Player1,Cards.FromId(id),zone:zone);
                Check(m[Dormant]==0 && m.Card[Dormant]==(id=="EDR_469"?0:1) && !m.IsDormant,"nonplay raw state leaked: "+zone.Type);
                int full=g.PowerHistory.Full.FindIndex(e=>e is PowerHistoryFullEntity f && f.Entity.Id==m.Id);
                var first=(PowerHistoryFullEntity)g.PowerHistory.Full[full];
                Check((first.Entity.Tags.TryGetValue(Dormant,out int raw) ? raw==0 : id=="EDR_469"),"nonplay FULL omitted explicit zero: "+zone.Type);
                Check(!g.PowerHistory.Full.Take(full).OfType<PowerHistoryTagChange>().Any(t=>t.EntityId==m.Id && t.Tag==Dormant),"Dormant tag published before identity");
                if(zone.Type==Zone.HAND)
                {
                    var show=g.PowerHistory.Full.OfType<PowerHistoryShowEntity>().Last(e=>e.Entity.Id==m.Id);
                    Check((show.Entity.Tags.TryGetValue(Dormant,out int shown) ? shown==0 : id=="EDR_469"),"hand SHOW published dormant");
                }
            }
        }
        var game=Fresh();var sprite=Hand(game.Player1,"EDR_469");
        Check(game.Process(PlayCardTask.Minion(game.Player1,sprite)),"raw hand-to-play rejected");
        var h=game.PowerHistory.Last;
        int play=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==sprite.Id && t.Tag==GameTag.ZONE && t.Value==(int)Zone.PLAY);
        int on=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==sprite.Id && t.Tag==Dormant && t.Value==1);
        Check(play>=0 && on>play && sprite[Dormant]==1,"raw Dormant turned on before PLAY");
        Check(game.Process(HeroPowerTask.Any(game.Player1,game.Player2.Hero)) && sprite[Dormant]==0 && sprite.Card[Dormant]==0 && sprite.Card[(GameTag)1652]==1,"wake did not preserve explicit zero override");
    }
    private static void LeavePlay()
    {
        foreach(string id in new[]{"EDR_469","CORE_BT_156"})
        foreach(Zone destination in new[]{Zone.HAND,Zone.DECK,Zone.SETASIDE,Zone.GRAVEYARD})
        {
            var g=Fresh();var m=Play(g,id);var helper=Condition(m);
            if(destination==Zone.HAND) Check(Generic.ReturnToHandBlock(g.Player1,m),"return-to-hand failed");
            else
            {
                Generic.RemoveFromZone(g.Player1,m);
                g.Player1.ControlledZones[destination].Add(m);
            }
            Check(m[Dormant]==0 && m.Card[Dormant]==(id=="EDR_469"?0:1) && !m.IsDormant && Condition(m)==null,"leaving PLAY retained raw state/helper: "+destination);
            if(helper!=null) Check(helper[GameTag.ATTACHED]==0 && helper.ActivatedTrigger==null,"outside-play helper remained active");
            if(destination==Zone.HAND)
            {
                var show=g.PowerHistory.Last.OfType<PowerHistoryShowEntity>().Last(e=>e.Entity.Id==m.Id);
                Check((show.Entity.Tags.TryGetValue(Dormant,out int raw) ? raw==0 : id=="EDR_469"),"return-to-hand SHOW omitted zero");
                Check(g.Process(PlayCardTask.Minion(g.Player1,m)) && m[Dormant]==1,"replayed hand minion did not start Dormant");
            }
        }
    }
    private static void HelperDefinition()
    {
        var helper=Cards.FromId("EDR_469e");
        Check(helper.AssetId==114345 && helper.Type==CardType.ENCHANTMENT && helper.Implemented,"exact helper not implemented");
        Check(helper.Name=="Knighttime" && helper.Text=="<b>Dormant</b>. Awaken after you use your Hero Power.","helper name/text changed");
        Check(helper[GameTag.TRIGGER_VISUAL]==1 && helper[(GameTag)1519]==1,"helper original tags absent");
        Check(Cards.FromId("EDR_469")[Dormant]==0 && Cards.FromId("EDR_469")[(GameTag)1652]==1 && Cards.FromId("CORE_BT_156")[Dormant]==1,"parent original Dormant metadata absent");
    }
    private static void HandDeckDormant()
    {
        var g=Fresh();
        foreach(string id in new[]{"EDR_469","CORE_BT_156"})
        {
            var hand=Hand(g.Player1,id);var deck=(Minion)Entity.FromCard(g.Player1,Cards.FromId(id),zone:g.Player1.DeckZone);
            Check(hand.Card[Dormant]==(id=="EDR_469"?0:1) && deck.Card[Dormant]==(id=="EDR_469"?0:1) && hand[Dormant]==0 && deck[Dormant]==0 && !hand.IsDormant && !deck.IsDormant,"printed metadata made hand/deck asleep");
            Check(Condition(hand)==null && Condition(deck)==null,"condition attached outside board");
            Check(g.Process(PlayCardTask.Minion(g.Player1,hand)) && hand.IsDormant,"board did not start asleep");
        }
    }
    private static void HelperLifecycle()
    {
        var g=Fresh();var sprite=Play(g,"EDR_469");var helper=Condition(sprite);
        Check(helper!=null && helper.Target==sprite && helper.Creator==sprite && helper.Controller==g.Player1 &&
            helper[GameTag.ATTACHED]==sprite.Id && helper.ActivatedTrigger!=null,"real condition watcher/attachment absent");
        g.Player1.UsedMana=10;
        Check(!g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)) && Condition(sprite)==helper,"failed power removed helper");
        End(g);Check(g.Process(HeroPowerTask.Any(g.Player2)) && Condition(sprite)==helper,"opponent power removed helper");End(g);
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"owner helper wake rejected");
        Check(Condition(sprite)==null && helper[GameTag.ATTACHED]==0 && helper.ActivatedTrigger==null &&
            helper[GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME && sprite[Dormant]==0,"wake did not remove condition");
        foreach(bool removeEnchantments in new[]{true,false})
        {
            g=Fresh();sprite=Play(g,"EDR_469");helper=Condition(sprite);Check(helper!=null,"transform helper absent");
            Generic.ChangeEntityBlock(g.Player1,sprite,Cards.FromId("Core_CS2_200"),removeEnchantments);
            Check(Condition(sprite)==null && helper[GameTag.ATTACHED]==0 && helper.ActivatedTrigger==null &&
                helper[GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME,"transform retained condition");
        }
    }
    private static void HelperOwnership()
    {
        var g=Fresh();var sprite=Play(g,"EDR_469");var helper=Condition(sprite);Check(helper!=null,"source helper absent");
        var copy=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,sprite,Zone.PLAY);var copyHelper=Condition(copy);
        Check(copyHelper!=null && copyHelper.Id!=helper.Id && copyHelper.Target==copy && copyHelper.Creator==copy &&
            copyHelper[GameTag.ATTACHED]==copy.Id && Condition(sprite)==helper,"copy shared helper ownership");
        var enemy=(Minion)Generic.Copy(g.Player2,g.Player2.Hero,sprite,Zone.PLAY);var enemyHelper=Condition(enemy);
        Check(enemyHelper!=null && enemyHelper.Controller==g.Player2 && enemyHelper.Target==enemy && enemyHelper.Creator==enemy,
            "opponent copy retained source owner");
        var clone=g.Clone(resetRandomSeed:false,history:true);var cloned=(Minion)clone.IdEntityDic[sprite.Id];var clonedHelper=Condition(cloned);
        Check(clonedHelper!=null && !ReferenceEquals(clonedHelper,helper) && clonedHelper.Target==cloned && clonedHelper.Creator==cloned &&
            clonedHelper.Game==clone && clonedHelper[GameTag.ATTACHED]==cloned.Id,"clone retained original helper references");
        Check(clone.Process(HeroPowerTask.Any(clone.Player1,clone.Player2.Hero)),"cloned condition failed");
        Check(Condition(cloned)==null && Condition((Minion)clone.IdEntityDic[copy.Id])==null && Condition(sprite)==helper &&
            Condition(copy)==copyHelper && sprite.IsDormant && copy.IsDormant,"cloned helper removal mutated original");
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"original condition failed");
        Check(enemy.IsDormant && Condition(enemy)==enemyHelper,"other owner power woke copied Sprite");
        End(g);Check(g.Process(HeroPowerTask.Any(g.Player2)) && !enemy.IsDormant && Condition(enemy)==null,"copied owner power did not wake");
        var awake=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,sprite,Zone.PLAY);
        Check(!awake.IsDormant && awake[Dormant]==0 && Condition(awake)==null,"awake copy inherited helper or printed Dormant fallback");
    }
    private static void HelperHistory()
    {
        var g=Fresh();g.PowerHistory.Last.Clear();var sprite=Play(g,"EDR_469");var helper=Condition(sprite);Check(helper!=null,"history helper absent");
        var h=g.PowerHistory.Full;
        int parent=h.FindIndex(e=>e is PowerHistoryFullEntity f && f.Entity.Id==sprite.Id);
        int child=h.FindIndex(e=>e is PowerHistoryFullEntity f && f.Entity.Id==helper.Id && f.Entity.Name=="EDR_469e" && f.Entity.Tags.TryGetValue(GameTag.CARDTYPE,out int type) && type==6 &&
            f.Entity.Tags.TryGetValue(GameTag.ATTACHED,out int id) && id==sprite.Id);
        Check(parent>=0 && child>parent,"parent FULL did not precede identified child FULL/ATTACHED");
        g.PowerHistory.Last.Clear();var copy=(Minion)Generic.Copy(g.Player1,g.Player1.Hero,sprite,Zone.PLAY);var copied=Condition(copy);
        h=g.PowerHistory.Last;
        parent=h.FindIndex(e=>e is PowerHistoryFullEntity f && f.Entity.Id==copy.Id && (!f.Entity.Tags.TryGetValue(Dormant,out int raw) || raw==0));
        child=h.FindIndex(e=>e is PowerHistoryFullEntity f && f.Entity.Id==copied.Id &&
            f.Entity.Tags.TryGetValue(GameTag.ATTACHED,out int id) && id==copy.Id);
        int enter=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==copy.Id && t.Tag==Dormant && t.Value==1);
        Check(parent>=0 && enter>parent && child>enter && copied.Id!=helper.Id,"copy child attached before its own parent FULL/Dormant entry");
        g.PowerHistory.Last.Clear();Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"history helper wake failed");h=g.PowerHistory.Last;
        int wake=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==sprite.Id && t.Tag==Dormant && t.Value==0);
        int detach=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==helper.Id && t.Tag==GameTag.ATTACHED && t.Value==0);
        int hide=h.FindIndex(e=>e is PowerHistoryHideEntity x && x.EntityID==helper.Id);
        int remove=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==helper.Id && t.Tag==GameTag.ZONE && t.Value==(int)Zone.REMOVEDFROMGAME);
        Check(wake>=0 && detach>wake && hide>detach && remove>hide,"wake removal history did not detach/hide/remove helper");
        g=Fresh();sprite=Play(g,"EDR_469");helper=Condition(sprite);g.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(g.Player1,sprite,Cards.FromId("Core_CS2_200"),false);h=g.PowerHistory.Last;
        detach=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==helper.Id && t.Tag==GameTag.ATTACHED && t.Value==0);
        remove=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==helper.Id && t.Tag==GameTag.ZONE && t.Value==(int)Zone.REMOVEDFROMGAME);
        Check(detach>=0 && remove>detach,"transform removal history retained attachment");
    }
    private static void History()
    {
        var g=Fresh();var m=Play(g,"EDR_469");g.PowerHistory.Last.Clear();
        Check(g.Process(HeroPowerTask.Any(g.Player1,g.Player2.Hero)),"history power rejected");
        var h=g.PowerHistory.Last;
        int block=h.FindIndex(e=>e is PowerHistoryBlockStart b && b.BlockType==BlockType.TRIGGER && b.Source==m.Id);
        int dormant=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==m.Id && t.Tag==Dormant && t.Value==0);
        int visual=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==m.Id && t.Tag==Visual && t.Value==0);
        int touch=h.FindIndex(e=>e is PowerHistoryTagChange t && t.EntityId==m.Id && t.Tag==GameTag.UNTOUCHABLE && t.Value==0);
        Check(block>=0 && dormant>block && visual>dormant && touch>visual,"wake history order missing: " + string.Join(" | ", h.Select(e=>e.Print().Trim())));
        Check(g.Player1.BoardZone.Single()==m && !h.OfType<PowerHistoryFullEntity>().Any(e=>e.Entity.Id==m.Id),"wake replaced entity");
    }
}
