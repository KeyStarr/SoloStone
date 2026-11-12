using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class MalignantHorrorProof
{
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_745", (g, p, id) => {
        // NOTES/rules-evidence/cata_core_be-malignant-horror.yaml: tier A Gallon (full board: no spend),
        // tier A 25.4 fix (mortally wounded: no copy), tier C Advanced rulebook 4.3 (attached enchantments copied).
        // NOTES/rules-evidence/corpse-spend-automatic.yaml: tier C Corpse + tier E guides, automatic/all-or-nothing.
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        var source = StandardSetsProof.PlayAt(g, p, id, 2); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source[GameTag.REBORN] > 0, "printed Reborn");
        var doomedGame = g.CloneExact(history: true);
        var doomed = (Minion)doomedGame.IdEntityDic[source.Id];
        CorpseRules.Gain(doomed.Controller, 4); doomed.Damage = doomed.BaseHealth;
        doomed.Card.Power.Trigger.SingleTask.Process(doomedGame, doomed.Controller, doomed, null, new TaskStack());
        Core2026Proof.Check(doomed.Controller.BoardZone.SlotCount == 4 && CorpseRules.Available(doomed.Controller) == 4, "mortally wounded Horror never spends or copies");
        CorpseRules.Gain(p, 3); End(g);
        Core2026Proof.Check(CorpseRules.Available(p) == 3 && p.BoardZone.SlotCount == 4, "three Corpses: no spend or copy");
        End(g); CorpseRules.Gain(p, 1);
        Generic.AddEnchantmentBlock(g, Cards.FromId("RLK_958e"), source, source);
        var clone = g.CloneExact(history: true); End(g); End(clone);
        var copy = p.BoardZone.Single(m => m.Id != source.Id && m.Card.Id == id);
        Core2026Proof.Check(p.BoardZone.Slots.Select(c => c.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, copy.Id, right.Id })
            && copy.AttackDamage == 4 && copy.Health == 4 && copy[GameTag.REBORN] > 0 && CorpseRules.Available(p) == 0
            && copy.AppliedEnchantments.Single().Card.Id == "RLK_958e", "four spent; exact enchanted Reborn copy beside source on mixed board");
        Core2026Proof.Check(copy.Card.Id == clone.Player1.BoardZone.Single(m => m.Id != source.Id && m.Card.Id == id).Card.Id, "clone copy deterministic");
        CorpseRules.Gain(p, 8); End(g);
        Core2026Proof.Check(CorpseRules.Available(p) == 8 && p.BoardZone.SlotCount == 5, "opponent end turn never triggers");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); End(g);
        Core2026Proof.Check(CorpseRules.Available(p) == 8 && p.BoardZone.SlotCount == 7, "full board including location: no spend");
        End(g); source.Silence(); copy.Silence(); StandardSetsProof.ParkBoard(p);
        var silenced = (Minion)Play(g, p, id); silenced.Silence(); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && CorpseRules.Available(p) == 8, "Silence removes copy trigger/Reborn");
        End(g); StandardSetsProof.ParkBoard(p); var departed = (Minion)Play(g, p, id);
        p.HandZone.Add(p.BoardZone.Remove(departed)); End(g);
        Core2026Proof.Check(p.BoardZone.IsEmpty && CorpseRules.Available(p) == 8, "source leaving play removes trigger");
        End(g); EmptyHand(p); var rebornSource = (Minion)Play(g, p, id); Kill(g, rebornSource);
        var reborn = p.BoardZone.Single();
        Core2026Proof.Check(reborn.Card.Id == id && reborn.Health == 1 && reborn[GameTag.REBORN] == 0, "actual rebirth consumes Reborn");
        CorpseRules.TrySpend(p, CorpseRules.Available(p)); CorpseRules.Gain(p, 4); End(g);
        var rebornCopy = p.BoardZone.Single(m => m.Id != reborn.Id);
        Core2026Proof.Check(rebornCopy.Card.Id == id && rebornCopy[GameTag.REBORN] == 0 && rebornCopy.Health == 1
            && CorpseRules.Available(p) == 0, "copy retains consumed Reborn and current damage");
    });
}
