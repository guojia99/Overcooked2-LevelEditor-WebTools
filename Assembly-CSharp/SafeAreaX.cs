internal class SafeAreaX : SafeArea
{
	public override string Label
	{
		get
		{
			return "SafeAreaX";
		}
	}

	public override float SafeAreaAxis
	{
		get
		{
			return SafeAreaAdjuster.SafeAreaWidth;
		}
		set
		{
			SafeAreaAdjuster.SafeAreaWidth = value;
		}
	}
}
