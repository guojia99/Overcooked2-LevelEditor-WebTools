using System.Text;
using UnityEngine.UI;

public class ThaiFontProcessor : ITextProcessor
{
	private enum THAICHARACTERS
	{
		KO_KAI = 3585,
		YO_YING = 3597,
		DO_CHADA = 3598,
		TO_PATAK = 3599,
		THO_THAN = 3600,
		PO_PLA = 3611,
		FO_FA = 3613,
		FO_FAN = 3615,
		LO_CHULA = 3628,
		PAIYANNOI = 3631,
		SARA_A = 3632,
		MAI_HANAKAT = 3633,
		SARA_AA = 3634,
		SARA_AM = 3635,
		SARA_I = 3636,
		SARA_II = 3637,
		SARA_UE = 3638,
		SARA_UEE = 3639,
		SARA_U = 3640,
		PHINTHU = 3642,
		SARA_E = 3648,
		SARA_AE = 3649,
		MAITAIKHU = 3655,
		MAI_EK = 3656,
		THANTHAKHAT = 3660,
		NIKHAHIT = 3661,
		THO_THAN_DESCLESS = 63232,
		SARA_I_LEFT = 63233,
		SARA_II_LEFT = 63234,
		SARA_UE_LEFT = 63235,
		SARA_UEE_LEFT = 63236,
		MAI_EK_LOW_LEFT = 63237,
		MAI_EK_LOW = 63242,
		YO_YING_DESCLESS = 63247,
		MAI_HANAKAT_LEFT = 63248,
		SARA_AA_LEFT = 63249,
		SARA_AM_LEFT = 63250,
		MAI_EK_LEFT = 63251,
		SARA_U_LOW = 63256
	}

	private static StringBuilder s_FixedString = new StringBuilder();

	private const char m_kFirstThaiChar = '\u0e00';

	private const char m_kLastThaiChar = '\u0e7f';

	private static bool IsBase(char checkChar)
	{
		return (checkChar >= 'ก' && checkChar <= 'ฯ') || checkChar == 'ะ' || checkChar == 'เ' || checkChar == 'แ';
	}

	private static bool IsBaseDesc(char checkChar)
	{
		return checkChar == 'ฎ' || checkChar == 'ฏ';
	}

	private static bool IsBaseAsc(char checkChar)
	{
		return checkChar == 'ป' || checkChar == 'ฝ' || checkChar == 'ฟ' || checkChar == 'ฬ';
	}

	private static bool IsTop(char checkChar)
	{
		return checkChar >= '\u0e48' && checkChar <= '\u0e4c';
	}

	private static bool IsLower(char checkChar)
	{
		return checkChar >= '\u0e38' && checkChar <= '\u0e3a';
	}

	private static bool IsUpper(char checkChar)
	{
		return checkChar == '\u0e31' || checkChar == '\u0e34' || checkChar == '\u0e35' || checkChar == '\u0e36' || checkChar == '\u0e37' || checkChar == '\u0e47' || checkChar == '\u0e4d';
	}

	public bool OnPopulateMesh(VertexHelper _helper)
	{
		return false;
	}

	public bool HasEmbeddedImages(string inputString)
	{
		return false;
	}

	public bool ProcessText(ref string thaiString)
	{
		int length = thaiString.Length;
		s_FixedString.Length = 0;
		for (int i = 0; i < length; i++)
		{
			char c = thaiString[i];
			char c2 = c;
			char c3 = c;
			if (i > 0)
			{
				c2 = thaiString[i - 1];
			}
			if (i > 1)
			{
				c3 = thaiString[i - 2];
			}
			if (IsTop(c) && i > 0)
			{
				char checkChar = c2;
				if (IsLower(checkChar) && i > 1)
				{
					checkChar = c3;
				}
				if (IsBase(checkChar))
				{
					bool flag = i < length - 1 && (c2 == 'ำ' || c2 == '\u0e4d');
					if (IsBaseAsc(checkChar))
					{
						if (flag)
						{
							c = (char)(c + 59595);
							s_FixedString.Append('\uf711');
							s_FixedString.Append(c);
							if (c2 == 'ำ')
							{
								s_FixedString.Append('า');
							}
							i++;
							continue;
						}
						c = (char)(c + 59581);
					}
					else if (!flag)
					{
						c = (char)(c + 59586);
					}
				}
				if (i > 1 && IsUpper(c2) && IsBaseAsc(c3))
				{
					c = (char)(c + 59595);
				}
			}
			else if (IsUpper(c) && i > 0 && IsBaseAsc(c2))
			{
				switch ((THAICHARACTERS)c)
				{
				case THAICHARACTERS.MAI_HANAKAT:
					c = '\uf710';
					break;
				case THAICHARACTERS.SARA_I:
					c = '\uf701';
					break;
				case THAICHARACTERS.SARA_II:
					c = '\uf702';
					break;
				case THAICHARACTERS.SARA_UE:
					c = '\uf703';
					break;
				case THAICHARACTERS.SARA_UEE:
					c = '\uf704';
					break;
				case THAICHARACTERS.NIKHAHIT:
					c = '\uf711';
					break;
				case THAICHARACTERS.MAITAIKHU:
					c = '\uf712';
					break;
				}
			}
			else if (IsLower(c) && i > 0 && IsBaseDesc(c2))
			{
				c = (char)(c + 59616);
			}
			else if (c == 'ญ' && i < length - 1 && IsLower(c2))
			{
				c = '\uf70f';
			}
			else if (c == 'ฐ' && i < length - 1 && IsLower(c2))
			{
				c = '\uf700';
			}
			s_FixedString.Append(c);
		}
		thaiString = s_FixedString.ToString();
		s_FixedString.Length = 0;
		return true;
	}
}
