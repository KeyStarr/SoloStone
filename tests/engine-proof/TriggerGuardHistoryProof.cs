using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class TriggerGuardHistoryProof
{
    [GroupProof("trigger-guard-history")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (string id in new[] { "END_026", "CORE_BAR_812", "CS3_020", "CORE_CATA_004", "EDR_842", "CAP_103", "EDR_781", "TLC_229", "TLC_830", "TLC_EVENT_400", "TLC_435e", "TLC_442e", "TLC_900e", "TLC_824e", "TLC_836" })
        {
            try { CheckGuard(id); Console.WriteLine("TRIGGER-GUARD " + id + " PASS"); }
            catch (Exception error) { failures.Add(id + ": " + error.Message); Console.WriteLine("TRIGGER-GUARD " + id + " FAIL " + error.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static void CheckGuard(string id)
    {
        var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
        IPlayable source; Action negative;
        if (id is "END_026" or "TLC_836")
        {
            source = Summon(p, id);
            negative = id == "END_026" ? () => Play(g, p, "GAME_005") : () => Play(g, p, "CS2_182");
        }
        else if (id == "EDR_781")
        {
            source = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            negative = () => Generic.Draw(p);
        }
        else if (id is "TLC_229" or "TLC_830" or "TLC_EVENT_400")
        {
            source = Play(g, p, id); negative = () => Play(g, p, "CS2_182");
        }
        else if (id.EndsWith("e"))
        {
            source = Enchantment.GetInstance(p, p.Hero, p, Cards.FromId(id), 999999);
            source.Power.Trigger.Activate(source);
            negative = () => Play(g, p, "CS2_182");
        }
        else if (id is "CAP_103" or "EDR_842")
        {
            source = Play(g, p, id);
            if (id == "CAP_103") negative = () => Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack");
            else { var friend = Summon(p, "CS2_182"); friend.BaseHealth = 100;
                negative = () => new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, p, p.Hero, friend); }
        }
        else if (id == "CORE_BAR_812")
        {
            source = Play(g, p, id); End(g); var attacker = Summon(p.Opponent, "CS2_182");
            negative = () => Attack(g, attacker, p.Hero);
        }
        else if (id == "CS3_020")
        {
            source = Summon(p, id); Play(g, p, "CS2_106"); var victim = Summon(p.Opponent, "CS2_231");
            negative = () => Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, victim)), "lethal hero attack");
        }
        else
        {
            source = Summon(p, id); Summon(p, "CS2_182"); var second = Summon(p, id); var attacker = Summon(p, "CS2_182");
            negative = () => Attack(g, attacker, p.Opponent.Hero);
        }
        int cursor = g.PowerHistory.Full.Count; negative();
        Core2026Proof.Check(!g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryBlockStart>().Any(b => b.BlockType == BlockType.TRIGGER && b.Source == source.Id), "failed eligibility emitted TRIGGER");
    }
}
