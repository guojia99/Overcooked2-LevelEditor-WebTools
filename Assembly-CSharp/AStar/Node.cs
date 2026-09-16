using System;

namespace AStar
{
	public class Node
	{
		private Node parentNode;

		public Point2 Location { get; private set; }

		public bool IsWalkable { get; set; }

		public float G { get; private set; }

		public float H { get; private set; }

		public NodeState State { get; set; }

		public float F
		{
			get
			{
				return G + H;
			}
		}

		public Node ParentNode
		{
			get
			{
				return parentNode;
			}
			set
			{
				parentNode = value;
				G = parentNode.G + GetTraversalCost(Location, parentNode.Location);
			}
		}

		public Node(int x, int y, bool isWalkable, Point2 endLocation)
		{
			Location = new Point2(x, y);
			State = NodeState.Untested;
			IsWalkable = isWalkable;
			H = GetTraversalCost(Location, endLocation);
			G = 0f;
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}: {2}", Location.X, Location.Y, State);
		}

		internal static float GetTraversalCost(Point2 location, Point2 otherLocation)
		{
			float num = otherLocation.X - location.X;
			float num2 = otherLocation.Y - location.Y;
			return (float)Math.Sqrt(num * num + num2 * num2);
		}
	}
}
