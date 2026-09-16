public struct PlacementContext
{
	public enum Source
	{
		Game = 0,
		Player = 1
	}

	public Source m_source;

	public PlacementContext(Source source = Source.Game)
	{
		m_source = source;
	}
}
