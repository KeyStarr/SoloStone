using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Watfin
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Discover(CardType.MINION);
        [StandardCard("JAIL_EVENT_100", 129959)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask(StateTaskList.Suspendable("event/watfin/v1",
                new DiscoverThenContinueTask(Offers, DiscoverRealRules.PrepareDiscover), new CustomTask(Chosen))) };
        }
        internal static Card[] Offers(Game g, Controller c, IEntity source, TaskStack stack)
        {
            var offers = OfflineCardPools.SampleDistinct(g, Pool(), c.HeroClass, 129959, sourceClassMask: OfflineCardPools.PrintedClassMask(source.Card));
            if (offers.Length == 0) return offers;
            int suspicious = g.Random.Next(offers.Length);
            var original = offers[suspicious];
            var candidates = OfflineCardPools.Candidates(g,c.HeroClass,Pool(),129959,sourceClassMask:OfflineCardPools.PrintedClassMask(source.Card));
            bool Compatible(Card card, int mutation) => mutation == 0 ? card.Cost > 0
                : mutation == 3 ? SpiritOfTheMountain.Types.Any(r => !card.IsRace(r))
                : mutation != 4 || candidates.Any(other => other.Name != card.Name);
            // Choose the change first; an incompatible candidate must not change its probability.
            var kinds = Enumerable.Range(0,5).Where(k => candidates.Any(card => Compatible(card,k))).ToArray();
            int kind = kinds[g.Random.Next(kinds.Length)];
            if (!Compatible(original,kind)) {
                var compatible = candidates.Where(card => Compatible(card,kind)).ToArray();
                var replacement = compatible[g.Random.Next(compatible.Length)];
                int existing = Array.FindIndex(offers,card => card.Id == replacement.Id);
                if (existing >= 0) offers[existing] = original;
                original = replacement;
            }
            var types = SpiritOfTheMountain.Types.Where(r => !original.IsRace(r)).ToArray();
            var tags = new Dictionary<GameTag,int>(original.Tags);
            tags.Remove(GameTag.COLLECTIBLE);
            tags[GameTag.MODULAR_ENTITY_PART_1] = original.AssetId;
            tags[GameTag.OVERRIDECARDNAME] = original.AssetId;
            string name = original.Name;
            switch (kind) {
                case 0: tags[GameTag.COST] = original.Cost - 1; break;
                case 1: tags[GameTag.ATK] = original.ATK + 1; break;
                case 2: tags[GameTag.HEALTH] = original.Health + 1; break;
                case 3:
                    var race = types[g.Random.Next(types.Length)];
                    if (original.Races.Count == 0) tags[GameTag.CARDRACE] = (int)race;
                    else if (race == Race.TOTEM) {
                        foreach (var existing in original.Races) tags[Card.AdditionalRaceTags.First(x => x.Value == existing).Key] = 1;
                        tags[GameTag.CARDRACE] = (int)Race.TOTEM;
                    }
                    else tags[Card.AdditionalRaceTags.First(x => x.Value == race).Key] = 1;
                    break;
                case 4:
                    var names = candidates.Where(x => x.Name != original.Name).ToArray();
                    var other = names[g.Random.Next(names.Length)]; name = other.Name;
                    tags[GameTag.OVERRIDECARDNAME] = other.AssetId;
                    break;
            }
            var raw = tags.Select(x => new Tag(x.Key,x.Value)).Concat(new[] {
                new Tag(GameTag.CARDNAME,name), new Tag(GameTag.CARDTEXT,original.Text) }).ToArray();
            var custom = new Card("JAIL_EVENT_100t",133446,raw,new Dictionary<PlayReq,int>(original.PlayRequirements),
                original.Entourage.ToArray(),original.RefTags.Select(x=>new Tag(x.Key,x.Value)).ToArray(),preserveText:true);
            custom.Implemented = true; custom.ExcludeFromLegacyRandomPools = true; custom.UseBehaviourFrom(original);
            offers[suspicious] = custom;
            return offers;
        }
        internal static void RedactForFairView(IPlayable actor)
        {
            var custom = actor.Card;
            bool suspicious = custom.Id == "JAIL_EVENT_100t" && custom[GameTag.MODULAR_ENTITY_PART_1] != 0;
            if (!suspicious && !(actor.Game.IdEntityDic.TryGetValue(actor[GameTag.CREATOR],out var source)
                && source.Card.Id == "JAIL_EVENT_100")) return;
            var original = suspicious ? Cards.FromAssetId(custom[GameTag.MODULAR_ENTITY_PART_1]) : Cards.FromId(custom.Id);
            // Project every offer from its visible definition; shallow clones expose nested metadata identity.
            var tags = new Dictionary<GameTag,int>(custom.Tags);
            tags.Remove(GameTag.MODULAR_ENTITY_PART_1);
            if (original[GameTag.COLLECTIBLE] != 0) tags[GameTag.COLLECTIBLE] = original[GameTag.COLLECTIBLE];
            if (custom.Name == original.Name) tags.Remove(GameTag.OVERRIDECARDNAME);
            actor.Card = original.WithVisibleBase(custom,tags);
            actor.NativeTags.Remove(GameTag.MODULAR_ENTITY_PART_1);
        }
        private static void Chosen(Game g, Controller c, IEntity source, IPlayable target, TaskStack stack)
        {
            var chosen = stack.Playables?.LastOrDefault();
            if (chosen == null) return;
            if (chosen.Card.Id == "JAIL_EVENT_100t" && source is Minion watfin && watfin.Card.Id == "JAIL_EVENT_100"
                && watfin.Zone == c.BoardZone && watfin.Controller == c && !watfin.ToBeDestroyed)
                Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_EVENT_100e"),watfin,watfin);
            Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
        }
        [StandardCard("JAIL_EVENT_100e", 132769)] internal static void Buff(Card c) {
            c.Implemented = true; c.ExcludeFromLegacyRandomPools = true;
            c.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
        [StandardCard("JAIL_EVENT_100t", 133446)] internal static void Token(Card c) {
            c.Implemented = true; c.ExcludeFromLegacyRandomPools = true;
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class WatfinPools
    {
        [OfflinePool("JAIL_EVENT_100",129959)] internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("event-watfin-minions",Sets.Watfin.Pool(),new[] { "NEUTRAL" })
        };
    }
}
