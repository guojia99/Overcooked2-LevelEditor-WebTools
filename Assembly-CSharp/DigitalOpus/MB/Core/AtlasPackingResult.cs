using UnityEngine;

namespace DigitalOpus.MB.Core
{
	public class AtlasPackingResult
	{
		public int atlasX;

		public int atlasY;

		public int usedW;

		public int usedH;

		public Rect[] rects;

		public int[] srcImgIdxs;

		public object data;
	}
}
