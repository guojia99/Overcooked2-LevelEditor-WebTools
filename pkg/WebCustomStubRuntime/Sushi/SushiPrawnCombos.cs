using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// Web 寿司（commonW5/custom_recipes/sushi）含虾组合的装盘视觉补全。
    ///
    /// 原版寿司装盘：盘内组合先被 OptionalSushi 通配（optional=[海苔,米饭,青瓜,
    /// 生虾片]）接住 → 实例化 CompositeSushi 载体（ComboCosmeticDecisions +
    /// SushiCosmeticPrefabs 组合查找表）：[海苔+米饭]=寿司卷、[海苔+米饭+鱼]=
    /// m_recipe_sushi_01、[海苔+米饭+青瓜]=m_recipe_sushi_03、
    /// [海苔+米饭+青瓜+鱼]=m_recipe_sushi_04；查不到的组合落到
    /// Plated_Sushi_Wrong_01（"错误组合"兜底，视觉上是未完成的卷）。
    ///
    /// 两类缺口：
    ///  1) 含虾组合在原版查找表里没有条目 → 完成后仍显示 Wrong 兜底（卷）。
    ///  2) 含「鱼+虾」的组合跨原版两张通配表、无节点可匹配 → 黑块；由
    ///     Web_Sushi_OptionalCombo（optional 五材通配，保存菜谱时编辑器自动
    ///     注册进 optionalRecipeMatchListItems）把任意中间组合接到本载体。
    ///
    /// 这里 Harmony prefix ComboCosmeticDecisions.Start：载体初始化前克隆查找表
    /// 并追加条目（条目内容复用表内原版节点，uID 语义一致，匹配/收容限制都走
    /// 原版机制）：
    ///   成品：[海苔+米饭+虾]→m_recipe_sushi_01（鲜虾寿司占位：鱼寿司模型）、
    ///         [海苔+米饭+虾+青瓜]→m_recipe_sushi_03（虾什锦占位：黄瓜寿司）、
    ///         [海苔+米饭+鱼+虾+青瓜]→m_recipe_sushi_04（大满贯：什锦寿司）；
    ///   中间态（含「鱼+虾」、未集齐五材）：
    ///         无海苔 → 鱼虾拼盘（单鱼盘 + 虾片并排组合模板；+青瓜=拼盘+散黄瓜片）；
    ///         有海苔 → 寿司卷（复用原版 [海苔+米饭] 条目的 Plated_Seaweed_Rice）。
    /// 原版 Start 随后对克隆表整体 CacheAssembledOrderNodes（含新条目）。
    /// 组合模板须隐藏防世界原点幽灵，部分实例化路径不激活克隆体——载体上挂
    /// SushiComboActivator 按名兜底激活（同意面 PastaOriginalModels 的教训）。
    /// 仅处理名字含 "Sushi" 的组合查找表（SushiCosmeticPrefabs）；bundle47
    /// 不可达或缺节点时静默跳过，保持原版行为。
    /// </summary>
    public static class SushiPrawnCombos
    {
        const string BundleName = "bundle47";

        // 原版 orderdefinition 节点 uID（data/orderdefinitions/**，bundle18）。
        const int UidSeaweed = 29396;
        const int UidBoiledSushiRice = 18724;
        const int UidSushiFish = 23600;
        const int UidSushiPrawn = 21875;
        const int UidCucumber = 37422;

        static bool s_patched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (s_patched) return;
            Type combo = FindType("ComboCosmeticDecisions");
            if (combo == null) return;
            MethodInfo start = combo.GetMethod("Start",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (start == null) return;
            Harmony harmony = new Harmony("oc2.sushi.prawn-combos");
            harmony.Patch(start, new HarmonyMethod(typeof(SushiPrawnCombos).GetMethod(
                "BeforeCarrierStart", BindingFlags.Static | BindingFlags.NonPublic)));
            s_patched = true;
        }

        /// <summary>载体 Start 前扩展查找表；异常绝不拦原版逻辑。</summary>
        static bool BeforeCarrierStart(MonoBehaviour __instance)
        {
            try
            {
                ExtendLookup(__instance);
            }
            catch (Exception)
            {
            }
            return true;
        }

        static void ExtendLookup(MonoBehaviour carrier)
        {
            if (carrier == null) return;
            FieldInfo lookupField = FindField(carrier.GetType(), "m_comboPrefabLookup");
            if (lookupField == null) return;
            UnityEngine.Object src = lookupField.GetValue(carrier) as UnityEngine.Object;
            if (src == null) return;
            if (src.name.IndexOf("Sushi", StringComparison.OrdinalIgnoreCase) < 0) return;

            FieldInfo arrField = FindField(src.GetType(), "m_lookupArray");
            if (arrField == null) return;
            Array arr = arrField.GetValue(src) as Array;
            if (arr == null || arr.Length == 0) return;

            UnityEngine.Object seaweed = null;
            UnityEngine.Object rice = null;
            UnityEngine.Object fish = null;
            UnityEngine.Object prawn = null;
            UnityEngine.Object cucumber = null;
            CollectNodes(arr, ref seaweed, ref rice, ref fish, ref prawn, ref cucumber);
            if (seaweed == null || rice == null || fish == null || prawn == null || cucumber == null)
                return;

            GameObject fishSushi = LoadMealModel("m_recipe_sushi_01");
            GameObject cucumberSushi = LoadMealModel("m_recipe_sushi_03");
            GameObject assortedSushi = LoadMealModel("m_recipe_sushi_04");
            if (fishSushi == null || cucumberSushi == null || assortedSushi == null)
                return;

            // 中间态素材：卷 = 原版 [海苔+米饭] 条目的 Plated_Seaweed_Rice；
            // 鱼虾拼盘 = 原版单鱼盘 + 虾片（+青瓜=拼盘+散黄瓜片）组合模板。
            GameObject rollPrefab = FindRollPrefab(arr);
            GameObject fishSingle = FindSinglePrefab(arr, UidSushiFish);
            GameObject prawnSingle = FindSinglePrefab(arr, UidSushiPrawn);
            GameObject cucumberSingle = FindSinglePrefab(arr, UidCucumber);
            GameObject platterFP = null;
            GameObject platterFPC = null;
            if (fishSingle != null && prawnSingle != null)
            {
                platterFP = BuildPlatterTemplate("web_sushi_platter_fp",
                    new GameObject[] { fishSingle, prawnSingle },
                    new Vector3[] { new Vector3(-0.22f, 0f, 0.05f), new Vector3(0.22f, 0f, -0.05f) });
                if (cucumberSingle != null)
                    platterFPC = BuildPlatterTemplate("web_sushi_platter_fpc",
                        new GameObject[] { fishSingle, prawnSingle, cucumberSingle },
                        new Vector3[] { new Vector3(-0.22f, 0f, 0.08f), new Vector3(0.22f, 0f, -0.02f), new Vector3(0f, 0f, 0.18f) });
            }

            // 克隆后再改：不写回 bundle 载入的共享查找表实例（与意面 PastaOriginalModels 同则）。
            UnityEngine.Object clone = UnityEngine.Object.Instantiate(src);
            if (!AppendEntry(clone, new UnityEngine.Object[] { seaweed, rice, prawn }, fishSushi))
                return;
            if (!AppendEntry(clone, new UnityEngine.Object[] { seaweed, rice, prawn, cucumber }, cucumberSushi))
                return;
            if (!AppendEntry(clone, new UnityEngine.Object[] { seaweed, rice, fish, prawn, cucumber }, assortedSushi))
                return;
            // 中间态：无海苔 → 鱼虾拼盘（+青瓜 → 拼盘+散黄瓜片）；
            //          有海苔 → 寿司卷（卷制中）。
            if (platterFP != null)
            {
                AppendEntry(clone, new UnityEngine.Object[] { fish, prawn }, platterFP);
                AppendEntry(clone, new UnityEngine.Object[] { rice, fish, prawn }, platterFP);
            }
            if (platterFPC != null)
            {
                AppendEntry(clone, new UnityEngine.Object[] { cucumber, fish, prawn }, platterFPC);
                AppendEntry(clone, new UnityEngine.Object[] { rice, cucumber, fish, prawn }, platterFPC);
            }
            if (rollPrefab != null)
            {
                AppendEntry(clone, new UnityEngine.Object[] { seaweed, fish, prawn }, rollPrefab);
                AppendEntry(clone, new UnityEngine.Object[] { seaweed, rice, fish, prawn }, rollPrefab);
                AppendEntry(clone, new UnityEngine.Object[] { seaweed, cucumber, fish, prawn }, rollPrefab);
            }
            lookupField.SetValue(carrier, clone);
            if (platterFP != null || rollPrefab != null)
                carrier.gameObject.AddComponent<SushiComboActivator>();
        }

        /// <summary>鱼虾拼盘组合模板：部件按位并排、整体缩小 30%（单品盘模型
        ///  原尺寸并排偏大），模板整体隐藏（查找表条目是模板而非场景物体，
        ///  激活会变成世界原点幽灵）；克隆体的激活由 SushiComboActivator 按名兜底。</summary>
        const float PlatterPartScale = 0.7f;

        static GameObject BuildPlatterTemplate(string name, GameObject[] parts, Vector3[] offsets)
        {
            GameObject root = new GameObject(name);
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject part = UnityEngine.Object.Instantiate(
                    parts[i], offsets[i] * PlatterPartScale, Quaternion.identity, root.transform) as GameObject;
                if (part != null)
                    part.transform.localScale = part.transform.localScale * PlatterPartScale;
            }
            root.SetActive(false);
            return root;
        }

        /// <summary>组合模板激活兜底：每 32 帧扫一次载体层级，按名拉起未激活的
        ///  拼盘克隆体（部分实例化路径不会激活克隆体）。层级节点很少，开销可忽略。</summary>
        class SushiComboActivator : MonoBehaviour
        {
            const string TemplateMark = "web_sushi_platter";

            void Update()
            {
                if ((Time.frameCount & 31) != 0) return;
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child == null || child == transform || child.gameObject.activeSelf) continue;
                    if (child.name.IndexOf(TemplateMark, StringComparison.Ordinal) < 0) continue;
                    child.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>原版 [海苔+米饭]（寿司卷）条目的成品 prefab（Plated_Seaweed_Rice）。</summary>
        static GameObject FindRollPrefab(Array arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo contentField = FindField(entry.GetType(), "m_content");
                FieldInfo prefabField = FindField(entry.GetType(), "m_prefab");
                if (contentField == null || prefabField == null) continue;
                System.Collections.IList contents = contentField.GetValue(entry) as System.Collections.IList;
                if (contents == null || contents.Count != 2) continue;
                int uidA = ReadUid(contents[0]);
                int uidB = ReadUid(contents[1]);
                bool pair = (uidA == UidSeaweed && uidB == UidBoiledSushiRice)
                    || (uidA == UidBoiledSushiRice && uidB == UidSeaweed);
                if (!pair) continue;
                GameObject prefab = prefabField.GetValue(entry) as GameObject;
                if (prefab != null)
                    return prefab;
            }
            return null;
        }

        /// <summary>单节点条目（如 [鱼]→单鱼盘、[虾]→虾片、[青瓜]→黄瓜片）的 prefab。</summary>
        static GameObject FindSinglePrefab(Array arr, int uid)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo contentField = FindField(entry.GetType(), "m_content");
                FieldInfo prefabField = FindField(entry.GetType(), "m_prefab");
                if (contentField == null || prefabField == null) continue;
                System.Collections.IList contents = contentField.GetValue(entry) as System.Collections.IList;
                if (contents == null || contents.Count != 1) continue;
                if (ReadUid(contents[0]) != uid) continue;
                GameObject prefab = prefabField.GetValue(entry) as GameObject;
                if (prefab != null)
                    return prefab;
            }
            return null;
        }

        /// <summary>从现有条目里按 uID 收集五个原版节点（条目内容即节点直引）。</summary>
        static void CollectNodes(
            Array arr,
            ref UnityEngine.Object seaweed,
            ref UnityEngine.Object rice,
            ref UnityEngine.Object fish,
            ref UnityEngine.Object prawn,
            ref UnityEngine.Object cucumber)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo contentField = FindField(entry.GetType(), "m_content");
                if (contentField == null) continue;
                System.Collections.IList contents = contentField.GetValue(entry) as System.Collections.IList;
                if (contents == null) continue;
                for (int j = 0; j < contents.Count; j++)
                {
                    UnityEngine.Object node = contents[j] as UnityEngine.Object;
                    if (node == null) continue;
                    int uid = ReadUid(node);
                    if (uid == UidSeaweed) seaweed = node;
                    else if (uid == UidBoiledSushiRice) rice = node;
                    else if (uid == UidSushiFish) fish = node;
                    else if (uid == UidSushiPrawn) prawn = node;
                    else if (uid == UidCucumber) cucumber = node;
                }
            }
        }

        /// <summary>给克隆查找表追加一个条目（m_content=节点数组，m_prefab=成品模型）。
        ///  缓存由随后原版 Start 的 CacheAssembledOrderNodes 统一构建。</summary>
        static bool AppendEntry(UnityEngine.Object lookup, UnityEngine.Object[] contents, GameObject prefab)
        {
            FieldInfo arrField = FindField(lookup.GetType(), "m_lookupArray");
            if (arrField == null) return false;
            Array arr = arrField.GetValue(lookup) as Array;
            if (arr == null || arr.Length == 0) return false;
            Type entryType = arr.GetValue(0).GetType();
            FieldInfo contentField = FindField(entryType, "m_content");
            FieldInfo prefabField = FindField(entryType, "m_prefab");
            if (contentField == null || prefabField == null) return false;

            object entry = Activator.CreateInstance(entryType);
            Type elemType = contentField.FieldType.GetElementType();
            if (elemType == null) return false;
            Array contentArr = Array.CreateInstance(elemType, contents.Length);
            for (int i = 0; i < contents.Length; i++)
                contentArr.SetValue(contents[i], i);
            contentField.SetValue(entry, contentArr);
            prefabField.SetValue(entry, prefab);

            Array newArr = Array.CreateInstance(entryType, arr.Length + 1);
            Array.Copy(arr, newArr, arr.Length);
            newArr.SetValue(entry, arr.Length);
            arrField.SetValue(lookup, newArr);
            return true;
        }

        static int ReadUid(object node)
        {
            if (node == null) return 0;
            FieldInfo f = FindField(node.GetType(), "m_uID");
            if (f == null) return 0;
            object v = f.GetValue(node);
            if (v is int) return (int)v;
            return 0;
        }

        /// <summary>bundle47 原版寿司成品模型；编辑器兜底 jia_carnival_map 提取副本。</summary>
        static GameObject LoadMealModel(string name)
        {
            GameObject go = LoadOriginal("Assets/prefabs/meals/" + name + ".prefab");
            if (go == null)
                go = LoadOriginal("assets/prefabs/meals/" + name + ".prefab");
            if (go == null)
                go = LoadProjectPrefab("Assets/LevelSets/jia_carnival_map/official/prefabs/meals/" + name + ".prefab");
            return go;
        }

        static GameObject LoadProjectPrefab(string assetPath)
        {
            Type ad = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (ad == null) return null;
            MethodInfo load = ad.GetMethod("LoadAssetAtPath", new Type[] { typeof(string), typeof(Type) });
            if (load == null) return null;
            try
            {
                return load.Invoke(null, new object[] { assetPath, typeof(GameObject) }) as GameObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static GameObject LoadOriginal(string assetPath)
        {
            try
            {
                Type mgr = FindType("LevelEditor.PseudoPrefabManager");
                if (mgr == null) return null;
                MethodInfo getBundle = mgr.GetMethod("GetAssetBundle", BindingFlags.Public | BindingFlags.Static);
                if (getBundle == null) return null;
                AssetBundle bundle = getBundle.Invoke(null, new object[] { BundleName }) as AssetBundle;
                if (bundle == null) return null;
                GameObject go = bundle.LoadAsset<GameObject>(assetPath);
                if (go == null) go = bundle.LoadAsset<GameObject>(assetPath.ToLowerInvariant());
                return go;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>实例字段在基类上时，GetField 不会往上找。</summary>
        static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }

        static Type FindType(string fullName)
        {
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                Type t = asms[i].GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
