using System.Collections.Generic;

namespace AStar
{
	public class PathFinder
	{
		private int width;

		private int height;

		private Node[,] nodes;

		private Node startNode;

		private Node endNode;

		private SearchParameters searchParameters;

		public PathFinder(SearchParameters searchParameters)
		{
			this.searchParameters = searchParameters;
			InitializeNodes(searchParameters.Map);
			startNode = nodes[searchParameters.StartLocation.X, searchParameters.StartLocation.Y];
			startNode.State = NodeState.Open;
			if (searchParameters.EndLocation.X < nodes.GetLength(0) && searchParameters.EndLocation.Y < nodes.GetLength(1))
			{
				endNode = nodes[searchParameters.EndLocation.X, searchParameters.EndLocation.Y];
			}
		}

		public List<Point2> FindPath()
		{
			List<Point2> list = new List<Point2>();
			if (endNode != null && Search(startNode))
			{
				Node parentNode = endNode;
				while (parentNode.ParentNode != null)
				{
					list.Add(parentNode.Location);
					parentNode = parentNode.ParentNode;
				}
				list.Reverse();
			}
			return list;
		}

		private void InitializeNodes(bool[,] map)
		{
			width = map.GetLength(0);
			height = map.GetLength(1);
			nodes = new Node[width, height];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					nodes[j, i] = new Node(j, i, map[j, i], searchParameters.EndLocation);
				}
			}
		}

		private bool Search(Node currentNode)
		{
			currentNode.State = NodeState.Closed;
			List<Node> adjacentWalkableNodes = GetAdjacentWalkableNodes(currentNode);
			adjacentWalkableNodes.Sort((Node node1, Node node2) => node1.F.CompareTo(node2.F));
			foreach (Node item in adjacentWalkableNodes)
			{
				if (item.Location == endNode.Location)
				{
					return true;
				}
				if (Search(item))
				{
					return true;
				}
			}
			return false;
		}

		private List<Node> GetAdjacentWalkableNodes(Node fromNode)
		{
			List<Node> list = new List<Node>();
			IEnumerable<Point2> adjacentLocations = GetAdjacentLocations(fromNode.Location);
			foreach (Point2 item in adjacentLocations)
			{
				int x = item.X;
				int y = item.Y;
				if (x < 0 || x >= width || y < 0 || y >= height)
				{
					continue;
				}
				Node node = nodes[x, y];
				if ((!node.IsWalkable && node.Location != endNode.Location) || node.State == NodeState.Closed)
				{
					continue;
				}
				if (node.State == NodeState.Open)
				{
					float traversalCost = Node.GetTraversalCost(node.Location, node.ParentNode.Location);
					float num = fromNode.G + traversalCost;
					if (num < node.G)
					{
						node.ParentNode = fromNode;
						list.Add(node);
					}
				}
				else
				{
					node.ParentNode = fromNode;
					node.State = NodeState.Open;
					list.Add(node);
				}
			}
			return list;
		}

		private static IEnumerable<Point2> GetAdjacentLocations(Point2 fromLocation)
		{
			return new Point2[4]
			{
				new Point2(fromLocation.X - 1, fromLocation.Y),
				new Point2(fromLocation.X, fromLocation.Y + 1),
				new Point2(fromLocation.X + 1, fromLocation.Y),
				new Point2(fromLocation.X, fromLocation.Y - 1)
			};
		}
	}
}
