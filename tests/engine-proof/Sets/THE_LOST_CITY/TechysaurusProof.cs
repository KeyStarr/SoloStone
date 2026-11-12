using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TechysaurusProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_409", (g, p, id) => {
        var held = (Minion)Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Card.ConsumesHistory && held.Cost == 7 && held.AttackDamage == 3 && held.Health == 6
            && held.HasTaunt && held.Card.IsRace(Race.MECHANICAL) && held.Card.IsRace(Race.BEAST), "Techysaurus identity and both tribes");
        var original = p.DeckZone[0]; Generic.Draw(p, original); PlayEntity(g, p, original);
        var summoned = Summon(p, "CS2_231");
        GladesongSirenProof.Cast(g, p, "GAME_005", HistoryCastKind.Effect);
        var trade = Add(p, "CORE_EX1_002"); Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)), "Trade accepted");
        Core2026Proof.Check(held.Cost == 7, "initial-deck play, summon, effect cast and Trade excluded");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Core2026Proof.Check(held.Cost == 7, "opponent foreign play excluded");
        TriggerManager.TriggerHandler counter = entity => ((Spell)entity).IsCountered = true;
        g.TriggerManager.CastSpellTrigger += counter; Play(g, p, "GAME_005"); g.TriggerManager.CastSpellTrigger -= counter;
        Core2026Proof.Check(held.Cost == 6 && held[GameTag.COST] == 6, "countered committed foreign play counted with visible COST");
        End(g); End(g);
        var late = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(late.Cost == 6, "game memory before creation and previous turns retained");
        var clone = g.Clone(); Play(clone, clone.Player1, "GAME_005");
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 5 && held.Cost == 6, "clone play diverges without original mutation");
        for (int i = 0; i < 8; i++) Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Cost == 0 && late.Cost == 0 && held[GameTag.COST] == 0, "discount floors at zero");
        var fair = FairBotState.Create(g, p.PlayerId, 72, DeckStyle.Control);
        var projected = HistoryFairOriginProof.FairGame(fair); projected.AuraUpdate();
        Core2026Proof.Check(projected.IdEntityDic[held.Id].Cost == 0, "past play origins retained in fair search");
        PlayEntity(g, p, held); held.Silence();
        Core2026Proof.Check(!held.HasTaunt && held.AttackDamage == 3 && held.Health == 6, "native Taunt removed by Silence");
    });
}
