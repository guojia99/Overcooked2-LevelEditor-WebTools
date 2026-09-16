using System.Collections;
using System.Collections.Generic;

public class AcyclicGraph<NodeContents, LinkContents> : IEnumerable where NodeContents : class where LinkContents : class
{
	public class Node
	{
		public NodeContents m_value;

		public List<Link> m_connected = new List<Link>();
	}

	public class Link
	{
		public Node m_target;

		public LinkContents m_data;

		public Link(Node n, LinkContents data)
		{
			m_target = n;
			m_data = data;
		}
	}

	public class LinkKey
	{
	}

	private class TwowayLink
	{
		public Link m_link1;

		public Link m_link2;

		public TwowayLink(Link _link1, Link _link2)
		{
			m_link1 = _link1;
			m_link2 = _link2;
		}
	}

	private Dictionary<LinkKey, TwowayLink> m_keyLinkLookup = new Dictionary<LinkKey, TwowayLink>();

	private List<Node> m_nodes = new List<Node>();

	public AcyclicGraph()
	{
	}

	public AcyclicGraph(NodeContents _head)
	{
		FindOrAddNode(_head);
	}

	public LinkKey AddLink(NodeContents t1, NodeContents t2, LinkContents _linkData)
	{
		Node node = FindOrAddNode(t1);
		Node node2 = FindOrAddNode(t2);
		Link link = new Link(node2, _linkData);
		node.m_connected.Add(link);
		Link link2 = new Link(node, _linkData);
		node2.m_connected.Add(link2);
		LinkKey linkKey = new LinkKey();
		m_keyLinkLookup[linkKey] = new TwowayLink(link, link2);
		return linkKey;
	}

	public static AcyclicGraph<NodeContents, LinkContents> Merge(AcyclicGraph<NodeContents, LinkContents> _graph1, AcyclicGraph<NodeContents, LinkContents> _graph2)
	{
		AcyclicGraph<NodeContents, LinkContents> acyclicGraph = new AcyclicGraph<NodeContents, LinkContents>();
		foreach (NodeContents item in _graph1)
		{
			Node node = _graph1.GetNode(item);
			foreach (Link item2 in node.m_connected)
			{
				acyclicGraph.AddLink(item, item2.m_target.m_value, item2.m_data);
			}
		}
		foreach (NodeContents item3 in _graph2)
		{
			Node node2 = _graph2.GetNode(item3);
			foreach (Link item4 in node2.m_connected)
			{
				acyclicGraph.AddLink(item3, item4.m_target.m_value, item4.m_data);
			}
		}
		return acyclicGraph;
	}

	private Node FindOrAddNode(NodeContents _value)
	{
		Node node = m_nodes.Find((Node n) => n.m_value == _value);
		if (node == null)
		{
			node = new Node();
			node.m_value = _value;
			m_nodes.Add(node);
		}
		return node;
	}

	public void GetNodesFromLink(LinkKey _key, out Node _node1, out Node _node2)
	{
		TwowayLink twowayLink = m_keyLinkLookup[_key];
		_node1 = twowayLink.m_link1.m_target;
		_node2 = twowayLink.m_link2.m_target;
	}

	public void RemoveLink(LinkKey _key)
	{
		TwowayLink twoWayLink = m_keyLinkLookup[_key];
		foreach (Node node in m_nodes)
		{
			node.m_connected.RemoveAll((Link link) => link == twoWayLink.m_link1);
			node.m_connected.RemoveAll((Link link) => link == twoWayLink.m_link2);
		}
		m_nodes.RemoveAll((Node node) => node.m_connected.Count == 0);
	}

	public Node GetNode(NodeContents _value)
	{
		return m_nodes.Find((Node n) => n.m_value == _value);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public AcyclicGraphEnumerator<NodeContents, LinkContents> GetEnumerator()
	{
		return new AcyclicGraphEnumerator<NodeContents, LinkContents>(m_nodes);
	}
}
