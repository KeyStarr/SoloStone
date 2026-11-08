using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class AfterAttackRules
    {
        private const int Pending = 17017;
        private static readonly Dictionary<int, SuspendableTask> Recipes = new Dictionary<int, SuspendableTask>();

        // Registered definitions only; every activation's mutable data belongs to a private payload.
        public static ISimpleTask Register(int dbfId, params ISimpleTask[] steps)
        {
            Recipes.Add(dbfId, StateTaskList.Suspendable("engine/after-attack/" + dbfId + "/v1", steps));
            return new CustomTask((g, c, s, t, stack) => {
                var entry = PayloadRules.StoreCard(c.Hero, Pending, Cards.FromAssetId(dbfId), 0);
                entry[PayloadRules.Link] = s.Id;
                entry[PayloadRules.Value] = Math.Max(0, c.Hero.AttackDamage);
                entry[PayloadRules.Recipient] = g.CurrentEventData?.EventTarget?.Id ?? 0;
            });
        }

        private static IPlayable[] PendingEntries(Game game) => PayloadRules.Entries(game.Player1.Hero, Pending)
            .Concat(PayloadRules.Entries(game.Player2.Hero, Pending)).OrderBy(e => e.Id).ToArray();

        internal static EffectFrame TakeNext(Game game)
        {
            foreach (var entry in PendingEntries(game))
            {
                PayloadRules.Remove(entry);
                if (game.State == State.COMPLETE) continue;
                var source = game.IdEntityDic[entry[PayloadRules.Link]];
                return new EffectFrame(Recipes[entry.Card.AssetId].RecipeKey, entry.Controller, source, null,
                    new TaskStack { Number = entry[PayloadRules.Value], Number1 = entry[PayloadRules.Recipient] });
            }
            return null;
        }

        internal static bool IsRecipe(string key) => Recipes.Values.Any(recipe => recipe.RecipeKey == key);
        internal static bool IsContinuation(EffectFrame parent, EffectFrame child) =>
            IsRecipe(child.RecipeKey) && EffectFrameRecipes.Resolve(parent.RecipeKey)[parent.NextStep - 1] is AttackTask;

        internal static void Flush(Game game)
        {
            if (game.Resolution.CurrentEffect != null || game.Player1.Choice != null || game.Player2.Choice != null) return;
            EffectFrame frame;
            while ((frame = TakeNext(game)) != null)
            {
                var c = game.ControllerById(frame.ControllerId);
                if (c[Generic.AutomaticDecisionTag] != 0)
                    game.Resolution.RunAutomaticEffect(game, frame.RecipeKey, c, game.IdEntityDic[frame.SourceId], null, frame.Restore(game));
                else game.Resolution.RunEffect(game, frame.RecipeKey, c, game.IdEntityDic[frame.SourceId], null, frame.Restore(game));
                if (game.Resolution.HasSuspendedEffect) return;
            }
        }

        internal static void Cancel(Game game)
        {
            foreach (var entry in PendingEntries(game)) PayloadRules.Remove(entry);
        }
    }
}
