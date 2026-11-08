using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FyrakkTheBlazing
    {
        internal const int DbfId=115756;
        [StandardCard("FIR_959",DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                int remaining=15;
                // Unpinned_Fyrakk: zero rolls are allowed, but a zero-only remainder terminates.
                while (remaining>0 && g.State!=State.COMPLETE)
                {
                    var filter=CombatEmeraldDreamPools.FireBudget().WithMaxCost(remaining);
                    var candidates=OfflineCardPools.Candidates(g,c.HeroClass,filter,DbfId, PoolDestination.Other);
                    if (!candidates.Any(candidate=>candidate.Cost>0)) break;
                    var chosen=OfflineCardPools.Pick(g,filter,c.HeroClass,DbfId, PoolDestination.Other);
                    g.OnRandomHappened(true);
                    remaining-=chosen.Cost;
                    var spell=(Spell)Entity.FromCard(c,chosen,new EntityData {
                        [GameTag.CREATOR]=s.Id,[GameTag.DISPLAYED_CREATOR]=s.Id });
                    int mode=spell.ChooseOne && !c.ChooseBoth ? g.Random.Next(1,spell.ChooseOnePlayables.Length+1) : 0;
                    var profile=Generic.SpellTargetingProfile(spell,mode);
                    var legal=profile.ValidPlayTargets.Where(profile.IsValidPlayTarget).ToArray();
                    var enemies=legal.Where(e=>e.Controller!=c).ToArray();
                    var targets=enemies.Length>0 ? enemies : legal;
                    ICharacter defender=targets.Length==0 ? null : targets[g.Random.Next(targets.Length)];
                    if (defender!=null) spell.CardTarget=defender.Id;
                    // An unlocked automatic cast preserves required-target fizzle semantics.
                    Generic.CastAutomaticSpell(c,g,spell,defender,mode);
                    while (c.Choice!=null) Generic.ChoicePick(c,g,c.Choice.Choices[g.Random.Next(c.Choice.Choices.Count)]);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
