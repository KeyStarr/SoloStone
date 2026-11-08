using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.Events
{
    // Compatibility payloads describe manager invocations, not completed rules facts.
    // Sender retains legacy null/reference semantics; richer immutable facts arrive with recipes.
    public interface IGameEvent
    {
        IEntity Sender { get; }
        string Channel { get; }
    }

    // One completed non-aura effect operation, never individual ATK/HEALTH tag updates.
    public readonly record struct StatGainedEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public int TargetId => Sender.Id;
        public int SourceId { get; }
        public int SourceControllerId { get; }
        public string EffectId { get; }
        public Enums.Zone Zone { get; }
        public int Attack { get; }
        public int Health { get; }
        public string Channel => nameof(TriggerManager.StatGainedTrigger);
        public StatGainedEvent(Minion target, IEntity source, Card effect, Enums.Zone zone, int attack, int health)
        {
            Sender = target; SourceId = source?.Id ?? 0; SourceControllerId = source?.Controller?.Id ?? 0;
            EffectId = effect?.Id ?? source?.Card?.Id ?? ""; Zone = zone; Attack = attack; Health = health;
        }
    }

    public readonly record struct DealDamageEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.DealDamageTrigger);
        public DealDamageEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct DamageEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.DamageTrigger);
        public DamageEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct HealIntentEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.HealIntentTrigger);
        public HealIntentEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct HealEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.HealTrigger);
        public HealEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct LoseDivineShieldEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.LoseDivineShield);
        public LoseDivineShieldEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct EndTurnEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.EndTurnTrigger);
        public EndTurnEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct TurnStartEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.TurnStartTrigger);
        public TurnStartEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct SummonEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.SummonTrigger);
        public SummonEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct AfterSummonEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.AfterSummonTrigger);
        public AfterSummonEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct AttackEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.AttackTrigger);
        public AttackEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct DeathEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.DeathTrigger);
        public int LastKnownAttack { get; }
        public int KillerId { get; }
        public int BoardPosition { get; }
        public DeathEvent(IEntity sender)
        {
            Sender = sender;
            LastKnownAttack = sender is IPlayable playable ? Actions.DeathContextRules.AttackAtDeath(playable) : 0;
            KillerId = sender?[Actions.DeathContextRules.FatalSource] ?? 0;
            BoardPosition = sender is Minion minion ? minion.LastBoardPosition : -1;
        }
    }

    public readonly record struct PlayCardEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.PlayCardTrigger);
        public PlayCardEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct AfterPlayCardEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.AfterPlayCardTrigger);
        public AfterPlayCardEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct PlayMinionEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.PlayMinionTrigger);
        public PlayMinionEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct AfterPlayMinionEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.AfterPlayMinionTrigger);
        public AfterPlayMinionEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct CastSpellEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.CastSpellTrigger);
        public CastSpellEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct AfterCastEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.AfterCastTrigger);
        public AfterCastEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct SecretRevealedEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.SecretRevealedTrigger);
        public SecretRevealedEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct ZoneEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.ZoneTrigger);
        public ZoneEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct DiscardEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.DiscardTrigger);
        public DiscardEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct GameStartEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.GameStartTrigger);
        public GameStartEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct DrawEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.DrawTrigger);
        public DrawEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct TargetEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.TargetTrigger);
        public TargetEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct InspireEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.InspireTrigger);
        public InspireEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct FreezeEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.FreezeTrigger);
        public FreezeEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct ArmorEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.ArmorTrigger);
        public ArmorEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct EquipWeaponEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.EquipWeaponTrigger);
        public EquipWeaponEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct ShuffleIntoDeckEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(TriggerManager.ShuffleIntoDeckTrigger);
        public ShuffleIntoDeckEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct OverloadEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public int Amount { get; }
        public string Channel => nameof(TriggerManager.OverloadTrigger);
        public OverloadEvent(IPlayable sender, int amount) { Sender = sender; Amount = amount; }
    }
    public readonly record struct CardPreparedEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public int SourceId { get; }
        public int ControllerId { get; }
        public int ManaSpent { get; }
        public int NominalDiscount { get; }
        public int RealizedReduction { get; }
        public string Channel => nameof(TriggerManager.CardPreparedTrigger);
        public CardPreparedEvent(IPlayable source, int spent, int discount, int reduction)
        {
            Sender = source; SourceId = source.Id; ControllerId = source.Controller.Id;
            ManaSpent = spent; NominalDiscount = discount; RealizedReduction = reduction;
        }
    }

    // Character-local compatibility windows retain their existing sender and task timing.
    public readonly record struct PreDamageEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(Character.PreDamageTrigger);
        public PreDamageEvent(IEntity sender) => Sender = sender;
    }

    public readonly record struct TakeDamageEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(Character.TakeDamageTrigger);
        public TakeDamageEvent(IEntity sender) => Sender = sender;
    }

    // Shared payload; character and manager registrations live on independent buses.
    public readonly record struct AfterAttackEvent : IGameEvent
    {
        public IEntity Sender { get; }
        public string Channel => nameof(Character.AfterAttackTrigger);
        public AfterAttackEvent(IEntity sender) => Sender = sender;
    }
}
