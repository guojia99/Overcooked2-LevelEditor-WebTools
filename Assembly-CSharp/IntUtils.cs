using System.Text;

public static class IntUtils
{
	private static StringBuilder s_stringBuilder = new StringBuilder(16, 16);

	private static string[] m_digits = new string[10] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

	public static string ToTimeString(this int _value)
	{
		s_stringBuilder.Length = 0;
		int num = _value / 60;
		int num2 = _value % 60;
		if (num < 10)
		{
			s_stringBuilder.Append("0");
			s_stringBuilder.Append(m_digits[num]);
		}
		else
		{
			int num3 = num / 10;
			int num4 = num % 10;
			s_stringBuilder.Append(m_digits[num3]);
			s_stringBuilder.Append(m_digits[num4]);
		}
		s_stringBuilder.Append(":");
		if (num2 < 10)
		{
			s_stringBuilder.Append("0");
			s_stringBuilder.Append(m_digits[num2]);
		}
		else
		{
			int num5 = num2 / 10;
			int num6 = num2 % 10;
			s_stringBuilder.Append(m_digits[num5]);
			s_stringBuilder.Append(m_digits[num6]);
		}
		return s_stringBuilder.ToString();
	}
}
