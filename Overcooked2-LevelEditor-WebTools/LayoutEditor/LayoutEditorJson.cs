using System;
using System.Text;
using UnityEngine;

public static class LayoutEditorJson
{
    public static string ToJson(object obj)
    {
        if (obj == null)
            return "null";

        var asString = obj as string;
        if (asString != null)
            return "\"" + Escape(asString) + "\"";

        var doc = obj as LayoutDocumentDto;
        if (doc != null)
            return JsonUtility.ToJson(doc);

        var grid = obj as GridInfoDto;
        if (grid != null)
            return JsonUtility.ToJson(grid);

        var list = obj as LevelSetSceneListDto;
        if (list != null)
            return JsonUtility.ToJson(list);

        var err = obj as ApiErrorDto;
        if (err != null)
            return JsonUtility.ToJson(err);

        var item = obj as LayoutItemDto;
        if (item != null)
            return JsonUtility.ToJson(item);

        var ingredients = obj as IngredientCatalogDto;
        if (ingredients != null)
            return JsonUtility.ToJson(ingredients);

        var recipes = obj as RecipeCatalogDto;
        if (recipes != null)
            return JsonUtility.ToJson(recipes);

        var levelRecipes = obj as LevelRecipesDto;
        if (levelRecipes != null)
            return JsonUtility.ToJson(levelRecipes);

        var floorMaterials = obj as FloorMaterialCatalogDto;
        if (floorMaterials != null)
            return JsonUtility.ToJson(floorMaterials);

        if (obj is bool)
            return (bool)obj ? "true" : "false";

        if (obj is int)
            return ((int)obj).ToString();

        if (obj is float)
            return SanitizeFloat((float)obj);

        if (obj is double)
            return SanitizeFloat((float)(double)obj);

        return JsonUtility.ToJson(obj);
    }

    public static LayoutDocumentDto ParseLayoutDocument(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new LayoutDocumentDto { items = new LayoutItemDto[0] };

        LayoutDocumentDto doc;
        try
        {
            doc = JsonUtility.FromJson<LayoutDocumentDto>(json);
        }
        catch (Exception)
        {
            doc = ParseLayoutDocumentLegacy(json);
        }

        if (doc == null)
            doc = new LayoutDocumentDto();
        if (doc.items == null)
            doc.items = new LayoutItemDto[0];
        if (doc.floors == null)
            doc.floors = new FloorDto[0];

        for (int i = 0; i < doc.items.Length; i++)
        {
            var item = doc.items[i];
            if (item == null)
                continue;
            if (string.IsNullOrEmpty(item.hierarchyPath))
                item.hierarchyPath = item.instanceId;
            if (item.localPosition == null)
                item.localPosition = new LayoutVector3();
            if (item.worldPosition == null)
                item.worldPosition = item.localPosition;
            if (item.footprint == null)
                item.footprint = new LayoutFootprint();
            if (item.footprint.cellsX <= 0)
                item.footprint.cellsX = 1;
            if (item.footprint.cellsZ <= 0)
                item.footprint.cellsZ = 1;
        }

        for (int i = 0; i < doc.floors.Length; i++)
        {
            var f = doc.floors[i];
            if (f == null)
                continue;
            if (string.IsNullOrEmpty(f.hierarchyPath))
                f.hierarchyPath = f.instanceId;
            if (f.localPosition == null)
                f.localPosition = new LayoutVector3();
            if (f.worldPosition == null)
                f.worldPosition = f.localPosition;
            if (f.localScale == null)
                f.localScale = new LayoutVector3 { x = 1, y = 1, z = 1 };
        }

        return doc;
    }

    private static string SanitizeFloat(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v))
            return "0";
        return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Escape(string s)
    {
        if (s == null)
            return "";
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 32)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    // Fallback if JsonUtility.FromJson fails on older Unity edge cases.
    private static LayoutDocumentDto ParseLayoutDocumentLegacy(string json)
    {
        var doc = new LayoutDocumentDto();
        doc.sceneAssetPath = ReadString(json, "sceneAssetPath");
        var items = new System.Collections.Generic.List<LayoutItemDto>();
        int arrStart = json.IndexOf("\"items\"", StringComparison.Ordinal);
        if (arrStart < 0)
        {
            doc.items = items.ToArray();
            return doc;
        }

        arrStart = json.IndexOf('[', arrStart);
        if (arrStart < 0)
        {
            doc.items = items.ToArray();
            return doc;
        }

        int i = arrStart + 1;
        while (i < json.Length)
        {
            while (i < json.Length && (json[i] == ' ' || json[i] == ',' || json[i] == '\n' || json[i] == '\r'))
                i++;
            if (i >= json.Length || json[i] == ']')
                break;
            if (json[i] != '{')
                break;

            int depth = 0;
            int start = i;
            for (; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        i++;
                        break;
                    }
                }
            }

            var block = json.Substring(start, i - start);
            items.Add(ParseItemLegacy(block));
        }

        doc.items = items.ToArray();
        return doc;
    }

    private static LayoutItemDto ParseItemLegacy(string block)
    {
        var item = new LayoutItemDto
        {
            instanceId = ReadString(block, "instanceId"),
            hierarchyPath = ReadString(block, "hierarchyPath"),
            prefabGuid = ReadString(block, "prefabGuid"),
            prefabAssetPath = ReadString(block, "prefabAssetPath"),
            parentPath = ReadString(block, "parentPath"),
            displayName = ReadString(block, "displayName"),
            localRotationX = ReadFloat(block, "localRotationX"),
            localRotationY = ReadFloat(block, "localRotationY"),
            footprint = new LayoutFootprint()
        };

        item.localPosition = ReadVector3(block, "localPosition");
        item.worldPosition = ReadVector3(block, "worldPosition");

        item.footprint.cellsX = ReadFloat(block, "cellsX");
        item.footprint.cellsZ = ReadFloat(block, "cellsZ");

        return item;
    }

    private static LayoutVector3 ReadVector3(string block, string key)
    {
        var posKey = "\"" + key + "\"";
        int p = block.IndexOf(posKey, StringComparison.Ordinal);
        if (p < 0)
            return new LayoutVector3();
        p = block.IndexOf('{', p);
        if (p < 0)
            return new LayoutVector3();
        return new LayoutVector3
        {
            x = ReadFloat(block, "x", p),
            y = ReadFloat(block, "y", p),
            z = ReadFloat(block, "z", p)
        };
    }

    private static string ReadString(string json, string key, int searchFrom = 0)
    {
        var token = "\"" + key + "\"";
        int i = json.IndexOf(token, searchFrom, StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf(':', i);
        if (i < 0) return null;
        i++;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length || json[i] != '"') return null;
        i++;
        var sb = new StringBuilder();
        while (i < json.Length)
        {
            char c = json[i++];
            if (c == '\\' && i < json.Length)
            {
                sb.Append(json[i++]);
                continue;
            }

            if (c == '"')
                break;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static float ReadFloat(string json, string key, int searchFrom = 0)
    {
        var token = "\"" + key + "\"";
        int i = json.IndexOf(token, searchFrom, StringComparison.Ordinal);
        if (i < 0) return 0f;
        i = json.IndexOf(':', i);
        if (i < 0) return 0f;
        i++;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        int start = i;
        while (i < json.Length && "0123456789.-eE+".IndexOf(json[i]) >= 0) i++;
        float val;
        if (float.TryParse(json.Substring(start, i - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out val))
            return val;
        return 0f;
    }
}
