using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Auras
{
    /// <summary>
    /// Garrison Commander: "You can use your Hero Power twice a turn." While the owner is active, the controller's Hero Power
    /// is ready again after its first use of their turn (so also when the owner enters play after that first use), and it is
    /// spent again when the last source leaves. It never grants a third use.
    /// </summary>
    public sealed class ExtraHeroPowerUseAura : IAura
    {
        private IPlayable _owner;
        public IPlayable Owner => _owner;

        public void Activate(IPlayable owner)
        {
            var instance = new ExtraHeroPowerUseAura { _owner = owner };
            owner.OngoingEffect = instance;
            owner.Game.Auras.Add(instance);
            instance.Update();
        }

        private static bool Active(IPlayable source) =>
            source.Zone?.Type == Zone.PLAY && !source.ToBeDestroyed && !(source is Minion m && (m.IsSilenced || m.IsDormant));

        private bool OtherSource() => _owner.Controller.BoardZone.Any(m => m != _owner && m.Card.Id == _owner.Card.Id && Active(m));

        public void Update()
        {
            if (!Active(_owner)) { Remove(); return; }
            var c = _owner.Controller;
            var power = c.Hero?.HeroPower;
            if (power == null || c.Game.CurrentPlayer != c) return;
            if (power.IsExhausted && c.HeroPowerActivationsThisTurn == 1) power.IsExhausted = false;
        }

        public void Remove()
        {
            var c = _owner.Controller;
            var power = c.Hero?.HeroPower;
            _owner.Game.Auras.Remove(this);
            if (_owner.OngoingEffect == this) _owner.OngoingEffect = null;
            if (power != null && c.Game.CurrentPlayer == c && c.HeroPowerActivationsThisTurn >= 1 && !power.IsExhausted && !OtherSource())
                power.IsExhausted = true;
        }

        public void Clone(IPlayable clone)
        {
            if (clone.Id != _owner.Id || clone.Game == _owner.Game) { Activate(clone); return; }
            var instance = new ExtraHeroPowerUseAura { _owner = clone };
            clone.OngoingEffect = instance;
            clone.Game.Auras.Add(instance);
        }
    }
}
