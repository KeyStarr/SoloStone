using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SchismProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_306", (g, p, id) => {
        ShatterSpellProof.Family(g, p, id, "CATA_306t1", "CATA_306t2", "CATA_306e");
        var location = LocationProofs.Location(p); var target = Summon(p, "CS2_182");
        var right = Summon(p, "CS2_231"); var enemy = Summon(p.Opponent, "CS2_182");
        var dormant = Summon(p, "TIME_046");
        foreach (var effect in Effects.AttackHealth_N(1)) effect.ApplyTo(target);
        target.TakeDamage(enemy, 2);
        target.HasDivineShield = true;
        var source = ShatterRules.GiveCombined(p, id, p.Hero);
        var targets = source.ValidPlayTargets.ToArray();
        Core2026Proof.Check(targets.Contains(target) && targets.Contains(right) && !targets.Contains(enemy) && !targets.Contains(p.Hero)
            && !targets.Contains(dormant), "Unpinned_DormantExclusions: only friendly active minions, never heroes/enemies/Dormant");
        ShatterSpellProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (Minion)next.IdEntityDic[target.Id]));
        var copy = p.BoardZone.Single(m => m.Id != target.Id && m.Card.Id == target.Card.Id);
        Core2026Proof.Check(target.AttackDamage == 7 && target.BaseHealth == 9 && target.Damage == 2 && target.Health == 7
            && target.CantBeTargetedBySpells && copy.AttackDamage == 7 && copy.BaseHealth == 9 && copy.Damage == 2
            && copy.CantBeTargetedBySpells && copy.CantBeTargetedByHeroPowers && copy.HasDivineShield && copy.IsExhausted
            && copy.AppliedEnchantments.Single().Card.Id == "CATA_306e" && copy.ZonePosition == 4
            && p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(2) == right,
            "Unpinned_SchismCopy: same buffed target, retained damage/buffs/Elusive; spell copy appends");
        Core2026Proof.Check(enemy.AttackDamage == 4 && !enemy.CantBeTargetedBySpells, "opponent unchanged");
        copy.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.AttackDamage == 4 && copy.BaseHealth == 5 && !copy.CantBeTargetedBySpells && !copy.CantBeTargetedByHeroPowers
            && target.AttackDamage == 7 && target.CantBeTargetedBySpells, "Silence removes independent copy's grants");
        Kill(g, target); Core2026Proof.Check(copy.Zone == p.BoardZone && copy.AttackDamage == 4, "original departure cannot remove copy");
        StandardSetsProof.ParkBoard(p);
        var firstTarget = Summon(p, "CS2_182"); Play(g, p, "CATA_306t1", firstTarget);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1 && firstTarget.AttackDamage == 6 && firstTarget.Health == 8
            && firstTarget.CantBeTargetedBySpells, "first half buffs without copying");
        var battlecry = Summon(p, "CS2_189"); int hand = p.HandZone.Count;
        Play(g, p, "CATA_306t2", battlecry);
        var plainCopy = p.BoardZone.Last();
        Core2026Proof.Check(plainCopy.Card.Id == "CS2_189" && plainCopy.AttackDamage == 1 && plainCopy.Health == 1
            && !plainCopy.CantBeTargetedBySpells && p.HandZone.Count == hand, "second half copies without buff or Battlecry draw");
        StandardSetsProof.ParkBoard(p);
        LocationProofs.Location(p); var fullTarget = Summon(p, "CS2_182");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        PlayEntity(g, p, ShatterRules.GiveCombined(p, id, p.Hero), fullTarget);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && fullTarget.AttackDamage == 6 && fullTarget.Health == 8
            && fullTarget.CantBeTargetedBySpells, "Unpinned_SchismCopy: full physical board still buffs, blocks copy");
        StandardSetsProof.ParkBoard(p);
        foreach (string spell in new[] { id, "CATA_306t1", "CATA_306t2" })
        {
            var held = spell == id ? ShatterRules.GiveCombined(p, id, p.Hero) : Add(p, spell);
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id), "no friendly target blocks " + spell);
            p.SetasideZone.Add(p.HandZone.Remove(held));
        }
        CopyRegressions(g, p, id);
    });

    private static void CopyRegressions(Game g, Controller p, string id)
    {
        var failures = new List<string>();
        byte[] baseline = g.SaveSnapshot();
        foreach (string form in new[] { id, "CATA_306t2" })
        foreach (string fixture in new[] { "silenced-aura", "silenced-trigger", "EDR_469", "CORE_BT_156", "attacks" })
        {
            g = Game.LoadSnapshot(baseline); p = g.Player1;
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            try
            {
                var target = Summon(p, fixture switch {
                    "silenced-aura" => "CS2_122", "silenced-trigger" => "NEW1_019", "attacks" => "CS2_182", _ => fixture });
                var neighbor = Summon(p, "CS2_182");
                if (fixture.StartsWith("silenced")) { target.Silence(); g.DeathProcessingAndAuraUpdate(); }
                if (fixture == "EDR_469")
                {
                    p.UsedMana = 0; p.Hero.HeroPower.IsExhausted = false;
                    Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "Sprite awakening Hero Power accepted");
                }
                if (fixture == "CORE_BT_156") { End(g); End(g); End(g); End(g); }
                Minion defender = null;
                if (fixture == "attacks")
                {
                    target.IsRush = true; target.HasWindfury = true;
                    defender = Summon(p.Opponent, "CS2_182"); defender.AttackDamage = 0; defender.BaseHealth = 100;
                    Attack(g, target, defender);
                    Core2026Proof.Check(target.NumAttacksThisTurn == 1, "source really attacked once");
                }
                if (fixture == "EDR_469" || fixture == "CORE_BT_156")
                    Core2026Proof.Check(!target.IsDormant, "legacy target really awakened");
                var spell = form == id ? ShatterRules.GiveCombined(p, id, p.Hero) : Add(p, form);
                bool observed = false;
                void Observe(IEntity entity)
                {
                    var summoned = (Minion)entity; observed = true;
                    if (fixture.StartsWith("silenced"))
                        Core2026Proof.Check(summoned.IsSilenced && summoned.OngoingEffect == null && summoned.ActivatedTrigger == null
                            && neighbor.AttackDamage == 4, "silenced printed aura/trigger suppressed before summon reactions");
                    if (fixture == "attacks")
                        Core2026Proof.Check(summoned.NumAttacksThisTurn == 0 && !summoned.IsExhausted
                            && summoned.IsRush && summoned.HasWindfury, "fresh attack allowance before summon reactions");
                    if (fixture == "EDR_469" || fixture == "CORE_BT_156")
                        Core2026Proof.Check(!summoned.IsDormant && summoned[GameTag.DORMANT] == 0,
                            "awakened copy remains awake before summon reactions");
                }
                g.TriggerManager.AfterSummonTrigger += Observe;
                try { PlayEntity(g, p, spell, target); }
                finally { g.TriggerManager.AfterSummonTrigger -= Observe; }
                var copy = p.BoardZone.Last();
                Core2026Proof.Check(observed && copy.Id != target.Id && copy.Card.Id == target.Card.Id,
                    "real independent copy summoned");
                foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
                {
                    var copied = (Minion)next.IdEntityDic[copy.Id];
                    if (fixture.StartsWith("silenced"))
                    {
                        Core2026Proof.Check(copied.IsSilenced && copied.OngoingEffect == null && copied.ActivatedTrigger == null
                            && copied.AttackDamage == copied.Card.ATK + (form == id ? 2 : 0)
                            && copied.BaseHealth == copied.Card.Health + (form == id ? 3 : 0)
                            && copied.CantBeTargetedBySpells == (form == id),
                            "Unpinned_SchismCopy: silence retained without losing post-silence grants on clone/load");
                        Summon(next.Player1, "CS2_231"); next.DeathProcessingAndAuraUpdate();
                        Core2026Proof.Check(((Minion)next.IdEntityDic[neighbor.Id]).AttackDamage == 4,
                            "silenced copied aura stays off after later summon");
                    }
                    if (fixture == "EDR_469" || fixture == "CORE_BT_156")
                        Core2026Proof.Check(!copied.IsDormant && copied.AppliedEnchantments?.Any(e => e.Card.Id == "EDR_469e") != true,
                            "Unpinned_SchismCopy: awakened legacy copy stays awake without sleeper helper on clone/load");
                    if (fixture == "attacks")
                    {
                        for (int attack = 0; attack < 2; attack++)
                        {
                            var task = next.Player1.Options().SingleOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                                && t.Source?.Id == copied.Id && t.Target?.Id == defender.Id);
                            Core2026Proof.Check(task != null && next.Process(task), "copy's real attack accepted: " + (attack + 1));
                        }
                        Core2026Proof.Check(copied.NumAttacksThisTurn == 2 && copied.IsExhausted
                            && ((Minion)next.IdEntityDic[target.Id]).NumAttacksThisTurn == 1,
                            "Unpinned_SchismCopy: new Windfury copy gets two attacks; original counter unchanged");
                    }
                }
            }
            catch (Exception e) { failures.Add(form + "/" + fixture + ": " + e.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }
}
