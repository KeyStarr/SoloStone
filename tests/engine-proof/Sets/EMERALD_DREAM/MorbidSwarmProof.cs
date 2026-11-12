using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MorbidSwarmProof
{
    internal static void Unpinned_IndependentBranches(Game g, Controller p, string id)
    {
        var card = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source == card).All(t => t.ChooseOne == 1 && t.Target == null), "empty board Ants need no root target");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card, chooseOne: 1)) && p.BoardZone.Count == 2
            && p.BoardZone.All(m => m.Card.Id == "EDR_813at" && m.AttackDamage == 1 && m.Health == 1), "two exact Ant Husks");
        foreach (int corpses in new[] { 0, 1, 2, 3 })
        foreach (bool full in new[] { false, true })
        {
            var branch = DeathKnightC1Proof.NewGame(g.Seed); var b = branch.Player1;
            if (full) DeathKnightC1Proof.FullBoard(b);
            var target = Summon(b.Opponent, "CS2_182");
            CorpseRules.Gain(b, corpses); var source = Add(b, id);
            var offers = b.Options().Where(t => t.Source == source).ToArray();
            Core2026Proof.Check(offers.Any(t => t.ChooseOne == 1) == !full && offers.Any(t => t.ChooseOne == 2) == (corpses >= 2), "funding/capacity filter branches independently");
            Core2026Proof.Check(offers.Where(t => t.ChooseOne == 2).All(t => t.Target is Minion), "damage branch excludes heroes");
            if (corpses >= 2)
                PoolPilotProof.Resume(branch, next => {
                    var owner = next.Player1; owner.ControllerAuraEffects[GameTag.SPELLPOWER] = 1;
                    Core2026Proof.Check(next.Process(PlayCardTask.Any(owner, next.IdEntityDic[source.Id], next.IdEntityDic[target.Id], chooseOne: 2)), "funded damage branch");
                    Core2026Proof.Check(((Minion)next.IdEntityDic[target.Id]).Damage == 5 && CorpseRules.Available(owner) == corpses - 2, "four plus Spell Damage; exact two-Corpse debit");
                });
            else if (full) Core2026Proof.Check(!source.IsPlayable && !branch.Process(PlayCardTask.Any(b, source, chooseOne: 1)), "neither branch usable rejects play");
        }
        var elusiveGame = DeathKnightC1Proof.NewGame(g.Seed); var owner2 = elusiveGame.Player1;
        var elusive = Summon(owner2.Opponent, "CS2_182"); elusive.CantBeTargetedBySpells = true;
        CorpseRules.Gain(owner2, 4); card = Add(owner2, id);
        Core2026Proof.Check(owner2.Options().Where(t => t.Source == card).All(t => t.ChooseOne == 1 && t.Target == null)
            && elusiveGame.Process(PlayCardTask.Any(owner2, card, chooseOne: 1)), "Unpinned_MorbidTargetFallback preserves Ants against Elusive");
        var partial = DeathKnightC1Proof.NewGame(g.Seed); var player = partial.Player1;
        for (int i = 0; i < 6; i++) Summon(player, "CS2_182");
        card = Add(player, id); Core2026Proof.Check(partial.Process(PlayCardTask.Any(player, card, chooseOne: 1))
            && player.BoardZone.Count == 7 && player.BoardZone.Count(m => m.Card.Id == "EDR_813at") == 1, "one free slot summons one Ant");
        Unpinned_ChooseBothKeepsDamageTarget(g.Seed, id);
    }

    private static void Unpinned_ChooseBothKeepsDamageTarget(long seed, string id)
    {
        foreach (int corpses in new[] { 2, 0 })
        foreach (bool full in new[] { true, false })
        {
            var game = DeathKnightC1Proof.NewGame(seed); var owner = game.Player1;
            ChooseOneProof.Both(owner);
            if (full) while (!owner.BoardZone.IsFull) Summon(owner, "CS2_182");
            var yeti = Summon(owner.Opponent, "CS2_182");
            CorpseRules.Gain(owner, corpses); var source = Add(owner, id);
            PoolPilotProof.Resume(game, next => {
                var p = next.Player1; var card = next.IdEntityDic[source.Id];
                var target = (Minion)next.IdEntityDic[yeti.Id];
                var offers = p.Options().Where(t => t.Source == card).ToArray();
                if (full && corpses == 0)
                {
                    Core2026Proof.Check(offers.Length == 0 && !next.Process(PlayCardTask.Any(p, card)), "Choose Both rejects both unusable branches");
                    return;
                }
                Core2026Proof.Check(offers.Length > 0 && offers.All(t => t.ChooseOne <= 0
                    && (corpses == 2 ? t.Target is Minion : t.Target == null)), "Choose Both retains funded minion targeting; unfunded Ants stay untargeted");
                var option = offers.Single(t => t.Target == (corpses == 2 ? target : null));
                int mana = p.RemainingMana;
                Core2026Proof.Check(next.Process(option) && target.Damage == (corpses == 2 ? 4 : 0)
                    && CorpseRules.Available(p) == 0 && p.RemainingMana == mana - 1
                    && p.BoardZone.Count == (full ? 7 : 3)
                    && p.BoardZone.Count(m => m.Card.Id == "EDR_813at") == (full ? 0 : 2), "Choose Both resolves damage and exact Corpse payment despite full board");
            });
        }
        var ants = DeathKnightC1Proof.NewGame(seed); var player = ants.Player1;
        Summon(player.Opponent, "CS2_182"); CorpseRules.Gain(player, 2);
        var spell = Add(player, id);
        ChooseOneProof.PlayOption(ants, player, spell, 1);
        Core2026Proof.Check(player.BoardZone.Count == 2 && CorpseRules.Available(player) == 2,
            "ordinary Ant-only play needs no target even with funded damage and legal minion");
    }
}
