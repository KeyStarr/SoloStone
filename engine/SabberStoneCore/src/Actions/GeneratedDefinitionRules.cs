using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    /// <summary>Immutable compositions of installed recipes; no match state or generated-ID registry.</summary>
    public static class GeneratedDefinitionRules
    {
        public const GameTag TextBuilder = (GameTag)782;
        private const string RecipePrefix = "generated/creation/v1/";
        private const string ComponentPrefix = "generated/component/v1/";
        private static readonly GameTag[] Keywords = {
            GameTag.TAUNT, GameTag.CHARGE, GameTag.RUSH, GameTag.STEALTH, GameTag.DIVINE_SHIELD,
            GameTag.WINDFURY, GameTag.LIFESTEAL, GameTag.POISONOUS, GameTag.REBORN,
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
            GameTag.BATTLECRY, GameTag.DEATHRATTLE, GameTag.COMBO, GameTag.FREEZE,
            GameTag.CANT_ATTACK, GameTag.OVERLOAD, GameTag.ECHO, GameTag.REWIND, GameTag.TRADEABLE, GameTag.OUTCAST,
            (GameTag)1824, (GameTag)4548, (GameTag)32, (GameTag)3744, (GameTag)4028,
            DeathContextRules.CaptureDeath, DeathContextRules.PositionAnchor
        };

        public static Card Creation(Card dragon, Card undead)
        {
            if (dragon == null || undead == null || dragon.Type != CardType.MINION || undead.Type != CardType.MINION)
                throw new ArgumentException("Creation requires two minion definitions.");
            var template = Cards.FromId("CATA_470t1");
            var tags = new Dictionary<GameTag, int>(template.Tags);
            tags[GameTag.COST] = checked(dragon.Cost + undead.Cost);
            tags[GameTag.ATK] = checked(dragon.ATK + undead.ATK);
            tags[GameTag.HEALTH] = checked(dragon.Health + undead.Health);
            foreach (var tag in Keywords)
                if (Math.Max(dragon[tag], undead[tag]) is int value && value != 0) tags[tag] = value;
                else tags.Remove(tag); // Card's keyword parser tests presence, not value.
            tags[GameTag.SPELLPOWER] = checked(dragon.SpellPower + undead.SpellPower);
            if (dragon.IsRace(Race.ALL) || undead.IsRace(Race.ALL))
            {
                tags[GameTag.CARDRACE] = (int)Race.ALL;
                foreach (var tag in Card.AdditionalRaceTags.Keys) tags.Remove(tag);
            }
            tags[GameTag.MODULAR_ENTITY_PART_1] = dragon.AssetId;
            tags[GameTag.MODULAR_ENTITY_PART_2] = undead.AssetId;
            tags[TextBuilder] = 14; // Pinned Zombeast builder merges complete component text.
            var requirements = new Dictionary<PlayReq, int>();
            if (dragon.TargetingType != TargetingType.None || undead.TargetingType != TargetingType.None)
                requirements[dragon.MustHaveTargetToPlay || undead.MustHaveTargetToPlay
                    ? PlayReq.REQ_TARGET_TO_PLAY : PlayReq.REQ_TARGET_IF_AVAILABLE] = 0;
            var result = Build(template, tags, dragon.Text + "\n" + undead.Text, requirements);
            result.UseGeneratedTargeting();
            result.Entourage = dragon.Entourage.Concat(undead.Entourage).Distinct().ToArray();
            var first = dragon.Power ?? new Power(); var second = undead.Power ?? new Power();
            var triggers = new[] { first.Trigger, second.Trigger }.Where(t => t != null).ToArray();
            var auras = new[] { first.Aura, second.Aura }.Where(a => a != null).ToArray();
            result.Power = new Power {
                PowerTask = first.PowerTask == null && second.PowerTask == null && !dragon.ChooseOne && !undead.ChooseOne ? null :
                    new DecisionBattlecryTask(SuspendableTask.FromDefinitionKey(RecipePrefix + dragon.Id + "/" + undead.Id)),
                DeathrattleTask = Chain(first.DeathrattleTask, second.DeathrattleTask),
                ComboTask = Chain(first.ComboTask, second.ComboTask),
                TopdeckTask = Chain(first.TopdeckTask, second.TopdeckTask),
                HandEntryTask = Chain(first.HandEntryTask, second.HandEntryTask),
                OverkillTask = Chain(first.OverkillTask, second.OverkillTask),
                Trigger = triggers.Length == 0 ? null : new MultiTrigger(triggers),
                Aura = auras.Length == 0 ? null : auras.Length == 1 ? auras[0] : new MultiAura(auras),
                RetainInHeldForm = first.RetainInHeldForm || second.RetainInHeldForm
            };
            return result;
        }

        internal static IEnumerable<Card> Parts(Card card) => new[] { GameTag.MODULAR_ENTITY_PART_1, GameTag.MODULAR_ENTITY_PART_2 }
            .Where(t => card[t] != 0).Select(t => Cards.FromAssetId(card[t]));
        internal static bool TargetAvailable(Controller c, Card card) => Parts(card)
            .Any(part => part.TargetingType != TargetingType.None && (part.TargetingAvailabilityPredicate?.Invoke(c, part) ?? true));
        internal static bool PlayAvailable(Controller c, Card card) => Parts(card).All(part => part.IsPlayableByCardReq(c));
        internal static bool ValidTarget(IPlayable source, ICharacter target) => Parts(source.Card)
            .Where(part => part.TargetingType != TargetingType.None && (part.TargetingAvailabilityPredicate?.Invoke(source.Controller, part) ?? true))
            .Any(part => ComponentTarget(part, source, target) != null);

        private static IPlayable ComponentTarget(Card part, IPlayable source, IPlayable target) =>
            part.TargetingType == TargetingType.None ? target : target is ICharacter character
                && part.GetValidPlayTargets(source.Controller).Contains(character)
                && (part.SourceTargetingPredicate?.Invoke(source, character) ?? true) ? target : null;

        internal static IPlayable RecipeTarget(string key, IEntity source, IPlayable target) =>
            key.StartsWith(ComponentPrefix, StringComparison.Ordinal)
                ? ComponentTarget(Cards.FromId(key.Substring(ComponentPrefix.Length)), (IPlayable)source, target) : target;

        internal static void CaptureTags(IPlayable source, Dictionary<GameTag, int> tags)
        {
            // Retail zilliax_deluxe_3000_friendly.195635.hsreplay.xml:11741-11763,11808-11828 freezes parts in SHOW/FULL.
            foreach (var tag in new[] { GameTag.MODULAR_ENTITY_PART_1, GameTag.MODULAR_ENTITY_PART_2,
                AssembleRules.FirstHalf, AssembleRules.SecondHalf })
                if (source.Card[tag] != 0 && !tags.ContainsKey(tag)) tags[tag] = source[tag];
            bool watfin = source.Game.IdEntityDic.TryGetValue(source[GameTag.CREATOR],out var creator) && creator.Card.Id == "JAIL_EVENT_100";
            if (!watfin && source.Card.Id != "CATA_470t1" && source.Card.Id != "JAIL_504t3p" && source.Card.Id != "ICC_828t" && source.Card.Id != "JAIL_EVENT_100t") return;
            if (!watfin && source.Card[GameTag.MODULAR_ENTITY_PART_1] == 0) return;
            // Historical snapshots cannot consult the later live object or the uncomposed registry token.
            foreach (var tag in source.Card.Tags.Keys)
                if (!tags.ContainsKey(tag)) tags[tag] = source[tag];
        }

        private static IEnumerable<IAura> AuraParts(IAura aura) => aura is MultiAura multi
            ? multi.SelectMany(AuraParts) : new[] { aura };
        private static bool CompositeMinion(Card card) => card != null && (card.Id == "CATA_470t1" || card.Id == "ICC_828t" && card[GameTag.MODULAR_ENTITY_PART_2] != 0);
        internal static void ActivateHandAuras(IPlayable source)
        {
            if (CompositeMinion(source.Card) && source.Power?.Aura is MultiAura multi
                && AuraParts(multi).Any(a => a is AdaptiveCostEffect)) ((IAura)multi).Activate(source);
        }
        internal static void ActivateAuras(MultiAura definition, IPlayable source, bool cloning)
        {
            var live = new List<IAura>();
            source.OngoingEffect = null;
            foreach (var aura in AuraParts(definition).Where(a => (a is AdaptiveCostEffect) == (source.Zone?.Type == Zone.HAND)))
            {
                if (cloning) aura.Clone(source); else aura.Activate(source);
                var activated = source.OngoingEffect;
                if (activated == null) continue;
                live.Add(activated); source.Game.Auras.Remove(activated); source.OngoingEffect = null;
            }
            if (live.Count == 0) return;
            var combined = new MultiAura(live.ToArray()) { Owner = source };
            source.Game.Auras.Add(combined); source.OngoingEffect = combined;
        }
        internal static int ApplyAdaptive(AdaptiveCostEffect fallback, int cost)
        {
            if (!CompositeMinion(fallback?.Owner.Card) || !(fallback.Owner.OngoingEffect is MultiAura multi))
                return fallback?.Apply(cost) ?? cost;
            foreach (var aura in AuraParts(multi).OfType<AdaptiveCostEffect>()) cost = aura.Apply(cost);
            return cost;
        }

        // Rebuild from installed identities on a fresh process, including an open composed Battlecry.
        internal static ISimpleTask[] ResolveRecipe(string key)
        {
            if (key != null && key.StartsWith(ComponentPrefix, StringComparison.Ordinal))
            {
                var task = Cards.FromId(key.Substring(ComponentPrefix.Length)).Power.PowerTask;
                return new[] { task is DecisionBattlecryTask decision ? decision.Recipe : task };
            }
            if (key == null || !key.StartsWith(RecipePrefix, StringComparison.Ordinal)) return null;
            var ids = key.Substring(RecipePrefix.Length).Split('/');
            if (ids.Length != 2) throw new NotSupportedException("Invalid Creation recipe.");
            var steps = new List<ISimpleTask>();
            foreach (var component in ids.Select(Cards.FromId))
            {
                steps.Add(new CustomTask(ResetStack));
                if (component.ChooseOne)
                {
                    steps.Add(new MathAddTask(component.AssetId));
                    steps.Add(new DiscoverThenContinueTask(ChooseOneOffers));
                    steps.Add(new CustomTask(RunChosen));
                }
                else if (component.Power?.PowerTask != null)
                    steps.Add(SuspendableTask.FromDefinitionKey(ComponentPrefix + component.Id));
            }
            return steps.ToArray();
        }

        private static void ResetStack(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            stack.Number = stack.Number1 = stack.Number2 = stack.Number3 = stack.Number4 = 0;
            stack.Flag = false; stack.Playables = new List<IPlayable>();
        }
        private static Card[] ChooseOneOffers(Game g, Controller c, IEntity s, TaskStack stack) =>
            c[GameTag.CHOOSE_BOTH] != 0 ? Array.Empty<Card>() : ChooseOneOptions.Resolve(Cards.FromAssetId(stack.Number));
        private static void RunChosen(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            var options = c[GameTag.CHOOSE_BOTH] != 0 ? ChooseOneOptions.Resolve(Cards.FromAssetId(stack.Number))
                : stack.Playables.Select(p => p.Card).ToArray();
            foreach (var option in options)
                option.Power?.PowerTask?.Process(g, c, s, ComponentTarget(option, (IPlayable)s, t), new TaskStack());
        }

        public static Card Potion(Card first, Card second)
        {
            if (first == null || second == null || first.Id == second.Id ||
                !CardSets.Client.Sets.AyaLotusKingpin.Ingredients.Contains(first.Id) ||
                !CardSets.Client.Sets.AyaLotusKingpin.Ingredients.Contains(second.Id))
                throw new ArgumentException("A lesser potion requires two distinct audited ingredients.");
            var template = Cards.FromId("JAIL_504t3p");
            var tags = new Dictionary<GameTag, int>(template.Tags) {
                [GameTag.MODULAR_ENTITY_PART_1] = first.AssetId,
                [GameTag.MODULAR_ENTITY_PART_2] = second.AssetId,
                [TextBuilder] = 3,
                [GameTag.AFFECTED_BY_SPELL_POWER] = Math.Max(first[GameTag.AFFECTED_BY_SPELL_POWER], second[GameTag.AFFECTED_BY_SPELL_POWER])
            };
            var requirements = new Dictionary<PlayReq, int>();
            if (first.Id == "CFM_621t2" || second.Id == "CFM_621t2") requirements[PlayReq.REQ_TARGET_TO_PLAY] = 0;
            var result = Build(template, tags, first.Text + "\n" + second.Text, requirements);
            result.Power = new Power { PowerTask = Chain(first.Power.PowerTask, second.Power.PowerTask) };
            return result;
        }

        private static ISimpleTask Chain(ISimpleTask first, ISimpleTask second) => first == null ? second :
            second == null ? first : StateTaskList.Chain(first, second);

        private static Card Build(Card template, Dictionary<GameTag, int> tags, string text, Dictionary<PlayReq, int> requirements)
        {
            var raw = tags.Select(p => new Tag(p.Key, p.Value)).Concat(new[] {
                new Tag(GameTag.CARDNAME, template.Name), new Tag(GameTag.CARDTEXT, text)
            }).ToArray();
            var result = new Card(template.Id, template.AssetId, raw, requirements,
                template.Entourage.ToArray(), template.RefTags.Select(p => new Tag(p.Key, p.Value)).ToArray(), preserveText: true);
            result.Implemented = true; result.ExcludeFromLegacyRandomPools = true;
            return result;
        }
    }
}
