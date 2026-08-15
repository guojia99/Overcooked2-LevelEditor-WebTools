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

    /// <summary>Utensil / workstation sets per cooking step (mirrors STEP_UTENSILS in build-catalog.mjs
    ///  and the web frontend). First entry is the station, last entry the actual cooking vessel.</summary>
    public static readonly Dictionary<string, string[]> StepUtensils = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        { "Pot", new[] { "Cooker", "Pot" } },
        { "FryingPan", new[] { "Cooker", "FryPan" } },
        { "DeepFatFryer", new[] { "FryingStation", "FrierBasket" } },
        { "OvenTray", new[] { "Oven" } },
        { "Steamer", new[] { "Cooker", "Steamer" } },
        { "Mixer", new[] { "Mixer", "MixerBowl", "Oven" } },
        { "Blender", new[] { "Blender", "BlenderCup" } },
        { "GriddlePan", new[] { "Campfire", "GriddlePan" } },
        { "KebabSkewer", new[] { "Barbeque", "Skewer" } },
        { "ToastingFork", new[] { "Campfire", "ToastingFork" } },
        { "MixingBowl", new[] { "Mixer", "MixerBowl" } },
        { "HotPot", new[] { "cooking_region_floorburner", "utensil_large_pot_01" } },
        { "RoastingTray", new[] { "Oven", "utensil_roasting_tray" } },
    };

    public static string[] UtensilsForStep(string step)
    {
        string[] utensils;
        if (!string.IsNullOrEmpty(step) && StepUtensils.TryGetValue(step, out utensils))
            return utensils;
        return new string[0];
    }

    /// <summary>Recipe-book ingredient grouping (mirrors computeCookingGroups in build-catalog.mjs).
    ///  Rules (evaluated in order):
    ///  - 半成品：直接按自身烹饪步骤成组。
    ///  - 煎锅中间产物覆盖：汉堡肉排 → FryingPan，面包胚/生菜保持生。
    ///  - 寿司：只有寿司米蒸，其余不处理。
    ///  - 卷饼：米饭煮，卷饼皮不处理，其余煎。
    ///  - 棉花糖饼干：只有棉花糖烤，其余不处理。
    ///  - 意面：意面煮，其余食材各自煎（分开成组）。
    ///  - 炸物：所有食材分别炸（分开成组）。
    ///  - 面糊/面团（含面粉）：面粉鸡蛋搅拌，其余进最终锅具；最终锅具图标作为标记组追加。 </summary>
    public static RecipeCookingGroupDto[] ComputeCookingGroups(RecipeEntryDto recipe, List<RecipeEntryDto> allRecipes)
    {
        var finalStep = recipe != null ? recipe.cookingStep : "";
        var ingredients = recipe != null ? recipe.ingredients : null;
        var type = recipe != null ? recipe.type : "";
        var intermediate = recipe != null && recipe.intermediate;
        if (ingredients == null || ingredients.Length == 0)
            return new RecipeCookingGroupDto[0];

        if (intermediate)
        {
            var step = IsCookStep(finalStep) ? finalStep : "";
            return new[]
            {
                new RecipeCookingGroupDto { step = step, utensils = UtensilsForStep(step), ingredients = ingredients }
            };
        }

        // 自定义菜谱的"工序"分组：当菜谱自身没有整体烹饪步骤（Composite/组装型）时，
        // 按其直接组成展开 —— 组成若是子菜谱（中间产物/半成品），以子菜谱自身的烹饪
        // 步骤成组并展开其叶食材；普通食材归生组。组成顺序即组顺序。
        if (recipe != null && recipe.compositionIds != null && recipe.compositionIds.Length > 0 &&
            !IsCookStep(finalStep))
        {
            var byId = new Dictionary<string, RecipeEntryDto>(StringComparer.Ordinal);
            if (allRecipes != null)
            {
                foreach (var r in allRecipes)
                {
                    if (r == null || string.IsNullOrEmpty(r.id))
                        continue;
                    if (!byId.ContainsKey(r.id))
                        byId[r.id] = r;
                }
            }

            var compResult = new List<RecipeCookingGroupDto>();
            var compRaw = new List<string>();
            var compSteps = new List<string>();
            var compStepIngs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var compId in recipe.compositionIds)
            {
                if (string.IsNullOrEmpty(compId))
                    continue;
                RecipeEntryDto sub;
                if (byId.TryGetValue(compId, out sub) && sub != null &&
                    sub.ingredients != null && sub.ingredients.Length > 0)
                {
                    var subStep = IsCookStep(sub.cookingStep) ? sub.cookingStep : "";
                    if (subStep == "")
                    {
                        foreach (var ing in sub.ingredients)
                            compRaw.Add(ing);
                        continue;
                    }
                    List<string> lst;
                    if (!compStepIngs.TryGetValue(subStep, out lst))
                    {
                        lst = new List<string>();
                        compStepIngs[subStep] = lst;
                        compSteps.Add(subStep);
                    }
                    foreach (var ing in sub.ingredients)
                        lst.Add(ing);
                }
                else
                {
                    compRaw.Add(compId);
                }
            }

            if (compRaw.Count > 0)
                compResult.Add(new RecipeCookingGroupDto { step = "", utensils = new string[0], ingredients = compRaw.ToArray() });
            for (int i = 0; i < compSteps.Count; i++)
            {
                var st = compSteps[i];
                compResult.Add(new RecipeCookingGroupDto
                {
                    step = st,
                    utensils = UtensilsForStep(st),
                    ingredients = compStepIngs[st].ToArray()
                });
            }
            return compResult.ToArray();
        }

        var prep = new Dictionary<string, string>(StringComparer.Ordinal); // ingredient -> step ("" = raw)

        if (finalStep == "FryingPan")
        {
            var candidates = new List<RecipeEntryDto>();
            if (allRecipes != null)
            {
                foreach (var r in allRecipes)
                {
                    if (!r.intermediate || !IsCookStep(r.cookingStep))
                        continue;
                    if (r.cookingStep != "FryingPan" && r.cookingStep != "MixingBowl")
                        continue;
                    if (r.ingredients == null || r.ingredients.Length == 0)
                        continue;
                    bool subset = true;
                    foreach (var ing in r.ingredients)
                    {
                        if (Array.IndexOf(ingredients, ing) < 0)
                        {
                            subset = false;
                            break;
                        }
                    }
                    if (subset)
                        candidates.Add(r);
                }
            }
            candidates.Sort((a, b) =>
            {
                int c = b.ingredients.Length.CompareTo(a.ingredients.Length);
                if (c != 0)
                    return c;
                return string.Compare(a.id, b.id, StringComparison.Ordinal);
            });
            foreach (var cand in candidates)
            {
                foreach (var ing in cand.ingredients)
                {
                    if (!prep.ContainsKey(ing))
                        prep[ing] = cand.cookingStep;
                }
            }
        }

        bool flourBranch = false;
        if (type == "sushi")
        {
            foreach (var ing in ingredients)
                prep[ing] = (ing == "SushiRiceSO" || ing == "RiceSO") ? "Steamer" : "";
        }
        else if (type == "burrito")
        {
            foreach (var ing in ingredients)
            {
                prep[ing] = ing == "TortillaSO"
                    ? ""
                    : (ing == "SushiRiceSO" || ing == "RiceSO") ? "Pot" : "FryingPan";
            }
        }
        else if (type == "smores")
        {
            foreach (var ing in ingredients)
                prep[ing] = ing == "DLC05_Marshmallow" ? "ToastingFork" : "";
        }
        else if (finalStep == "Pot" && Array.IndexOf(ingredients, "PastaSO") >= 0)
        {
            foreach (var ing in ingredients)
                prep[ing] = ing == "PastaSO" ? "Pot" : "FryingPan";
        }
        else if (type == "hotdog")
        {
            // 只有热狗肠需要煮；洋葱单独煎；面包/芥末/番茄酱无需烹饪
            foreach (var ing in ingredients)
            {
                if (ing == "frankfurter" || ing == "dlc11_frankfurter")
                    prep[ing] = "Pot";
                else if (ing == "dlc08_onion" || ing == "dlc11_onion")
                    prep[ing] = "FryingPan";
                else
                    prep[ing] = "";
            }
        }
        else if (type == "hotchocolate")
        {
            // 全程只有牛奶+巧克力需要煮；奶油/棉花糖是单独的（无需烹饪）
            foreach (var ing in ingredients)
            {
                if (ing == "milk" || ing == "dlc09_milk" || ing == "dlc03_chocolate" || ing == "dlc09_chocolate")
                    prep[ing] = "Pot";
                else
                    prep[ing] = "";
            }
        }
        else if (type == "float")
        {
            // 冰淇淋汽水：汽水单独（无需搅拌）；牛奶/口味/冰块进搅拌机（Blender）
            foreach (var ing in ingredients)
            {
                if (ing == "orangesoda" || ing == "rootbeer")
                    prep[ing] = "";
                else
                    prep[ing] = "Blender";
            }
        }
        else if ((finalStep == "DeepFatFryer" && type != "donut") || (type == "fry" && !IsCookStep(finalStep)))
        {
            // 名称前缀推断的 fry（如 FriedEgg）不得覆盖显式烹饪步骤（FryingPan 等）
            // 甜甜圈走搅拌+炸篮分支（下方 flourBranch）
            foreach (var ing in ingredients)
                prep[ing] = "DeepFatFryer";
        }
        else if (type == "cake" ||
            (Array.IndexOf(ingredients, "FlourSO") >= 0 && finalStep != "Mixer" && finalStep != "MixingBowl"))
        {
            // 蛋糕：搅拌 + 烤箱；面糊/面团（松饼/饺子）：搅拌 + 最终锅具
            flourBranch = type != "cake";
            var cookStep = type == "cake" ? "OvenTray" : (IsCookStep(finalStep) ? finalStep : "");
            foreach (var ing in ingredients)
            {
                if (prep.ContainsKey(ing))
                    continue;
                prep[ing] = (ing == "FlourSO" || ing == "EggSO")
                    ? "MixingBowl"
                    : cookStep;
            }
        }

        bool anyAssigned = false;
        foreach (var kv in prep)
        {
            if (kv.Value != "")
            {
                anyAssigned = true;
                break;
            }
        }
        var fallbackStep = anyAssigned ? "" : (IsCookStep(finalStep) ? finalStep : "");
        foreach (var ing in ingredients)
        {
            if (!prep.ContainsKey(ing))
                prep[ing] = fallbackStep;
        }

        var groupMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var ing in ingredients)
        {
            var st = prep[ing];
            List<string> lst;
            if (!groupMap.TryGetValue(st, out lst))
            {
                lst = new List<string>();
                groupMap[st] = lst;
                order.Add(st);
            }
            lst.Add(ing);
        }

        var ordered = new List<string>();
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] != "")
                ordered.Add(order[i]);
        }
        if (flourBranch && IsCookStep(finalStep) && !groupMap.ContainsKey(finalStep) && order.Count > 0)
            ordered.Add(finalStep);

        bool splitPerIngredient = (finalStep == "DeepFatFryer" && type != "donut") ||
            (type == "fry" && !IsCookStep(finalStep)) ||
            (finalStep == "Pot" && Array.IndexOf(ingredients, "PastaSO") >= 0);

        var result = new List<RecipeCookingGroupDto>();
        List<string> raw;
        if (groupMap.TryGetValue("", out raw) && raw.Count > 0)
            result.Add(new RecipeCookingGroupDto { step = "", utensils = new string[0], ingredients = raw.ToArray() });
        for (int i = 0; i < ordered.Count; i++)
        {
            var st = ordered[i];
            List<string> lst;
            groupMap.TryGetValue(st, out lst);
            var ings = lst ?? new List<string>();
            if (splitPerIngredient && st != "" && ings.Count > 1)
            {
                for (int j = 0; j < ings.Count; j++)
                {
                    result.Add(new RecipeCookingGroupDto
                    {
                        step = st,
                        utensils = UtensilsForStep(st),
                        ingredients = new[] { ings[j] }
                    });
                }
            }
            else
            {
                result.Add(new RecipeCookingGroupDto
                {
                    step = st,
                    utensils = UtensilsForStep(st),
                    ingredients = ings.ToArray()
                });
            }
        }
        return result.ToArray();
    }

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

        Put(d, "Cake_Plain_SO", "FryingPan", "FlourSO", "EggSO");
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
                if (!string.IsNullOrEmpty(iid))
                    ids.Add(iid);
            }
        }
    }

    /// <summary>Web 内置菜谱分数估算：分数 = 20 × 食材种类数 + 烹调难度加成，
    ///  clamp [20,120]、级距 20（对齐游戏攻略：材料越多越高、烹调越麻烦越高）。
    ///  加成：搅拌+烘焙(蛋糕/派/月亮派/布丁/搅拌步骤)=60；搅拌+煎炸(松饼/甜甜圈)=40；
    ///  煮/炸/蒸/烤/火锅/烤串/搅拌机(Blender)=20；组装/切菜/煎(FryingPan)=0。</summary>
    public static int EstimateWebRecipeScore(string id, string step, string[] ingredients)
    {
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        if (ingredients != null)
        {
            foreach (var i in ingredients)
                if (!string.IsNullOrEmpty(i))
                    distinct.Add(i);
        }
        var lower = (id ?? "").ToLowerInvariant();
        int bonus;
        // 先判「搅拌+煎/炸」再判「搅拌+烤」（pancake 含 cake 子串，顺序关键）
        if (lower.Contains("pancake") || lower.Contains("donut"))
            bonus = 40;
        else if (lower.Contains("cake") || lower.Contains("pie") || lower.Contains("moonpie") || lower.Contains("pudding"))
            bonus = 60;
        else if (step == "Mixer" || step == "MixingBowl" || step == "OvenCakeTin")
            bonus = 60;
        else if (step == "Pot" || step == "OvenTray" || step == "Steamer" || step == "DeepFatFryer"
            || step == "RoastingTray" || step == "HotPot" || step == "KebabSkewer" || step == "ToastingFork"
            || step == "GriddlePan" || step == "Blender")
            bonus = 20;
        else
            bonus = 0;
        var score = 20 * distinct.Count + bonus;
        return Mathf.Clamp(score, 20, 120);
    }
}
