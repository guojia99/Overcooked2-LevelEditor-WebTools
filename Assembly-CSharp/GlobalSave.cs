using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Steamworks;
using UnityEngine;

public class GlobalSave : IByteSerialization
{
	[Serializable]
	private class Entry
	{
		public string m_JSON;
	}

	[Serializable]
	private class EntInt
	{
		public int m_Value;

		public EntInt(int val)
		{
			m_Value = val;
		}
	}

	[Serializable]
	private class EntFloat
	{
		public float m_Value;

		public EntFloat(float val)
		{
			m_Value = val;
		}
	}

	[Serializable]
	private class EntString
	{
		public string m_Value;

		public EntString(string val)
		{
			m_Value = val;
		}
	}

	[Serializable]
	private class EntBool
	{
		public bool m_Value;

		public EntBool(bool val)
		{
			m_Value = val;
		}
	}

	[Serializable]
	private class EntInt64Array
	{
		public long[] m_Value;

		public EntInt64Array(long[] val)
		{
			m_Value = val;
		}
	}

	private class EntIntArray
	{
		public int[] m_Value;

		public EntIntArray(int[] val)
		{
			m_Value = val;
		}
	}

	private class EntDictionary
	{
		public string[] m_Key;

		public string[] m_Value;

		public EntDictionary(Dictionary<string, string> _dict)
		{
			int num = 0;
			m_Key = new string[_dict.Keys.Count];
			m_Value = new string[_dict.Keys.Count];
			foreach (string key in _dict.Keys)
			{
				m_Key[num] = key;
				m_Value[num] = _dict[key];
				num++;
			}
		}

