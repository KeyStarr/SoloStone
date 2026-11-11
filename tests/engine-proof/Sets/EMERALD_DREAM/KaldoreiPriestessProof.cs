using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class KaldoreiPriestessProof
{
    private static int Debuffs(IPlayable target) => target.AppliedEnchantments?.Count(e => e.Card.Id == "EDR_970e") ?? 0;
    internal static void Run() => StandardSetsProof.Case("EDR_970", Lean_KaldoreiUntilYourNextTurn);

    // Lean_KaldoreiUntilYourNextTurn: imbue-01/16 text/order; design_plan §6 caster-owned TURN_START expiry.
    private static void Lean_KaldoreiUntilYourNextTurn(Game g, Controller p, string id)
    {
        p.Hero.Card = Cards.FromId("HERO_09");
        var enchantment = Cards.FromId("EDR_970e");
        Core2026Proof.Check(enchantment.Implemented && enchantment.AssetId == 115731
            && EffectModules.ByDbfId[115731] == enchantment.Id && enchantment.Type == CardType.ENCHANTMENT
            && enchantment.Class == CardClass.PRIEST && enchantment.Cost == 0 && enchantment.ATK == 0 && enchantment.Health == 0
            && enchantment.Text == "-2 Attack until your next turn.", "exact exported Pacified enchantment");
        var friendly = Summon(p, "CS2_182"); var friendlyLocation = LocationProofs.Location(p);
        var enemy = Summon(p.Opponent, "CS2_182"); var small = Summon(p.Opponent, "CS2_171");
        var silenced = Summon(p.Opponent, "CS2_182"); var stolen = Summon(p.Opponent, "CS2_182");
        var dormant = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(DormantRules.Make(dormant, 3, "TIME_046e", p.Opponent.Hero), "Dormant fixture");
        var location = LocationProofs.Location(p.Opponent);
        int cursor = g.PowerHistory.Full.Count;
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.Cost == 3 && first.AttackDamage == 3 && first.Health == 3
            && first.Card.Class == CardClass.PRIEST, "3 mana 3/3 Priest");
        Core2026Proof.Check(enemy.AttackDamage == 2 && small.AttackDamage == 0 && silenced.AttackDamage == 2
            && stolen.AttackDamage == 2 && new[] { enemy, small, silenced, stolen }.All(m => Debuffs(m) == 1),
            "all active enemy minions get exact -2; Attack floors at zero");
        Core2026Proof.Check(friendly.AttackDamage == 4 && first.AttackDamage == 3 && Debuffs(friendly) == 0
            && dormant.IsDormant && dormant.AttackDamage == 4 && Debuffs(dormant) == 0
            && Debuffs(location) == 0 && Debuffs(friendlyLocation) == 0 && p.Hero.AttackDamage == 0
            && p.Opponent.Hero.AttackDamage == 0, "friendly, Dormant, locations and heroes excluded");
        var changes = g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>().ToArray();
        int attack = Array.FindIndex(changes, c => c.EntityId == enemy.Id && c.Tag == GameTag.ATK && c.Value == 2);
        int imbue = Array.FindIndex(changes, c => c.EntityId == p.Id && c.Tag == GameTag.IMBUE && c.Value == 1);
        Core2026Proof.Check(attack >= 0 && imbue > attack && p.Hero.HeroPower.Card.Id == "EDR_449p",
            "enemy Attack reduction precedes Imbue");
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(new[] { enemy, small, silenced, stolen }.All(m => m.AttackDamage == 0 && Debuffs(m) == 2)
            && ImbueRules.Count(p) == 2, "two casts stack independently and both Imbue");
        Core2026Proof.Check(enemy.AppliedEnchantments.All(e => e.Card.Id != "EDR_970e" || e.Controller == p),
            "expiry enchantments owned by caster");
        silenced.Silence();
        Core2026Proof.Check(silenced.AttackDamage == 4 && Debuffs(silenced) == 0, "Silence removes both debuffs");
        first.Silence(); Kill(g, second);
        Core2026Proof.Check(enemy.AttackDamage == 0 && Debuffs(enemy) == 2, "source Silence/death does not remove debuffs");
        new ControlTask(EntityType.TARGET).Process(g, p, p.Hero, stolen);
        Core2026Proof.Check(stolen.Controller == p && stolen.AttackDamage == 0 && Debuffs(stolen) == 2,
            "control change preserves debuffs");
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        string before = Canonical.State(g);
        foreach (var child in new[] { clone, loaded }) Advance(child, enemy.Id, small.Id, stolen.Id, silenced.Id);
        Core2026Proof.Check(Canonical.State(g) == before, "child expiry leaves parent untouched");
        Advance(g, enemy.Id, small.Id, stolen.Id, silenced.Id);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
            "clone/snapshot preserve future expiry and subscription order");
        var empty = ImbueProof.Make(CardClass.PRIEST, (int)g.Seed);
        Play(empty, empty.Player1, id);
        Core2026Proof.Check(empty.Player1.Hero.HeroPower.Card.Id == "EDR_449p" && ImbueRules.Count(empty.Player1) == 1,
            "empty enemy board still Imbues");
        while (!empty.Player1.BoardZone.IsFull) Summon(empty.Player1, "CS2_182");
        var untouched = Summon(empty.Player2, "CS2_182"); var full = Add(empty.Player1, id);
        empty.Player1.UsedMana = 0; string fullBefore = Canonical.State(empty);
        Core2026Proof.Check(!empty.Player1.Options().Any(t => t.Source?.Id == full.Id)
            && !Generic.PlayCard(empty.Player1, full)
            && untouched.AttackDamage == 4 && Canonical.State(empty) == fullBefore,
            "full board prevents Battlecry reduction and Imbue without mutation");
    }
    private static void Advance(Game g, int enemy, int small, int stolen, int silenced)
    {
        End(g);
        Core2026Proof.Check(new[] { enemy, small, stolen }.All(id => ((Minion)g.IdEntityDic[id]).AttackDamage == 0
            && Debuffs(g.IdEntityDic[id]) == 2), "survives opponent turn start");
        var late = Summon(g.Player2, "CS2_171");
        Core2026Proof.Check(late.AttackDamage == 1 && Debuffs(late) == 0, "later summon untouched; no aura");
        End(g);
        Core2026Proof.Check(new[] { enemy, stolen, silenced }.All(id => ((Minion)g.IdEntityDic[id]).AttackDamage == 4)
            && ((Minion)g.IdEntityDic[small]).AttackDamage == 1
            && new[] { enemy, small, stolen, silenced }.All(id => Debuffs(g.IdEntityDic[id]) == 0),
            "both stacks expire exactly at caster next turn, including transferred target; Silence never over-restores");
        End(g); End(g);
        Core2026Proof.Check(((Minion)g.IdEntityDic[enemy]).AttackDamage == 4
            && ((Minion)g.IdEntityDic[stolen]).AttackDamage == 4, "expiry never repeats");
    }
}
