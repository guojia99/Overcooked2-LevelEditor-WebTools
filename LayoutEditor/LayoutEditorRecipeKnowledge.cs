using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Recipe composition knowledge. Primary source: layout-editor/scripts/data/recipe-knowledge.json
/// (shared with build-catalog.mjs). Hardcoded tables below are a fallback when the file is missing.
/// </summary>
public static class LayoutEditorRecipeKnowledge
{
    /// <summary>Bumped together with SCHEMA_VERSION in build-catalog.mjs.</summary>
    public const int BridgeSchemaVersion = 3;

    private struct Entry
    {
        public string Step;
        public string[] Ingredients;
    }

#pragma warning disable 0649 // fields assigned by JsonUtility deserialization
    [Serializable]
    private class KnowledgeDoc
    {
        public int schemaVersion;
        public string[] cookSteps;
        public string[] skip;
        public KnowledgeEntry[] recipes;
    }

    [Serializable]
    private class KnowledgeEntry
    {
        public string id;
        public string step;
        public string[] ingredients;
    }
#pragma warning restore 0649

    private static Dictionary<string, Entry> _originals;
    private static HashSet<string> _skip;
    private static HashSet<string> _cookSteps;
    private static bool _loaded;
    private static bool _knowledgeFileLoaded;
    private static DateTime _lastWriteTime;

    /// <summary>False when the shared JSON is missing/unreadable and hardcoded fallback tables are in use.</summary>
    public static bool KnowledgeFileLoaded
    {
        get
        {
            EnsureLoaded();
            return _knowledgeFileLoaded;
        }
    }

    private static void EnsureLoaded()
    {
        var path = KnowledgePath();
        var writeTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        if (_loaded && writeTime == _lastWriteTime)
            return;
        _loaded = true;
        _lastWriteTime = writeTime;

        if (TryLoadFromJson(out _originals, out _skip, out _cookSteps))
        {
            _knowledgeFileLoaded = true;
            return;
        }
        _knowledgeFileLoaded = false;

        _originals = BuildOriginals();
        _skip = new HashSet<string>();
        _cookSteps = new HashSet<string>
        {
            "FryingPan", "Pot", "OvenTray", "DeepFatFryer", "Steamer", "Mixer",
        };
    }

