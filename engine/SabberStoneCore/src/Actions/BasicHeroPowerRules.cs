using System;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Kettle;
using System.Linq;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    /// <summary>Installed power slots, temporary swaps, and direct basic-power effects.</summary>
    public static class BasicHeroPowerRules
    {
        public const GameTag SecondaryEntity = (GameTag)4029;
        public const GameTag SecondaryIndex = (GameTag)3919;
        private const GameTag ReturnPower = (GameTag)31290;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31290 && (int)tag <= 31293;

        public static HeroPower InstallPassive(Controller controller, IEntity source, Card assembled)
        {
            if (assembled.Power?.Trigger == null || assembled.Power.PowerTask != null || assembled.Power.DeathrattleTask != null)
                throw new ArgumentException("Passive composition requires an audited continuous trigger.");
            var definition = Cards.FromId("BE_004p").Clone();
            definition.Power = assembled.Power;
            definition.Text = assembled.Text;
            definition.Tags[AssembleRules.FirstHalf] = assembled[AssembleRules.FirstHalf];
            definition.Tags[AssembleRules.SecondHalf] = assembled[AssembleRules.SecondHalf];
            var power = (HeroPower)Entity.FromCard(controller, definition, new EntityData { [GameTag.CREATOR] = source.Id });
            controller.SetasideZone.Add(controller.Hero.HeroPower);
            controller.Hero.HeroPower = power;
            EnrollPowerAuras(controller, power);
            return power;
        }

        internal static void ActivatePassive(HeroPower power)
        {
            if (power?.Card.Id == "JAIL_EVENT_101hp") {
                if (power.ActivatedTrigger == null) power.Power?.Trigger?.Activate(power);
                return;
            }
            if (power?.Card.Id != "BE_004p" || power.ActivatedTrigger != null || power.Power?.Trigger == null) return;
            power.Power.Trigger.Activate(power);
            var controller = power.Controller;
            var assembled = Cards.FromAssetId(power.Card[AssembleRules.FirstHalf]);
            if (assembled.CantBeTargetedBySpells || assembled.DivineShield) {
                var grant = Cards.FromId("BE_044e").Clone();
                grant.Power = new Power {
                    Aura = assembled.CantBeTargetedBySpells ? new Aura(AuraType.ATTACHED, Effects.CantBeTargetedBySpellsAndHeroPowers) : null,
                    Enchant = assembled.DivineShield ? new Enchant(new Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1)) : null
                };
                // The grant owns cleanup even when presentation history is disabled.
                // "Assembled with {0}" (builder 23) names the half that lent the keyword: a stored copy, made first so the grant's first
                // snapshots carry its id. The hero power is on the board but its halves are not shown to the opponent: owner only.
                var half = PayloadRules.PrepareCard(controller, assembled);
                var actor = Enchantment.GetInstance(controller, power, controller.Hero, grant, 0, 0, half.Id);
                PayloadRules.Bind(actor, 1, half, PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
                grant.Power.Aura?.Activate(actor);
                grant.Power.Enchant?.ActivateTo(controller.Hero);
                controller.Game.AuraUpdate();
            }
        }

        internal static void DetachPassive(HeroPower power)
        {
            if (power?.Card.Id == "JAIL_EVENT_101hp" && power.Controller.Hero.HeroPower == power) { power.ActivatedTrigger?.Remove(); return; }
            // An inactive clone's zone assignment must not detach or refresh the live power.
            if (power?.Card.Id != "BE_004p" || power.Controller.Hero.HeroPower != power) return;
            power.ActivatedTrigger?.Remove();
            power.OngoingEffect?.Remove();
            foreach (var actor in power.Controller.Hero.AppliedEnchantments?.Where(e => e.Creator?.Id == power.Id
                && e.Card.Id == "BE_044e").ToArray() ?? Array.Empty<Enchantment>()) {
                foreach (var effect in actor.Power.Enchant?.Effects ?? Array.Empty<IEffect>()) effect.RemoveFrom(actor.Target);
                actor.Remove();
            }
            power.Game.AuraUpdate();
        }

        public static HeroPower InstallSecondary(Controller controller, IEntity source, string id)
        {
            var existing = controller.HeroPowers.FirstOrDefault(p => p.Card.Id == id);
            if (existing != null) return existing;
            // Exactly two native slots. Replacement is explicit; never create an unreachable third power.
            if (controller.SecondaryHeroPower != null) return null;
            var power = (HeroPower)Entity.FromCard(controller, Cards.FromId(id), new EntityData {
                [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id, [SecondaryIndex] = 1
            });
            controller[SecondaryEntity] = power.Id;
            EnrollPowerAuras(controller, power);
            CardSets.Client.Sets.MugZee.Refresh(controller);
            return power;
        }

        public static void SwapForTwoUses(Controller controller, IEntity source)
        {
            var current = controller.Hero.HeroPower;
            int original = IsSulfuras(current) && current[ReturnPower] != 0 ? current[ReturnPower] : current.Id;
            var power = (HeroPower)Entity.FromCard(controller, Cards.FromId("TLC_632t"), new EntityData {
                [GameTag.CREATOR] = source.Id, [ReturnPower] = original
            });
            controller.SetasideZone.Add(current);
            controller.Hero.HeroPower = power;
            EnrollPowerAuras(controller, power);
        }

        public static void EnrollPowerAuras(Controller c, HeroPower power)
        {
            ActivatePassive(power);
            foreach (var aura in c.Game.Auras.OfType<Aura>().Where(a =>
                a.Type == AuraType.HEROPOWER && a.Owner.Controller == c ||
                a.Type == AuraType.OP_HEROPOWER && a.Owner.Controller.Opponent == c).ToArray())
                if (!aura.AppliedEntities.Contains(power)) aura.EntityAdded(power);
        }

        private static bool IsSulfuras(HeroPower power) => power.Card.Id == "TLC_632t" || power.Card.Id == "TLC_632t2";
        public static void AfterUse(HeroPower power)
        {
            var c = power.Controller;
            if (c.Hero.HeroPower != power || !IsSulfuras(power) || power[ReturnPower] == 0) return;
            if (power.Card.Id == "TLC_632t")
            {
                // Both definitions have identical stats/recipes; only remaining-use text changes.
                power.Card = Cards.FromId("TLC_632t2");
                c.Game.Facts.AdmitDefinition(power.Card);
                if (c.Game.History) c.Game.PowerHistory.Add(new PowerHistoryChangeEntity {
                    CardId = power.Card.Id, Entity = PowerHistoryBuilder.FullEntity(power).Entity
                });
            }
            else if (c.Game.IdEntityDic.TryGetValue(power[ReturnPower], out var saved) && saved is HeroPower original
                && original.Controller == c && original.Zone == c.SetasideZone)
            {
                c.SetasideZone.Add(power);
                c.SetasideZone.Remove(original);
                original[GameTag.ZONE] = (int)Zone.PLAY;
                original.IsExhausted = false;
                c.Hero.HeroPower = original;
                EnrollPowerAuras(c, original);
                c.Game.AuraUpdate();
            }
        }

        public static int PortalMagnitude(Controller c) => Math.Max(1, ImbueRules.Count(c));
        public static void RefreshPortals(Controller c)
        {
            // Runs per player on every aura update: indexed scan, no iterator chain.
            RefreshPortals(c.HandZone, c);
            RefreshPortals(c.DeckZone, c);
        }

        private static void RefreshPortals(SabberStoneCore.Model.Zones.Zone<IPlayable> zone, Controller c)
        {
            for (int i = 0; i < zone.Count; i++)
            {
                var portal = zone[i];
                if (portal.Card.Id == "EDR_445pt3" && portal[GameTag.TAG_SCRIPT_DATA_NUM_1] != PortalMagnitude(c))
                    portal[GameTag.TAG_SCRIPT_DATA_NUM_1] = PortalMagnitude(c);
            }
        }

        public static bool RandomTargetsActive(Controller c) => c.BoardZone.Concat(c.Opponent.BoardZone).Any(m =>
            (m.Card.Id == "CORE_CFM_670" || m.Card.Id == "CFM_670") && !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed);

        public static string PowerId(CardClass heroClass) =>
            Cards.FromAssetId(Cards.HeroCard(heroClass)[GameTag.HERO_POWER]).Id;

        public static bool Trigger(Controller controller, IEntity source, CardClass heroClass, ICharacter target = null)
        {
            var game = controller.Game;
            if (target != null && (target.Game != game || !game.IdEntityDic.TryGetValue(target.Id, out var known)
                || !ReferenceEquals(known, target))) return false;
            var card = Cards.FromId(PowerId(heroClass));
            // shortcut: temporary power packet order is untraced; revisit after a retail Larva trace.
            var power = (HeroPower)Entity.FromCard(controller, card, new EntityData {
                [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id
            }, controller.SetasideZone);
            // Larva chooses its Combo target as a minion; triggering a power does not retarget it.
            if (!(source is Minion minion ? minion.IsValidPlayTarget(target) : power.IsValidPlayTarget(target)))
            {
                controller.GraveyardZone.Add(controller.SetasideZone.Remove(power));
                return false;
            }
            var previousEvent = game.CurrentEventData;
            game.TaskQueue.StartEvent();
            try
            {
                game.CurrentEventData = new EventMetaData(power, target);
                if (power.IsPlayableByCardReq) power.ActivateTask(PowerActivation.POWER, target);
                game.ProcessTasks();
            }
            finally
            {
                game.TaskQueue.EndEvent();
                controller.GraveyardZone.Add(controller.SetasideZone.Remove(power));
                game.CurrentEventData = previousEvent;
            }
            return true;
        }
    }
}
