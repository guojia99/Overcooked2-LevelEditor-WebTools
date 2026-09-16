using UnityEngine.UI;

public interface ITextProcessor
{
	bool HasEmbeddedImages(string inputString);

	bool ProcessText(ref string inputString);

	bool OnPopulateMesh(VertexHelper _helper);
}