    private static string KnowledgePath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../layout-editor/scripts/data/recipe-knowledge.json"));
    }

    private static bool TryLoadFromJson(out Dictionary<string, Entry> originals, out HashSet<string> skip, out HashSet<string> cookSteps)
    {
        originals = null;
        skip = null;
        cookSteps = null;

        var path = KnowledgePath();
        if (!File.Exists(path))
            return false;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return false;
        }

        KnowledgeDoc doc;
        try
        {
            doc = JsonUtility.FromJson<KnowledgeDoc>(text);
        }
        catch
        {
            return false;
        }
        if (doc == null)
            return false;

        originals = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (doc.recipes != null)
        {
            foreach (var r in doc.recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.id))
                    continue;
                originals[r.id] = new Entry { Step = r.step ?? "", Ingredients = r.ingredients ?? new string[0] };
            }
        }

        skip = new HashSet<string>(doc.skip ?? new string[0]);
        cookSteps = new HashSet<string>(doc.cookSteps ?? new string[0]);
        return true;
    }

    /// <summary>Reload the JSON knowledge file (e.g. after editing it).</summary>
    public static void InvalidateCache()
    {
        _loaded = false;
    }

    private static Dictionary<string, Entry> BuildOriginals()
    {
        var d = new Dictionary<string, Entry>();

        Put(d, "Burger_Plain_SO", "FryingPan", "DLC02_ChoppedBun", "MeatSO");
        Put(d, "Burger_Cheese_SO", "FryingPan", "DLC02_ChoppedBun", "CheeseSO", "MeatSO");
        Put(d, "Burger_LettuceTomato_SO", "FryingPan", "DLC02_ChoppedBun", "LettuceSO", "TomatoSO", "MeatSO");
        Put(d, "Burger_CheeseLettuce_SO", "FryingPan", "DLC02_ChoppedBun", "CheeseSO", "LettuceSO", "MeatSO");

        Put(d, "Burrito_Chicken_SO", "FryingPan", "TortillaSO", "SushiRiceSO", "BurritoChickenSO");
        Put(d, "Burrito_Meat_SO", "FryingPan", "TortillaSO", "SushiRiceSO", "BurritoMeatSO");
        Put(d, "Burrito_Mushroom_SO", "FryingPan", "TortillaSO", "SushiRiceSO", "MushroomSO");

        Put(d, "Cake_Plain_SO", "Mixer", "FlourSO", "EggSO");
        Put(d, "Cake_Honey_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO");
        Put(d, "Cake_Chocolate_SO", "FryingPan", "FlourSO", "EggSO", "ChocolateSO");
        Put(d, "Cake_HoneyCarrot_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO", "CarrotSO");
        Put(d, "Cake_HoneyChocolate_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO", "ChocolateSO");

        Put(d, "Fry_Chips_SO", "DeepFatFryer", "PotatoSO");
        Put(d, "Fry_Chicken_SO", "DeepFatFryer", "NuggetChickenSO");
        Put(d, "Fry_All_SO", "DeepFatFryer", "PotatoSO", "NuggetChickenSO");

        Put(d, "OnionCarrotPotatoSoup_SO", "Pot", "OnionSO", "CarrotSO", "PotatoSO");

        Put(d, "Pasta_Marinara_SO", "Pot", "PastaSO", "FishSO", "PrawnSO");
        Put(d, "Pasta_MeatOnly_SO", "Pot", "PastaSO", "MeatSO");
        Put(d, "Pasta_MushroomOnly_SO", "Pot", "PastaSO", "MushroomSO");
        Put(d, "Pasta_TomatoOnly_SO", "Pot", "PastaSO", "TomatoSO");

        Put(d, "Pizza_Plain_SO", "OvenTray", "DLC05_Dough", "TomatoSO", "CheeseSO");
        Put(d, "Pizza_Peperoni_SO", "OvenTray", "DLC05_Dough", "TomatoSO", "CheeseSO", "PepperoniSO");
        Put(d, "Pizza_Chicken_SO", "OvenTray", "DLC05_Dough", "TomatoSO", "CheeseSO", "ChickenSO");

        Put(d, "Salad_Plain_SO", "", "LettuceSO", "TomatoSO", "OnionSO");
        Put(d, "Salad_Cucumber_SO", "", "LettuceSO", "TomatoSO", "CucumberSO");
        Put(d, "Salad_Tomato_SO", "", "LettuceSO", "TomatoSO");

        Put(d, "Steamed_Carrot_SO", "Steamer", "FlourSO", "CarrotSO");
        Put(d, "Steamed_Fish_SO", "Steamer", "FishSO");
        Put(d, "Steamed_Meat_SO", "Steamer", "FlourSO", "MeatSO");
        Put(d, "Steamed_Prawns_SO", "Steamer", "FlourSO", "PrawnSO");

        Put(d, "Sushi_PlainFish_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiFishSO");
        Put(d, "Sushi_PlainPrawn_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiPrawnSO");
        Put(d, "Sushi_Cucumber_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "CucumberSO");
        Put(d, "Sushi_Fish_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiFishSO");
        Put(d, "Sushi_All_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiFishSO", "SushiPrawnSO", "CucumberSO");

        return d;
    }

    private static void Put(Dictionary<string, Entry> d, string id, string step, params string[] ings)
    {
        d[id] = new Entry { Step = step, Ingredients = ings };
    }

    public static bool TryGetOriginal(string id, out string step, out string[] ingredients)
    {
        EnsureLoaded();
        Entry e;
        if (id != null && _originals.TryGetValue(id, out e))
        {
            step = e.Step;
            ingredients = e.Ingredients;
            return true;
        }
        step = "";
        ingredients = new string[0];
        return false;
    }

    /// <summary>Recipes listed in knowledge skip[] (score-0 optional/model variants) are excluded from catalogs.</summary>
    public static bool IsSkipped(string id)
    {
        EnsureLoaded();
        return id != null && _skip.Contains(id);
    }

    public static List<string> CustomIngredients(CustomRecipeSO so)
    {
        var ids = new List<string>();
        Collect(so, ids, new HashSet<UnityEngine.Object>());
        return ids;
    }

    public static bool IsCookStep(string step)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(step) && _cookSteps.Contains(step);
    }

    public static void CustomStats(CustomRecipeSO so, out int ingredientCount, out int cookingStepCount)
    {
        var ings = 0;
        var cooks = 0;
        CollectStats(so, ref ings, ref cooks, new HashSet<UnityEngine.Object>());
        ingredientCount = ings;
        cookingStepCount = cooks;
    }

    private static void CollectStats(CustomRecipeSO so, ref int ings, ref int cooks, HashSet<UnityEngine.Object> seen)
    {
        if (so == null || !seen.Add(so))
            return;
        if (so.cookingStepSO != null && IsCookStep(CustomCookingStep(so)))
            cooks++;
        if (so.compositionSOs == null)
            return;
        foreach (var c in so.compositionSOs)
        {
            if (c == null)
                continue;
            var sub = c as CustomRecipeSO;
            if (sub != null)
            {
                CollectStats(sub, ref ings, ref cooks, seen);
                continue;
            }
            ings++;
        }
    }

    public static string CustomCookingStep(CustomRecipeSO so)
    {
        if (so == null || so.cookingStepSO == null)
            return "";
        var path = AssetDatabase.GetAssetPath(so.cookingStepSO);
        return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
    }

    private static void Collect(CustomRecipeSO so, List<string> ids, HashSet<UnityEngine.Object> seen)
    {
        if (so == null || !seen.Add(so))
            return;
        if (so.compositionSOs == null)
            return;
        foreach (var c in so.compositionSOs)
        {
            if (c == null)
                continue;
            var sub = c as CustomRecipeSO;
            if (sub != null)
            {
                Collect(sub, ids, seen);
                continue;
            }
            var path = AssetDatabase.GetAssetPath(c);
            if (!string.IsNullOrEmpty(path))
            {
                var iid = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(iid) && !ids.Contains(iid))
                    ids.Add(iid);
            }
        }
    }
}
