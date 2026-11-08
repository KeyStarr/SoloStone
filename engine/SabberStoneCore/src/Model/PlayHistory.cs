#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using SabberStoneCore.Model.Entities;
using System.Text;

namespace SabberStoneCore.Model
{
	public readonly struct PlayHistoryEntry
	{
		public readonly int SourceController;
		public readonly int TargetController;
		public readonly Card SourceCard;
		public readonly Card TargetCard;
		public readonly int SubOption;
		public readonly int SourceId;
        public readonly int HandPosition;
        public readonly int HandCount;

		public PlayHistoryEntry(in IPlayable source, in IPlayable target = null, in int chooseOne = -1)
		{
			HandPosition = source.Zone == source.Controller.HandZone ? source.ZonePosition : -1;
            HandCount = source.Zone == source.Controller.HandZone ? source.Controller.HandZone.Count : 0;
            SourceController = source.Controller.PlayerId;
			TargetController = target?.Controller.PlayerId ?? 0;
			SourceCard = source.Card;
			TargetCard = target?.Card;
			SubOption = chooseOne;
			SourceId = source.Id;
		}

		public PlayHistoryEntry(in Card srcCard)
		{
			SourceCard = srcCard;
            HandPosition = -1;
            HandCount = 0;

			SourceController = 0;
			TargetController = 0;
			TargetCard = null;
			SubOption = 0;
			SourceId = 0;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder($"[P{SourceController}]");
			sb.Append($"[{SourceCard}]");
			if (SubOption > 0)
				sb.Append($"[SubOption:{SubOption}]");
			if (TargetController != 0)
			{
				sb.Append("=>");
				sb.Append($"[P{TargetController}]");
				sb.Append($"[{TargetCard}]");
			}
			return sb.ToString();
		}
	}
}

namespace SabberStoneCore.Actions
{
    public static class CopyPlayHistory
    {
        // Controller tags keep immutable per-play witnesses without allocating native entities.
        internal const int TagBase = 0x40000000;
        internal static void Record(Controller owner, IPlayable source)
        {
            if ((owner.Game[PayloadRules.Tracking] & 2) == 0) return;
            int origin = source[PayloadRules.CopySourceController];
            if (origin != 0) owner[(Enums.GameTag)(TagBase + owner.PlayHistory.Count - 1)] = origin;
        }
        public static int SourceControllerAt(Controller owner, int playIndex)
        {
            if (playIndex < 0 || playIndex >= owner.PlayHistory.Count) throw new System.ArgumentOutOfRangeException(nameof(playIndex));
            return owner[(Enums.GameTag)(TagBase + playIndex)];
        }
    }
}
