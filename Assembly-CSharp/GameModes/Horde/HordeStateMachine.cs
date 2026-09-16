using System;
using System.Collections.Generic;

namespace GameModes.Horde
{
	public class HordeStateMachine<T> : IHordeStateMachine<T> where T : struct, IConvertible, IComparable
	{
		public delegate void OnBegin(IHordeStateMachine<T> stateMachine, T fromState, T toState);

		public delegate void OnEnd(IHordeStateMachine<T> stateMachine, T fromState, T toState);

		public delegate void OnUpdate(IHordeStateMachine<T> stateMachine, T state, float dT);

		private Dictionary<T, IHordeState<T>> m_states;

		private OnBegin m_onBegin;

		private OnEnd m_onEnd;

		private OnUpdate m_onUpdate;

		public T StateId { get; private set; }

		public IHordeState<T> State
		{
			get
			{
				return m_states[StateId];
			}
		}

		public HordeStateMachine(T startState, IHordeState<T>[] states, OnBegin onBegin, OnEnd onEnd, OnUpdate onUpdate)
		{
			StateId = startState;
			m_states = new Dictionary<T, IHordeState<T>>();
			for (int i = 0; i < states.Length; i++)
			{
				m_states.Add(states[i].StateId, states[i]);
			}
			m_onBegin = onBegin;
			m_onEnd = onEnd;
			m_onUpdate = onUpdate;
		}

		public bool Transition(T state)
		{
			if (state.Equals(StateId))
			{
				return false;
			}
			T stateId = StateId;
			if (m_onEnd != null)
			{
				m_onEnd(this, stateId, state);
			}
			StateId = state;
			if (m_onBegin != null)
			{
				m_onBegin(this, stateId, StateId);
			}
			return true;
		}

		public void Tick(float dT)
		{
			if (m_onUpdate != null)
			{
				m_onUpdate(this, StateId, dT);
			}
		}
	}
}
