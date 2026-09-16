using System;
using System.Collections.Generic;

public class DeedManagerBase : Manager
{
	public delegate void DeedHandler<T>(T _deed) where T : Deed;

	public abstract class Deed
	{
	}

	public abstract class PadDeed : Deed, IPadDeed
	{
		private ControlPadInput.PadNum m_pad;

		private Publicity m_scope;

		public ControlPadInput.PadNum Pad
		{
			get
			{
				return m_pad;
			}
		}

		public Publicity Scope
		{
			get
			{
				return m_scope;
			}
		}

		public PadDeed(ControlPadInput.PadNum _pad, Publicity _scope)
		{
			m_pad = _pad;
			m_scope = _scope;
		}
	}

	public interface IPadDeed
	{
		ControlPadInput.PadNum Pad { get; }

		Publicity Scope { get; }
	}

	private Dictionary<Type, MulticastDelegate> m_handlerLookup = new Dictionary<Type, MulticastDelegate>();

	public void RegisterHandler<T>(DeedHandler<T> _handler) where T : Deed
	{
		Type typeFromHandle = typeof(T);
		if (!m_handlerLookup.ContainsKey(typeFromHandle))
		{
			DeedHandler<T> value = delegate
			{
			};
			m_handlerLookup.Add(typeFromHandle, value);
		}
		MulticastDelegate multicastDelegate = m_handlerLookup[typeof(T)];
		DeedHandler<T> a = multicastDelegate as DeedHandler<T>;
		a = (DeedHandler<T>)Delegate.Combine(a, _handler);
		m_handlerLookup[typeof(T)] = a;
	}

	public void UnregisterHandler<T>(DeedHandler<T> _handler) where T : Deed
	{
		Type typeFromHandle = typeof(T);
		MulticastDelegate multicastDelegate = m_handlerLookup[typeof(T)];
		DeedHandler<T> source = multicastDelegate as DeedHandler<T>;
		source = (DeedHandler<T>)Delegate.Remove(source, _handler);
	}

	protected virtual void OnDeedFired(Deed _deed)
	{
	}

	public void FireDeed<T>(T _deed) where T : Deed
	{
		Type type = _deed.GetType();
		if (m_handlerLookup.ContainsKey(type))
		{
			m_handlerLookup[type].DynamicInvoke(_deed);
		}
		OnDeedFired(_deed);
	}
}
