using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

public static class LayoutEditorRecipeKnowledge
{
    private struct Entry
    {
        public string Step;
        public string[] Ingredients;
    }

    private static readonly Dictionary<string, Entry> Originals = BuildOriginals();

    private static Dictionary<string, Entry> BuildOriginals()
    {
        var d = new Dictionary<string, Entry>();

        Put(d, "Burger_Plain_SO", "FryingPan", "ChoppedBunSO", "MeatSO");
        Put(d, "Burger_Cheese_SO", "FryingPan", "ChoppedBunSO", "CheeseSO", "MeatSO");
        Put(d, "Burger_LettuceTomato_SO", "FryingPan", "ChoppedBunSO", "LettuceSO", "TomatoSO", "MeatSO");
        Put(d, "Burger_CheeseLettuce_SO", "FryingPan", "ChoppedBunSO", "CheeseSO", "LettuceSO", "MeatSO");

        Put(d, "Burrito_Chicken_SO", "FryingPan", "TortillaSO", "BurritoChickenSO");
        Put(d, "Burrito_Meat_SO", "FryingPan", "TortillaSO", "BurritoMeatSO");
        Put(d, "Burrito_Mushroom_SO", "FryingPan", "TortillaSO", "MushroomSO");

        Put(d, "Cake_Plain_SO", "Mixer", "FlourSO", "EggSO");
        Put(d, "Cake_Honey_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO");
        Put(d, "Cake_Chocolate_SO", "Mixer", "FlourSO", "EggSO", "ChocolateSO");
        Put(d, "Cake_HoneyCarrot_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO", "CarrotSO");
        Put(d, "Cake_HoneyChocolate_SO", "Mixer", "FlourSO", "EggSO", "HoneycombSO", "ChocolateSO");

        Put(d, "Fry_Chips_SO", "DeepFatFryer", "PotatoSO");
        Put(d, "Fry_Chicken_SO", "DeepFatFryer", "NuggetChickenSO");
        Put(d, "Fry_All_SO", "DeepFatFryer", "PotatoSO", "NuggetChickenSO");

        Put(d, "OnionCarrotPotatoSoup_SO", "Pot", "OnionSO", "CarrotSO", "PotatoSO");

        Put(d, "Pasta_Marinara_SO", "Pot", "PastaSO", "TomatoSO");
        Put(d, "Pasta_MeatOnly_SO", "Pot", "PastaSO", "MeatSO");
        Put(d, "Pasta_MushroomOnly_SO", "Pot", "PastaSO", "MushroomSO");
        Put(d, "Pasta_TomatoOnly_SO", "Pot", "PastaSO", "TomatoSO");

        Put(d, "Pizza_Plain_SO", "OvenTray", "DoughSO");
        Put(d, "Pizza_Peperoni_SO", "OvenTray", "DoughSO", "PepperoniSO");
        Put(d, "Pizza_Chicken_SO", "OvenTray", "DoughSO", "ChickenSO");

        Put(d, "Salad_Plain_SO", "", "LettuceSO", "TomatoSO", "OnionSO");
        Put(d, "Salad_Cucumber_SO", "", "LettuceSO", "TomatoSO", "CucumberSO");
        Put(d, "Salad_Tomato_SO", "", "LettuceSO", "TomatoSO");

        Put(d, "Steamed_Carrot_SO", "Steamer", "CarrotSO");
        Put(d, "Steamed_Fish_SO", "Steamer", "FishSO");
        Put(d, "Steamed_Meat_SO", "Steamer", "MeatSO");
        Put(d, "Steamed_Prawns_SO", "Steamer", "PrawnSO");

        Put(d, "Sushi_PlainFish_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiFishSO");
        Put(d, "Sushi_PlainPrawn_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiPrawnSO");
        Put(d, "Sushi_Cucumber_SO", "Steamer", "SeaweedSO", "SushiRiceSO", "SushiFishSO", "CucumberSO");
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
        Entry e;
        if (id != null && Originals.TryGetValue(id, out e))
        {
            step = e.Step;
            ingredients = e.Ingredients;
            return true;
        }
        step = "";
        ingredients = new string[0];
        return false;
    }

    public static List<string> CustomIngredients(CustomRecipeSO so)
    {
        var ids = new List<string>();
        Collect(so, ids, new HashSet<Object>());
        return ids;
    }

    private static readonly HashSet<string> CookSteps = new HashSet<string>
    {
        "FryingPan", "Pot", "OvenTray", "DeepFatFryer", "Steamer", "Mixer",
    };

    public static bool IsCookStep(string step)
    {
        return !string.IsNullOrEmpty(step) && CookSteps.Contains(step);
    }

    public static void CustomStats(CustomRecipeSO so, out int ingredientCount, out int cookingStepCount)
    {
        var ings = 0;
        var cooks = 0;
        CollectStats(so, ref ings, ref cooks, new HashSet<Object>());
        ingredientCount = ings;
        cookingStepCount = cooks;
    }

    private static void CollectStats(CustomRecipeSO so, ref int ings, ref int cooks, HashSet<Object> seen)
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

    private static void Collect(CustomRecipeSO so, List<string> ids, HashSet<Object> seen)
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
