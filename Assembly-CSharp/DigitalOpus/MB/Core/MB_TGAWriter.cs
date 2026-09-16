using System.IO;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	public static class MB_TGAWriter
	{
		public static void Write(Color[] pixels, int width, int height, string path)
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			FileStream output = File.Create(path);
			Write(pixels, width, height, output);
		}

		public static void Write(Color[] pixels, int width, int height, Stream output)
		{
			byte[] array = new byte[pixels.Length * 4];
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					Color color = pixels[num];
					array[num2] = (byte)(color.b * 255f);
					array[num2 + 1] = (byte)(color.g * 255f);
					array[num2 + 2] = (byte)(color.r * 255f);
					array[num2 + 3] = (byte)(color.a * 255f);
					num++;
					num2 += 4;
				}
			}
			byte[] buffer = new byte[18]
			{
				0,
				0,
				2,
				0,
				0,
				0,
				0,
				0,
				0,
				0,
				0,
				0,
				(byte)(width & 0xFF),
				(byte)((width & 0xFF00) >> 8),
				(byte)(height & 0xFF),
				(byte)((height & 0xFF00) >> 8),
				32,
				0
			};
			using (BinaryWriter binaryWriter = new BinaryWriter(output))
			{
				binaryWriter.Write(buffer);
				binaryWriter.Write(array);
			}
		}
	}
}
