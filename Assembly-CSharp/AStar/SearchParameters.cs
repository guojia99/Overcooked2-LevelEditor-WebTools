namespace AStar
{
	public class SearchParameters
	{
		public Point2 StartLocation { get; set; }

		public Point2 EndLocation { get; set; }

		public bool[,] Map { get; set; }

		public SearchParameters(Point2 startLocation, Point2 endLocation, bool[,] map)
		{
			StartLocation = startLocation;
			EndLocation = endLocation;
			Map = map;
		}
	}
}
