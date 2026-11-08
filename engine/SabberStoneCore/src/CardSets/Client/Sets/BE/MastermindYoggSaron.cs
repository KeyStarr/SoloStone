using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MastermindYoggSaron
    {
        internal const int DbfId = 132547;
        private static OfflinePoolFilter Spells() => OfflinePoolFilter.Random(type:CardType.SPELL);
        [OfflinePool("BE_009",DbfId)]
        internal static PoolRequirement[] Pools() => new[] {
            new PoolRequirement("ascent-spell",Spells(),new[] { "NEUTRAL" },PoolCostBinding.StackNumber,ordinaryCost:5, destination: PoolDestination.Transform)
        };
        [StandardCard("BE_009",DbfId)]
        internal static void Configure(Card card)
        {
            _ = Abilities;
            Token(card);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => AutonomousTurnRules.Grant(c,(IPlayable)s)) };
        }

        internal static readonly SuspendableTask Abilities = OldGodRules.Recipe("engine/old-god/yogg/v1",
            OldGodRules.Branch("engine/old-god/yogg/brain/v1",0,OldGodRules.Power((g,c,s,t,stack) =>
                RepeatRules.Grant(c,g.IdEntityDic[stack.Number2],"BE_009t1e"))),
            OldGodRules.Branch("engine/old-god/yogg/madness/v1",1,
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board,SelectorSide.Both,includeHeroes:true)),
                OldGodRules.Power((g,c,s,t,stack) => {
                    if (stack.Playables.LastOrDefault() is ICharacter target)
                        new DamageTask(10,EntityType.TARGET).Process(g,c,s,target);
                },targeted:true)),
            OldGodRules.Branch("engine/old-god/yogg/ascent/v1",2,OldGodRules.Power((g,c,s,t,stack) => {
                var captured = c.HandZone.OfType<Spell>().Select(spell => (Spell:spell,Cost:Math.Max(0,spell.Cost))).ToArray();
                foreach (var entry in captured)
                {
                    if (entry.Spell.Zone != c.HandZone || entry.Cost > int.MaxValue - 3) continue;
                    stack.Number = entry.Cost + 3;
                    var pick = new EffectContext(g,c.Id,stack.Number2,stack:stack)
                        .PickRandomCard(Spells(),PoolCostBinding.StackNumber,DbfId);
                    if (pick == null) continue;
                    // A Shatter parent splits when this scope closes, so its halves inherit the kept Cost.
                    using (c.HandZone.BeginCardStateMutation())
                    {
                        var changed = Generic.ChangeEntityBlock(c,entry.Spell,pick,true);
                        g.AuraUpdate();
                        int adjustment = changed.Cost-entry.Cost;
                        if (!g.History) Enchantment.GetInstance(c,g.IdEntityDic[stack.Number2],changed,Cards.FromId("BE_009t3e"),adjustment);
                        Generic.AddEnchantmentBlock(g,Cards.FromId("BE_009t3e"),g.IdEntityDic[stack.Number2],changed,adjustment);
                    }
                }
            })),
            OldGodRules.Branch("engine/old-god/yogg/domain/v1",3,OldGodRules.Power((g,c,s,t,stack) => {
                for (int i = 0; i < 5 && g.State != State.COMPLETE; i++)
                {
                    var spells = c.DeckZone.OfType<Spell>().ToArray();
                    if (spells.Length == 0) break;
                    Generic.Draw(c,spells[g.Random.Next(spells.Length)]);
                    g.DeathProcessingAndAuraUpdate();
                }
            })));

        private static void Token(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("BE_009t1",132548)] internal static void Brain(Card card) => Token(card);
        [StandardCard("BE_009t2",132549)] internal static void Madness(Card card) => Token(card);
        [StandardCard("BE_009t3",132550)] internal static void Ascent(Card card) => Token(card);
        [StandardCard("BE_009t4",132551)] internal static void Domain(Card card) => Token(card);
        [StandardCard("BE_009e2",134655)] internal static void Controlled(Card card) { Token(card); card.Power = new Power(); }
        [StandardCard("BE_009t1e",134808)] internal static void Brainy(Card card) { Token(card); card.Power = new Power(); }
        [StandardCard("BE_009t3e",133322)] internal static void Reduced(Card card)
        {
            Token(card); card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
    }
}
