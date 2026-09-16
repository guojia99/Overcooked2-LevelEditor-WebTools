using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class UnmanagedMemoryManager
{
	public class AllocInfo
	{
		public IntPtr ptr { get; private set; }

		public int size { get; private set; }

		public AllocInfo(IntPtr _ptr, int _size)
		{
			ptr = _ptr;
			size = _size;
		}
	}

	public static List<AllocInfo> s_NeedFreeList = new List<AllocInfo>();

	public static IntPtr Alloc(int size)
	{
		IntPtr intPtr = Marshal.AllocHGlobal(size);
		if (intPtr == IntPtr.Zero)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.Alloc : Marshal.AllocHGlobal(" + size + ") failed.");
		}
		try
		{
			s_NeedFreeList.Add(new AllocInfo(intPtr, size));
			return intPtr;
		}
		catch (OutOfMemoryException ex)
		{
			PiaPluginUtil.UnityLog(string.Concat("s_NeedFreeList.Add is OutOfMemoryException.[", ex, "]"));
			return IntPtr.Zero;
		}
	}

	public static IntPtr Alloc<T>()
	{
		int size = Marshal.SizeOf(typeof(T));
		return Alloc(size);
	}

	public static bool Free(IntPtr p)
	{
		if (p == IntPtr.Zero)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.Free : p == IntPtr.Zero");
			return false;
		}
		List<AllocInfo> list = s_NeedFreeList.FindAll(delegate(AllocInfo info)
		{
			if (info == null)
			{
				PiaPluginUtil.UnityLog("List.FindAll is failed.");
				return false;
			}
			return info.ptr == p;
		});
		if (list.Count != 1)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.Free : allocInfoList.Count:" + list.Count);
			for (int num = 0; num < list.Count; num++)
			{
				Debug.LogErrorFormat("[{0}] 0x{1:X} size:{2}", num, list[num].ptr, list[num].size);
			}
		}
		else
		{
			s_NeedFreeList.Remove(list[0]);
		}
		Marshal.FreeHGlobal(p);
		return true;
	}

	public static void DestroyStructure<T>(IntPtr p)
	{
		Marshal.DestroyStructure(p, typeof(T));
	}

	public static IntPtr WriteObject<T>(T obj, ref int bufferSize, int allocSize = 0)
	{
		if (obj == null)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.WriteObject : obj == null");
			bufferSize = 0;
			return IntPtr.Zero;
		}
		if (allocSize == 0)
		{
			bufferSize = Marshal.SizeOf(typeof(T));
		}
		else
		{
			if (allocSize < Marshal.SizeOf(typeof(T)))
			{
				PiaPluginUtil.UnityLog(string.Format("UnmanagedMemoryManager.WriteObject : allocSize({0}) < {1}", allocSize, Marshal.SizeOf(typeof(T))));
				bufferSize = 0;
				return IntPtr.Zero;
			}
			bufferSize = allocSize;
		}
		IntPtr intPtr = Alloc(bufferSize);
		if (intPtr == IntPtr.Zero)
		{
			bufferSize = 0;
			return intPtr;
		}
		Marshal.StructureToPtr(obj, intPtr, false);
		return intPtr;
	}

	public static bool ReadObject<T>(IntPtr p, ref T obj)
	{
		try
		{
			if (p == IntPtr.Zero)
			{
				PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadObject : p == IntPtr.Zero");
				return false;
			}
			obj = (T)Marshal.PtrToStructure(p, typeof(T));
		}
		catch (Exception ex)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadObject : exception " + ex);
		}
		return true;
	}

	public static IntPtr WriteArray<T>(T[] array, ref int bufferSize)
	{
		if (array.Length == 0)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.WriteArray : array.Length == 0");
			bufferSize = 0;
			return IntPtr.Zero;
		}
		int num = Marshal.SizeOf(typeof(T));
		bufferSize = num * array.Length;
		IntPtr intPtr = Alloc(bufferSize);
		if (intPtr == IntPtr.Zero)
		{
			bufferSize = 0;
			return intPtr;
		}
		IntPtr ptr = intPtr;
		for (int i = 0; i < array.Length; i++)
		{
			Marshal.StructureToPtr(array[i], ptr, false);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		return intPtr;
	}

	public static bool ReadArray<T>(IntPtr p, int arrayLength, ref T[] array)
	{
		try
		{
			if (p == IntPtr.Zero)
			{
				PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadArray : p == IntPtr.Zero");
				return false;
			}
			int num = Marshal.SizeOf(typeof(T));
			IntPtr ptr = p;
			for (int i = 0; i < arrayLength; i++)
			{
				array[i] = (T)Marshal.PtrToStructure(ptr, typeof(T));
				ptr = new IntPtr(ptr.ToInt64() + num);
			}
		}
		catch (Exception ex)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadArray : exception " + ex);
		}
		return true;
	}

	public static IntPtr WriteList<T>(List<T> list, ref int bufferSize)
	{
		if (list.Count == 0)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.WriteList : list.Count == 0");
			bufferSize = 0;
			return IntPtr.Zero;
		}
		int num = Marshal.SizeOf(typeof(T));
		bufferSize = num * list.Count;
		IntPtr intPtr = Alloc(bufferSize);
		if (intPtr == IntPtr.Zero)
		{
			bufferSize = 0;
			return intPtr;
		}
		IntPtr ptr = intPtr;
		foreach (T item in list)
		{
			Marshal.StructureToPtr(item, ptr, false);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		return intPtr;
	}

	public static bool ReadList<T>(IntPtr p, int listCount, ref List<T> list)
	{
		try
		{
			if (p == IntPtr.Zero)
			{
				PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadList : p == IntPtr.Zero");
				return false;
			}
			int num = Marshal.SizeOf(typeof(T));
			IntPtr ptr = p;
			list.Clear();
			for (int i = 0; i < listCount; i++)
			{
				list.Add((T)Marshal.PtrToStructure(ptr, typeof(T)));
				ptr = new IntPtr(ptr.ToInt64() + num);
			}
		}
		catch (Exception ex)
		{
			PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ReadList : exception " + ex);
		}
		return true;
	}

	public static IntPtr WriteUtf8(string str, ref int bufferSize)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		int num = Marshal.SizeOf(typeof(byte));
		bufferSize = num * (bytes.Length + 1);
		IntPtr intPtr = Alloc(bufferSize);
		IntPtr ptr = intPtr;
		for (int i = 0; i < bytes.Length; i++)
		{
			Marshal.WriteByte(ptr, bytes[i]);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		Marshal.WriteByte(ptr, 0);
		return intPtr;
	}

	public static string ReadUtf8(IntPtr pStr, int stringSize)
	{
		byte[] array = new byte[stringSize];
		int num = Marshal.SizeOf(typeof(byte));
		IntPtr ptr = pStr;
		for (int i = 0; i < stringSize; i++)
		{
			array[i] = Marshal.ReadByte(ptr);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		return Encoding.UTF8.GetString(array);
	}

	public static IntPtr WriteUtf16(string str, ref int bufferSize)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(str);
		int num = Marshal.SizeOf(typeof(byte));
		bufferSize = num * (bytes.Length + 2);
		IntPtr intPtr = Alloc(bufferSize);
		IntPtr ptr = intPtr;
		for (int i = 0; i < bytes.Length; i++)
		{
			Marshal.WriteByte(ptr, bytes[i]);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		Marshal.WriteByte(ptr, 0);
		ptr = new IntPtr(ptr.ToInt64() + num);
		Marshal.WriteByte(ptr, 0);
		return intPtr;
	}

	public static string ReadUtf16(IntPtr pStr, int stringSize)
	{
		byte[] array = new byte[stringSize];
		int num = Marshal.SizeOf(typeof(byte));
		IntPtr ptr = pStr;
		for (int i = 0; i < stringSize; i++)
		{
			array[i] = Marshal.ReadByte(ptr);
			ptr = new IntPtr(ptr.ToInt64() + num);
		}
		return Encoding.Unicode.GetString(array);
	}

	public static void ValidateAllocInfo()
	{
		PiaPluginUtil.UnityLog("UnmanagedMemoryManager.ValidateAllocInfo s_NeedFreeList count:" + s_NeedFreeList.Count);
		for (int i = 0; i < s_NeedFreeList.Count; i++)
		{
			Debug.LogFormat("[{0}] 0x{1:X} size:{2}", i, s_NeedFreeList[i].ptr.ToInt64(), s_NeedFreeList[i].size);
		}
	}
}
