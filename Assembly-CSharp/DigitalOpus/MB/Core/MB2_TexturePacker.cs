using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	public class MB2_TexturePacker
	{
		private enum NodeType
		{
			Container = 0,
			maxDim = 1,
			regular = 2
		}

		private class PixRect
		{
			public int x;

			public int y;

			public int w;

			public int h;

			public PixRect()
			{
			}

			public PixRect(int xx, int yy, int ww, int hh)
			{
				x = xx;
				y = yy;
				w = ww;
				h = hh;
			}

			public override string ToString()
			{
				return string.Format("x={0},y={1},w={2},h={3}", x, y, w, h);
			}
		}

		private class Image
		{
			public int imgId;

			public int w;

			public int h;

			public int x;

			public int y;

			public Image(int id, int tw, int th, int padding, int minImageSizeX, int minImageSizeY)
			{
				imgId = id;
				w = Mathf.Max(tw + padding * 2, minImageSizeX);
				h = Mathf.Max(th + padding * 2, minImageSizeY);
			}

			public Image(Image im)
			{
				imgId = im.imgId;
				w = im.w;
				h = im.h;
				x = im.x;
				y = im.y;
			}
		}

		private class ImgIDComparer : IComparer<Image>
		{
			public int Compare(Image x, Image y)
			{
				if (x.imgId > y.imgId)
				{
					return 1;
				}
				if (x.imgId == y.imgId)
				{
					return 0;
				}
				return -1;
			}
		}

		private class ImageHeightComparer : IComparer<Image>
		{
			public int Compare(Image x, Image y)
			{
				if (x.h > y.h)
				{
					return -1;
				}
				if (x.h == y.h)
				{
					return 0;
				}
				return 1;
			}
		}

		private class ImageWidthComparer : IComparer<Image>
		{
			public int Compare(Image x, Image y)
			{
				if (x.w > y.w)
				{
					return -1;
				}
				if (x.w == y.w)
				{
					return 0;
				}
				return 1;
			}
		}

		private class ImageAreaComparer : IComparer<Image>
		{
			public int Compare(Image x, Image y)
			{
				int num = x.w * x.h;
				int num2 = y.w * y.h;
				if (num > num2)
				{
					return -1;
				}
				if (num == num2)
				{
					return 0;
				}
				return 1;
			}
		}

		private class ProbeResult
		{
			public int w;

			public int h;

			public int outW;

			public int outH;

			public Node root;

			public bool largerOrEqualToMaxDim;

			public float efficiency;

			public float squareness;

			public float totalAtlasArea;

			public int numAtlases;

			public void Set(int ww, int hh, int outw, int outh, Node r, bool fits, float e, float sq)
			{
				w = ww;
				h = hh;
				outW = outw;
				outH = outh;
				root = r;
				largerOrEqualToMaxDim = fits;
				efficiency = e;
				squareness = sq;
			}

			public float GetScore(bool doPowerOfTwoScore)
			{
				float num = ((!largerOrEqualToMaxDim) ? 0f : 1f);
				if (doPowerOfTwoScore)
				{
					return num * 2f + efficiency;
				}
				return squareness + 2f * efficiency + num;
			}

			public void PrintTree()
			{
				printTree(root, "  ");
			}
		}

		private class Node
		{
			public NodeType isFullAtlas;

			public Node[] child = new Node[2];

			public PixRect r;

			public Image img;

			public Node(NodeType rootType)
			{
				isFullAtlas = rootType;
			}

			private bool isLeaf()
			{
				if (child[0] == null || child[1] == null)
				{
					return true;
				}
				return false;
			}

			public Node Insert(Image im, bool handed)
			{
				int num;
				int num2;
				if (handed)
				{
					num = 0;
					num2 = 1;
				}
				else
				{
					num = 1;
					num2 = 0;
				}
				if (!isLeaf())
				{
					Node node = child[num].Insert(im, handed);
					if (node != null)
					{
						return node;
					}
					return child[num2].Insert(im, handed);
				}
				if (img != null)
				{
					return null;
				}
				if (r.w < im.w || r.h < im.h)
				{
					return null;
				}
				if (r.w == im.w && r.h == im.h)
				{
					img = im;
					return this;
				}
				child[num] = new Node(NodeType.regular);
				child[num2] = new Node(NodeType.regular);
				int num3 = r.w - im.w;
				int num4 = r.h - im.h;
				if (num3 > num4)
				{
					child[num].r = new PixRect(r.x, r.y, im.w, r.h);
					child[num2].r = new PixRect(r.x + im.w, r.y, r.w - im.w, r.h);
				}
				else
				{
					child[num].r = new PixRect(r.x, r.y, r.w, im.h);
					child[num2].r = new PixRect(r.x, r.y + im.h, r.w, r.h - im.h);
				}
				return child[num].Insert(im, handed);
			}
		}

		public MB2_LogLevel LOG_LEVEL = MB2_LogLevel.info;

		private ProbeResult bestRoot;

		public int atlasY;

		public bool doPowerOfTwoTextures = true;

		private static void printTree(Node r, string spc)
		{
			Debug.Log(spc + "Nd img=" + (r.img != null) + " r=" + r.r);
			if (r.child[0] != null)
			{
				printTree(r.child[0], spc + "      ");
			}
			if (r.child[1] != null)
			{
				printTree(r.child[1], spc + "      ");
			}
		}

		private static void flattenTree(Node r, List<Image> putHere)
		{
			if (r.img != null)
			{
				r.img.x = r.r.x;
				r.img.y = r.r.y;
				putHere.Add(r.img);
			}
			if (r.child[0] != null)
			{
				flattenTree(r.child[0], putHere);
			}
			if (r.child[1] != null)
			{
				flattenTree(r.child[1], putHere);
			}
		}

		private static void drawGizmosNode(Node r)
		{
			Vector3 size = new Vector3(r.r.w, r.r.h, 0f);
			Vector3 center = new Vector3((float)r.r.x + size.x / 2f, (float)(-r.r.y) - size.y / 2f, 0f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(center, size);
			if (r.img != null)
			{
				Gizmos.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
				size = new Vector3(r.img.w, r.img.h, 0f);
				center = new Vector3((float)r.r.x + size.x / 2f, (float)(-r.r.y) - size.y / 2f, 0f);
				Gizmos.DrawCube(center, size);
			}
			if (r.child[0] != null)
			{
				Gizmos.color = Color.red;
				drawGizmosNode(r.child[0]);
			}
			if (r.child[1] != null)
			{
				Gizmos.color = Color.green;
				drawGizmosNode(r.child[1]);
			}
		}

		private static Texture2D createFilledTex(Color c, int w, int h)
		{
			Texture2D texture2D = new Texture2D(w, h);
			for (int i = 0; i < w; i++)
			{
				for (int j = 0; j < h; j++)
				{
					texture2D.SetPixel(i, j, c);
				}
			}
			texture2D.Apply();
			return texture2D;
		}

		public void DrawGizmos()
		{
			if (bestRoot != null)
			{
				drawGizmosNode(bestRoot.root);
				Gizmos.color = Color.yellow;
				Vector3 size = new Vector3(bestRoot.outW, -bestRoot.outH, 0f);
				Vector3 center = new Vector3(size.x / 2f, size.y / 2f, 0f);
				Gizmos.DrawWireCube(center, size);
			}
		}

		private bool ProbeSingleAtlas(Image[] imgsToAdd, int idealAtlasW, int idealAtlasH, float imgArea, int maxAtlasDim, ProbeResult pr)
		{
			Node node = new Node(NodeType.maxDim);
			node.r = new PixRect(0, 0, idealAtlasW, idealAtlasH);
			for (int i = 0; i < imgsToAdd.Length; i++)
			{
				Node node2 = node.Insert(imgsToAdd[i], false);
				if (node2 == null)
				{
					return false;
				}
				if (i != imgsToAdd.Length - 1)
				{
					continue;
				}
				int x = 0;
				int y = 0;
				GetExtent(node, ref x, ref y);
				int num = x;
				int num2 = y;
				bool flag;
				float num6;
				float num7;
				if (doPowerOfTwoTextures)
				{
					num = Mathf.Min(CeilToNearestPowerOfTwo(x), maxAtlasDim);
					num2 = Mathf.Min(CeilToNearestPowerOfTwo(y), maxAtlasDim);
					if (num2 < num / 2)
					{
						num2 = num / 2;
					}
					if (num < num2 / 2)
					{
						num = num2 / 2;
					}
					flag = x <= maxAtlasDim && y <= maxAtlasDim;
					float num3 = Mathf.Max(1f, (float)x / (float)maxAtlasDim);
					float num4 = Mathf.Max(1f, (float)y / (float)maxAtlasDim);
					float num5 = (float)num * num3 * (float)num2 * num4;
					num6 = 1f - (num5 - imgArea) / num5;
					num7 = 1f;
				}
				else
				{
					num6 = 1f - ((float)(x * y) - imgArea) / (float)(x * y);
					num7 = ((x >= y) ? ((float)y / (float)x) : ((float)x / (float)y));
					flag = x <= maxAtlasDim && y <= maxAtlasDim;
				}
				pr.Set(x, y, num, num2, node, flag, num6, num7);
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug("Probe success efficiency w=" + x + " h=" + y + " e=" + num6 + " sq=" + num7 + " fits=" + flag);
				}
				return true;
			}
			Debug.LogError("Should never get here.");
			return false;
		}

		private bool ProbeMultiAtlas(Image[] imgsToAdd, int idealAtlasW, int idealAtlasH, float imgArea, int maxAtlasDim, ProbeResult pr)
		{
			int num = 0;
			Node node = new Node(NodeType.maxDim);
			node.r = new PixRect(0, 0, idealAtlasW, idealAtlasH);
			for (int i = 0; i < imgsToAdd.Length; i++)
			{
				Node node2 = node.Insert(imgsToAdd[i], false);
				if (node2 == null)
				{
					if (imgsToAdd[i].x > idealAtlasW && imgsToAdd[i].y > idealAtlasH)
					{
						return false;
					}
					Node node3 = new Node(NodeType.Container);
					node3.r = new PixRect(0, 0, node.r.w + idealAtlasW, idealAtlasH);
					Node node4 = new Node(NodeType.maxDim);
					node4.r = new PixRect(node.r.w, 0, idealAtlasW, idealAtlasH);
					node3.child[1] = node4;
					node3.child[0] = node;
					node = node3;
					node2 = node.Insert(imgsToAdd[i], false);
					num++;
				}
			}
			pr.numAtlases = num;
			pr.root = node;
			pr.totalAtlasArea = num * maxAtlasDim * maxAtlasDim;
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				MB2_Log.LogDebug("Probe success efficiency numAtlases=" + num + " totalArea=" + pr.totalAtlasArea);
			}
			return true;
		}

		private void GetExtent(Node r, ref int x, ref int y)
		{
			if (r.img != null)
			{
				if (r.r.x + r.img.w > x)
				{
					x = r.r.x + r.img.w;
				}
				if (r.r.y + r.img.h > y)
				{
					y = r.r.y + r.img.h;
				}
			}
			if (r.child[0] != null)
			{
				GetExtent(r.child[0], ref x, ref y);
			}
			if (r.child[1] != null)
			{
				GetExtent(r.child[1], ref x, ref y);
			}
		}

		private int StepWidthHeight(int oldVal, int step, int maxDim)
		{
			if (doPowerOfTwoTextures && oldVal < maxDim)
			{
				return oldVal * 2;
			}
			int num = oldVal + step;
			if (num > maxDim && oldVal < maxDim)
			{
				num = maxDim;
			}
			return num;
		}

		public static int RoundToNearestPositivePowerOfTwo(int x)
		{
			int num = (int)Mathf.Pow(2f, Mathf.RoundToInt(Mathf.Log(x) / Mathf.Log(2f)));
			if (num == 0 || num == 1)
			{
				num = 2;
			}
			return num;
		}

		public static int CeilToNearestPowerOfTwo(int x)
		{
			int num = (int)Mathf.Pow(2f, Mathf.Ceil(Mathf.Log(x) / Mathf.Log(2f)));
			if (num == 0 || num == 1)
			{
				num = 2;
			}
			return num;
		}

		public AtlasPackingResult[] GetRects(List<Vector2> imgWidthHeights, int maxDimension, int padding)
		{
			return GetRects(imgWidthHeights, maxDimension, padding, false);
		}

		public AtlasPackingResult[] GetRects(List<Vector2> imgWidthHeights, int maxDimension, int padding, bool doMultiAtlas)
		{
			if (doMultiAtlas)
			{
				return _GetRectsMultiAtlas(imgWidthHeights, maxDimension, padding, 2 + padding * 2, 2 + padding * 2, 2 + padding * 2, 2 + padding * 2);
			}
			AtlasPackingResult atlasPackingResult = _GetRectsSingleAtlas(imgWidthHeights, maxDimension, padding, 2 + padding * 2, 2 + padding * 2, 2 + padding * 2, 2 + padding * 2, 0);
			if (atlasPackingResult == null)
			{
				return null;
			}
			return new AtlasPackingResult[1] { atlasPackingResult };
		}

		private AtlasPackingResult _GetRectsSingleAtlas(List<Vector2> imgWidthHeights, int maxDimension, int padding, int minImageSizeX, int minImageSizeY, int masterImageSizeX, int masterImageSizeY, int recursionDepth)
		{
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				Debug.Log(string.Format("_GetRects numImages={0}, maxDimension={1}, padding={2}, minImageSizeX={3}, minImageSizeY={4}, masterImageSizeX={5}, masterImageSizeY={6}, recursionDepth={7}", imgWidthHeights.Count, maxDimension, padding, minImageSizeX, minImageSizeY, masterImageSizeX, masterImageSizeY, recursionDepth));
			}
			if (recursionDepth > 10)
			{
				if (LOG_LEVEL >= MB2_LogLevel.error)
				{
					Debug.LogError("Maximum recursion depth reached. Couldn't find packing for these textures.");
				}
				return null;
			}
			float num = 0f;
			int num2 = 0;
			int num3 = 0;
			Image[] array = new Image[imgWidthHeights.Count];
			for (int i = 0; i < array.Length; i++)
			{
				int tw = (int)imgWidthHeights[i].x;
				int th = (int)imgWidthHeights[i].y;
				Image image = (array[i] = new Image(i, tw, th, padding, minImageSizeX, minImageSizeY));
				num += (float)(image.w * image.h);
				num2 = Mathf.Max(num2, image.w);
				num3 = Mathf.Max(num3, image.h);
			}
			if ((float)num3 / (float)num2 > 2f)
			{
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug("Using height Comparer");
				}
				Array.Sort(array, new ImageHeightComparer());
			}
			else if ((double)((float)num3 / (float)num2) < 0.5)
			{
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug("Using width Comparer");
				}
				Array.Sort(array, new ImageWidthComparer());
			}
			else
			{
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug("Using area Comparer");
				}
				Array.Sort(array, new ImageAreaComparer());
			}
			int num4 = (int)Mathf.Sqrt(num);
			int num5;
			int num6;
			if (doPowerOfTwoTextures)
			{
				num5 = (num6 = RoundToNearestPositivePowerOfTwo(num4));
				if (num2 > num5)
				{
					num5 = CeilToNearestPowerOfTwo(num5);
				}
				if (num3 > num6)
				{
					num6 = CeilToNearestPowerOfTwo(num6);
				}
			}
			else
			{
				num5 = num4;
				num6 = num4;
				if (num2 > num4)
				{
					num5 = num2;
					num6 = Mathf.Max(Mathf.CeilToInt(num / (float)num2), num3);
				}
				if (num3 > num4)
				{
					num5 = Mathf.Max(Mathf.CeilToInt(num / (float)num3), num2);
					num6 = num3;
				}
			}
			if (num5 == 0)
			{
				num5 = 4;
			}
			if (num6 == 0)
			{
				num6 = 4;
			}
			int num7 = (int)((float)num5 * 0.15f);
			int num8 = (int)((float)num6 * 0.15f);
			if (num7 == 0)
			{
				num7 = 1;
			}
			if (num8 == 0)
			{
				num8 = 1;
			}
			int num9 = 2;
			int num10 = num5;
			int num11 = num6;
			while (num9 >= 1 && num11 < num4 * 1000)
			{
				bool flag = false;
				num9 = 0;
				num10 = num5;
				while (!flag && num10 < num4 * 1000)
				{
					ProbeResult probeResult = new ProbeResult();
					if (LOG_LEVEL >= MB2_LogLevel.trace)
					{
						Debug.Log("Probing h=" + num11 + " w=" + num10);
					}
					if (ProbeSingleAtlas(array, num10, num11, num, maxDimension, probeResult))
					{
						flag = true;
						if (bestRoot == null)
						{
							bestRoot = probeResult;
						}
						else if (probeResult.GetScore(doPowerOfTwoTextures) > bestRoot.GetScore(doPowerOfTwoTextures))
						{
							bestRoot = probeResult;
						}
						continue;
					}
					num9++;
					num10 = StepWidthHeight(num10, num7, maxDimension);
					if (LOG_LEVEL >= MB2_LogLevel.trace)
					{
						MB2_Log.LogDebug("increasing Width h=" + num11 + " w=" + num10);
					}
				}
				num11 = StepWidthHeight(num11, num8, maxDimension);
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug("increasing Height h=" + num11 + " w=" + num10);
				}
			}
			if (bestRoot == null)
			{
				return null;
			}
			int num12 = 0;
			int num13 = 0;
			if (doPowerOfTwoTextures)
			{
				num12 = Mathf.Min(CeilToNearestPowerOfTwo(bestRoot.w), maxDimension);
				num13 = Mathf.Min(CeilToNearestPowerOfTwo(bestRoot.h), maxDimension);
				if (num13 < num12 / 2)
				{
					num13 = num12 / 2;
				}
				if (num12 < num13 / 2)
				{
					num12 = num13 / 2;
				}
			}
			else
			{
				num12 = Mathf.Min(bestRoot.w, maxDimension);
				num13 = Mathf.Min(bestRoot.h, maxDimension);
			}
			bestRoot.outW = num12;
			bestRoot.outH = num13;
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				Debug.Log("Best fit found: atlasW=" + num12 + " atlasH" + num13 + " w=" + bestRoot.w + " h=" + bestRoot.h + " efficiency=" + bestRoot.efficiency + " squareness=" + bestRoot.squareness + " fits in max dimension=" + bestRoot.largerOrEqualToMaxDim);
			}
			List<Image> list = new List<Image>();
			flattenTree(bestRoot.root, list);
			list.Sort(new ImgIDComparer());
			AtlasPackingResult result = ScaleAtlasToFitMaxDim(bestRoot, imgWidthHeights, list, maxDimension, padding, minImageSizeX, minImageSizeY, masterImageSizeX, masterImageSizeY, num12, num13, recursionDepth);
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				MB2_Log.LogDebug(string.Format("Done GetRects atlasW={0} atlasH={1}", bestRoot.w, bestRoot.h));
			}
			return result;
		}

		private AtlasPackingResult ScaleAtlasToFitMaxDim(ProbeResult root, List<Vector2> imgWidthHeights, List<Image> images, int maxDimension, int padding, int minImageSizeX, int minImageSizeY, int masterImageSizeX, int masterImageSizeY, int outW, int outH, int recursionDepth)
		{
			int minImageSizeX2 = minImageSizeX;
			int minImageSizeY2 = minImageSizeY;
			bool flag = false;
			float num = (float)padding / (float)outW;
			if (root.w > maxDimension)
			{
				num = (float)padding / (float)maxDimension;
				float num2 = (float)maxDimension / (float)root.w;
				if (LOG_LEVEL >= MB2_LogLevel.warn)
				{
					Debug.LogWarning("Packing exceeded atlas width shrinking to " + num2);
				}
				for (int i = 0; i < images.Count; i++)
				{
					Image image = images[i];
					if ((float)image.w * num2 < (float)masterImageSizeX)
					{
						if (LOG_LEVEL >= MB2_LogLevel.debug)
						{
							Debug.Log("Small images are being scaled to zero. Will need to redo packing with larger minTexSizeX.");
						}
						flag = true;
						minImageSizeX2 = Mathf.CeilToInt((float)minImageSizeX / num2);
					}
					int num3 = (int)((float)(image.x + image.w) * num2);
					image.x = (int)(num2 * (float)image.x);
					image.w = num3 - image.x;
				}
				outW = maxDimension;
			}
			float num4 = (float)padding / (float)outH;
			if (root.h > maxDimension)
			{
				num4 = (float)padding / (float)maxDimension;
				float num5 = (float)maxDimension / (float)root.h;
				if (LOG_LEVEL >= MB2_LogLevel.warn)
				{
					Debug.LogWarning("Packing exceeded atlas height shrinking to " + num5);
				}
				for (int j = 0; j < images.Count; j++)
				{
					Image image2 = images[j];
					if ((float)image2.h * num5 < (float)masterImageSizeY)
					{
						if (LOG_LEVEL >= MB2_LogLevel.debug)
						{
							Debug.Log("Small images are being scaled to zero. Will need to redo packing with larger minTexSizeY.");
						}
						flag = true;
						minImageSizeY2 = Mathf.CeilToInt((float)minImageSizeY / num5);
					}
					int num6 = (int)((float)(image2.y + image2.h) * num5);
					image2.y = (int)(num5 * (float)image2.y);
					image2.h = num6 - image2.y;
				}
				outH = maxDimension;
			}
			if (!flag)
			{
				AtlasPackingResult atlasPackingResult = new AtlasPackingResult();
				atlasPackingResult.rects = new Rect[images.Count];
				atlasPackingResult.srcImgIdxs = new int[images.Count];
				atlasPackingResult.atlasX = outW;
				atlasPackingResult.atlasY = outH;
				atlasPackingResult.usedW = -1;
				atlasPackingResult.usedH = -1;
				for (int k = 0; k < images.Count; k++)
				{
					Image image3 = images[k];
					Rect rect = (atlasPackingResult.rects[k] = new Rect((float)image3.x / (float)outW + num, (float)image3.y / (float)outH + num4, (float)image3.w / (float)outW - num * 2f, (float)image3.h / (float)outH - num4 * 2f));
					atlasPackingResult.srcImgIdxs[k] = image3.imgId;
					if (LOG_LEVEL >= MB2_LogLevel.debug)
					{
						MB2_Log.LogDebug("Image: " + k + " imgID=" + image3.imgId + " x=" + rect.x * (float)outW + " y=" + rect.y * (float)outH + " w=" + rect.width * (float)outW + " h=" + rect.height * (float)outH + " padding=" + padding);
					}
				}
				return atlasPackingResult;
			}
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				Debug.Log("==================== REDOING PACKING ================");
			}
			root = null;
			return _GetRectsSingleAtlas(imgWidthHeights, maxDimension, padding, minImageSizeX2, minImageSizeY2, masterImageSizeX, masterImageSizeY, recursionDepth + 1);
		}

		private AtlasPackingResult[] _GetRectsMultiAtlas(List<Vector2> imgWidthHeights, int maxDimensionPassed, int padding, int minImageSizeX, int minImageSizeY, int masterImageSizeX, int masterImageSizeY)
		{
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				Debug.Log(string.Format("_GetRects numImages={0}, maxDimension={1}, padding={2}, minImageSizeX={3}, minImageSizeY={4}, masterImageSizeX={5}, masterImageSizeY={6}", imgWidthHeights.Count, maxDimensionPassed, padding, minImageSizeX, minImageSizeY, masterImageSizeX, masterImageSizeY));
			}
			float num = 0f;
			int a = 0;
			int a2 = 0;
			Image[] array = new Image[imgWidthHeights.Count];
			int num2 = maxDimensionPassed;
			if (doPowerOfTwoTextures)
			{
				num2 = RoundToNearestPositivePowerOfTwo(num2);
			}
			for (int i = 0; i < array.Length; i++)
			{
				int a3 = (int)imgWidthHeights[i].x;
				int a4 = (int)imgWidthHeights[i].y;
				a3 = Mathf.Min(a3, num2 - padding * 2);
				a4 = Mathf.Min(a4, num2 - padding * 2);
				Image image = (array[i] = new Image(i, a3, a4, padding, minImageSizeX, minImageSizeY));
				num += (float)(image.w * image.h);
				a = Mathf.Max(a, image.w);
				a2 = Mathf.Max(a2, image.h);
			}
			int num3;
			int num4;
			if (doPowerOfTwoTextures)
			{
				num3 = RoundToNearestPositivePowerOfTwo(num2);
				num4 = RoundToNearestPositivePowerOfTwo(num2);
			}
			else
			{
				num3 = num2;
				num4 = num2;
			}
			if (num4 == 0)
			{
				num4 = 4;
			}
			if (num3 == 0)
			{
				num3 = 4;
			}
			ProbeResult probeResult = new ProbeResult();
			Array.Sort(array, new ImageHeightComparer());
			if (ProbeMultiAtlas(array, num4, num3, num, num2, probeResult))
			{
				bestRoot = probeResult;
			}
			Array.Sort(array, new ImageWidthComparer());
			if (ProbeMultiAtlas(array, num4, num3, num, num2, probeResult) && probeResult.totalAtlasArea < bestRoot.totalAtlasArea)
			{
				bestRoot = probeResult;
			}
			Array.Sort(array, new ImageAreaComparer());
			if (ProbeMultiAtlas(array, num4, num3, num, num2, probeResult) && probeResult.totalAtlasArea < bestRoot.totalAtlasArea)
			{
				bestRoot = probeResult;
			}
			if (bestRoot == null)
			{
				return null;
			}
			if (LOG_LEVEL >= MB2_LogLevel.debug)
			{
				Debug.Log("Best fit found: w=" + bestRoot.w + " h=" + bestRoot.h + " efficiency=" + bestRoot.efficiency + " squareness=" + bestRoot.squareness + " fits in max dimension=" + bestRoot.largerOrEqualToMaxDim);
			}
			List<AtlasPackingResult> list = new List<AtlasPackingResult>();
			List<Node> list2 = new List<Node>();
			Stack<Node> stack = new Stack<Node>();
			for (Node node = bestRoot.root; node != null; node = node.child[0])
			{
				stack.Push(node);
			}
			while (stack.Count > 0)
			{
				Node node = stack.Pop();
				if (node.isFullAtlas == NodeType.maxDim)
				{
					list2.Add(node);
				}
				if (node.child[1] != null)
				{
					for (node = node.child[1]; node != null; node = node.child[0])
					{
						stack.Push(node);
					}
				}
			}
			for (int j = 0; j < list2.Count; j++)
			{
				List<Image> list3 = new List<Image>();
				flattenTree(list2[j], list3);
				Rect[] array2 = new Rect[list3.Count];
				int[] array3 = new int[list3.Count];
				for (int k = 0; k < list3.Count; k++)
				{
					array2[k] = new Rect(list3[k].x - list2[j].r.x, list3[k].y, list3[k].w, list3[k].h);
					array3[k] = list3[k].imgId;
				}
				AtlasPackingResult atlasPackingResult = new AtlasPackingResult();
				GetExtent(list2[j], ref atlasPackingResult.usedW, ref atlasPackingResult.usedH);
				atlasPackingResult.usedW -= list2[j].r.x;
				int w = list2[j].r.w;
				int h = list2[j].r.h;
				if (doPowerOfTwoTextures)
				{
					w = Mathf.Min(CeilToNearestPowerOfTwo(atlasPackingResult.usedW), list2[j].r.w);
					h = Mathf.Min(CeilToNearestPowerOfTwo(atlasPackingResult.usedH), list2[j].r.h);
					if (h < w / 2)
					{
						h = w / 2;
					}
					if (w < h / 2)
					{
						w = h / 2;
					}
				}
				else
				{
					w = atlasPackingResult.usedW;
					h = atlasPackingResult.usedH;
				}
				atlasPackingResult.atlasY = h;
				atlasPackingResult.atlasX = w;
				atlasPackingResult.rects = array2;
				atlasPackingResult.srcImgIdxs = array3;
				list.Add(atlasPackingResult);
				normalizeRects(atlasPackingResult, padding);
				if (LOG_LEVEL >= MB2_LogLevel.debug)
				{
					MB2_Log.LogDebug(string.Format("Done GetRects "));
				}
			}
			return list.ToArray();
		}

		private void normalizeRects(AtlasPackingResult rr, int padding)
		{
			for (int i = 0; i < rr.rects.Length; i++)
			{
				rr.rects[i].x = (rr.rects[i].x + (float)padding) / (float)rr.atlasX;
				rr.rects[i].y = (rr.rects[i].y + (float)padding) / (float)rr.atlasY;
				rr.rects[i].width = (rr.rects[i].width - (float)(padding * 2)) / (float)rr.atlasX;
				rr.rects[i].height = (rr.rects[i].height - (float)(padding * 2)) / (float)rr.atlasY;
			}
		}
	}
}
