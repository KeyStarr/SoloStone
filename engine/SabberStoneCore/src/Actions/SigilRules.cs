using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>A Sigil waits in its owner's SECRET zone until its start-of-turn effect, as objectives do. The client shows it
    /// there (ZoneSecret and EntityBase.IsSigil), and a TRIGGER block keyed SIGIL is what PowerProcessor hands to
    /// SigilSpellController, which marks the waiting Sigil as triggered and plays its banner. Like an objective it waits in the
    /// zone's own list (SecretZone.MoveTo), so it takes no Secret slot and no Secret count sees it.</summary>
    public static class SigilRules
    {
        public const GameTag Sigil = (GameTag)1749;

        public static bool IsSigil(IPlayable card) => card is Spell && card.Card[Sigil] == 1;

        public static bool IsActive(IPlayable card) => IsSigil(card) && card.Zone == card.Controller.SecretZone;

        /// <summary>In place of the graveyard once the Sigil resolves, while its trigger is still pending. A spell can be
        /// retired twice (a decision spell's completion follows its cast); the second call leaves a waiting Sigil where it
        /// is, since leaving a zone drops the trigger.</summary>
        internal static bool Install(Controller owner, Spell spell)
        {
            if (IsActive(spell)) return true;
            if (!IsSigil(spell) || spell.ActivatedTrigger == null || spell.Zone != null) return false;
            owner.SecretZone.Add(spell);
            return true;
        }

        /// <summary>Runs at the end of the Sigil's own trigger block, so the Sigil leaves SECRET inside it.</summary>
        internal static readonly ISimpleTask Retire = new CustomTask((g, c, s, t, k) => {
            if (!(s is Spell spell) || !IsActive(spell)) return;
            spell.Controller.SecretZone.Remove(spell);
            spell[GameTag.ZONE_POSITION] = 0;
            spell.Controller.GraveyardZone.Add(spell);
        });

        /// <summary>The Sigil's effect, then Retire whatever the effect returned. A task chain stops at the first task that
        /// doesn't complete, and a summon onto a full board stops, so a chained Retire would leave the Sigil in SECRET.
        /// No Sigil's effect suspends (Discover, choices); one that did would need to retire after it resumes.</summary>
        internal static ISimpleTask FireThenRetire(ISimpleTask effect) => new FireThenRetireTask(effect);

        private sealed class FireThenRetireTask : SimpleTask
        {
            private readonly ISimpleTask _effect;

            internal FireThenRetireTask(ISimpleTask effect) => _effect = effect;

            public override TaskState Process(in Game game, in Controller controller, in IEntity source, in IPlayable target,
                in TaskStack stack = null)
            {
                TaskStack shared = stack ?? new TaskStack();
                _effect.Process(in game, in controller, in source, in target, in shared);
                Retire.Process(in game, in controller, in source, in target, in shared);
                return TaskState.COMPLETE;
            }
        }
    }
}
