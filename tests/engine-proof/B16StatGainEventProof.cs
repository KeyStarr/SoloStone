using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16StatGainEventProof
{
    internal static IEnumerable<Game> Futures(Game g) => new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g };
    [GroupProof("b16-stat-gain-event")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool opponent in new[] { false, true })
        {
            var g = B14H1StatsProof.NewGame(history);
            var p = opponent ? g.Player2 : g.Player1;
            var m = Summon(p, "CS2_182");
            var events = new List<StatGainedEvent>();
            TriggerManager.TriggerHandler observer = _ => { };
            g.TriggerManager.StatGainedTrigger += observer;
            g.TriggerManager.Events.Trace = (value, handler, index) => {
                if (value is StatGainedEvent gain && index == 0) events.Add(gain);
            };
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_092e"), p.Hero, m);
            Core2026Proof.Check(events.Count == 1 && events[0].Attack == 4 && events[0].Health == 4
                && events[0].TargetId == m.Id && events[0].SourceId == p.Hero.Id
                && events[0].SourceControllerId == p.Id && events[0].EffectId == "CS2_092e" && events[0].Zone == Zone.PLAY,
                "completed combined grant has one attributed payload on either side");
            new ApplyEffectTask(EntityType.TARGET, Effects.Attack_N(2), Effects.Health_N(3)).Process(g, p, p.Hero, m);
            Core2026Proof.Check(events.Count == 2 && events[1].Attack == 2 && events[1].Health == 3, "direct effect batch is one operation");
            Effects.Attack_N(1).ApplyTo(m);
            Core2026Proof.Check(events.Count == 3 && events[2].Attack == 1 && events[2].Health == 0, "standalone generic stat effect publishes");
            int count = events.Count;
            var aura = Effects.Attack_N(8); aura.ApplyAuraTo(m); aura.RemoveAuraFrom(m);
            m.Damage = 2; m.TakeFullHeal(p.Hero);
            new Enchant(Effects.Attack_N(-1), Effects.Health_N(-1)).ActivateTo(m);
            Core2026Proof.Check(events.Count == count, "aura, damage, healing and losses are not gains");
            new Enchant(Effects.Attack_N(3), Effects.Health_N(-1)).ActivateTo(m);
            Core2026Proof.Check(events.Count == count + 1 && events[^1].Attack == 3 && events[^1].Health == 0, "mixed grant filters negative components");
            var temp = new Enchant(Effects.Attack_N(2), Effects.Health_N(1)) { IsOneTurnEffect = true };
            temp.ActivateTo(m); count = events.Count;
            End(g);
            Core2026Proof.Check(events.Count == count, "temporary expiry never publishes a gain");
            var copy = (Minion)Generic.Copy(p, p.Hero, m, Zone.HAND);
            Core2026Proof.Check(events.Count == count, "copy construction is not a gain");
            g.TriggerManager.StatGainedTrigger -= observer;
            g.TriggerManager.Events.Trace = null;
            foreach (var future in Futures(g))
            {
                var copied = (Minion)future.IdEntityDic[copy.Id];
                int attack = copied.AttackDamage;
                new Enchant(Effects.Attack_N(1)).ActivateTo(copied);
                Core2026Proof.Check(copied.AttackDamage == attack + 1 && copied.NativeTags.Keys.All(t => !StatGainRules.IsInternalTag(t)),
                    "operation state is clear and future buffs work after clone/snapshot");
            }
        }
        Console.WriteLine("PASS stat-gain operations: attribution, grouping, exclusions, both seats/history modes, clone/snapshot");
    }
}
