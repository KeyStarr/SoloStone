using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Issue #152: a switching AdaptiveEffect (a tag SET on a condition: CANT_ATTACK, CHARGE, TAUNT, LIFESTEAL, IMMUNE)
    // wrote the new value straight into the entity and sent nothing, while its removal sent the printed value. The client
    // then held the printed value (Specter of Despair "can't attack" 0 while the engine held 1), the test-control audit
    // saw `tag:71:227:0!=1`, and a lane wedged. Every switching Adaptive card is played and its condition flipped both
    // ways; after each step the tags the client reconstructs from the wire must equal the engine's effective values.
    private static IEnumerable<GameTag> SwitchingAdaptiveTags(IAura aura)
    {
        if (aura is MultiAura multi)
        {
            foreach (var child in multi) foreach (var tag in SwitchingAdaptiveTags(child)) yield return tag;
            yield break;
        }
        if (aura is AdaptiveEffect effect
            && (bool)typeof(AdaptiveEffect).GetField("_isSwitching", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect))
            yield return (GameTag)typeof(AdaptiveEffect).GetField("_tag", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);
    }

    private static void AdaptiveTagHistory()
    {
        var cards = Cards.All.Where(c => c.Power?.Aura != null && SwitchingAdaptiveTags(c.Power.Aura).Any())
            .OrderBy(c => c.Id).ToList();
        var failures = new List<string>();
        var untested = new List<string>();
        int played = 0, steps = 0;
        foreach (var card in cards)
        {
            if (card.Type != CardType.MINION) { untested.Add(card.Id + ":" + card.Type); continue; }
            var tags = SwitchingAdaptiveTags(card.Power.Aura).Distinct().ToArray();
            var (match, capture) = AnimationMatch(false);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId(card.Id), zone: owner.HandZone);
            FlushFieldHistory(match);
            var play = owner.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
            if (play == null) { untested.Add(card.Id + ":unplayable"); continue; }
            int cursor = capture.Packets.Count;
            InvokeProcess(match, play);
            played++;
            void Step(string label)
            {
                game.DeathProcessingAndAuraUpdate();
                FlushFieldHistory(match);
                steps++;
                var client = ReplayNativeEntityTags(NativeHistory(capture), source.Id);
                foreach (var tag in tags)
                {
                    int engine = source[tag];
                    int wire = client.TryGetValue((int)tag, out int sent) ? sent : source.Card[tag];
                    if (engine != wire) failures.Add($"{card.Id} {label} {tag}: engine {engine}, client {wire}");
                    var changes = History(capture, cursor).Where(e => e.HasTagChange
                        && e.TagChange.Entity == source.Id && e.TagChange.Tag == (int)tag).ToArray();
                    // RemoveFrom publishes printed resets; count the applied effective value separately.
                    int applied = changes.Count(e => e.TagChange.Value != source.Card[tag]);
                    if (applied > 1) failures.Add($"{card.Id} {label} {tag}={engine}: {applied} effective tag-change rows");
                }
                cursor = capture.Packets.Count;
            }
            Step("played");
            source.Damage = 1; Step("damaged");
            source.Damage = 0; Step("healed");
            owner.Hero.Armor = 10; Step("armor 10");
            owner.Hero.Armor = 0; Step("armor 0");
            source.AttackDamage += 3; Step("attack +3");
            if (card.Id == "GIL_685") Check(source.HasTaunt && source.HasLifeSteal, "Paragon switches on at attack >= 3");
            source.AttackDamage -= 3; Step("attack back");
            if (card.Id == "GIL_685") Check(!source.HasTaunt && !source.HasLifeSteal, "Paragon switches off at attack < 3");
            var axe = (Weapon)EngineEntity.FromCard(owner, Cards.FromId("CS2_106"), zone: owner.HandZone);
            FlushFieldHistory(match);
            owner.UsedMana = 0;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == axe.Id));
            Step("weapon equipped");
            if (owner.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK) is { } attack)
            { InvokeProcess(match, attack); Step("hero attacked"); }
            axe.Destroy(); Step("weapon gone");
            var spell = EngineEntity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.HandZone);
            FlushFieldHistory(match);
            owner.UsedMana = 0;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id));
            Step("spell played");
            var geomancer = EngineEntity.FromCard(owner, Cards.FromId("CS2_142"), zone: owner.BoardZone);
            Step("spell power");
            foreach (var other in owner.BoardZone.Where(m => m.Id != source.Id).ToArray()) other.Destroy();
            Step("other minions gone");
        }
        Console.WriteLine($"adaptive tags: {cards.Count} cards ({string.Join(" ", cards.Select(c => c.Id))}), {played} played, {steps} states; not exercised: {string.Join(", ", untested)}");
        Check(played >= 10, "adaptive tag sweep played the minions");
        Check(failures.Count == 0, "switching adaptive tag reaches the client as the engine value: " + string.Join("; ", failures.Take(40)));
        Console.WriteLine("PASS switching adaptive effects publish effective tags once; Paragon switches both ways");
    }
}
