using System;
using System.Runtime.InteropServices;
using System.Security;

namespace BitStream
{
	internal static class Native
	{
		internal static readonly uint SizeOfInt;

		internal static readonly uint SizeOfUInt;

		internal static readonly uint SizeOfUShort;

		internal static readonly uint SizeOfByte;

		internal static readonly uint SizeOfFloat;

		internal static readonly uint SizeOfDouble;

		internal static readonly uint SizeOfGuid;

		internal static readonly uint SizeOfDecimal;

		internal const int BitsPerByte = 8;

		internal const int BitsPerShort = 16;

		internal const int BitsPerInt = 32;

		internal const int BitsPerLong = 64;

		internal const int BitsPerStringLength = 10;

		internal const int MaxFloatToIntValue = 2147483583;

		[SecurityCritical]
		[SecuritySafeCritical]
		static Native()
		{
			SizeOfInt = (uint)Marshal.SizeOf(typeof(int));
			SizeOfUInt = (uint)Marshal.SizeOf(typeof(uint));
			SizeOfUShort = (uint)Marshal.SizeOf(typeof(ushort));
			SizeOfByte = (uint)Marshal.SizeOf(typeof(byte));
			SizeOfFloat = (uint)Marshal.SizeOf(typeof(float));
			SizeOfDouble = (uint)Marshal.SizeOf(typeof(double));
			SizeOfGuid = (uint)Marshal.SizeOf(typeof(Guid));
			SizeOfDecimal = (uint)Marshal.SizeOf(typeof(decimal));
		}
	}
}
