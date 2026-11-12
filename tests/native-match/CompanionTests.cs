using System;
using System.Linq;
using System.Collections.Generic;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void CompanionAndLarvaProjection()
    {
        for (int i = 1; i <= 11; i++)
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture);
            var p = match.Game.Player1;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            p.BaseMana = 10; p.UsedMana = 0; p.IsComboActive = true; p.Hero.Damage = 5;
            var installed = p.Hero.HeroPower; installed.IsExhausted = true;
            var larva = Generic.DrawCard(p, Cards.FromId("BE_720t" + i));
            var target = larva.Card.Class == CardClass.MAGE ? p.Opponent.Hero : larva.Card.Class == CardClass.PRIEST ? p.Hero : null;
            FlushHistory(match);
            InvokeProcess(match, PlayCardTask.Any(p, larva, target));
            FlushHistory(match);
            var power = p.GraveyardZone.OfType<HeroPower>().Single();
            var history = NativeHistory(capture);
            Check(history.Any(x => x.HasFullEntity && x.FullEntity.Entity == power.Id
                && x.FullEntity.Name == NativeMatch.TransportCardId(power.Card.Id)), "temporary basic power reveals pinned native identity");
            Check(history.Count(x => x.HasPowerStart && x.PowerStart.Source == power.Id) == 1,
                "triggered basic power has one activation block");
            var tags = ReplayNativeEntityTags(history, power.Id);
            Check(tags[(int)GameTag.CARDTYPE] == (int)CardType.HERO_POWER
                && tags[(int)GameTag.COST] == (larva.Card.Class == CardClass.DEMONHUNTER ? 1 : 2)
                && tags[(int)GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.GRAVEYARD
                && (!tags.TryGetValue((int)GameTag.EXHAUSTED, out int exhausted) || exhausted == 0),
                "triggered basic power native type/cost/retirement/exhaustion");
            var installedTags = ReplayNativeEntityTags(history, installed.Id);
            Check(installedTags[(int)GameTag.EXHAUSTED] == 1 && installedTags[(int)GameTag.COST] == installed.Cost,
                "installed power native cost and exhaustion remain intact");
            Check(p.Hero.HeroPower == installed && installed.IsExhausted && p.UsedMana == 1,
                "native activation preserves installed power and pays only Larva");
            CheckPinnedClientIds(capture); capture.SerializeAll();
        }
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var p = match.Game.Player1;
            p.BaseMana = 10; p.UsedMana = 0;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            var spell = Generic.DrawCard(p, Cards.FromId("MEND_300"));
            FlushHistory(match);
            InvokeProcess(match, PlayCardTask.Spell(p, spell));
            FlushHistory(match);
            var history = NativeHistory(capture);
            foreach (var entry in history)
            {
                var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                Check(entity == null || entity.Tags.All(t => !CompanionRules.IsInternalTag((GameTag)t.Name)), "roster tags do not leak in native entities");
                Check(!entry.HasTagChange || !CompanionRules.IsInternalTag((GameTag)entry.TagChange.Tag), "roster tag changes remain private");
            }
            var frozen = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(spell).Entity;
            foreach (int tag in Enumerable.Range(31070, 4)) frozen.Tags[(GameTag)tag] = 1;
            int cursor = capture.Packets.Count;
            SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                new SabberStoneCore.Kettle.PowerHistoryFullEntity { Entity = frozen },
                new SabberStoneCore.Kettle.PowerHistoryShowEntity { Entity = frozen },
                new SabberStoneCore.Kettle.PowerHistoryChangeEntity { Entity = frozen, CardId = spell.Card.Id }
            });
            Check(NativeHistory(capture, cursor).SelectMany(e => e.HasFullEntity ? e.FullEntity.Tags
                    : e.HasShowEntity ? e.ShowEntity.Tags : e.HasChangeEntity ? e.ChangeEntity.Tags : new List<PegasusGame.Tag>())
                .All(t => !CompanionRules.IsInternalTag((GameTag)t.Name)), "private roster blocked on all three snapshot paths");
            var summon = Generic.DrawCard(p, Cards.FromId("CORE_NEW1_031")); p.UsedMana = 0;
            FlushHistory(match);
            InvokeProcess(match, PlayCardTask.Spell(p, summon));
            FlushHistory(match);
            var beast = p.BoardZone.Single();
            Check(CompanionRules.Roster(p).Contains(beast.Card.Id)
                && NativeHistory(capture).Any(x => x.HasFullEntity && x.FullEntity.Entity == beast.Id && x.FullEntity.Name == beast.Card.Id),
                "native companion summon projects replacement identity");
        }
        Console.WriteLine("PASS companion/Larva native identities, costs, exhaustion, retirement and private roster");
    }
}
