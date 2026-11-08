using System;
using System.Linq;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class OldGodRules
    {
        private const GameTag Used = (GameTag)32700, FreeTurn = (GameTag)32701, DeathrattleSummonDelta = (GameTag)32702;
        public static bool IsInternalTag(GameTag tag) => tag == Used || tag == FreeTurn || tag == DeathrattleSummonDelta;
        // Keep historical definitions immutable; only live/printed differences need additional saved state.
        internal static void RecordSummon(Minion minion)
        {
            int delta = (minion.HasDeathrattle ? 1 : 0) - (minion.Card.Deathrattle ? 1 : 0);
            if (delta != 0) minion.Controller[DeathrattleSummonDelta] += delta;
        }
        internal static int DeathrattleSummons(Controller owner) => owner[DeathrattleSummonDelta] +
            owner.Game.HistoryFacts.Query.Summons().Count(f => f.ControllerId == owner.Id && f.Definition.ReadDefinition().Deathrattle);
        public static OfflinePoolFilter Pool => OfflinePoolFilter.Random(type: CardType.MINION, keyword: (GameTag)4754);
        public static string[] Catalog(string id) => id switch {
            "BE_009" => new[] { "BE_009t1", "BE_009t2", "BE_009t3", "BE_009t4" },
            "BE_013" => new[] { "BE_013t1", "BE_013t2", "BE_013t6", "BE_013t4" },
            "BE_017" => new[] { "BE_017t3", "BE_017t5", "BE_017t6", "BE_017t4" },
            "BE_784" => new[] { "BE_784t1", "BE_784t3", "BE_784t2", "BE_784t4" },
            _ => Array.Empty<string>()
        };
        private static string[] Available(Minion source) => Catalog(source.Card.Id).Where((id,i) =>
            (source[Used] & (1 << i)) == 0 && (source.Card.Id != "BE_017" || i < 2 ||
                source.Controller.BoardZone.Any(m => m.Id != source.Id && !m.IsDead && !m.Untouchable
                    && (i == 2 || m.HasDeathrattle)))).ToArray();
        public static int UsedAbilities(Minion source) => source[Used];
        public static bool HasAbilities(Minion source) => !source.IsSilenced && Catalog(source.Card.Id).Length != 0;
        public static bool BlocksAttack(Minion source) => HasAbilities(source) && source[Used] != 15;
        public static bool CanActivate(Controller owner, Minion source) => owner != null && source != null
            && source.Game == owner.Game && owner.Game.CurrentPlayer == owner && owner.Game.State == State.RUNNING
            && owner.Game.Step == Step.MAIN_ACTION && owner.Game.Player1.Choice == null && owner.Game.Player2.Choice == null
            && !owner.Game.Resolution.HasSuspendedEffect && source.Controller == owner && source.Zone == owner.BoardZone
            && owner.Game.IdEntityDic.TryGetValue(source.Id,out var actual) && ReferenceEquals(source,actual)
            && HasAbilities(source) && Available(source).Length != 0 && !source.CantAttack && !source.IsDead && !source.Untouchable && !source.IsFrozen
            && source.NumAttacksThisTurn < CombatRules.AttackLimit(source);

        public static bool Activate(Controller owner, Minion source)
        {
            if (!CanActivate(owner,source)) return false;
            owner.Game.Facts.AcceptPlayerAction(source);
            owner.NumOptionsPlayedThisTurn++;
            var recipe = source.Card.Id switch {
                "BE_013" => CthunRules.OnBoardRecipe,
                "BE_017" => NzothDreamsEnd.Abilities,
                "BE_784" => YshaarjTitanBane.Abilities,
                "BE_009" => MastermindYoggSaron.Abilities,
                _ => throw new InvalidOperationException("Unknown Old God.")
            };
            recipe.Process(owner.Game,owner,source,source,new TaskStack());
            return true;
        }

        internal static SuspendableTask Recipe(string key, params ISimpleTask[] tasks) => StateTaskList.Suspendable(key,
            new ISimpleTask[] {
                new DiscoverThenContinueTask((g,c,s,stack) => Available((Minion)s).Select(Cards.FromId).ToArray()),
                new CustomTask((g,c,s,t,stack) => {
                    var body = (Minion)s; var ability = stack.Playables.Last();
                    stack.Number1 = Array.IndexOf(Catalog(s.Card.Id),ability.Card.Id);
                    stack.Number2 = ability.Id;
                    stack.Number3 = s.Card.Id == "BE_013" && CthunRules.Progress(c) == 15 ? 3 : 1;
                    s[Used] |= 1 << stack.Number1;
                    if (s[FreeTurn] == g.Turn + 1) body.NumAttacksThisTurn++;
                    else s[FreeTurn] = g.Turn + 1;
                    body.IsExhausted = body.NumAttacksThisTurn >= CombatRules.AttackLimit(body);
                    c.SetasideZone.Remove(ability); ability[GameTag.ZONE] = (int)Zone.PLAY;
                    if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(ability));
                    stack.Playables.Clear();
                })
            }.Concat(tasks).Concat(new ISimpleTask[] { new CustomTask((g,c,s,t,stack) =>
                g.IdEntityDic[stack.Number2][GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME) }).ToArray());

        internal static SuspendableTask Branch(string key, int index, params ISimpleTask[] tasks) => StateTaskList.Suspendable(key,
            new ISimpleTask[] { new CustomTask((g,c,s,t,stack) => stack.Number = stack.Number1),
                new NumberConditionTask(index,RelaSign.EQ),
                new FlagTask(false, SabberStoneCore.EffectSystem.EffectContext.Task(_ => TaskState.STOP)) }.Concat(tasks).ToArray());

        internal static CustomTask Power(Action<Game,Controller,IEntity,IPlayable,TaskStack> effect, bool targeted = false) => new CustomTask((g,c,s,t,stack) => {
            var ability = g.IdEntityDic[stack.Number2];
            var target = targeted ? stack.Playables.LastOrDefault() : null;
            if (g.History) {
                g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER,ability.Id,"",-1,target?.Id ?? 0));
                if (target != null) g.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET,
                    Info = new System.Collections.Generic.List<int> { target.Id } });
            }
            try { effect(g,c,s,t,stack); g.DeathProcessingAndAuraUpdate(); }
            finally { if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd()); }
        });

        internal static void Reset(IPlayable body) { body.NativeTags.Remove(Used); body.NativeTags.Remove(FreeTurn); }
        internal static void CopyState(IPlayable source, IPlayable copy) {
            if (Catalog(source.Card.Id).Length == 0) return;
            copy[Used] = source[Used]; copy.NativeTags.Remove(FreeTurn);
            if (copy is Minion body) body.NumAttacksThisTurn = 0;
        }

        internal static void FinishCopy(IPlayable copy) {
            if (copy is Minion body && body.Zone == body.Controller.BoardZone && HasAbilities(body)
                && body[Used] == 15 && body.Controller == body.Game.CurrentPlayer) body.IsExhausted = false;
        }

    }
}
