namespace Team17.Online.Shared
{
	public static class BitHelper
	{
		public static int CalculateRequirement(uint maxVal)
		{
			int num = (((maxVal & 0xFFFF0000u) != 0) ? 16 : 0);
			if (((maxVal >>= num) & 0xFF00) != 0)
			{
				num |= 8;
				maxVal >>= 8;
			}
			if ((maxVal & 0xF0) != 0)
			{
				num |= 4;
				maxVal >>= 4;
			}
			if ((maxVal & 0xC) != 0)
			{
				num |= 2;
				maxVal >>= 2;
			}
			return (int)(((uint)num | (maxVal >> 1)) + 1);
		}
	}
}
