using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BitStream
{
	public class BitStreamWriter
	{
		private FastList<byte> _targetBuffer;

		private int _remaining;

		public BitStreamWriter(FastList<byte> bufferToWriteTo)
		{
			if (bufferToWriteTo == null)
			{
				throw new ArgumentNullException("bufferToWriteTo");
			}
			_targetBuffer = bufferToWriteTo;
		}

		public void Reset(FastList<byte> bufferToWriteTo)
		{
			_targetBuffer = bufferToWriteTo;
			_remaining = 0;
		}

		public void Write(string _text, Encoding _encoding)
		{
			byte[] bytes = _encoding.GetBytes(_text);
			if (bytes.Length * 8 + 10 > GetRemainingBitCount())
			{
				Write(0u, 10);
				return;
			}
			Write((uint)bytes.Length, 10);
			Write(bytes, 8 * bytes.Length);
		}

		public unsafe void Write(ref Quaternion value)
		{
			uint num = 0u;
			float num2 = value.x;
			uint* ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.y;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.z;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.w;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
		}

		public unsafe void Write(ref Vector3 value)
		{
			uint num = 0u;
			float num2 = value.x;
			uint* ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.y;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.z;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
		}

		public unsafe void Write(ref Vector2 value)
		{
			uint num = 0u;
			float num2 = value.x;
			uint* ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
			num2 = value.y;
			ptr = (uint*)(&num2);
			num = *ptr;
			Write(num, 32);
		}

		public unsafe void Write(double value)
		{
			ulong num = 0uL;
			ulong* ptr = (ulong*)(&value);
			num = *ptr;
			Write(num, 64);
		}

		public unsafe void Write(float value)
		{
			uint num = 0u;
			uint* ptr = (uint*)(&value);
			num = *ptr;
			Write(num, 32);
		}

		public void Write(bool value)
		{
			Write((byte)(value ? byte.MaxValue : 0), 1);
		}

		public void Write(byte[] bytes, int countOfBits)
		{
			if (bytes != null && countOfBits > 0 && countOfBits <= bytes.Length << 3)
			{
				int num = countOfBits / 8;
				int num2 = countOfBits % 8;
				int i;
				for (i = 0; i < num; i++)
				{
					Write(bytes[i], 8);
				}
				if (num2 > 0)
				{
					Write(bytes[i], num2);
				}
			}
		}

		public void Write(ulong bits, int countOfBits)
		{
			if (countOfBits <= 0 || countOfBits > 64)
			{
				return;
			}
			int num = countOfBits / 8;
			int num2 = countOfBits % 8;
			while (num >= 0)
			{
				byte bits2 = (byte)(bits >> num * 8);
				if (num2 > 0)
				{
					Write(bits2, num2);
				}
				if (num > 0)
				{
					num2 = 8;
				}
				num--;
			}
		}

		public void Write(uint bits, int countOfBits)
		{
			if (countOfBits <= 0 || countOfBits > 32)
			{
				return;
			}
			int num = countOfBits / 8;
			int num2 = countOfBits % 8;
			while (num >= 0)
			{
				byte bits2 = (byte)(bits >> num * 8);
				if (num2 > 0)
				{
					Write(bits2, num2);
				}
				if (num > 0)
				{
					num2 = 8;
				}
				num--;
			}
		}

		public void WriteReverse(uint bits, int countOfBits)
		{
			if (countOfBits > 0 && countOfBits <= 32)
			{
				int num = countOfBits / 8;
				int num2 = countOfBits % 8;
				if (num2 > 0)
				{
					num++;
				}
				for (int i = 0; i < num; i++)
				{
					byte bits2 = (byte)(bits >> i * 8);
					Write(bits2, 8);
				}
			}
		}

		public void Write(byte bits, int countOfBits)
		{
			if (countOfBits > 0 && countOfBits <= 8)
			{
				if (_remaining > 0)
				{
					byte b = _targetBuffer._items[_targetBuffer.Count - 1];
					b = ((countOfBits <= _remaining) ? ((byte)(b | (byte)((bits & (255 >> 8 - countOfBits)) << _remaining - countOfBits))) : ((byte)(b | (byte)((bits & (255 >> 8 - countOfBits)) >> countOfBits - _remaining))));
					_targetBuffer._items[_targetBuffer.Count - 1] = b;
				}
				if (countOfBits > _remaining)
				{
					_remaining = 8 - (countOfBits - _remaining);
					byte b = (byte)(bits << _remaining);
					_targetBuffer.Add(b);
				}
				else
				{
					_remaining -= countOfBits;
				}
			}
		}

		public int GetUsedBitCount()
		{
			return _targetBuffer.Count * 8 - _remaining;
		}

		public int GetRemainingBitCount()
		{
			return (_targetBuffer.Capacity - _targetBuffer.Count) * 8 + _remaining;
		}
	}
}
