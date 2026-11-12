using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GaronasLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_203", (g, p, id) => {
        var spell = Add(p, id); var ordinary = Summon(p, "CS2_182");
        Core2026Proof.Check(spell.Card[GameTag.TRADEABLE] == 1 && TradeCardTask.IsValid(p, spell), "Tradeable option available without targets");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "ordinary minion is not valid target");
        int deck = p.DeckZone.Count, hand = p.HandZone.Count; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, spell)), "Trade succeeds");
        Core2026Proof.Check(spell.Zone.Type == Zone.DECK && p.DeckZone.Count == deck && p.HandZone.Count == hand && p.UsedMana == 1 && ordinary.Zone.Type == Zone.PLAY, "Trade costs one, replaces hand card, never casts destroy");
        var friendly = Summon(p, "EX1_110"); var enemy = Summon(p.Opponent, "EX1_563"); spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length == 2 && options.Any(t => t.Target == friendly) && options.Any(t => t.Target == enemy), "only Legendary minions on either side");
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Any(m => m.Card.Id == "EX1_110t"), "destroy runs friendly Deathrattle");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && ordinary.Zone.Type == Zone.PLAY, "enemy Legendary destroyed");
        spell = Add(p, id); p.BaseMana = 0; p.UsedMana = 0;
        Core2026Proof.Check(!TradeCardTask.IsValid(p, spell), "Trade unavailable without Mana");
        p.BaseMana = 10; foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Core2026Proof.Check(!TradeCardTask.IsValid(p, spell), "Trade unavailable with empty deck");
    });
}
