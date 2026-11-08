namespace SabberStoneCore.Model
{
	/// <summary>A versioned snapshot of the legacy xoroshiro-derived RNG stream.</summary>
	public readonly struct RngState
	{
		public readonly long State0, State1, DrawCount;
		public readonly int AlgorithmVersion;

		public RngState(long state0, long state1, long drawCount, int algorithmVersion = 1)
		{
			State0 = state0;
			State1 = state1;
			DrawCount = drawCount;
			AlgorithmVersion = algorithmVersion;
		}
	}
}
