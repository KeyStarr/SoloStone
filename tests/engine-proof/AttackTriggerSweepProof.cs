using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Triggers;

// Born from a live bug: Wrathspike Brute animated on every friendly attack and (reported) never dealt its damage. Its
// AFTER_ATTACK listener fired for all attacks and checked the defender inside the task, which left an empty TRIGGER
// that the client still animated, and a "not dead" guard that blocked retaliation when the Brute took lethal damage.
// A sweep of every card with an attack-event trigger found 3 broken (Brute, SI:7 Supplier, Rehgar). Every such card
// now gets a negative control here: an unrelated attack must change no effect state.
internal static class AttackTriggerSweepProof
{
    internal static IEnumerable<Trigger> Triggers(Trigger trigger) => trigger is MultiTrigger multi
        ? multi.SelectMany(Triggers) : trigger == null ? Enumerable.Empty<Trigger>() : new[] { trigger };

    // Cards the trigger-member scan below cannot find, added by hand so the negative control still covers them.
    private static readonly string[] EventGuardCards = {
        "EDR_487", "TLC_821", "TIME_217", "TIME_214", "TTN_737e", "TLC_229", "TLC_830", "TLC_EVENT_400", "END_026",
        "TLC_435e", "TLC_442e", "TLC_824e", "TLC_900e", "TLC_836", "EDR_781", "TID_712"
    };
    internal static string[] Members() => OfflinePoolPolicy.Load().Members.Select(m => m.CardId)
        .Concat(EffectModules.ByDbfId.Keys.Select(key => Cards.FromAssetId(key).Id))
        .Concat(LocalCardCatalog.Current.Select(c => c.Id)).Concat(LocalCardCatalog.SupplementalGeneratedIds)
        .Distinct().Order(StringComparer.Ordinal).Where(id => Triggers(Cards.FromId(id).Power?.Trigger).Any(t =>
            t.Type is TriggerType.ATTACK or TriggerType.AFTER_ATTACK or TriggerType.TARGET
            || t.TriggerSource is TriggerSource.FRIENDLY_SPELL_CASTED_ON_THE_OWNER or TriggerSource.FRIENDLY_SPELL_CASTED_ON_OWN_MINIONS or TriggerSource.FRIENDLY_EVENT_SOURCE)).Concat(EventGuardCards).Distinct().Order(StringComparer.Ordinal).ToArray();

    internal static void PositiveControls() => StandardSetsProof.Run(selectedProofs: new Action[] {
        WrathspikeBruteProof.Run, IllidariInquisitorProof.Run, OasisAllyProof.Run, DefiledSpearProof.Run,
        HandCannonProof.Run, SI7SupplierProof.Run, B15CombatContextCORECardsProof.Run, B14F1EmeraldDreamProof.Run,
        WallowTheWretchedProof.Run, FragmentofNothingProof.Run, B15QuestClosureCardsProof.Run,
        B16C16FoodChainProof.Run, EvHooksCardsProof.Run, CryptMapProof.Run, SubmergedMapProof.Run,
        HiveMapProof.Run, B15PoolFiltersTHELOSTCITYProof.Run, Unpinned_NirioftheCraterProof.Run
    });

    [GroupProof("attack-trigger-negatives")]
    internal static void NegativeControls()
    {
        var failures = new List<string>();
        foreach (string id in Members())
        foreach (int seat in new[] { 1, 2 })
        {
            try { Negative(id, seat); Console.WriteLine($"ATTACK-NEGATIVE {id} seat={seat} PASS"); }
            catch (Exception error) { failures.Add(id + "/" + seat + ": " + error.Message); Console.WriteLine($"ATTACK-NEGATIVE {id} seat={seat} FAIL {error.Message}"); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS attack trigger negatives: {Members().Length} cards x both seats; real combat, effect-state assertions");
    }

    private static void Negative(string id, int seat)
    {
        var g = new Game(new GameConfig { RandomSeed = 123, StartPlayer = seat, SkipMulligan = true, History = true,
            Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); var p = g.CurrentPlayer; var card = Cards.FromId(id);
        IPlayable source;
        var activation = Triggers(card.Power?.Trigger).FirstOrDefault()?.TriggerActivation ?? TriggerActivation.PLAY;
        if (activation is TriggerActivation.HAND or TriggerActivation.HAND_OR_DECK) source = Core2026Proof.Add(p, id);
        else if (card.Type == CardType.MINION) source = Core2026Proof.Summon(p, id);
        else if (card.Type == CardType.WEAPON) { new WeaponTask(id).Process(g, p, p.Hero, null); source = p.Hero.Weapon; }
        else if (card.Type == CardType.ENCHANTMENT)
        {
            var recipient = Core2026Proof.Summon(p, "CS2_182");
            source = Enchantment.GetInstance(p, p.Hero, recipient, card);
            card.Power.Trigger.Activate(source);
            Core2026Proof.Check(source.ActivatedTrigger != null, "enchantment listener is active");
        }
        else source = card.IsSecret || id is "TLC_229" or "TLC_830" or "TLC_EVENT_400" ? Core2026Proof.Play(g, p, id) : Core2026Proof.Add(p, id);
        if (id == "CORE_CFM_670") ((Minion)source).Silence(); // Global random targeting has no wrong event source; Silence is its negative.
        // Keep Rehgar far from the unrelated attacker; non-Stealth/non-Mech controls cover Shaw/Slayer/Gaia.
        if (id == "CORE_CATA_004") Core2026Proof.Summon(p, "CS2_182");
        var attacker = id == "TLC_811" ? (Minion)source : Core2026Proof.Summon(p, "CS2_182");
        var defender = Core2026Proof.Summon(p.Opponent, "CS2_182"); defender.BaseHealth = 100;
        attacker.BaseHealth = 100;
        string State() => string.Join("|", g.IdEntityDic.Values.OfType<IPlayable>()
            .Where(e => e.Id != attacker.Id && e.Id != defender.Id && e.Zone != null)
            .OrderBy(e => e.Id).Select(e => e.Id + ":" + e.Card.Id + ":" + e.Zone.Type + ":" + e.Cost
                + (e is ICharacter c ? $":{c.AttackDamage}:{c.Health}:{c.Damage}:{c.IsFrozen}" : "")
                + (e is Hero h ? ":" + h.Armor : "")
                + (e is Spell spell ? ":" + spell.QuestProgress : "")));
        string before = State(); Core2026Proof.Attack(g, attacker, defender);
        Core2026Proof.Check(State() == before && p.Choice == null && p.Opponent.Choice == null, "unrelated attack changed effect state");
    }

    internal static void Inventory()
    {
        foreach (string id in Members())
        {
            var card = Cards.FromId(id);
            if (id == "TID_712")
                Console.WriteLine("ATTACK-CARD " + JsonSerializer.Serialize(new { id, card.Name, type = "ATTACK replacement", source = "SELF", text = card.Text }));
            foreach (var trigger in Triggers(card.Power?.Trigger))
                Console.WriteLine("ATTACK-CARD " + JsonSerializer.Serialize(new { id, card.Name, type = trigger.Type.ToString(), source = trigger.TriggerSource.ToString(), text = card.Text }));
        }
    }
}
