internal class SafeAreaY : SafeArea
{
	public override string Label
	{
		get
		{
			return "SafeAreaY";
		}
	}

	public override float SafeAreaAxis
	{
		get
		{
			return SafeAreaAdjuster.SafeAreaHeight;
		}
		set
		{
			SafeAreaAdjuster.SafeAreaHeight = value;
		}
	}
}
