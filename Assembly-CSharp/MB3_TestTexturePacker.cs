using System.Collections.Generic;
using DigitalOpus.MB.Core;
using UnityEngine;

public class MB3_TestTexturePacker : MonoBehaviour
{
	private MB2_TexturePacker texturePacker;

	public int numTex = 32;

	public int min = 126;

	public int max = 2046;

	public float xMult = 1f;

	public float yMult = 1f;

	public bool imgsMustBePowerOfTwo;

	public List<Vector2> imgsToAdd = new List<Vector2>();

	public int padding = 1;

	public int maxDim = 4096;

	public bool doPowerOfTwoTextures = true;

	public bool doMultiAtlas;

	public MB2_LogLevel logLevel;

	public string res;

	public AtlasPackingResult[] rs;

	[ContextMenu("Generate List Of Images To Add")]
	public void GenerateListOfImagesToAdd()
	{
		imgsToAdd = new List<Vector2>();
		for (int i = 0; i < numTex; i++)
		{
			Vector2 item = new Vector2(Mathf.RoundToInt((float)Random.Range(min, max) * xMult), Mathf.RoundToInt((float)Random.Range(min, max) * yMult));
			if (imgsMustBePowerOfTwo)
			{
				item.x = MB2_TexturePacker.RoundToNearestPositivePowerOfTwo((int)item.x);
				item.y = MB2_TexturePacker.RoundToNearestPositivePowerOfTwo((int)item.y);
			}
			imgsToAdd.Add(item);
		}
	}

	[ContextMenu("Run")]
	public void RunTestHarness()
	{
		texturePacker = new MB2_TexturePacker();
		texturePacker.doPowerOfTwoTextures = doPowerOfTwoTextures;
		texturePacker.LOG_LEVEL = logLevel;
		rs = texturePacker.GetRects(imgsToAdd, maxDim, padding, doMultiAtlas);
		if (rs != null)
		{
			Debug.Log("NumAtlas= " + rs.Length);
			for (int i = 0; i < rs.Length; i++)
			{
				for (int j = 0; j < rs[i].rects.Length; j++)
				{
					Rect rect = rs[i].rects[j];
					rect.x *= rs[i].atlasX;
					rect.y *= rs[i].atlasY;
					rect.width *= rs[i].atlasX;
					rect.height *= rs[i].atlasY;
					Debug.Log(rect.ToString("f5"));
				}
				Debug.Log("===============");
			}
			res = "mxX= " + rs[0].atlasX + " mxY= " + rs[0].atlasY;
		}
		else
		{
			res = "ERROR: PACKING FAILED";
		}
	}

	private void OnDrawGizmos()
	{
		if (rs == null)
		{
			return;
		}
		for (int i = 0; i < rs.Length; i++)
		{
			Vector2 vector = new Vector2((float)i * 1.5f * (float)maxDim, 0f);
			AtlasPackingResult atlasPackingResult = rs[i];
			Vector2 vector2 = new Vector2(vector.x + (float)(atlasPackingResult.atlasX / 2), vector.y + (float)(atlasPackingResult.atlasY / 2));
			Gizmos.DrawWireCube(size: new Vector2(atlasPackingResult.atlasX, atlasPackingResult.atlasY), center: vector2);
			for (int j = 0; j < rs[i].rects.Length; j++)
			{
				Rect rect = rs[i].rects[j];
				Gizmos.color = new Color(Random.value, Random.value, Random.value);
				vector2 = new Vector2(vector.x + (rect.x + rect.width / 2f) * (float)rs[i].atlasX, vector.y + (rect.y + rect.height / 2f) * (float)rs[i].atlasY);
				Gizmos.DrawCube(size: new Vector2(rect.width * (float)rs[i].atlasX, rect.height * (float)rs[i].atlasY), center: vector2);
			}
		}
	}

	[ContextMenu("Test1")]
	private void Test1()
	{
		texturePacker = new MB2_TexturePacker();
		texturePacker.doPowerOfTwoTextures = true;
		List<Vector2> list = new List<Vector2>();
		list.Add(new Vector2(450f, 200f));
		list.Add(new Vector2(450f, 200f));
		list.Add(new Vector2(450f, 80f));
		texturePacker.LOG_LEVEL = logLevel;
		rs = texturePacker.GetRects(list, 512, 8, true);
		Debug.Log("Success! ");
	}

	[ContextMenu("Test2")]
	private void Test2()
	{
		texturePacker = new MB2_TexturePacker();
		texturePacker.doPowerOfTwoTextures = true;
		List<Vector2> list = new List<Vector2>();
		list.Add(new Vector2(200f, 450f));
		list.Add(new Vector2(200f, 450f));
		list.Add(new Vector2(80f, 450f));
		texturePacker.LOG_LEVEL = logLevel;
		rs = texturePacker.GetRects(list, 512, 8, true);
		Debug.Log("Success! ");
	}

	[ContextMenu("Test3")]
	private void Test3()
	{
		texturePacker = new MB2_TexturePacker();
		texturePacker.doPowerOfTwoTextures = false;
		List<Vector2> list = new List<Vector2>();
		list.Add(new Vector2(450f, 200f));
		list.Add(new Vector2(450f, 200f));
		list.Add(new Vector2(450f, 80f));
		texturePacker.LOG_LEVEL = logLevel;
		rs = texturePacker.GetRects(list, 512, 8, true);
		Debug.Log("Success! ");
	}

	[ContextMenu("Test4")]
	private void Test4()
	{
		texturePacker = new MB2_TexturePacker();
		texturePacker.doPowerOfTwoTextures = false;
		List<Vector2> list = new List<Vector2>();
		list.Add(new Vector2(200f, 450f));
		list.Add(new Vector2(200f, 450f));
		list.Add(new Vector2(80f, 450f));
		texturePacker.LOG_LEVEL = logLevel;
		rs = texturePacker.GetRects(list, 512, 8, true);
		Debug.Log("Success! ");
	}
}
