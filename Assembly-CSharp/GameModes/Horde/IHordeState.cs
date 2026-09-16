using System;

namespace GameModes.Horde
{
	public interface IHordeState<T> where T : struct, IConvertible, IComparable
	{
		T StateId { get; }
	}
}
