using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch9Proof;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using static ChooseOneProof;

internal static class SleepParalysisProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_490", (g, p, id) => {
        var batch9 = g.CloneExact(history: true);
        RunBatch9(batch9, batch9.Player1, id);
        Core2026Proof.Check(HsSim.Canonical.State(batch9) == HsSim.Canonical.State(batch9.CloneExact(history: true)) &&
            HsSim.Canonical.State(batch9) == HsSim.Canonical.State(SabberStoneCore.Model.Game.LoadSnapshot(batch9.SaveSnapshot())), "batch-9 proof clone/snapshot future");
        RunSelectors(g, p, id);
    });

    private static void RunBatch9(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        LocationProofs.Location(p, position: 0);
        var source = Choose(g, p, id, 1);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "EDR_490t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.Card.AssetId == 115937 && m.Card.Implemented && m.Card.IsRace(Race.DEMON)
            && m.AttackDamage == 3 && m.Health == 6 && m.HasTaunt && m.CantAttack && m.Controller == p && m.Zone == p.BoardZone
            && m[GameTag.DISPLAYED_CREATOR] == source.Id && g.IdEntityDic[m.Id] == m), "two exact 3/6 Taunt Demons, can't attack");
        Core2026Proof.Check(tokens[0].ZonePosition == 2 && tokens[1].ZonePosition == 3 && enemy.Zone == p.Opponent.BoardZone, "append in creation order after physical slots; no destruction");
        End(g); End(g);
        Core2026Proof.Check(p.Options().All(t => t.PlayerTaskType != PlayerTaskType.MINION_ATTACK || tokens.All(m => m.Id != t.Source?.Id)), "attack prohibition persists after readying");
        tokens[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!tokens[0].CantAttack && !tokens[0].HasTaunt && p.Options().Any(t => t.Source?.Id == tokens[0].Id && t.PlayerTaskType == PlayerTaskType.MINION_ATTACK), "Silence removes printed attack prohibition and Taunt");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.ChooseOne == 2).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion m && m.Controller == p.Opponent)
            && options.All(t => t.Target?.Id != friendly.Id), "destroy branch enemy minion only; no heroes/friendly targets");
        Choose(g, p, id, 2, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.BoardZone.SlotCount == 4, "only destruction branch resolves");
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Choose(g, p, id, 1);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 3, "one free physical slot creates one token");
        var blocked = Add(p, id); p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        // Tier C: https://hearthstone.wiki.gg/wiki/Choose_One — unavailable summon options are disabled.
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id), "full physical board and no enemy disable both branches");
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, blocked, chooseOne: 1)) && blocked.Zone == p.HandZone
            && p.UsedMana == 0 && p.BoardZone.SlotCount == 7, "full-board summon rejected without spending card or mana");
        enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id && t.ChooseOne == 1), "enemy target does not enable full-board summon branch");
        var destroy = p.Options().Single(t => t.Source?.Id == blocked.Id && t.ChooseOne == 2 && t.Target?.Id == enemy.Id);
        Core2026Proof.Check(g.Process(destroy) && enemy.Zone == p.Opponent.GraveyardZone && p.BoardZone.SlotCount == 7
            && blocked.Zone == p.GraveyardZone && p.UsedMana == blocked.Cost, "destroy branch remains playable on full physical board");
        Kill(g, tokens[1]); Core2026Proof.Check(p.BoardZone.SlotCount == 6, "token has no deathrattle");
        ClearBoard(g, p); enemy = Summon(p.Opponent, "CS2_182"); p.ChooseBoth = true;
        Choose(g, p, id, 0, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 2,
            "Choose Both summons Demons and destroys selected enemy");
        p.ChooseBoth = false;
    }

    private static void RunSelectors(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        OptionsSurvive(g, p, id, "EDR_490a", "EDR_490b");
        var source = Add(p, id); var friend = Summon(p, "CS2_182");
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.ChooseOne == 1 && t.Target == null), "no enemy minion excludes destroy branch");
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, source, friend, chooseOne: 2)), "friendly destroy target rejected");
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id && t.ChooseOne == 2).All(t => t.Target?.Id == enemy.Id), "only enemy minion is legal");
        PlayOption(g, p, source, 2, enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friend.Zone.Type == Zone.PLAY, "destroy chosen enemy");
        PlayOption(g, p, id, 1);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "EDR_490t").ToArray();
        Core2026Proof.Check(tokens.Length == 2 && p.BoardZone.Last().Id == tokens.Last().Id && tokens.All(m => m.AttackDamage == 3 && m.Health == 6 && m.HasTaunt && m.CantAttack), "two exact Night Terrors at right end");
        End(g); End(g);
        Core2026Proof.Check(tokens.All(m => !m.IsExhausted) && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && tokens.Any(m => m.Id == t.Source?.Id)), "Night Terrors cannot attack after waking");
        Both(p); enemy = Summon(p.Opponent, "CS2_182");
        PlayOption(g, p, id, -1, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 4, "Fandral summons two then destroys target");
        ClearBoard(g, p); Both(p);
        PlayOption(g, p, id, -1);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 2, "Choose Both without enemy target still summons");
        ClearBoard(g, p); var location = Play(g, p, "JAIL_877");
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        PlayOption(g, p, id, 1);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 1, "location consumes slot; only one summon fits");
        var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, blocked, chooseOne: 1)) && blocked.Zone == p.HandZone && p.UsedMana == 0
            && p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 1, "full board rejects summon without spending card or mana");
    }
}
