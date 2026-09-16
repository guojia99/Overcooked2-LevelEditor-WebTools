using System;
using System.Text;
using UnityEngine;

namespace BitStream
{
	public class BitStreamReader
	{
		private byte[] _byteArray;

		private uint _bufferLengthInBits;

		private int _byteArrayIndex;

		private byte _partialByte;

		private int _cbitsInPartialByte;

		public bool EndOfStream
		{
			get
			{
				return 0 == _bufferLengthInBits;
			}
		}

		public int CurrentIndex
		{
			get
			{
				return _byteArrayIndex - 1;
			}
		}

		public BitStreamReader(byte[] buffer)
		{
			_byteArray = buffer;
			_bufferLengthInBits = (uint)(buffer.Length * 8);
		}

		public BitStreamReader(byte[] buffer, int startIndex)
		{
			if (startIndex < 0 || startIndex >= buffer.Length)
			{
				throw new ArgumentOutOfRangeException("startIndex");
			}
			_byteArray = buffer;
			_byteArrayIndex = startIndex;
			_bufferLengthInBits = (uint)((buffer.Length - startIndex) * 8);
		}

		public BitStreamReader(byte[] buffer, uint bufferLengthInBits)
			: this(buffer)
		{
			if (bufferLengthInBits <= buffer.Length * 8)
			{
				_bufferLengthInBits = bufferLengthInBits;
			}
		}

		public void Reset(byte[] buffer)
		{
			_byteArray = buffer;
			_bufferLengthInBits = (uint)(buffer.Length * 8);
			_byteArrayIndex = 0;
			_partialByte = 0;
			_cbitsInPartialByte = 0;
		}

		public void Reset(byte[] buffer, int usedLength)
		{
			Reset(buffer);
			_bufferLengthInBits = (uint)(usedLength * 8);
		}

		public void AdvanceToNextByteBoundary()
		{
			if (_cbitsInPartialByte > 0)
			{
				_cbitsInPartialByte = 0;
				_partialByte = 0;
			}
		}

		public void SkipToByteIndex(int index)
		{
			_byteArrayIndex = index;
			AdvanceToNextByteBoundary();
		}

		public long ReadUInt64(int countOfBits)
		{
			if (countOfBits > 64 || countOfBits <= 0)
			{
				return 0L;
			}
			long num = 0L;
			while (countOfBits > 0)
			{
				int num2 = 8;
				if (countOfBits < 8)
				{
					num2 = countOfBits;
				}
				num <<= num2;
				byte b = ReadByte(num2);
				num |= (int)b;
				countOfBits -= num2;
			}
			return num;
		}

		public ushort ReadUInt16(int countOfBits)
		{
			if (countOfBits > 16 || countOfBits <= 0)
			{
				return 0;
			}
			ushort num = 0;
			while (countOfBits > 0)
			{
				int num2 = 8;
				if (countOfBits < 8)
				{
					num2 = countOfBits;
				}
				num = (ushort)(num << num2);
				byte b = ReadByte(num2);
				num |= b;
				countOfBits -= num2;
			}
			return num;
		}

		public uint ReadUInt16Reverse(int countOfBits)
		{
			if (countOfBits > 16 || countOfBits <= 0)
			{
				return 0u;
			}
			ushort num = 0;
			int num2 = 0;
			while (countOfBits > 0)
			{
				int num3 = 8;
				if (countOfBits < 8)
				{
					num3 = countOfBits;
				}
				ushort num4 = ReadByte(num3);
				num4 = (ushort)(num4 << num2 * 8);
				num |= num4;
				num2++;
				countOfBits -= num3;
			}
			return num;
		}

		public uint ReadUInt32(int countOfBits)
		{
			if (countOfBits > 32 || countOfBits <= 0)
			{
				return 0u;
			}
			uint num = 0u;
			while (countOfBits > 0)
			{
				int num2 = 8;
				if (countOfBits < 8)
				{
					num2 = countOfBits;
				}
				num <<= num2;
				byte b = ReadByte(num2);
				num |= b;
				countOfBits -= num2;
			}
			return num;
		}

		public uint ReadUInt32Reverse(int countOfBits)
		{
			if (countOfBits > 32 || countOfBits <= 0)
			{
				return 0u;
			}
			uint num = 0u;
			int num2 = 0;
			while (countOfBits > 0)
			{
				int num3 = 8;
				if (countOfBits < 8)
				{
					num3 = countOfBits;
				}
				uint num4 = ReadByte(num3);
				num4 <<= num2 * 8;
				num |= num4;
				num2++;
				countOfBits -= num3;
			}
			return num;
		}

		public bool ReadBit()
		{
			byte b = ReadByte(1);
			return (b & 1) == 1;
		}

		public byte ReadByte(int countOfBits)
		{
			if (EndOfStream)
			{
				return 0;
			}
			if (countOfBits > 8 || countOfBits <= 0)
			{
				return 0;
			}
			if (countOfBits > _bufferLengthInBits)
			{
				return 0;
			}
			_bufferLengthInBits -= (uint)countOfBits;
			byte b = 0;
			if (_cbitsInPartialByte >= countOfBits)
			{
				int num = 8 - countOfBits;
				b = (byte)(_partialByte >> num);
				_partialByte = (byte)(_partialByte << countOfBits);
				_cbitsInPartialByte -= countOfBits;
			}
			else
			{
				byte b2 = _byteArray[_byteArrayIndex];
				_byteArrayIndex++;
				int num2 = 8 - countOfBits;
				b = (byte)(_partialByte >> num2);
				int num3 = Math.Abs(countOfBits - _cbitsInPartialByte - 8);
				b |= (byte)(b2 >> num3);
				_partialByte = (byte)(b2 << countOfBits - _cbitsInPartialByte);
				_cbitsInPartialByte = 8 - (countOfBits - _cbitsInPartialByte);
			}
			return b;
		}

		public unsafe float ReadFloat32()
		{
			float num = 0f;
			uint num2 = ReadUInt32(32);
			float* ptr = (float*)(&num2);
			return *ptr;
		}

		public unsafe double ReadFloat64()
		{
			double num = 0.0;
			long num2 = ReadUInt64(64);
			double* ptr = (double*)(&num2);
			return *ptr;
		}

		public unsafe void ReadVector3(ref Vector3 value)
		{
			uint num = ReadUInt32(32);
			float* ptr = (float*)(&num);
			value.x = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.y = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.z = *ptr;
		}

		public unsafe void ReadVector2(ref Vector2 value)
		{
			uint num = ReadUInt32(32);
			float* ptr = (float*)(&num);
			value.x = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.y = *ptr;
		}

		public unsafe void ReadQuaternion(ref Quaternion value)
		{
			uint num = ReadUInt32(32);
			float* ptr = (float*)(&num);
			value.x = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.y = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.z = *ptr;
			num = ReadUInt32(32);
			ptr = (float*)(&num);
			value.w = *ptr;
		}

		public string ReadString(Encoding _encoding)
		{
			if (EndOfStream)
			{
				return string.Empty;
			}
			uint num = ReadUInt32(10);
			if (num == 0)
			{
				return string.Empty;
			}
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				byte b = ReadByte(8);
				array[i] = b;
			}
			return _encoding.GetString(array);
		}
	}
}
