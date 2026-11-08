using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DragonSoulShattered
    {
        internal const GameTag ChainRoot = (GameTag)32601, ChainOrder = (GameTag)32602;
        internal static bool Essence(Card card) => card != null && card.Id.Length == 16 && card.Id.StartsWith("CATA_EVENT_110t")
            && card.Id[15] >= '2' && card.Id[15] <= '7';
        [StandardCard("CATA_EVENT_110", 123345)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            StartOfGameRules.Declare(card, Break);
        }
        private static void Break(IPlayable source)
        {
            var c = source.Controller;
            if (source.Zone != c.DeckZone && source.Zone != c.HandZone) return;
            source[ShatterRules.PrivateHandOwner] = c.PlayerId;
            CardStateBoundary.EnrollAttachments(source);
            c.SetasideZone.Add(source.Zone.Remove(source));
            source[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
            if (c.Game.History) c.Game.PowerHistory.Add(new PowerHistoryHideEntity { EntityID = source.Id, Zone = Zone.REMOVEDFROMGAME });
            for (int i = 2; i <= 7 && !c.DeckZone.IsFull; i++)
            {
                var essence = Entity.FromCard(c, Cards.FromId("CATA_EVENT_110t" + i), new EntityData {
                    [GameTag.ZONE] = (int)Zone.DECK, [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id });
                Generic.ShuffleIntoDeck(c, source, essence);
            }
        }
        // Capture before hand compaction; tags reserve identity/order without retaining live closures.
        internal static void Capture(IPlayable source)
        {
            var c = source.Controller;
            if (!Essence(source.Card) || source.Zone != c.HandZone) return;
            var hand = c.HandZone.ToArray(); int index = Array.IndexOf(hand, source), left = index, right = index;
            while (left > 0 && Essence(hand[left - 1].Card)) left--;
            while (right + 1 < hand.Length && Essence(hand[right + 1].Card)) right++;
            for (int i = left; i <= right; i++) if (i != index) {
                hand[i][ChainRoot] = source.Id; hand[i][ChainOrder] = i + 1;
            }
        }
        internal static void Reserve(Spell root)
        {
            if (!Essence(root.Card)) return;
            var c = root.Controller;
            foreach (var spell in c.HandZone.Where(e => e[ChainRoot] == root.Id).ToArray())
                c.SetasideZone.Add(c.HandZone.Remove(spell));
        }
        internal static void Finish(Spell root)
        {
            if (!Essence(root.Card)) return;
            var c = root.Controller; var g = c.Game;
            var run = g.IdEntityDic.Values.OfType<Spell>().Where(e => e[ChainRoot] == root.Id)
                .OrderBy(e => e[ChainOrder]).ToArray();
            foreach (var spell in run) { spell.NativeTags.Remove(ChainRoot); spell.NativeTags.Remove(ChainOrder); }
            if (root.IsCountered) return;
            if (run.Length == 0) return;
            g.DeathProcessingAndAuraUpdate();
            if (g.State == State.COMPLETE) return;
            foreach (var spell in run.Where(e => e.Zone == c.SetasideZone && e.Controller == c)) {
                spell[GameTag.ZONE] = (int)Zone.PLAY;
                if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(spell));
                ICharacter target = null;
                if (spell.Card.MustHaveTargetToPlay) target = ((Playable)spell).GetRandomValidTarget();
                Generic.CastSpell(c, g, spell, target, 0);
                g.DeathProcessingAndAuraUpdate();
                if (g.State == State.COMPLETE) break;
            }
        }
        private static void ConfigureEssence(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                switch (card.Id) {
                    case "CATA_EVENT_110t2": if (ctx.TargetId.HasValue) ctx.Damage(ctx.TargetId.Value, 8, true); break;
                    case "CATA_EVENT_110t3": for (int i = 0; i < 3; i++) ctx.DrawWhere(p => p.Card.Type == CardType.SPELL); break;
                    case "CATA_EVENT_110t4": ctx.RefreshMana(8); break;
                    case "CATA_EVENT_110t5": return ctx.RunLegacy(new ArmorTask(12));
                    case "CATA_EVENT_110t6": ctx.Summon("CATA_EVENT_110t6t"); break;
                    case "CATA_EVENT_110t7": return ctx.RunLegacy(new DamageTask(4, EntityType.OP_MINIONS, true));
                }
                return TaskState.COMPLETE;
            }) };
        }
        [StandardCard("CATA_EVENT_110t2", 123339)] internal static void Red(Card c) {
            c.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> {
                [PlayReq.REQ_TARGET_TO_PLAY] = 0, [PlayReq.REQ_ENEMY_TARGET] = 0 }); ConfigureEssence(c);
        }
        [StandardCard("CATA_EVENT_110t3", 123340)] internal static void Blue(Card c) => ConfigureEssence(c);
        [StandardCard("CATA_EVENT_110t4", 123341)] internal static void Bronze(Card c) => ConfigureEssence(c);
        [StandardCard("CATA_EVENT_110t5", 123342)] internal static void Black(Card c) => ConfigureEssence(c);
        [StandardCard("CATA_EVENT_110t6", 123343)] internal static void Green(Card c) => ConfigureEssence(c);
        [StandardCard("CATA_EVENT_110t7", 123349)] internal static void Storm(Card c) => ConfigureEssence(c);
        [StandardCard("CATA_EVENT_110t6t", 132017)] internal static void Dragon(Card c) { c.Implemented = true; c.ExcludeFromLegacyRandomPools = true; }
    }
}
