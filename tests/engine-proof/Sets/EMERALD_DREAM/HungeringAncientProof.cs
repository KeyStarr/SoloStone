using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HungeringAncientProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_494", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var food = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(food);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        var eater = (Minion)Play(g, p, id);
        int deaths = g.HistoryFacts.Query.Deaths().Count();
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[eater.Id]).AttackDamage == 10 && eater.AttackDamage == 6, "clone eats independently");
        End(g);
        Core2026Proof.Check(eater.AttackDamage == 10 && eater.Health == 12 && food.Zone == p.SetasideZone, "eats only deck minion and gains 4/5");
        Core2026Proof.Check(g.HistoryFacts.Query.Deaths().Count() == deaths, "deck consumption not a minion death");
        Core2026Proof.Check(eater.AppliedEnchantments.Single(x => x.Card.Id == "EDR_494e").CapturedCard.Id == "CS2_182", "exact captured meal enchant");
        End(g); End(g);
        Core2026Proof.Check(eater.AttackDamage == 10 && p.DeckZone.IsEmpty, "spell draw then no-minion deck is no-op");
        int hand = p.HandZone.Count; Kill(g, eater);
        var returned = p.HandZone.Last();
        Core2026Proof.Check(p.HandZone.Count == hand + 1 && returned.Card.Id == "CS2_182" && ((Minion)returned).AttackDamage == 4 && ((Minion)returned).Health == 5, "deathrattle returns consumed definition with pinned stats");
        End(g);
        var silenced = (Minion)Play(g, p, id);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        End(g);
        Core2026Proof.Check(silenced.AttackDamage == 10 && silenced.Health == 12
            && silenced.AppliedEnchantments.Single(x => x.Card.Id == "EDR_494e").CapturedCard.Id == "CS2_182", "silence fixture has eaten and captured a meal");
        new SilenceTask(EntityType.TARGET).Process(g, p, p.Hero, silenced, new TaskStack());
        Core2026Proof.Check(silenced.AttackDamage == 6 && silenced.Health == 7
            && !silenced.AppliedEnchantments.Any(x => x.Card.Id == "EDR_494e") && !silenced.HasDeathrattle, "silence removes meal stats and captured Deathrattle");
        End(g);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        End(g);
        Core2026Proof.Check(p.DeckZone.Count == 1 && silenced.AttackDamage == 6 && silenced.Health == 7, "silence removes eat trigger");
        hand = p.HandZone.Count; Kill(g, silenced);
        Core2026Proof.Check(p.HandZone.Count == hand, "silence removes deathrattle");
        End(g);
        var multi = (Minion)Play(g, p, id);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        End(g); // Eats the supplied Yeti.
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_171")));
        multi.Card.Power.Trigger.SingleTask.Process(g, p, multi, null, new TaskStack());
        Core2026Proof.Check(multi.AttackDamage == 11 && multi.Health == 13, "two meals accumulate current stats");
        var copy = (Minion)Generic.Copy(p, p.Hero, multi, Zone.PLAY);
        hand = p.HandZone.Count;
        Kill(g, copy);
        Core2026Proof.Check(p.HandZone.Skip(hand).Select(x => x.Card.Id).SequenceEqual(new[] { "CS2_171", "CS2_182" }), "copied meal Deathrattles return independently, newest first (live-check order)");
        Core2026Proof.Check(multi.AppliedEnchantments.Count(x => x.Card.Id == "EDR_494e") == 2, "copy death leaves original captures intact");
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        Kill(g, multi);
        Core2026Proof.Check(p.HandZone.IsFull, "full hand burns returned meals without overflow");
    });
}
