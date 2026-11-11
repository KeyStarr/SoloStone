using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using L = LocationProofs;

internal static class LocationDeathProof
{
    internal static void Run()
    {
        foreach (bool context in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1; var location = L.Location(p);
            location[GameTag.REBORN] = 1;
            int deaths = 0; game.TriggerManager.DeathTrigger += _ => deaths++;
            if (context) new EffectContext(game, p.Id, location.Id).Destroy(location.Id);
            else ((Playable)location).Destroy();
            L.Check(game.DeadBoardObjects.SequenceEqual(new[] { location }), "board-object destruction queued once");
            location.Destroy(); L.Check(game.DeadBoardObjects.Count == 1, "duplicate destroy idempotent");
            var clone = game.CloneExact();
            L.Check(clone.DeadBoardObjects.Count == 1 && clone.DeadBoardObjects[0] != location, "pending deaths cloned");
            bool rejected = false; try { game.SaveSnapshot(); } catch (NotSupportedException) { rejected = true; }
            L.Check(rejected, "pending location death snapshot rejected");
            game.DeathProcessingAndAuraUpdate();
            L.Check(location.Zone == p.GraveyardZone && game.DeadBoardObjects.Count == 0 && p.BoardZone.SlotCount == 0, "destroy retires without Reborn");
            L.Check(deaths == 0 && p.NumFriendlyMinionsThatDiedThisTurn == 0 && game.NumMinionsKilledThisTurn == 0, "location bypasses minion death events/counters");
        }
        AuraRemovedBeforeLocationDeathrattle();
        GrantedDeathrattleAfterRecipe();
        DeathrattleAfterRecipe();
        FinalChargeTiming();
        PinnedRule_MixedDeathOrder();
        Console.WriteLine("PASS location death/final retirement/no Reborn; FinalChargeTiming; GrantedDeathrattleAfterRecipe; PinnedRule_MixedDeathOrder");
    }
    private static void FinalChargeTiming()
    {
        foreach (bool history in new[] { false, true })
        foreach (string id in new[] { "CORE_REV_990", "BE_251", "TIME_044", "TIME_044t1", "TIME_044t2", "JAIL_877" })
        {
            var game = L.Game(history: history); var p = game.Player1; var target = L.Minion(p);
            var definition = Cards.FromId(id).Clone();
            var recipe = definition.Power.LocationActivation;
            BoardObject location = null;
            bool observed = false;
            definition.Power = new Power { LocationActivation = new LocationActivation {
                RequiresFreeSlot = recipe.RequiresFreeSlot, Target = recipe.Target,
                Task = EffectContext.Task(context => {
                    L.Check((location.Zone == p.GraveyardZone) == recipe.RequiresFreeSlot,
                        "only summon final charges clear before the effect");
                    L.Check(recipe.RequiresFreeSlot || location.Durability == 1, "non-summon retains final charge during effect");
                    var result = context.RunLegacy(recipe.Task);
                    L.Check((location.Zone == p.BoardZone) != recipe.RequiresFreeSlot,
                        "non-summon remains on board throughout effect");
                    observed = true;
                    return result;
                }) } };
            location = (BoardObject)Entity.FromCard(p, definition, zone:p.BoardZone);
            location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1;
            L.Use(location, recipe.Target == LocationTargetKind.Minion ? target : null);
            L.Check(observed && location.Zone == p.GraveyardZone && location[GameTag.DAMAGE] == 0 && location.Card.Id == id &&
                game.DeadBoardObjects.Count == 0, "final effect completes then retires, clears damage, never advances");
            if (!history) continue;
            var entries = game.PowerHistory.Last.ToList();
            int deaths = entries.FindIndex(e => e is PowerHistoryBlockStart b && b.BlockType == BlockType.DEATHS);
            int spent = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == location.Id && t.Tag == GameTag.DAMAGE && t.Value == location.Card.Health);
            int position = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == location.Id && t.Tag == GameTag.ZONE_POSITION && t.Value == 0);
            int zone = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == location.Id && t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD);
            int reset = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == location.Id && t.Tag == GameTag.DAMAGE && t.Value == 0);
            L.Check(spent >= 0 && deaths > spent && position > deaths && zone > position && reset > zone,
                "final-charge DEATHS: ZONE_POSITION=0, GRAVEYARD, DAMAGE=0");
        }
    }
    private static void AuraRemovedBeforeLocationDeathrattle()
    {
        foreach (bool mixed in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            int spellPowerAtDeathrattle = -1;
            var definition = Cards.FromId("JAIL_877").Clone();
            definition.Power = new Power {
                Aura = new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.CONTROLLER,
                    new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 3)),
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => spellPowerAtDeathrattle = c.CurrentSpellPower)
            };
            var location = (BoardObject)Entity.FromCard(p, definition, zone: p.HandZone);
            L.Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, location)), "play aura location");
            location.HasDeathrattle = true;
            L.Check(p.CurrentSpellPower == 3, "location aura active");
            if (mixed) L.Minion(p).Destroy();
            location.Destroy(); game.DeathProcessingAndAuraUpdate();
            L.Check(spellPowerAtDeathrattle == 0 && p.CurrentSpellPower == 0,
                "location aura removed before Deathrattle, mixed=" + mixed);
        }
    }

    private static void GrantedDeathrattleAfterRecipe()
    {
        var game = L.Game(); var p = game.Player1;
        var location = L.Location(p, "JAIL_877"); location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1;
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(game, Cards.FromId("EX1_158e"), p.Hero, location);
        L.Use(location);
        L.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "JAIL_877t", "EX1_158t" }), "granted Deathrattle resolves once after recipe");
        var history = game.PowerHistory.Last.OfType<PowerHistoryFullEntity>().Select(e => game.IdEntityDic[e.Entity.Id].Card.Id).ToArray();
        L.Check(Array.IndexOf(history, "EX1_158t") > Array.IndexOf(history, "JAIL_877t"), "granted Deathrattle entity created after Rat");
        L.Check(game.Step == Step.MAIN_ACTION && game.DeadBoardObjects.Count == 0 && game.SaveSnapshot().Length > 0, "granted Deathrattle drains before snapshot boundary");
        game.DeathProcessingAndAuraUpdate();
        L.Check(p.BoardZone.Count == 2, "granted Deathrattle cannot resolve twice");
    }
    private static void DeathrattleAfterRecipe()
    {
        var game = L.Game(); var p = game.Player1;
        L.Minion(p, "FP1_031"); // Baron must not multiply a location's Deathrattle.
        var definition = Cards.FromId("JAIL_877").Clone();
        definition.Power = new Power { LocationActivation = definition.Power.LocationActivation,
            DeathrattleTask = Cards.FromId("JAIL_877t").Power.DeathrattleTask };
        var location = (BoardObject)Entity.FromCard(p, definition, zone: p.BoardZone);
        location.HasDeathrattle = true; location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1;
        int deck = p.DeckZone.Count;
        L.Use(location);
        var rat = p.BoardZone.Single(m => m.Card.Id == "JAIL_877t");
        L.Check(p.DeckZone.Count == deck - 1, $"location Deathrattle runs once despite Baron deck={p.DeckZone.Count} before={deck} has={location.HasDeathrattle}");
        var history = game.PowerHistory.Last.ToList();
        int summon = history.FindIndex(e => e is PowerHistoryFullEntity f && f.Entity.Id == rat.Id);
        int draw = history.FindIndex(e => e is PowerHistoryTagChange t && t.Tag == GameTag.ZONE && t.Value == (int)Zone.HAND);
        L.Check(summon >= 0 && draw > summon, "final-charge location Deathrattle after activation recipe");
        game.DeathProcessingAndAuraUpdate();
        L.Check(p.DeckZone.Count == deck - 1, "retirement cannot requeue Deathrattle");
    }
    private static void PinnedRule_MixedDeathOrder()
    {
        foreach (bool locationFirst in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            BoardObject location; Minion minion;
            if (locationFirst) { location = L.Location(p); minion = L.Minion(p); }
            else { minion = L.Minion(p); location = L.Location(p); }
            game.DeathProcessingAndAuraUpdate();
            int deaths = 0; game.TriggerManager.DeathTrigger += _ => deaths++;
            var expected = new IPlayable[] { minion, location }.OrderBy(e => e.OrderOfPlay).Select(e => e.Id).ToArray();
            game.PowerHistory.Last.Clear(); location.Destroy(); minion.Destroy(); game.DeathProcessingAndAuraUpdate();
            var actual = game.PowerHistory.Last.OfType<PowerHistoryTagChange>().Where(t => t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD).Select(t => t.EntityId);
            L.Check(actual.SequenceEqual(expected), "mixed deaths ordered by ORDER_OF_PLAY");
            L.Check(deaths == 1 && game.NumMinionsKilledThisTurn == 1 && p.NumFriendlyMinionsThatDiedThisTurn == 1, "only minion counted/observed");
        }
    }
}
