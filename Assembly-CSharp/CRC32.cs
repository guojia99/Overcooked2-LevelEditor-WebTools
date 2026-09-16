using System;
using UnityEngine;

public class CRC32
{
	public const uint c_HashSize = 4u;

	private const uint poly = 1491524015u;

	private const uint seed = 3605721660u;

	private static uint[] s_table;

	private CRC32()
	{
		if (s_table == null)
		{
			MakeTable();
		}
	}

	protected void MakeTable()
	{
		s_table = new uint[256];
		for (uint num = 0u; num < 256; num++)
		{
			uint num2 = num;
			for (uint num3 = 0u; num3 < 8; num3++)
			{
				num2 = (((num2 & 1) != 1) ? (num2 >> 1) : (num2 ^ 0x58E6D9AF));
			}
			s_table[num] = num2;
		}
	}

	public static uint Calculate(byte[] _data)
	{
		return new CRC32().CalculateHash(_data);
	}

	public uint CalculateHash(byte[] _data)
	{
		return CalculateHash(_data, 0u, (uint)_data.Length);
	}

	public static uint Calculate(byte[] _data, uint _size)
	{
		return new CRC32().CalculateHash(_data, _size);
	}

	public uint CalculateHash(byte[] _data, uint _size)
	{
		return CalculateHash(_data, 0u, _size);
	}

	public static uint Calculate(byte[] _data, uint _start, uint _size)
	{
		return new CRC32().CalculateHash(_data, _start, _size);
	}

	public uint CalculateHash(byte[] _data, uint _start, uint _size)
	{
		uint num = 3605721660u;
		for (uint num2 = _start; num2 < _start + _size; num2++)
		{
			num = (num >> 8) ^ s_table[_data[num2] ^ (num & 0xFF)];
		}
		return num;
	}

	public static void Append(ref byte[] _buffer)
	{
		new CRC32().AppendHash(ref _buffer);
	}

	public void AppendHash(ref byte[] _buffer)
	{
		AppendHash(ref _buffer, 0u, (uint)_buffer.Length);
	}

	public static void Append(ref byte[] _buffer, uint _start, uint _size)
	{
		new CRC32().AppendHash(ref _buffer, _start, _size);
	}

	public void AppendHash(ref byte[] _buffer, uint _start, uint _size)
	{
		AppendHash(ref _buffer, 0u, _size, CalculateHash(_buffer, _start, _size));
	}

	public static void Append(ref byte[] _buffer, uint _start, uint _size, uint _hash)
	{
		new CRC32().AppendHash(ref _buffer, _start, _size, _hash);
	}

	public void AppendHash(ref byte[] _buffer, uint _start, uint _size, uint _hash)
	{
		byte[] bytes = BitConverter.GetBytes(_hash);
		int num = _buffer.Length - (int)(_start + _size + 4);
		if (num < 0)
		{
			num = Mathf.Abs(num);
			if ((long)num < 4L)
			{
				_buffer = _buffer.AllRemoved_Generic((int idx, byte val) => idx > _start + _size);
			}
			_buffer = _buffer.Union(bytes);
		}
		else
		{
			int num2 = (int)_size;
			for (int num3 = 0; num3 < bytes.Length; num3++)
			{
				_buffer[num3 + num2] = bytes[num3];
			}
		}
	}

	public static bool Validate(byte[] _buffer, uint _size)
	{
		return new CRC32().HasValidHash(_buffer, _size);
	}

	public bool HasValidHash(byte[] _buffer, uint _size)
	{
		return HasValidHash(_buffer, 0u, _size);
	}

	public static bool Validate(byte[] _buffer, uint _start, uint _size)
	{
		return new CRC32().HasValidHash(_buffer, _start, _size);
	}

	public bool HasValidHash(byte[] _buffer, uint _start, uint _size)
	{
		return HasValidHash(_buffer, _start, _size, _start + _size);
	}

	public static bool Validate(byte[] _buffer, uint _start, uint _size, uint _hashStar)
	{
		return new CRC32().HasValidHash(_buffer, _start, _size, _hashStar);
	}

	public bool HasValidHash(byte[] _buffer, uint _start, uint _size, uint _hashStart)
	{
		if (_hashStart + 4 > _buffer.Length)
		{
			return false;
		}
		uint num = CalculateHash(_buffer, _start, _size);
		uint num2 = BitConverter.ToUInt32(_buffer, (int)_hashStart);
		if (num != num2)
		{
			return false;
		}
		return true;
	}
}
