using System.Collections.Generic;
using SabberStoneCore.Enums;
namespace SabberStoneCore.Model.Entities
{
    public sealed class BoardObject : Playable
    {
        public LocationState Location = new LocationState();
        public int LastBoardPosition;
        public BoardObject(in Controller controller, in Card card, in IDictionary<GameTag, int> tags, in int id = -1)
            : base(in controller, in card, in tags, in id) { }
        private BoardObject(in Controller controller, BoardObject source) : base(in controller, source)
        {
            Location = source.Location.Clone();
            LastBoardPosition = source.LastBoardPosition;
        }
        public override bool ToBeDestroyed
        {
            get => base.ToBeDestroyed;
            set
            {
                base.ToBeDestroyed = value;
                if (value && Zone == Controller.BoardZone && !Game.DeadBoardObjects.Contains(this)) Game.DeadBoardObjects.Add(this);
                if (!value) Game.DeadBoardObjects.Remove(this);
            }
        }
        public override bool IsPlayableByPlayer => !Controller.BoardZone.IsFull && base.IsPlayableByPlayer;
        public int Durability => this[GameTag.HEALTH] - this[GameTag.DAMAGE];
        public override bool HasDeathrattle
        {
            get => this[GameTag.DEATHRATTLE] == 1;
            set => this[GameTag.DEATHRATTLE] = value ? 1 : 0;
        }
        public override IPlayable Clone(in Controller controller) => new BoardObject(in controller, this);
    }
}
