using System;

namespace GameModes.Horde
{
	public interface IHordeStateMachine<T> where T : struct, IConvertible, IComparable
	{
		T StateId { get; }

		IHordeState<T> State { get; }

		bool Transition(T state);

		void Tick(float dT);
	}
}
