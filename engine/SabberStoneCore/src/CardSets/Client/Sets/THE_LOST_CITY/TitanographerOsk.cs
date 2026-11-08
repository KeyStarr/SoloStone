using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TitanographerOsk
    {
        internal const int DbfId = 117956;
        internal static readonly string[] Variants = { "TLC_452t1", "TLC_452t2", "TLC_452t3", "TLC_452t4", "TLC_452t5", "TLC_452t6", "TLC_452t7", "TLC_452t8", "TLC_452t9", "TLC_452t13", "TLC_452t14", "TLC_452t15", "TLC_452t16", "TLC_452t17", "TLC_452t18", "TLC_452t19", "TLC_452t20", "TLC_452t21", "TLC_452t22", "TLC_452t23", "TLC_452t24", "TLC_452t26", "TLC_452t27", "TLC_452t28", "TLC_452t29", "TLC_452t30", "TLC_452t31", "TLC_452t32", "TLC_452t33", "TLC_452t34", "TLC_452t35" };
        internal static OfflinePoolFilter Deathrattles => OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.DEATHRATTLE);
        internal static OfflinePoolFilter Six => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 6, maxCost: 6);
        internal static OfflinePoolFilter Secrets => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE,
            type: CardType.SPELL, keyword: GameTag.SECRET);
        [StandardCard("TLC_452", DbfId)]
        internal static void Configure(Card card) => ConfigureForm(card, 0);
        [StandardCard("TLC_452t1",120714)]
        internal static void Form1(Card card) => ConfigureForm(card,1);
        [StandardCard("TLC_452t2",121297)]
        internal static void Form2(Card card) => ConfigureForm(card,2);
        [StandardCard("TLC_452t3",121298)]
        internal static void Form3(Card card) => ConfigureForm(card,3);
        [StandardCard("TLC_452t4",121299)]
        internal static void Form4(Card card) => ConfigureForm(card,4);
        [StandardCard("TLC_452t5",121300)]
        internal static void Form5(Card card) => ConfigureForm(card,5);
        [StandardCard("TLC_452t6",121301)]
        internal static void Form6(Card card) => ConfigureForm(card,6);
        [StandardCard("TLC_452t7",121302)]
        internal static void Form7(Card card) => ConfigureForm(card,7);
        [StandardCard("TLC_452t8",121303)]
        internal static void Form8(Card card) => ConfigureForm(card,8);
        [StandardCard("TLC_452t9",121304)]
        internal static void Form9(Card card) => ConfigureForm(card,9);
        [StandardCard("TLC_452t13",121308)]
        internal static void Form13(Card card) => ConfigureForm(card,13);
        [StandardCard("TLC_452t14",121309)]
        internal static void Form14(Card card) => ConfigureForm(card,14);
        [StandardCard("TLC_452t15",121310)]
        internal static void Form15(Card card) => ConfigureForm(card,15);
        [StandardCard("TLC_452t16",121311)]
        internal static void Form16(Card card) => ConfigureForm(card,16);
        [StandardCard("TLC_452t17",121312)]
        internal static void Form17(Card card) => ConfigureForm(card,17);
        [StandardCard("TLC_452t18",121313)]
        internal static void Form18(Card card) => ConfigureForm(card,18);
        [StandardCard("TLC_452t19",121314)]
        internal static void Form19(Card card) => ConfigureForm(card,19);
        [StandardCard("TLC_452t20",121315)]
        internal static void Form20(Card card) => ConfigureForm(card,20);
        [StandardCard("TLC_452t21",121316)]
        internal static void Form21(Card card) => ConfigureForm(card,21);
        [StandardCard("TLC_452t22",121317)]
        internal static void Form22(Card card) => ConfigureForm(card,22);
        [StandardCard("TLC_452t23",121318)]
        internal static void Form23(Card card) => ConfigureForm(card,23);
        [StandardCard("TLC_452t24",121319)]
        internal static void Form24(Card card) => ConfigureForm(card,24);
        [StandardCard("TLC_452t26",121321)]
        internal static void Form26(Card card) => ConfigureForm(card,26);
        [StandardCard("TLC_452t27",121322)]
        internal static void Form27(Card card) => ConfigureForm(card,27);
        [StandardCard("TLC_452t28",121323)]
        internal static void Form28(Card card) => ConfigureForm(card,28);
        [StandardCard("TLC_452t29",121324)]
        internal static void Form29(Card card) => ConfigureForm(card,29);
        [StandardCard("TLC_452t30",121325)]
        internal static void Form30(Card card) => ConfigureForm(card,30);
        [StandardCard("TLC_452t31",121326)]
        internal static void Form31(Card card) => ConfigureForm(card,31);
        [StandardCard("TLC_452t32",121327)]
        internal static void Form32(Card card) => ConfigureForm(card,32);
        [StandardCard("TLC_452t33",121328)]
        internal static void Form33(Card card) => ConfigureForm(card,33);
        [StandardCard("TLC_452t34",121329)]
        internal static void Form34(Card card) => ConfigureForm(card,34);
        [StandardCard("TLC_452t35",121330)]
        internal static void Form35(Card card) => ConfigureForm(card,35);
        private static void ConfigureForm(Card card, int ability)
        {
            Token(card);
            if (ability == 0) card.Entourage = Variants;
            if (ability == 33) card.Entourage = new[] { "YOG_514" };
            var power = new Power {
                HandEntryTask = ability == 0 ? new CustomTask((g,c,s,t,stack) => Roll((IPlayable)s)) : null,
                Trigger = new Trigger(TriggerType.TURN_START) { TriggerActivation = TriggerActivation.HAND,
                    SingleTask = new CustomTask((g,c,s,t,stack) => Roll((IPlayable)s)) },
                PowerTask = new CustomTask((g,c,s,t,stack) => Execute(ability,g,c,s,t,stack))
            };
            if (ability == 4) power.PowerTask = new DecisionBattlecryTask("b18/osk/discover/v1",
                new DiscoverThenContinueTask(Deathrattles, recipeDbfId: DbfId),
                new CustomTask((g,c,s,t,stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen == null) return;
                    Generic.AddEnchantmentBlock(g,Cards.FromId("TTN_862t1e"),(IPlayable)s,chosen);
                    Generic.AddHandPhase(c,c.SetasideZone.Remove(chosen));
                }));
            if (ability == 21) power.PowerTask = new DecisionBattlecryTask("b18/osk/banish/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.PlayTargetOrBoard,SelectorSide.Enemy,
                    cardType:CardType.MINION,groupKey:"osk-banish")),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board,SelectorSide.Enemy,
                    cardType:CardType.MINION,groupKey:"osk-banish",excludeEarlierPicks:true)),
                new CustomTask((g,c,s,t,stack) => {
                    foreach (int id in g.Resolution.CurrentEffect.SelectedInGroup.ToArray())
                        if (g.IdEntityDic[id] is Minion m && m.Controller == c.Opponent && m.Zone == c.Opponent.BoardZone) Banish(m);
                }));
            if (new[] { 1,13,19,21,26,35 }.Contains(ability)) {
                var requirements = new Dictionary<PlayReq,int> { [PlayReq.REQ_TARGET_IF_AVAILABLE] = 0 };
                if (ability != 13) requirements[PlayReq.REQ_MINION_TARGET] = 0;
                if (ability == 1 || ability == 21 || ability == 35) requirements[PlayReq.REQ_ENEMY_TARGET] = 0;
                card.SetPlayRequirements(requirements);
                if (ability == 19) card.AddTargetingPredicate(target => target[(GameTag)2772] == 0);
            }
            card.Power = power;
        }
        internal static void Roll(IPlayable card)
        {
            if (card.Zone != card.Controller.HandZone) return;
            var g = card.Game;
            var next = Cards.FromId(Variants[g.Random.Next(Variants.Length)]);
            g.OnRandomHappened(true);
            // Ability identity changes; same printed body and every owned buff remain intact.
            card.Card = next; g.Facts.AdmitDefinition(next);
            if (g.History) g.PowerHistory.Add(new PowerHistoryChangeEntity {
                CardId = next.Id, Entity = PowerHistoryBuilder.FullEntity(card).Entity
            });
        }
        private static void Buff(Game g, IEntity source, IEntity target, string id, int amount = 0) =>
            Generic.AddEnchantmentBlock(g,Cards.FromId(id),(IPlayable)source,target,amount);
        private static void Banish(Minion minion)
        {
            var owner = minion.Controller;
            owner.BoardZone.Remove(minion); minion.Reset(); owner.SetasideZone.Add(minion);
            minion[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
            if (owner.Game.History) owner.Game.PowerHistory.Add(new PowerHistoryHideEntity { EntityID=minion.Id, Zone=Zone.REMOVEDFROMGAME });
            owner.Game.AuraUpdate();
        }
        private static void Execute(int ability, Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            var ctx = new EffectContext(g,c.Id,s.Id,t?.Id,stack);
            switch (ability) {
                case 1:
                    if (t is Minion victim) {
                        int health = victim.Health; victim.Destroy();
                        Buff(g,s,s,"TTN_737te",health); Buff(g,s,c.Hero,"TTN_737te",health);
                    } break;
                case 2: Buff(g,s,c,"TTN_737e"); break;
                case 3: for (int i=0;i<2;i++) ctx.Summon("TTN_737t2"); break;
                case 5:
                    foreach (var m in c.HandZone.OfType<Minion>().ToArray()) Buff(g,s,m,"TTN_862t3e"); break;
                case 6: for (int i=0;i<4;i++) ctx.Summon("TTN_862t4"); break;
                case 7:
                    while (!c.HandZone.IsFull && !c.DeckZone.IsEmpty && g.State != State.COMPLETE) Generic.Draw(c); break;
                case 8: ctx.Heal(c.Hero.Id,c.Hero.Damage); break;
                case 9: c.UsedMana=0; break;
                case 13: if (t != null) ctx.Damage(t.Id,5); break;
                case 14:
                    if (c.SecretZone.Count >= 5) break;
                    var secrets = OfflineCardPools.Candidates(g,c.HeroClass,Secrets,DbfId, PoolDestination.Other)
                        .Where(card => !c.SecretZone.Any(active => active.Card.Id == card.Id)).ToArray();
                    if (secrets.Length > 0) AutoPlayRules.Play(Entity.FromCard(c,secrets[g.Random.Next(secrets.Length)]),s);
                    break;
                case 15: Buff(g,s,c.Opponent,"TTN_075t2e"); break;
                case 16:
                    foreach (var enemy in c.Opponent.BoardZone.Where(m=>!m.IsDormant).ToArray()) Buff(g,s,enemy,"TTN_858t3e"); break;
                case 17:
                    foreach (var ally in c.BoardZone.Where(m=>m.Id!=s.Id && !m.IsDormant).ToArray()) Buff(g,s,ally,"TTN_858t2e1"); break;
                case 18:
                    for (int i=0;i<2;i++) {
                        var minions=c.DeckZone.OfType<Minion>().ToArray(); if (minions.Length==0) break;
                        var drawn=minions[g.Random.Next(minions.Length)]; Generic.Draw(c,drawn);
                        if (drawn.Zone==c.HandZone) { Buff(g,s,drawn,"TTN_858t1e"); Buff(g,s,drawn,"TTN_858t1e1"); }
                    } break;
                case 19:
                    if (t is Minion original && original[(GameTag)2772]==0 && original.Zone==original.Controller.BoardZone && !c.BoardZone.IsFull)
                        Generic.Copy(c,s,original,Zone.PLAY,preserveBoardMinionState:true,
                            initialize:copy=>Buff(g,s,copy,"TTN_429e"));
                    break;
                case 20:
                    if (c.BoardZone.IsFull) break;
                    var pick=ctx.PickRandomCard(Six,recipeDbfId:DbfId);
                    if (pick!=null) {
                        var minion=(Minion)Entity.FromCard(c,pick);
                        new Effect(GameTag.TAUNT,EffectOperator.SET,1).ApplyTo(minion);
                        new Effect(GameTag.LIFESTEAL,EffectOperator.SET,1).ApplyTo(minion);
                        Generic.SummonBlock(g,minion,-1,s);
                    } break;
                case 22:
                    Buff(g,s,s,"TTN_721te");
                    var enemies=c.Opponent.BoardZone.Where(m=>!m.IsDormant).Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
                    ctx.Damage(enemies[g.Random.Next(enemies.Length)].Id,4); break;
                case 23: Buff(g,s,s,"TTN_721t1e"); Generic.Draw(c); break;
                case 24: Buff(g,s,s,"TTN_721t2e2"); break;
                case 26: if (t != null && t.Id != s.Id) ctx.Damage(t.Id,20); break;
                case 27:
                    foreach (var enemy in c.Opponent.BoardZone.Where(m=>!m.IsDormant).Cast<ICharacter>().Append(c.Opponent.Hero).ToArray()) ctx.Damage(enemy.Id,3);
                    foreach (var ally in c.BoardZone.Where(m=>!m.IsDormant).Cast<ICharacter>().Append(c.Hero).ToArray()) ctx.Heal(ally.Id,6);
                    break;
                case 28: for (int i=0;i<2;i++) ctx.Summon("TTN_960t5"); break;
                case 29:
                    foreach (var minion in c.BoardZone.Concat(c.Opponent.BoardZone).Where(m=>m.Id!=s.Id && !m.IsDormant).ToArray()) Banish(minion); break;
                case 30: Buff(g,s,s,"TTN_415t2e2"); c.Hero.Armor+=5; break;
                case 31: Buff(g,s,s,"TTN_415t2e"); Effects.Attack_N(5).ApplyTo(c.Hero,true); break;
                case 32:
                    Buff(g,s,s,"TTN_415te"); ctx.DrawWhere(card=>card.Card.Type==CardType.WEAPON); break;
                case 33:
                    while (!c.HandZone.IsFull) Generic.DrawCard(c,Cards.FromId("YOG_514")); break;
                case 34:
                    var cohort=c.Opponent.BoardZone.Where(m=>!m.IsDormant).OrderBy(m=>m.OrderOfPlay).ToArray();
                    foreach (var attacker in cohort) {
                        if (attacker.Zone!=c.Opponent.BoardZone || attacker.IsDead || attacker.IsDormant) continue;
                        var defenders=c.Opponent.BoardZone.Where(m=>m.Id!=attacker.Id && !m.IsDead && !m.IsDormant).ToArray();
                        if (defenders.Length==0) continue;
                        new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(g,attacker.Controller,attacker,defenders[g.Random.Next(defenders.Length)]);
                        g.DeathProcessingAndAuraUpdate();
                    } break;
                case 35: if (t!=null) new ControlTask(EntityType.TARGET).Process(g,c,s,t,stack); break;
            }
        }
        [StandardCard("TTN_737e",102034)]
        internal static void Frost(Card card)
        {
            Token(card);
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND,Effects.ReduceCost(3)) { Condition=SelfCondition.IsSpell },
                Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource=TriggerSource.FRIENDLY, FastExecution=true,
                    SingleTask=new CustomTask((g,c,s,t,stack)=>{
                        var spell=g.CurrentEventData.EventSource;
                        var grant=Cards.FromId("TTN_737e").Clone();
                        grant.Power=new Power {
                            Enchant=new Enchant(new Effect(GameTag.SPELLPOWER,EffectOperator.ADD,3)),
                            Trigger=new Trigger(TriggerType.AFTER_CAST) { TriggerSource=TriggerSource.ENCHANTMENT_TARGET,
                                SingleTask=RemoveEnchantmentTask.Task }
                        };
                        Generic.AddEnchantmentBlock(g,grant,(IPlayable)s,spell);
                        ((Enchantment)s).Remove();g.AuraUpdate();
                    }) }
            };
        }
        [StandardCard("TTN_075t2e",102042)]
        internal static void Tax(Card card)
        {
            Token(card); card.Power=new Power { Aura=new Aura(AuraType.OP_HAND,Effects.AddCost(1)) {
                RemoveTrigger=(TriggerType.TURN_END,SelfCondition.IsOpTurn) } };
        }
        [StandardCard("TTN_737t2",98288)]
        internal static void Helper0(Card card) { Token(card); }
        [StandardCard("TTN_862t4",98348)]
        internal static void Helper1(Card card) { Token(card); }
        [StandardCard("TTN_960t5",101510)]
        internal static void Helper2(Card card) { Token(card); }
        [StandardCard("TTN_737te",104881)]
        internal static void Helper3(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Health_N(0)) { UseScriptTag=true } }; }
        [StandardCard("TTN_862t1e",101500)]
        internal static void Helper4(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.ReduceCost(3)) }; }
        [StandardCard("TTN_862t3e",101501)]
        internal static void Helper5(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.ReduceCost(2)) }; }
        [StandardCard("TTN_858t3e",104882)]
        internal static void Helper6(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.SetAttack(2),Effects.SetMaxHealth(2)) }; }
        [StandardCard("TTN_858t2e1",98438)]
        internal static void Helper7(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(2)) }; }
        [StandardCard("TTN_858t1e",103577)]
        internal static void Helper8(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.SetAttack(2),Effects.SetMaxHealth(2)) }; }
        [StandardCard("TTN_858t1e1",103578)]
        internal static void Helper9(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.SetCost(2)) }; }
        [StandardCard("TTN_429e",101486)]
        internal static void Helper10(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(2)) }; }
        [StandardCard("TTN_721te",97644)]
        internal static void Helper11(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(2),Effects.Health_N(1)) }; }
        [StandardCard("TTN_721t1e",97647)]
        internal static void Helper12(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(1),Effects.Health_N(2)) }; }
        [StandardCard("TTN_721t2e2",98337)]
        internal static void Helper13(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Health_N(3),Effects.CantBeTargetedBySpellsAndHeroPowers) }; }
        [StandardCard("TTN_415t2e2",97470)]
        internal static void Helper14(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Health_N(5)) }; }
        [StandardCard("TTN_415t2e",97466)]
        internal static void Helper15(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(5)) }; }
        [StandardCard("TTN_415te",98627)]
        internal static void Helper16(Card card) { Token(card); card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(2)) }; }
        private static void Token(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.Power=new Power(); }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class TitanographerOskPools
    {
        [OfflinePool("TLC_452", Sets.TitanographerOsk.DbfId)]
        internal static PoolRequirement[] Pools() => new[] {
            new PoolRequirement("osk-deathrattle", Sets.TitanographerOsk.Deathrattles, new[] { "MAGE" }),
            new PoolRequirement("osk-six", Sets.TitanographerOsk.Six, new[] { "MAGE" }, allowThinExactCost: true, destination: PoolDestination.Summon),
            new PoolRequirement("osk-secret", Sets.TitanographerOsk.Secrets, new[] { "MAGE" }, destination: PoolDestination.Other)
        };
    }
}
