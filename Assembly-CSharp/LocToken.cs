public struct LocToken
{
	public string m_token;

	public string m_value;

	public LocToken(string token, string replaceWith)
	{
		m_token = token.ToLowerInvariant();
		if (m_token.Length > 0)
		{
			if (m_token[0] != '[')
			{
				m_token = "[" + m_token;
			}
			if (m_token[m_token.Length - 1] != ']')
			{
				m_token += "]";
			}
		}
		m_value = replaceWith;
	}
}
