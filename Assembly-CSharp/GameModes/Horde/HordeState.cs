using System;

namespace GameModes.Horde
{
	public class HordeState<T> : IHordeState<T> where T : struct, IConvertible, IComparable
	{
		public T StateId { get; private set; }

		public HordeState(T state)
		{
			StateId = state;
		}
	}
}
