using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Opt-in persistent grants; all state lives on ordinary entities and attachments.</summary>
    public static class LifecycleRules
    {
        // Part of HealingRules' filtered range. Survives Silence, resets with the residence.
        internal const GameTag RecruitAppliedPlayer1 = (GameTag)31287;
        internal const GameTag RecruitAppliedPlayer2 = (GameTag)31289;
        public static bool RetainActor(Card card) => card.Id == "BE_143t2e" || card.Id == "MEND_801e2" || card.Id == "CAP_800e";
        public static int RecruitHealth(Controller owner) => 1 + (owner.AppliedEnchantments?.Count(e => e.Card.Id == "MEND_801e") ?? 0);
        internal static bool IsRecruit(IPlayable card) => card.Card.Id == "CS2_101t" || card.Card[(GameTag)3444] != 0;

        public static void AdmitRecruit(IPlayable card)
        {
            if (!(card is Minion) || !IsRecruit(card)) return;
            var grants = card.Controller.AppliedEnchantments?.Where(e => e.Card.Id == "MEND_801e").ToArray();
            if (grants == null || grants.Length == 0) return;
            var appliedTag = card.Controller.PlayerId == 1 ? RecruitAppliedPlayer1 : RecruitAppliedPlayer2;
            int applied = card[appliedTag];
            card[appliedTag] = Math.Max(applied, grants.Length);
            for (int i = applied; i < grants.Length; i++)
                Generic.AddEnchantmentBlock(card.Game, Cards.FromId("MEND_801e2"), grants[i].Creator, card);
        }

        public static void StrengthenRecruits(IPlayable savior)
        {
            var owner = savior.Controller;
            Enchantment.GetInstance(owner, savior, owner, Cards.FromId("MEND_801e"));
            // Pinned SilverHandCardTextBuilder reads controller HP bonus, not script data.
            owner[(GameTag)4806] = RecruitHealth(owner) - 1;
            foreach (var recruit in owner.BoardZone.Cast<IPlayable>().Concat(owner.HandZone).Concat(owner.DeckZone).ToArray())
                AdmitRecruit(recruit);
        }

        internal static void CopyState(IPlayable source, IPlayable target)
        {
            if (source[RecruitAppliedPlayer1] != 0) target[RecruitAppliedPlayer1] = source[RecruitAppliedPlayer1];
            if (source[RecruitAppliedPlayer2] != 0) target[RecruitAppliedPlayer2] = source[RecruitAppliedPlayer2];
            if (source.AppliedEnchantments?.Any(e => e.Card.Id == "BE_143t2e") == true)
                target[GameTag.CASTSWHENDRAWN] = 1;
        }

        internal const GameTag DeathContinuationTarget = (GameTag)31288;

        // Death triggers reserve their subject; execute only after the complete death phase drains.
        internal static bool ContinueDeaths(Game game)
        {
            if (game.Player1.Choice != null || game.Player2.Choice != null || game.Resolution.HasSuspendedEffect) return false;
            // Runs on every death/aura pass: no LINQ chain, and no list unless a continuation is pending.
            List<Spell> pending = null;
            foreach (var s in game.Player1.SecretZone) if (s[DeathContinuationTarget] != 0) (pending ??= new List<Spell>()).Add(s);
            foreach (var s in game.Player2.SecretZone) if (s[DeathContinuationTarget] != 0) (pending ??= new List<Spell>()).Add(s);
            if (pending == null) return false;
            foreach (var secret in pending)
            {
                int targetId = secret[DeathContinuationTarget];
                secret[DeathContinuationTarget] = 0;
                var owner = secret.Controller;
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, secret.Id, "", 0, 0));
                secret[GameTag.REVEALED] = 1;
                owner.GraveyardZone.Add(owner.SecretZone.Remove(secret));
                if (!owner.BoardZone.IsFull && game.IdEntityDic.TryGetValue(targetId, out var target) && target is Minion dead)
                    Generic.Copy(owner, secret, dead, Zone.PLAY, deathrattle: true);
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
            return true;
        }

        public static void ArmFullHealth(IPlayable source)
        {
            var hero = source.Controller.Hero;
            // Remaining Health is a setter, not damage or healing; preserve the maximum.
            if (hero.BaseHealth < 15) hero.BaseHealth = 15;
            hero.Damage = hero.BaseHealth - 15;
            Enchantment.GetInstance(source.Controller, source, hero, Cards.FromId("CATA_307e"));
            hero.ResolveFullHealthGrants();
        }
    }
}
