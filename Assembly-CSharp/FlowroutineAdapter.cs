using System.Collections;

internal class FlowroutineAdapter : IFlowroutine, IEnumerator
{
	private IEnumerator m_coroutine;

	private CallbackVoid m_shutdown;

	public object Current
	{
		get
		{
			return m_coroutine.Current;
		}
	}

	public FlowroutineAdapter(IEnumerator _coroutine, CallbackVoid _shutdown)
	{
		m_coroutine = _coroutine;
		m_shutdown = _shutdown;
	}

	public bool MoveNext()
	{
		if (!m_coroutine.MoveNext())
		{
			Shutdown();
			return false;
		}
		return true;
	}

	public void Reset()
	{
	}

	public void Shutdown()
	{
		if (m_shutdown != null)
		{
			m_shutdown();
			m_shutdown = null;
		}
	}
}
