using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B15DrawReplacementProof;

internal static class B15DrawReplacementQuestProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_513", Unpinned_LieInWait);
    private static void Unpinned_LieInWait(Game g, Controller p, string id)
    {
        // One multi-card insertion is one operation; the three physical cards do not finish the quest.
        var quest = (Spell)Play(g, p, id);
        new AddCardTo("CS2_231", EntityType.DECK, 3).Process(g, p, p.Hero, null);
        g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 1, "multi-card shuffle counts one occurrence");
        Generic.ShuffleIntoDeck(p, p.Opponent.Hero, Entity.FromCard(p, Cards.FromId("CS2_231")));
        g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 1, "opponent shuffling into your deck does not advance your quest");
        for (int i = 0; i < 3; i++)
            Generic.ShuffleIntoDeck(p, p.Hero, Entity.FromCard(p, Cards.FromId("CS2_231")));
        g.ProcessTasks();
        Core2026Proof.Check(quest.QuestProgress == 4 && !p.HandZone.Any(c => c.Card.Id == "TLC_513t"), "four operations remain incomplete");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1;
            Generic.ShuffleIntoDeck(owner, owner.Hero, Entity.FromCard(owner, Cards.FromId("CS2_231")));
            next.ProcessTasks();
            Core2026Proof.Check(((Spell)next.IdEntityDic[quest.Id]).QuestProgress == 5
                && owner.HandZone.Count(c => c.Card.Id == "TLC_513t") == 1, "fifth operation grants one reward after clone/load");
        }
        NinjaCapacity();
        var weapon = Play(g, p, "CS2_106");
        p.Hero.NumAttacksThisTurn = 1;
        p.Hero.HeroPower.IsExhausted = true;
        int health = p.Hero.Health, armor = p.Hero.Armor;
        var reward = p.HandZone.Single(c => c.Card.Id == "TLC_513t");
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, reward)), "Master Dusk play");
        Core2026Proof.Check(p.Hero.Card.Id == "TLC_513t" && p.Hero.Health == health
            && p.Hero.Armor == armor + 8 && p.Hero.Weapon == weapon && p.Hero.NumAttacksThisTurn == 1
            && p.Hero.HeroPower.Card.Id == "TLC_513hp" && !p.Hero.HeroPower.IsExhausted,
            "Unpinned_Dusk preserves spent attack/weapon/health and refreshes new power");
        var ninjas = p.BoardZone.Where(m => m.Card.Id == "TLC_513t2").ToArray();
        Core2026Proof.Check(ninjas.Length == 2 && ninjas.All(n => n.AttackDamage == 3 && n.Health == 3 && n.HasStealth),
            "Dusk summons two 3/3 Stealth Ninjas");
        ninjas[0].Silence(); Kill(g, ninjas[0]);
        var recycled = p.DeckZone.Last(c => c.Card.Id == "TLC_513t2");
        Core2026Proof.Check(recycled.Id == ninjas[0].Id && recycled.Card[(GameTag)3128] == 1,
            "persistent hero effect recycles even a Silenced Ninja as the reset summoned-when-drawn original");
        EmptyDeck(p);
        var original = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        // Generated token has the existing summon-on-draw recipe; full board consumes it and still draws replacement.
        var ninja = Entity.FromCard(p, Cards.FromId("TLC_513t2"), zone: p.DeckZone);
        Generic.Draw(p);
        Core2026Proof.Check(ninja.Zone == p.BoardZone && original.Zone == p.HandZone, "Ninja summons and replacement draws from recipient deck");
        // Use a real initial-deck entity as the negative provenance control.
        var initial = g.IdEntityDic.Values.First(c => c.Card.Id == "CS2_182"
            && c.Controller == p && g.HistoryFacts.Query.StartedInDeck(c.Id, p.Id) && c.Zone == p.SetasideZone);
        p.SetasideZone.Remove(initial); p.DeckZone.Add(initial);
        var generated = new[] { "CS2_029", "CS2_029", "CS2_182", "CS2_231" }
            .Select(card => Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone).Id).ToList();
        g.ProcessTasks();
        g.DeathProcessingAndAuraUpdate();
        for (int seed = 1; seed <= 4; seed++)
        {
            var seeded = g.CloneExact(history: true);
            seeded.SetRandomSeed(seed);
            // Independent physical bucket preserves duplicate weighting and detects first-card selection.
            var remaining = generated.ToList();
            var expected = new List<int>();
            var rng = new SabberStoneCore.Model.Util.DeepCloneableRandom(seed);
            for (int i = 0; i < 2; i++)
            {
                int pick = rng.Next(remaining.Count);
                expected.Add(remaining[pick]); remaining.RemoveAt(pick);
            }
            foreach (var variant in new[] { seeded, seeded.CloneExact(history: true), Game.LoadSnapshot(seeded.SaveSnapshot()) })
            {
                var owner = variant.Player1;
                int handCount = owner.HandZone.Count;
                owner.UsedMana = 0;
                Core2026Proof.Check(variant.HistoryFacts.Query.StartedInDeck(initial.Id, owner.Id)
                    && generated.All(entity => !variant.HistoryFacts.Query.StartedInDeck(entity, owner.Id)),
                    "Way of the Shell preserves initial/generated provenance across clone/load");
                Core2026Proof.Check(variant.Process(HeroPowerTask.Any(owner))
                    && owner.HandZone.Skip(handCount).Select(c => c.Id).SequenceEqual(expected)
                    && owner.DeckZone.Select(c => c.Id).SequenceEqual(new[] { initial.Id }.Concat(remaining)),
                    "Way of the Shell draws two seeded ordered physical cards, excluding starting-deck entities across clone/load: " + seed);
            }
        }
    }
    private static void NinjaCapacity()
    {
        var fullBoard = NewGame(); var owner = fullBoard.Player1; EmptyDeck(owner);
        for (int i = 0; i < 7; i++) Summon(owner, "CS2_182");
        var ninja = Entity.FromCard(owner, Cards.FromId("TLC_513t2"), zone: owner.DeckZone);
        Generic.Draw(owner);
        Core2026Proof.Check(ninja.Zone == owner.GraveyardZone && owner.BoardZone.Count == 7 && owner.Hero.Damage == 1,
            "full board consumes summoned-when-drawn Ninja and replacement draw fatigues");
        var fullHand = NewGame(); owner = fullHand.Player1; EmptyDeck(owner);
        while (!owner.HandZone.IsFull) Add(owner, "CS2_182");
        ninja = Entity.FromCard(owner, Cards.FromId("TLC_513t2"), zone: owner.DeckZone);
        Generic.Draw(owner);
        Core2026Proof.Check(ninja.Zone == owner.GraveyardZone && owner.BoardZone.IsEmpty && owner.Hero.Damage == 0,
            "full-hand Ninja burns without summon or replacement draw");
    }
}
