using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BroodwatcherProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_132", (g, p, id) => {
        var overflow = g.Clone(); var overflowOwner = overflow.Player1;
        var unready = Add(overflowOwner, id);
        while (overflowOwner.HandZone.Count < 10) Add(overflowOwner, "GAME_005");
        PlayEntity(overflow, overflowOwner, unready);
        Core2026Proof.Check(overflowOwner.HandZone.Count == 10
            && overflowOwner.HandZone.Count(c => c.Card.Id == "CATA_132t") == 1,
            "unready full hand receives one Whelp and burns overflow");
        Play(g, p, "CS2_029", p.Opponent.Hero);
        var held = Add(p, id);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8, "mana before creation excluded from hand progress");
        Play(g, p, "EX1_400");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 7, "actual mana updates remaining display");
        End(g); Play(g, p.Opponent, "EX1_400"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 7, "own prior-turn progress retained; opposing spending excluded");
        var clone = g.Clone(); var owner = clone.Player1;
        owner.UsedMana = 0;
        Core2026Proof.Check(clone.Process(HeroPowerTask.Any(owner)), "holding hero power accepted");
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 5, "hero power updates remaining display");
        var trade = Add(owner, "CORE_EX1_005"); owner.UsedMana = 0;
        Core2026Proof.Check(clone.Process(TradeCardTask.Any(owner, trade)), "holding Trade accepted");
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "Trade updates remaining display");
        var prepare = Add(owner, "CS2_029"); prepare.Cost = 5; PrepareRules.Grant(prepare); owner.UsedMana = 0;
        Core2026Proof.Check(clone.Process(PrepareCardTask.Any(owner, prepare)), "holding Prepare accepted");
        var ready = clone.IdEntityDic[held.Id];
        Core2026Proof.Check(ready[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 7,
            "Prepare reaches eight with clone-isolated ready display");
        var late = Add(owner, id);
        var copied = Generic.Copy(owner, ready, ready, Zone.HAND);
        Core2026Proof.Check(late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8 && copied[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8,
            "new and copied physical cards start fresh hand residence");
        owner.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1;
        var healthSpell = Add(owner, "CS2_029"); owner.UsedMana = 0;
        Core2026Proof.Check(clone.Process(PlayCardTask.Any(owner, healthSpell, owner.Opponent.Hero)), "Health-paid spell accepted");
        owner.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 0;
        Core2026Proof.Check(late[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8, "Health payment is not Mana");
        var location = (BoardObject)Entity.FromCard(owner, Cards.FromId("CORE_REV_990"), zone: owner.BoardZone);
        var right = Summon(owner, "CS2_182"); owner.UsedMana = 0;
        Core2026Proof.Check(clone.Process(PlayCardTask.Any(owner, ready, zonePosition: location.ZonePosition + 1)), "ready Broodwatcher play accepted");
        var whelps = owner.BoardZone.Where(m => m.Card.Id == "CATA_132t").ToArray();
        Core2026Proof.Check(whelps.Length == 2 && whelps.All(m => m.Card.AssetId == 122969
            && m.AttackDamage == 3 && m.Health == 3 && m.HasTaunt && m.IsRace(Race.DRAGON)), "exact two Emerald Whelp tokens");
        Core2026Proof.Check(owner.BoardZone.GetSlot(ready.ZonePosition + 1).Id == whelps[0].Id
            && owner.BoardZone.GetSlot(ready.ZonePosition + 2).Id == whelps[1].Id
            && owner.BoardZone.GetSlot(ready.ZonePosition + 3).Id == right.Id, "whelps use successive physical slots right of source");
        var payment = Add(p, "CS2_029"); payment.Cost = 6;
        PlayEntity(g, p, payment, p.Opponent.Hero);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "seven Mana spent before own play leaves one remaining");
        var original = (Minion)held;
        PlayEntity(g, p, original);
        Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "CATA_132t") == 2
            && p.BoardZone.All(c => c.Card.Id != "CATA_132t"), "own four-Mana payment cannot complete threshold; unready gets two in hand");
        Play(g, p, "EX1_144", original);
        Core2026Proof.Check(original[GameTag.TAG_SCRIPT_DATA_NUM_1] == 8, "bounce begins fresh residence and resets display");
        Play(g, p, "CS2_029", p.Opponent.Hero); Play(g, p, "CS2_029", p.Opponent.Hero);
        var full = g.Clone(); var fullOwner = full.Player1;
        while (fullOwner.BoardZone.SlotCount < 6) Summon(fullOwner, "CS2_182");
        PlayEntity(full, fullOwner, full.IdEntityDic[original.Id]);
        Core2026Proof.Check(fullOwner.BoardZone.SlotCount == 7 && fullOwner.BoardZone.All(m => m.Card.Id != "CATA_132t"),
            "full board after source play suppresses both ready summons");
        while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_182");
        PlayEntity(g, p, original);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_132t") == 1
            && p.HandZone.Count(c => c.Card.Id == "CATA_132t") == 2, "one free summon slot gives one Whelp; overflow summon is lost");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
    });
}
