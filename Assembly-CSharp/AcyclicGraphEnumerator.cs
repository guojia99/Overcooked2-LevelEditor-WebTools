using System;
using System.Collections;
using System.Collections.Generic;

public class AcyclicGraphEnumerator<NodeContents, LinkContents> : IEnumerator where NodeContents : class where LinkContents : class
{
	public List<AcyclicGraph<NodeContents, LinkContents>.Node> m_list;

	private int position = -1;

	object IEnumerator.Current
	{
		get
		{
			return Current;
		}
	}

	public NodeContents Current
	{
		get
		{
			try
			{
				return m_list[position].m_value;
			}
			catch (IndexOutOfRangeException)
			{
				throw new InvalidOperationException();
			}
		}
	}

	public AcyclicGraphEnumerator(List<AcyclicGraph<NodeContents, LinkContents>.Node> _list)
	{
		m_list = _list;
	}

	public bool MoveNext()
	{
		position++;
		return position < m_list.Count;
	}

	public void Reset()
	{
		position = -1;
	}
}
