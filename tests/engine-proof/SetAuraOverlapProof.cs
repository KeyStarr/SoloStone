using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SetAuraOverlapProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);

    [GroupProof("set-aura-overlap")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            CostSourceHashes(history);
            MixedCostRemoval(history);
            var game = B14H1StatsProof.NewGame(history);
            var player = game.Player1;
            Play(game, player, "END_012");
            var first = player.Hero.Weapon;
            Play(game, player, "END_012");
            Check(first.Zone.Type == Zone.GRAVEYARD, "first Hand replaced");
            Summon(player.Opponent, "CS2_182");
            player.Hero.IsExhausted = false;
            Check(player.Hero[GameTag.CANNOT_ATTACK_HEROES] > 0, "replacement Hand retains hero restriction");
            Check(player.Options().Any(o => o.PlayerTaskType == PlayerTaskType.HERO_ATTACK && o.Target is Minion)
, "replacement Hand offers legal minion attack");
            Check(!player.Options().Any(o => o.PlayerTaskType == PlayerTaskType.HERO_ATTACK && o.Target == player.Opponent.Hero),
                "replacement Hand never offers enemy hero attack");

            foreach (bool removeFirst in new[] { false, true })
            {
                game = B14H1StatsProof.NewGame(history);
                player = game.Player1;
                // Two different printed sources grant Immune: Mal'Ganis and Violet Illusionist.
                var malganis = Summon(player, "GVG_021");
                var illusionist = Summon(player, "KAR_712");
                game.DeathProcessingAndAuraUpdate();
                Kill(game, removeFirst ? malganis : illusionist);
                Check(player.Hero.IsImmune, "other Immune source survives either removal order");
                Kill(game, removeFirst ? illusionist : malganis);
                Check(!player.Hero.IsImmune, "last Immune source removed");

                var restriction = new Effect(GameTag.CANNOT_ATTACK_HEROES, EffectOperator.SET, 1);
                var a = Summon(player, "CS2_182");
                var b = Summon(player, "CS2_182");
                new Aura(AuraType.HERO, restriction).Activate(a);
                new Aura(AuraType.HERO, restriction).Activate(b);
                game.DeathProcessingAndAuraUpdate();
                Kill(game, removeFirst ? a : b);
                Check(player.Hero[GameTag.CANNOT_ATTACK_HEROES] > 0, "restriction survives either source removal order");
                Kill(game, removeFirst ? b : a);
                Check(player.Hero[GameTag.CANNOT_ATTACK_HEROES] == 0, "last restriction source removed");
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var game = B14H1StatsProof.NewGame(history);
            var player = game.Player1;
            var first = Summon(player, "EX1_560");
            var second = Summon(player, "EX1_560");
            game.DeathProcessingAndAuraUpdate();
            Check(player.ControllerAuraEffects[GameTag.TIMEOUT] == -55, "two Nozdormus retain one timeout override");
            foreach (var future in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                Kill(future, (Minion)future.IdEntityDic[second.Id]);
                Check(future.Player1.ControllerAuraEffects[GameTag.TIMEOUT] == -55, "Nozdormu timeout survives clone/load removal");
                Kill(future, (Minion)future.IdEntityDic[first.Id]);
                Check(future.Player1.ControllerAuraEffects[GameTag.TIMEOUT] == 0, "last timeout source removed");
            }

            game = B14H1StatsProof.NewGame(history);
            player = game.Player1;
            first = Summon(player, "TRL_092"); second = Summon(player, "TRL_092");
            game.DeathProcessingAndAuraUpdate();
            Check(player.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 1,
                "overlapping Spirit of the Shark sources expose enabled Battlecry/Combo flag");
            Kill(game, first);
            Check(player.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 1, "remaining Shark keeps flag enabled");
            Kill(game, second);
            Check(player.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 0, "last Shark clears flag");

            foreach (var tag in new[] { GameTag.HEROPOWER_DAMAGE, GameTag.HEALTH_MINIMUM })
            {
                game = B14H1StatsProof.NewGame(history);
                var target = tag == GameTag.HEALTH_MINIMUM ? (IPlayable)Summon(game.Player1, "CS2_182") : game.Player1.Hero;
                target[tag] = 3;
                var low = new Effect(tag, EffectOperator.SET, 2);
                var high = new Effect(tag, EffectOperator.SET, 5);
                low.ApplyAuraTo(target, 11); high.ApplyAuraTo(target, 22); low.ApplyAuraTo(target, 33);
                Check(target[tag] == 5, "latest numeric SET plus native value: " + tag);
                foreach (var future in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
                {
                    var copy = future.IdEntityDic[target.Id];
                    low.RemoveAuraFrom(copy, 11);
                    Check(copy[tag] == 5, "removing oldest equal-valued source keeps latest: " + tag);
                    low.RemoveAuraFrom(copy, 33);
                    Check(copy[tag] == 8, "removing latest numeric SET falls back: " + tag);
                    high.RemoveAuraFrom(copy, 22);
                    Check(copy[tag] == 3, "numeric SET removal preserves native value: " + tag);
                }
            }

            foreach (var tag in new[] { GameTag.CHARGE, GameTag.RUSH, GameTag.TAUNT, GameTag.LIFESTEAL, GameTag.WINDFURY })
            {
                game = B14H1StatsProof.NewGame(history);
                var target = Summon(game.Player1, "CS2_182");
                var grant = new Effect(tag, EffectOperator.SET, 1);
                var additive = new Effect(tag, EffectOperator.ADD, 1);
                grant.ApplyAuraTo(target, 11); grant.ApplyAuraTo(target, 22); additive.ApplyAuraTo(target);
                grant.RemoveAuraFrom(target, 11); grant.RemoveAuraFrom(target, 22);
                Check(target[tag] > 0, "SET removal preserves additive keyword source: " + tag);
                additive.RemoveAuraFrom(target);
                Check(target[tag] == 0, "final keyword source removed: " + tag);
            }

            game = B14H1StatsProof.NewGame(history);
            player = game.Player1;
            var handCard = Add(player, "CS2_182");
            var aviana = Summon(player, "AT_045");
            var witch = Summon(player, "LOE_038");
            var laterAviana = Summon(player, "AT_045");
            game.DeathProcessingAndAuraUpdate();
            Check(handCard.Cost == 1, "latest Cost setter wins");
            foreach (var future in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var copy = future.IdEntityDic[handCard.Id];
                Kill(future, (Minion)future.IdEntityDic[aviana.Id]);
                Check(copy.Cost == 1, "removing oldest Aviana preserves latest equal-valued Cost source");
                Kill(future, (Minion)future.IdEntityDic[laterAviana.Id]);
                Check(copy.Cost == 5, "Cost falls back to surviving Naga Sea Witch");
                Kill(future, (Minion)future.IdEntityDic[witch.Id]);
                Check(copy.Cost == copy.Card.Cost, "Cost returns to base after final source leaves");
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var game = B14H1StatsProof.NewGame(history);
            var player = game.Player1;
            var target = Summon(player, "CS2_182");
            var a = Summon(player, "CS2_182");
            var b = Summon(player, "CS2_182");
            var rush = new Effect(GameTag.RUSH, EffectOperator.SET, 1);
            new Aura(AuraType.BOARD, rush) { Restless = true,
                Condition = SelfCondition.IsTagValue(GameTag.ATK, 5, RelaSign.GEQ) }.Activate(a);
            new Aura(AuraType.BOARD, rush).Activate(b);
            game.DeathProcessingAndAuraUpdate();
            Check(target.IsRush, "both Rush auras applied");
            Effects.SetAttack(1).ApplyTo(target);
            game.DeathProcessingAndAuraUpdate();
            Check(target.IsRush && !target.IsExhausted, "restless DeApply retains other Rush source and readiness");
            Effects.SetAttack(6).ApplyTo(target);
            game.DeathProcessingAndAuraUpdate();
            Kill(game, b);
            Check(target.IsRush, "restless reapplication remains after other source leaves");
            Kill(game, a);
            Check(!target.IsRush && target.IsExhausted, "last Rush removal restores summoning sickness");

            game = B14H1StatsProof.NewGame(history);
            player = game.Player1;
            target = Summon(player, "CS2_182");
            target.NumAttacksThisTurn = 1; target.IsExhausted = true;
            var windfury = new Effect(GameTag.WINDFURY, EffectOperator.SET, 1);
            windfury.ApplyAuraTo(target, 11); windfury.ApplyAuraTo(target, 22);
            Check(target.HasWindfury && !target.IsExhausted, "Windfury aura grants second attack");
            windfury.RemoveAuraFrom(target, 22);
            Check(target.HasWindfury && !target.IsExhausted, "remaining Windfury preserves readiness");
            windfury.RemoveAuraFrom(target, 11);
            Check(!target.HasWindfury && target.IsExhausted, "final Windfury removal updates readiness");

            game = B14H1StatsProof.NewGame(history);
            player = game.Player1;
            target = Summon(player, "CS2_182");
            var crystal = Cards.FromId("UNG_067t1e2").Power.Enchant.Effects;
            foreach (var effect in crystal) effect.ApplyAuraTo(target, 11);
            Check(target.AttackDamage == 5 && target.BaseHealth == 5, "Crystal Core sets absolute 5/5");
            foreach (var effect in crystal) effect.ChangeValue(7).ApplyAuraTo(target, 22);
            foreach (var effect in crystal) effect.ChangeValue(7).RemoveAuraFrom(target, 22);
            Check(target.AttackDamage == 5 && target.BaseHealth == 5, "numeric attribute SET falls back to Crystal Core");

            game = B14H1StatsProof.NewGame(history);
            player = game.Player1;
            player.Hero.Armor = 7;
            Play(game, player, "LOOT_044");
            Play(game, player, "LOOT_044");
            Check(player.Hero.AttackDamage == 7, "replacing legacy Bladed Gauntlet retains adaptive SET Attack");
        }
        foreach (bool history in new[] { false, true })
        {
            var game = B14H1StatsProof.NewGame(history);
            var player = game.Player1;
            var first = Summon(player, "TIME_606");
            Summon(player, "AT_085");
            var latest = Summon(player, "TIME_606");
            game.DeathProcessingAndAuraUpdate();
            Check(player.Hero.HeroPower.Cost == 0, "latest conditional Hero Power SET wins");
            foreach (var future in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                Kill(future, (Minion)future.IdEntityDic[latest.Id]);
                Check(future.Player1.Hero.HeroPower.Cost == 1, "conditional SET removes its own source, falling back to Maiden");
                Kill(future, (Minion)future.IdEntityDic[first.Id]);
                Check(future.Player1.Hero.HeroPower.Cost == 1, "earlier conditional SET removal keeps Maiden");
            }
        }
        Console.WriteLine("PASS SET aura overlap: replacement Hand and either source removal order");
    }

    private static void CostSourceHashes(bool history)
    {
        var game = B14H1StatsProof.NewGame(history);
        var card = Add(game.Player1, "CS2_182");
        Check(card.Cost == card.Card.Cost, "unmodified Boulderfist Ogre uses printed cost");
        string ordinaryHash = game.Hash();
        var reversed = game.CloneExact();
        var other = reversed.IdEntityDic[card.Id];
        var low = new Effect(GameTag.COST, EffectOperator.SET, 1);
        var high = new Effect(GameTag.COST, EffectOperator.SET, 5);
        low.ApplyAuraTo(card, 11); high.ApplyAuraTo(card, 22); low.ApplyAuraTo(card, 33);
        low.ApplyAuraTo(other, 33); high.ApplyAuraTo(other, 22); low.ApplyAuraTo(other, 11);
        Check(card.Cost == 1 && other.Cost == 1, "opposite equal-valued Cost source orders both expose one");
        Check(game.Hash() != reversed.Hash(), "game hash distinguishes ordered Cost aura source ownership");
        foreach (var original in new[] { game, reversed })
        {
            string hash = original.Hash();
            foreach (var future in new[] { original, original.CloneExact(), Game.LoadSnapshot(original.SaveSnapshot()) })
            {
                Check(future.Hash() == hash, "Cost aura source hash survives clone/load");
                var copy = future.IdEntityDic[card.Id];
                low.RemoveAuraFrom(copy, 11);
                Check(copy.Cost == (original == game ? 1 : 5), "removing same Cost source exposes different ordered fallback");
                low.RemoveAuraFrom(copy, 33); high.RemoveAuraFrom(copy, 22);
                Check(copy.Cost == copy.Card.Cost && future.Hash() == ordinaryHash,
                    "empty Cost manager preserves ordinary game hash");
            }
        }
    }

    private static void MixedCostRemoval(bool history)
    {
        foreach (bool removeApprentice in new[] { true, false })
        {
            var game = B14H1StatsProof.NewGame(history);
            var fireball = Add(game.Player1, "CS2_029");
            var apprentice = Summon(game.Player1, "EX1_608");
            Check(fireball.Cost == 3, "Apprentice reduces held Fireball to three");
            var witch = Summon(game.Player1, "LOE_038");
            Check(fireball.Cost == 5, "later Naga sets held Fireball to five after Apprentice reduction");
            foreach (var future in new[] { game, game.CloneExact(), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var copy = future.IdEntityDic[fireball.Id];
                Kill(future, (Minion)future.IdEntityDic[removeApprentice ? apprentice.Id : witch.Id]);
                Check(copy.Cost == (removeApprentice ? 5 : 3), removeApprentice
                    ? "removing Apprentice keeps Naga's Fireball cost at five"
                    : "removing Naga restores Apprentice's Fireball cost to three");
                Kill(future, (Minion)future.IdEntityDic[removeApprentice ? witch.Id : apprentice.Id]);
                Check(copy.Cost == 4, "Fireball returns to four after both cost sources leave");
            }
        }
    }
}
