using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class CthunRules
    {
        private const GameTag Ledger = (GameTag)31160;
        private const GameTag Discount = (GameTag)31161;
        internal const GameTag PendingAttackSource = (GameTag)31162;
        internal const string AttackRecipeKey = "engine/cthun/after-attack/v1";
        internal static readonly SuspendableTask AttackRecipe = ExternalAbility(AttackRecipeKey);
        private static readonly string[] Catalog = { "BE_013t1", "BE_013t2", "BE_013t3", "BE_013t4" };
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31160 && (int)tag <= 31162;
        public static int Progress(Controller controller) => controller[Ledger];
        public static int CostReduction(IPlayable card) => card.Card.Id == "BE_013" ? card.Controller[Discount] : 0;

        public static SuspendableTask ExternalAbility(string key) => Ability(key,
            new[] { "BE_013t1", "BE_013t2", "BE_013t3", "BE_013t4" });

        public static ISimpleTask[] TwoAbilities(string key) => new ISimpleTask[] {
            ExternalAbility(key + "/first"),
            new CustomTask((g,c,s,t,stack) => stack.Number3 = stack.Number1),
            Second(key, 0), Second(key, 1), Second(key, 2), Second(key, 3)
        };

        private static SuspendableTask Second(string key, int first) => StateTaskList.Suspendable(key + "/second/" + first,
            new CustomTask((g,c,s,t,stack) => stack.Number = stack.Number3),
            new NumberConditionTask(first, RelaSign.EQ),
            new FlagTask(false, EffectContext.Task(_ => TaskState.STOP)),
            Ability(key + "/remaining/" + first, Catalog.Where((_, i) => i != first).ToArray()));

        private static SuspendableTask Ability(string key, string[] offers) => StateTaskList.Suspendable(key,
            new DiscoverThenContinueTask(offers[0], offers.Skip(1).ToArray()),
            new CustomTask((g,c,s,t,stack) => {
                var ability = stack.Playables.Last();
                c.SetasideZone.Remove(ability);
                ability[GameTag.ZONE] = (int)Zone.PLAY;
                if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(ability));
                stack.Number1 = Array.IndexOf(Catalog, ability.Card.Id);
                stack.Number2 = ability.Id;
                stack.Number = stack.Number1;
                stack.Playables.Clear();
                c[Ledger] |= 1 << stack.Number1;
                if (stack.Number1 != 2) Resolve(g,c,s,ability,null);
            }),
            StateTaskList.Suspendable(key + "/glare",
                new NumberConditionTask(2, RelaSign.EQ),
                new FlagTask(false, EffectContext.Task(_ => TaskState.STOP)),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board, SelectorSide.Enemy,
                    cardType: CardType.MINION)),
                new CustomTask((g,c,s,t,stack) => Resolve(g,c,s,g.IdEntityDic[stack.Number2],stack.Playables.LastOrDefault()))));

        private static int RitualAmount(IPlayable source, int amount)
        {
            int shadow = source.Controller.BoardZone.Where(m => !m.IsSilenced && !m.Untouchable)
                .Aggregate(source.Controller[(GameTag)1950], (sum, m) => DamageArithmetic.Add(sum,m[(GameTag)1950]));
            return SpellDamageRules.Amount(source, DamageArithmetic.Add(amount,shadow));
        }

        internal static readonly SuspendableTask OnBoardRecipe = OldGodRules.Recipe("engine/old-god/cthun/v1",
            new CustomTask((g,c,s,t,stack) => {
                c[Ledger] |= 1 << stack.Number1;
                if (stack.Number1 != 2) Resolve(g,c,s,g.IdEntityDic[stack.Number2],null,stack.Number3,onBoard:true);
            }),
            OldGodRules.Branch("engine/old-god/cthun/glare/v1",2,
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board,SelectorSide.Enemy,cardType:CardType.MINION)),
                new CustomTask((g,c,s,t,stack) => Resolve(g,c,s,g.IdEntityDic[stack.Number2],stack.Playables.LastOrDefault(),stack.Number3,onBoard:true))));

        private static void Resolve(Game game, Controller controller, IEntity source, IPlayable ability, IPlayable target, int multiplier = 1, bool onBoard = false)
        {
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, ability.Id, "", -1, target?.Id ?? 0));
            try
            {
                switch (ability.Card.Id)
                {
                    case "BE_013t1":
                        int missiles = source.Card.Id == "BE_726" ? RitualAmount((IPlayable)source, 7) : 7 * multiplier;
                        for (int i = 0; i < missiles && (source.Card.Id != "BE_726" || game.State != State.COMPLETE); i++)
                        {
                            var enemies = controller.Opponent.BoardZone.GetAll().Where(m => !m.Untouchable && !m.IsDead)
                                .Cast<ICharacter>().Concat(new[] { controller.Opponent.Hero }).Where(e => !e.IsDead).ToArray();
                            if (enemies.Length == 0) break;
                            var hit = enemies[game.Random.Next(enemies.Length)];
                            if (onBoard) OnBoardDamage(game,controller,(Minion)source,ability,(IPlayable)hit,1,false);
                            else new DamageTask(1, EntityType.TARGET).Process(game,controller,source.Card.Id == "BE_726" ? source : ability,hit);
                            game.DeathProcessingAndAuraUpdate();
                        }
                        break;
                    case "BE_013t2":
                        var friends = controller.BoardZone.GetAll().Where(m => m.Id != source.Id && !m.Untouchable && !m.IsDead).ToList();
                        for (int i = 0; i < 2 && friends.Count != 0; i++)
                        {
                            int pick = game.Random.Next(friends.Count); var friend = friends[pick]; friends.RemoveAt(pick);
                            Generic.AddEnchantmentBlock(game, Cards.FromId(multiplier == 3 ? "BE_013t2e2" : "BE_013t2e1"), ability, friend);
                        }
                        break;
                    case "BE_013t3":
                    case "BE_013t6":
                        if (target != null)
                        {
                            if (onBoard)
                            {
                                ability[GameTag.LIFESTEAL] = 1;
                                OnBoardDamage(game,controller,(Minion)source,ability,target,5 * multiplier,true);
                                break;
                            }
                            var damageSource = source.Card.Id == "BE_726" ? (IPlayable)source : ability;
                            int lifesteal = damageSource[GameTag.LIFESTEAL];
                            damageSource[GameTag.LIFESTEAL] = 1;
                            try { new DamageTask(source.Card.Id == "BE_726" ? RitualAmount(damageSource,5 * multiplier) : 5 * multiplier, EntityType.TARGET).Process(game,controller,damageSource,target); }
                            finally { if (source.Card.Id == "BE_726") damageSource[GameTag.LIFESTEAL] = lifesteal; }
                        }
                        break;
                    case "BE_013t4":
                        var context = new EffectContext(game,controller.Id,ability.Id);
                        int position = source is Minion minion && minion.Zone == controller.BoardZone
                            ? minion.ZonePosition + 1 : controller.BoardZone.SlotCount;
                        if (multiplier == 1) { context.Summon("BE_013t5",position); context.Summon("BE_013t5",position + 1); break; }
                        for (int i = 0; i < 2; i++) {
                            if (controller.BoardZone.IsFull) break;
                            var fanatic = (Minion)Entity.FromCard(controller,Cards.FromId("BE_013t5"));
                            if (multiplier == 3) { fanatic.AttackDamage = 6; fanatic.BaseHealth = 9; }
                            Generic.SummonBlock(game,fanatic,Math.Min(position + i,controller.BoardZone.SlotCount),ability);
                        }
                        break;
                    default: throw new InvalidOperationException("Unknown external C'Thun ability.");
                }
                game.DeathProcessingAndAuraUpdate();
            }
            finally
            {
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                ability[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
            }
        }

        private static void OnBoardDamage(Game game, Controller owner, Minion body, IPlayable ability,
            IPlayable target, int amount, bool lifesteal)
        {
            long before = game.HistoryFacts.Query.Damage().LastOrDefault()?.Sequence ?? -1;
            new DamageTask(amount,EntityType.TARGET).Process(game,owner,body,target);
            var hit = game.HistoryFacts.Query.Damage().FirstOrDefault(f => f.Sequence > before && f.SourceId == body.Id && f.VictimId == target.Id);
            if (hit == null || hit.Amount <= 0) return;
            if (body.HasStealth) body.HasStealth = false;
            // Glare has Lifesteal even when the body does not; never double a body's own Lifesteal.
            if (lifesteal && !body.HasLifeSteal) HealingRules.Lifesteal(ability,hit.Amount);
        }

        public static void QueueAttackAbility(Controller controller, IEntity source)
        {
            if (controller[PendingAttackSource] != 0) throw new InvalidOperationException("C'Thun attack choice was not consumed before another attack.");
            var held = controller.HandZone.Where(c => c.Card.Id == "BE_013").ToArray();
            var costs = held.Select(c => c.Cost).ToArray();
            controller[Discount]++;
            for (int i = 0; i < held.Length; i++)
                if (controller.Game.History && costs[i] != held[i].Cost)
                    controller.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(held[i].Id,GameTag.COST,held[i].Cost));
            controller[PendingAttackSource] = source.Id;
        }

        internal static void AfterAttack(Game game, Controller controller)
        {
            int id = controller[PendingAttackSource];
            if (id == 0 || game.Resolution.CurrentEffect != null) return;
            controller[PendingAttackSource] = 0;
            if (controller[Generic.AutomaticDecisionTag] != 0)
                game.Resolution.RunAutomaticEffect(game,AttackRecipeKey,controller,game.IdEntityDic[id],null,new TaskStack());
            else AttackRecipe.Process(game,controller,game.IdEntityDic[id],null,new TaskStack());
        }

        internal static void CancelAbilities(Game game)
        {
            // A target prompt can hold a public ability actor separately from its parent card.
            foreach (var ability in game.IdEntityDic.Values.Where(e => e[GameTag.ZONE] == (int)Zone.PLAY && (Catalog.Contains(e.Card.Id) || new[] { "BE_009", "BE_013", "BE_017", "BE_784" }.Any(id => OldGodRules.Catalog(id).Contains(e.Card.Id)))).ToArray())
                ability[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
        }

        internal static bool IsAttackContinuation(EffectFrame parent, EffectFrame child) =>
            child.RecipeKey == AttackRecipeKey && parent.ControllerId == child.ControllerId &&
            EffectFrameRecipes.Resolve(parent.RecipeKey)[parent.NextStep - 1] is AttackTask;
    }
}
