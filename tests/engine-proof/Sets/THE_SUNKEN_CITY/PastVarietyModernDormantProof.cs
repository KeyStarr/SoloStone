using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class PastVarietyModernProof
{
    [GroupProof("pastvar-modern-dormant")]
    internal static void Unpinned_DormantExclusions()
    {
        // card-state/dormant-{aoe,friendly-counts,board-slots}: shipped guesses, not retail pins.
        var failures = new List<string>();
        foreach (string id in new[] { "TSC_059", "ETC_373", "SCH_609" })
        foreach (bool second in new[] { false, true })
        foreach (bool pupil in id == "TSC_059" ? new[] { false } : new[] { false, true }) {
            try { DormantBuff(id, second, pupil); }
            catch (Exception error) {
                string failure = $"{id} seat={(second ? 2 : 1)} pupil={pupil}: {error.Message}";
                Console.WriteLine("FAIL Unpinned_DormantExclusions " + failure);
                failures.Add(failure);
            }
        }
        DormantCapacityAndHandCounts();
        Core2026Proof.Check(failures.Count == 0, "Unpinned_DormantExclusions: " + string.Join("; ", failures));
        Console.WriteLine("PASS Unpinned_DormantExclusions: both seats, direct/Pupil buffs, awakening, clone/snapshot, capacity and hand-only counts");
    }

    private static void DormantBuff(string id, bool second, bool pupil)
    {
        var g = Fresh(23, pupil ? CandidatePools(id) : null);
        if (second) Core2026Proof.End(g);
        var p = g.CurrentPlayer;
        var active = Core2026Proof.Summon(p, "YOD_010");
        var sleeper = Core2026Proof.Summon(p, "YOD_010");
        var enemy = Core2026Proof.Summon(p.Opponent, "YOD_010");
        var enemySleeper = Core2026Proof.Summon(p.Opponent, "YOD_010");
        DormantRules.Make(sleeper, 2, "TIME_058e", p.Hero);
        DormantRules.Make(enemySleeper, 2, "TIME_058e", p.Opponent.Hero);
        var held = (Minion)Core2026Proof.Add(p, "YOD_010");
        var deck = (Minion)Entity.FromCard(p, Cards.FromId("YOD_010"), zone: p.DeckZone);
        IPlayable source;
        if (pupil) {
            Core2026Proof.Play(g, p, "TIME_704");
            Answer(g, p.Choice.Choices.Single());
            source = p.HandZone.Single(c => c.Card.Id == "TIME_704t");
            if (id == "ETC_373") {
                // Pin only the automatic Choose One result, without changing its RNG consumption.
                int seed = 1;
                do { g.SetRandomSeed(seed++); } while (g.Random.Clone().Next(1, 3) != 2);
            }
        } else source = Generic.DrawCard(p, Definition(g, p, id));
        Resume(g, next => {
            var owner = next.CurrentPlayer;
            Play(next, owner, next.IdEntityDic[source.Id], !pupil && id == "ETC_373" ? 2 : 0);
            var recipient = (Minion)next.IdEntityDic[active.Id];
            int delta = id == "SCH_609" ? 4 : id == "ETC_373" ? 2 : 0;
            Core2026Proof.Check(recipient.AttackDamage == 2 + delta && recipient.Health == (id == "TSC_059" ? 2 : 6)
                && recipient.HasTaunt == (id != "SCH_609") && recipient.HasDivineShield == (id == "TSC_059"), "active recipient still receives exact effect " + id);
            foreach (int entityId in new[] { held.Id, deck.Id }) {
                var offBoard = (Minion)next.IdEntityDic[entityId];
                Core2026Proof.Check(offBoard.AttackDamage == (id == "SCH_609" ? 6 : 2)
                    && offBoard.Health == (id == "SCH_609" ? 6 : 2) && !offBoard.HasTaunt && !offBoard.HasDivineShield,
                    "hand/deck recipients retain correct scope " + id);
            }
            Resume(next, fork => {
                foreach (int entityId in new[] { sleeper.Id, enemySleeper.Id }) {
                    var sleeping = (Minion)fork.IdEntityDic[entityId];
                    Core2026Proof.Check(sleeping.IsDormant, "effect does not awaken sleeper " + id);
                    DormantRules.Awaken(sleeping);
                    fork.DeathProcessingAndAuraUpdate();
                    Core2026Proof.Check(sleeping.AttackDamage == 2 && sleeping.Health == 2 && !sleeping.HasTaunt && !sleeping.HasDivineShield
                        && sleeping.AppliedEnchantments.All(e => e.Card.Id != "ETC_373be" && e.Card.Id != "SCH_609e"),
                        "Unpinned_DormantExclusions sleeper remains unchanged after awakening " + id);
                }
                var opponent = (Minion)fork.IdEntityDic[enemy.Id];
                Core2026Proof.Check(opponent.AttackDamage == 2 && opponent.Health == 2 && !opponent.HasTaunt && !opponent.HasDivineShield,
                    "opponent active minion excluded " + id);
            });
        });
    }

    private static void DormantCapacityAndHandCounts()
    {
        foreach (bool second in new[] { false, true })
        foreach (string id in new[] { "ONY_021", "ETC_373", "DEEP_028", "TSC_656" })
        foreach (int occupied in new[] { 6, 7 }) {
            var g = Fresh();
            if (second) Core2026Proof.End(g);
            var p = g.CurrentPlayer;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            for (int i = 0; i < occupied; i++) {
                var sleeper = Core2026Proof.Summon(p, "YOD_010");
                DormantRules.Make(sleeper, 2, "TIME_058e", p.Hero);
            }
            var spell = Generic.DrawCard(p, Definition(g, p, id));
            Resume(g, next => {
                var owner = next.CurrentPlayer;
                if (occupied == 7 && (id == "ONY_021" || id == "ETC_373")) {
                    owner.BaseMana = 10;
                    Core2026Proof.Check(!owner.Options().Any(t => t.Source?.Id == spell.Id && (id == "ONY_021" || t.ChooseOne == 1)),
                        "all-Dormant full board blocks manual summon-only branch " + id);
                    Generic.CastAutomaticSpell(owner, next, (Spell)next.IdEntityDic[spell.Id], null, id == "ETC_373" ? 1 : 0);
                    next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
                } else Play(next, owner, next.IdEntityDic[spell.Id], id == "ETC_373" ? 1 : 0);
                string token = id == "ONY_021" ? "ONY_001t" : id == "ETC_373" ? "ETC_373t" : id == "DEEP_028" ? "DEEP_028t" : "TSC_656t";
                var summoned = owner.BoardZone.GetAll();
                Core2026Proof.Check(owner.BoardZone.SlotCount == 7 && summoned.Length == 7 - occupied
                    && summoned.All(m => m.Card.Id == token && m.AttackDamage == (id == "ONY_021" || id == "ETC_373" ? 2 : id == "DEEP_028" ? 3 : owner.HandZone.Count)
                        && m.Health == (id == "ONY_021" ? 1 : id == "ETC_373" ? 2 : id == "DEEP_028" ? 7 : owner.HandZone.Count)
                        && m.HasTaunt == (id == "DEEP_028" || id == "TSC_656") && m.IsRush == (id == "ONY_021")),
                    "Dormant occupants consume physical summon slots, exact token outcome " + id);
                Core2026Proof.Check(owner.BoardZone.Count(m => m.IsDormant) == occupied, "summons preserve every sleeper " + id);
                if (id == "TSC_656") Core2026Proof.Check(owner.HandZone.Count == 3, "Miracle still draws three with sleepers/full board");
            });
        }
        foreach (bool second in new[] { false, true })
        foreach (bool holding in new[] { false, true }) {
            var g = Fresh(); if (second) Core2026Proof.End(g);
            var p = g.CurrentPlayer;
            var sleeper = Core2026Proof.Summon(p, "YOD_010");
            DormantRules.Make(sleeper, 2, "TIME_058e", p.Hero);
            if (holding) Core2026Proof.Add(p, "YOD_010");
            var wing = Core2026Proof.Add(p, "NX2_022");
            Resume(g, next => {
                Play(next, next.CurrentPlayer, next.IdEntityDic[wing.Id]);
                Core2026Proof.Check(((Minion)next.IdEntityDic[wing.Id]).HasWindfury == holding,
                    "Goldwing counts only held Mechs, regardless of Dormant board Mechs");
            });
        }
    }
}