		public Dictionary<string, string> ToDictionary()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			for (int i = 0; i < m_Key.Length; i++)
			{
				dictionary.Add(m_Key[i], m_Value[i]);
			}
			return dictionary;
		}
	}

	[Serializable]
	private class GlobalData
	{
		public int m_Version = 1;

		public string m_What = "OC2GSGD";

		public string[] m_Keys;

		public Entry[] m_Entries;
	}

	private const int GLOBALSAVE_VERSION = 1;

	private const string CRC_TAG = "CRC32";

	private const int IV_LENGTH = 16;

	private const string SALT_DEFAULT = "jjo+Ffqil5bdpo5VG82kLj8Ng1sK7L/rCqFTa39Zkom2/baqf5j9HMmsuCr0ipjYsPrsaNIOESWy7bDDGYWx1eA==";

	private Dictionary<string, Entry> m_Data = new Dictionary<string, Entry>();

	private GlobalData m_GlobalData = new GlobalData();

	public int ByteSaveSize
	{
		get
		{
			string s = ConvertDataToSave();
			return Encoding.UTF8.GetByteCount(s);
		}
	}

	public byte[] ByteSave()
	{
		string text = ConvertDataToSave();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		byte[] array = Obfuscate(bytes, bytes.Length);
		if (array == null)
		{
			return null;
		}
		byte[] bytes2 = BitConverter.GetBytes(CRC32.Calculate(array));
		byte[] array2 = new byte[array.Length + bytes2.Length];
		Array.Copy(array, array2, array.Length);
		Array.Copy(bytes2, 0, array2, array.Length, bytes2.Length);
		return array2;
	}

	private string GetUniqueId()
	{
		string text = null;
		text = SteamUser.GetSteamID().ToString();
		return (text == null) ? SystemInfo.deviceUniqueIdentifier : text;
	}

	private byte[] Obfuscate(byte[] deobfuscatedText, int size, int start = 0, string salt = "jjo+Ffqil5bdpo5VG82kLj8Ng1sK7L/rCqFTa39Zkom2/baqf5j9HMmsuCr0ipjYsPrsaNIOESWy7bDDGYWx1eA==", string hashFunction = "SHA1", int keySize = 256)
	{
		if (deobfuscatedText == null || deobfuscatedText.Length == 0 || start + size > deobfuscatedText.Length)
		{
			return null;
		}
		byte[] array = new byte[16];
		System.Random random = new System.Random();
		random.NextBytes(array);
		byte[] bytes = new PasswordDeriveBytes(GetUniqueId(), Encoding.ASCII.GetBytes(salt), hashFunction, 2).GetBytes(keySize / 8);
		RijndaelManaged rijndaelManaged = new RijndaelManaged();
		rijndaelManaged.Mode = CipherMode.CBC;
		byte[] array2 = null;
		try
		{
			using (ICryptoTransform transform = rijndaelManaged.CreateEncryptor(bytes, array))
			{
				using (MemoryStream memoryStream = new MemoryStream())
				{
					using (CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
					{
						cryptoStream.Write(deobfuscatedText, start, size);
						cryptoStream.FlushFinalBlock();
						array2 = memoryStream.ToArray();
						memoryStream.Close();
						cryptoStream.Close();
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("GlobalSave Obfuscate exception=" + ex.ToString());
			return null;
		}
		finally
		{
			rijndaelManaged.Clear();
		}
		byte[] array3 = new byte[16 + array2.Length];
		Array.Copy(array, array3, 16);
		Array.Copy(array2, 0, array3, 16, array2.Length);
		return array3;
	}

	private byte[] Deobfuscate(byte[] obfuscatedText, int size, int start = 0, string salt = "jjo+Ffqil5bdpo5VG82kLj8Ng1sK7L/rCqFTa39Zkom2/baqf5j9HMmsuCr0ipjYsPrsaNIOESWy7bDDGYWx1eA==", string hashFunction = "SHA1", int keySize = 256)
	{
		if (obfuscatedText == null || obfuscatedText.Length == 0 || obfuscatedText.Length <= start + size || obfuscatedText.Length <= 16)
		{
			return null;
		}
		byte[] array = new byte[16];
		Array.Copy(obfuscatedText, start, array, 0, 16);
		byte[] array2 = new byte[size - 16 - start];
		Array.Copy(obfuscatedText, 16, array2, 0, array2.Length);
		byte[] bytes = new PasswordDeriveBytes(GetUniqueId(), Encoding.ASCII.GetBytes(salt), hashFunction, 2).GetBytes(keySize / 8);
		RijndaelManaged rijndaelManaged = new RijndaelManaged();
		rijndaelManaged.Mode = CipherMode.CBC;
		byte[] array3 = new byte[array2.Length];
		try
		{
			using (ICryptoTransform transform = rijndaelManaged.CreateDecryptor(bytes, array))
			{
				using (MemoryStream memoryStream = new MemoryStream(array2))
				{
					using (CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Read))
					{
						cryptoStream.Read(array3, 0, array3.Length);
						memoryStream.Close();
						cryptoStream.Close();
						return array3;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("GlobalSave Deobfuscate exception=" + ex.ToString());
			return null;
		}
		finally
		{
			rijndaelManaged.Clear();
		}
	}

	public bool ByteLoad(byte[] _data)
	{
		if (_data == null || (long)_data.Length <= 4L)
		{
			return false;
		}
		int size = _data.Length - 4;
		if (!CRC32.Validate(_data, (uint)size))
		{
			return false;
		}
		byte[] array = Deobfuscate(_data, size);
		if (array == null)
		{
			return false;
		}
		string json = Encoding.UTF8.GetString(array);
		if (!ConvertDataFromSave(json))
		{
			return false;
		}
		return true;
	}

	private string ConvertDataToSave()
	{
		int num = 0;
		m_GlobalData.m_Entries = new Entry[m_Data.Keys.Count];
		m_GlobalData.m_Keys = new string[m_Data.Keys.Count];
		foreach (string key in m_Data.Keys)
		{
			m_GlobalData.m_Keys[num] = key;
			m_GlobalData.m_Entries[num] = m_Data[key];
			num++;
		}
		return JsonUtility.ToJson(m_GlobalData, false);
	}

	private bool ConvertDataFromSave(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return false;
		}
		try
		{
			JsonUtility.FromJsonOverwrite(json, m_GlobalData);
		}
		catch
		{
			return false;
		}
		m_Data.Clear();
		int num = m_GlobalData.m_Keys.Length;
		for (int i = 0; i < num; i++)
		{
			m_Data[m_GlobalData.m_Keys[i]] = m_GlobalData.m_Entries[i];
		}
		return true;
	}

	public void Set(string key, int value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntInt obj = new EntInt(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out int value, int def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntInt entInt = JsonUtility.FromJson<EntInt>(entry.m_JSON);
				value = entInt.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, float value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntFloat obj = new EntFloat(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out float value, float def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntFloat entFloat = JsonUtility.FromJson<EntFloat>(entry.m_JSON);
				value = entFloat.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, string value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntString obj = new EntString(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out string value, string def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntString entString = JsonUtility.FromJson<EntString>(entry.m_JSON);
				value = entString.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, bool value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntBool obj = new EntBool(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out bool value, bool def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntBool entBool = JsonUtility.FromJson<EntBool>(entry.m_JSON);
				value = entBool.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, long[] value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntInt64Array obj = new EntInt64Array(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out long[] value, long[] def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntInt64Array entInt64Array = JsonUtility.FromJson<EntInt64Array>(entry.m_JSON);
				value = entInt64Array.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, int[] value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntIntArray obj = new EntIntArray(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out int[] value, int[] def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntIntArray entIntArray = JsonUtility.FromJson<EntIntArray>(entry.m_JSON);
				value = entIntArray.m_Value;
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	public void Set(string key, Dictionary<string, string> value)
	{
		Entry entry;
		FindOrCreateKey(key, out entry);
		EntDictionary obj = new EntDictionary(value);
		entry.m_JSON = JsonUtility.ToJson(obj);
	}

	public bool Get(string key, out Dictionary<string, string> value, Dictionary<string, string> def)
	{
		Entry entry;
		FindKey(key, out entry);
		if (entry != null)
		{
			try
			{
				EntDictionary entDictionary = JsonUtility.FromJson<EntDictionary>(entry.m_JSON);
				value = entDictionary.ToDictionary();
				return true;
			}
			catch
			{
				value = def;
				return false;
			}
		}
		value = def;
		return false;
	}

	private void FindKey(string key, out Entry entry)
	{
		if (m_Data.ContainsKey(key))
		{
			entry = m_Data[key];
		}
		else
		{
			entry = null;
		}
	}

	private void FindOrCreateKey(string key, out Entry entry)
	{
		if (!m_Data.ContainsKey(key))
		{
			m_Data[key] = new Entry();
		}
		entry = m_Data[key];
	}

	public void ResetSave()
	{
		m_GlobalData = new GlobalData();
		m_Data.Clear();
	}
}
