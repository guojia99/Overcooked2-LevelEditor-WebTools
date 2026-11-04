using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 意面的煎寿司鱼 / 煎寿司虾，以及盘内组合，改用 bundle47 里的原版模型。
    /// 本地 OBJ + WebPlatedFood 在游戏光照下是黑块。
    ///
    /// 煎锅内熟物：寿司鱼用原版煎鱼 prep_fish_sliced_01；寿司虾用 commonW2 炸虾饼
    /// FriedShrimpCake（原版锅内是圆形煎制物）。场景 stub 已提供专属熟物模型
    /// （models/modelSOs 对齐 allowedIngredientSOs）时不覆盖。
    /// 装盘（盘=锅同款）：寿司鱼 prep_fish_sliced_01、寿司虾炸虾饼（PrawnNode 的
    /// model 直引）。意面成品盘四态全部用原版整盘模型（plated_pasta_01 /
    /// plated_pasta_fish_01 / plated_pasta_prawn_01 / recipe_seafood_pasta_01，
    /// 均 bundle47），本地 OBJ 拷贝仅作加载失败时的回落。
    /// 「意面拼盘」组合节点（Web_Seafood_Pasta_ToppingsCombo，optional=[煎鱼,煎虾]）
    /// 覆盖 [鱼]/[虾]/[鱼+虾]（无意面）子集装盘；AppendToppingsEntries 给盘体查找表
    /// 追加对应三态（鱼=煎鱼、虾=炸虾饼、鱼+虾=原版海鲜意面整盘造型）。
    /// 没有装盘模型的煎鱼中间产物（FriedFish）也补上同一张煎鱼网格。
    /// 海鲜意面盘：鱼/虾那两态同样换这两张，素意面和海鲜成品仍用原版盘子模型。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PastaOriginalModels : MonoBehaviour
    {
        const string BundleName = "bundle47";
        const int UidSushiFish = 23600;
        const int UidSushiPrawn = 21875;

        static bool s_patched;
        bool m_hasPairEntries;

        /// <summary>组合体模板根名（状态克隆按名识别，见 Update 的激活兜底）。</summary>
        const string PairTemplateName = "pasta_toppings_pair";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPanPatch()
        {
            if (s_patched) return;
            Type utensil = FindType("LevelEditor.PseudoPrefabCookingUtensil");
            Harmony harmony = new Harmony("oc2.pasta.original-models");
            MethodInfo setup = utensil != null
                ? utensil.GetMethod("Setup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                : null;
            if (setup != null)
                harmony.Patch(setup, null, new HarmonyMethod(typeof(PastaOriginalModels).GetMethod("AfterUtensilSetup", BindingFlags.Static | BindingFlags.NonPublic)));
            Type helper = FindType("LevelEditor.RecipeHelper");
            if (helper != null)
            {
                MethodInfo build = helper.GetMethod("GetOrderDefinitionNodeCustomRecipe", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (build != null)
                    harmony.Patch(build, null, new HarmonyMethod(typeof(PastaOriginalModels).GetMethod("AfterCustomRecipe", BindingFlags.Static | BindingFlags.NonPublic)));
            }
            s_patched = true;
        }

        /// <summary>
        /// 煎鱼中间产物（FriedFish）和煎寿司鱼/虾没有装盘模型时，用锅内同款网格补上
        /// （盘=锅同款：虾=炸虾饼、鱼=原版煎鱼）。否则盘子上的 m_platingPrefab 为空，
        /// 只会显示黑块。
        /// </summary>
        static void AfterCustomRecipe(object __result)
        {
            if (__result == null) return;
            FieldInfo prefabField = FindField(__result.GetType(), "m_platingPrefab");
            if (prefabField == null) return;
            if (prefabField.GetValue(__result) != null) return;
            string which = SushiKind(__result);
            if (which == null) return;
            GameObject standIn = LoadPanStandIn(which);
            if (standIn != null) prefabField.SetValue(__result, standIn);
        }

        static GameObject LoadSushiStandIn(string which)
        {
            if (which == "fish")
            {
                GameObject go = LoadOriginal("assets/prefabs/ingredients/prep_fish_sliced_01.prefab");
                if (go == null)
                    go = LoadProjectPrefab("Assets/common01/food/CustomRecipes/Fry/models/Fry_Fish.prefab");
                return go;
            }
            if (which == "prawn")
            {
                GameObject go = LoadOriginal("assets/prefabs/ingredients/prawn_fried_01.prefab");
                if (go == null)
                    go = LoadProjectPrefab("Assets/commonW2/custom_recipes/burger/models/FriedShrimpCake.prefab");
                return go;
            }
            return null;
        }

        /// <summary>煎锅内熟寿司虾/鱼模型（盘=锅同款）：虾=commonW2 炸虾饼（原版锅内
        /// 为圆形煎制物），编辑器经 AssetDatabase 解析；真机的锅/盘模型分别由煎锅 stub
        /// 的 models 与 PrawnNode.model 直引提供（均不经过这里），此处回落仅编辑器兜底。</summary>
        static GameObject LoadPanStandIn(string which)
        {
            if (which == "prawn")
            {
                GameObject cake = LoadProjectPrefab("Assets/commonW2/custom_recipes/burger/models/FriedShrimpCake.prefab");
                if (cake != null)
                    return cake;
            }
            return LoadSushiStandIn(which);
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

        static void AfterUtensilSetup(MonoBehaviour __instance)
        {
            if (__instance == null) return;
            FieldInfo childField = FindField(__instance.GetType(), "childGameObject");
            if (childField == null) return;
            GameObject child = childField.GetValue(__instance) as GameObject;
            if (child == null) return;
            PatchLookupsOn(child);
            Component[] roots = child.GetComponents<Component>();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                if (roots[i].GetType().Name != "CookableContainer") continue;
                FieldInfo cosField = FindField(roots[i].GetType(), "m_cosmeticsPrefab");
                if (cosField == null) continue;
                GameObject cosmetics = cosField.GetValue(roots[i]) as GameObject;
                if (cosmetics != null) PatchLookupsOn(cosmetics);
            }
        }

        void Awake()
        {
            Component[] all = GetComponents<Component>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i] == this) continue;
                FieldInfo lookupField = FindField(all[i].GetType(), "m_comboPrefabLookup");
                if (lookupField == null) continue;
                UnityEngine.Object src = lookupField.GetValue(all[i]) as UnityEngine.Object;
                if (src == null) continue;
                // 克隆后再改，避免把运行时原版 prefab 写回工程里的查找表资产。
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(src);
                ReplaceCombo(clone);
                // 先提交（含四态原版模型替换），再防御性追加拼盘三态——
                // 追加失败绝不能把整条 Awake 炸掉（否则连基础四态都回退本地黑块）。
                lookupField.SetValue(all[i], clone);
                try
                {
                    AppendToppingsEntries(clone, this);
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// 「意面拼盘」组合节点（optional=[煎鱼,煎虾]）装盘时盘内容是 [鱼]/[虾]/[鱼+虾]
        /// （不含意面），原查找表只有含意面的 4 态——这里给克隆查找表追加三个状态条目：
        /// [鱼]→原版煎鱼、[虾]→锅内同款炸虾饼、[鱼+虾]→原版海鲜意面整盘造型
        /// （recipe_seafood_pasta_01，鱼+虾原版摆盘；不可达时回落本地并排组合体）。
        /// 条目内容复用表内现有的熟鱼/熟虾克隆节点（uID 语义一致）。
        /// 真机 bundle47 不可达时跳过（保持原表）。
        /// </summary>
        static void AppendToppingsEntries(UnityEngine.Object clone, PastaOriginalModels self)
        {
            if (clone == null) return;
            FieldInfo arrField = FindField(clone.GetType(), "m_lookupArray");
            if (arrField == null) return;
            Array arr = arrField.GetValue(clone) as Array;
            if (arr == null || arr.Length == 0) return;
            Type entryType = arr.GetValue(0).GetType();
            FieldInfo contentField = FindField(entryType, "m_content");
            FieldInfo prefabField = FindField(entryType, "m_prefab");
            if (contentField == null || prefabField == null) return;

            UnityEngine.Object fishClone = null;
            UnityEngine.Object prawnClone = null;
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                System.Collections.IList contents = contentField.GetValue(entry) as System.Collections.IList;
                if (contents == null) continue;
                for (int j = 0; j < contents.Count; j++)
                {
                    UnityEngine.Object node = contents[j] as UnityEngine.Object;
                    if (node == null) continue;
                    string kind = SushiKind(node);
                    if (kind == "fish" && fishClone == null) fishClone = node;
                    else if (kind == "prawn" && prawnClone == null) prawnClone = node;
                }
            }
            if (fishClone == null || prawnClone == null) return;

            GameObject fishGo = LoadPanStandIn("fish");
            GameObject prawnGo = LoadPanStandIn("prawn");
            if (fishGo == null || prawnGo == null) return;

            // [鱼+虾] 混合装盘优先原版海鲜意面整盘造型（鱼+虾的原版摆盘）。
            GameObject pairPrefab = LoadOriginal("assets/prefabs/meals/recipe_seafood_pasta_01.prefab");
            GameObject pair;
            if (pairPrefab != null)
            {
                pair = pairPrefab;
            }
            else
            {
                pair = new GameObject(PairTemplateName);
                UnityEngine.Object.Instantiate(fishGo, new Vector3(-0.24f, 0f, 0.04f), Quaternion.identity, pair.transform);
                UnityEngine.Object.Instantiate(prawnGo, new Vector3(0.24f, 0f, -0.04f), Quaternion.identity, pair.transform);
                // 模板必须隐藏（查找表条目是模板而非场景物体，激活会变成世界原点幽灵）。
                // 部分实例化路径不会激活克隆体——Update 里按名兜底激活。
                pair.SetActive(false);
            }

            object fishEntry = MakeLookupEntry(entryType, contentField, prefabField, new UnityEngine.Object[] { fishClone }, fishGo);
            object prawnEntry = MakeLookupEntry(entryType, contentField, prefabField, new UnityEngine.Object[] { prawnClone }, prawnGo);
            object pairEntry = MakeLookupEntry(entryType, contentField, prefabField, new UnityEngine.Object[] { fishClone, prawnClone }, pair);
            if (fishEntry == null || prawnEntry == null || pairEntry == null) return;
            self.m_hasPairEntries = true;
            Array newArr = Array.CreateInstance(entryType, arr.Length + 3);
            Array.Copy(arr, newArr, arr.Length);
            newArr.SetValue(fishEntry, arr.Length);
            newArr.SetValue(prawnEntry, arr.Length + 1);
            newArr.SetValue(pairEntry, arr.Length + 2);
            arrField.SetValue(clone, newArr);
        }

        /// <summary>激活兜底：组合体模板是 inactive（防世界原点幽灵），若盘内状态的
        /// 实例化路径未激活克隆体（克隆名带 "(Clone)" 后缀），这里按名兜底拉起。
        /// 每 32 帧扫一次盘体层级（节点很少，开销可忽略）；无组合条目时零成本。</summary>
        void Update()
        {
            if (!m_hasPairEntries || (Time.frameCount & 31) != 0) return;
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == transform || child.gameObject.activeSelf) continue;
                if (child.name.IndexOf(PairTemplateName, StringComparison.Ordinal) < 0) continue;
                child.gameObject.SetActive(true);
            }
        }

        static object MakeLookupEntry(
            Type entryType,
            FieldInfo contentField,
            FieldInfo prefabField,
            UnityEngine.Object[] contents,
            GameObject prefab)
        {
            object entry = Activator.CreateInstance(entryType);
            // m_content 是 OrderDefinitionNode[]（数组）：数组没有默认构造器，
            // Activator.CreateInstance 会抛 MissingMethodException——必须走
            // Array.CreateInstance；兼容 List< T > 形态以防万一。
            Type elemType = contentField.FieldType.GetElementType();
            if (elemType != null)
            {
                Array contentArr = Array.CreateInstance(elemType, contents.Length);
                for (int i = 0; i < contents.Length; i++)
                    contentArr.SetValue(contents[i], i);
                contentField.SetValue(entry, contentArr);
            }
            else
            {
                System.Collections.IList list = Activator.CreateInstance(contentField.FieldType) as System.Collections.IList;
                if (list == null) return null;
                for (int i = 0; i < contents.Length; i++)
                    list.Add(contents[i]);
                contentField.SetValue(entry, list);
            }
            prefabField.SetValue(entry, prefab);
            return entry;
        }

        static void PatchLookupsOn(GameObject root)
        {
            if (root == null) return;
            Component[] all = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                FieldInfo lookupField = FindField(all[i].GetType(), "m_prefabLookup");
                if (lookupField == null) continue;
                ReplaceCookedSushi(lookupField.GetValue(all[i]));
            }
        }

        static void ReplaceCookedSushi(object lookup)
        {
            if (lookup == null) return;
            FieldInfo arrField = FindField(lookup.GetType(), "m_lookupArray");
            if (arrField == null) return;
            Array arr = arrField.GetValue(lookup) as Array;
            if (arr == null) return;

            // 第一遍：收集生寿司鱼/虾条目的锅内模型。Setup 未给熟物提供模型时会把
            // 生物模型复用给熟物条目——熟物 prefab 与同种生物相同（或为空）即视为
            // 「无专属熟物模型」，需要兜底替换；不同则说明场景 stub 的
            // models/modelSOs 已提供（如炸虾饼），不覆盖。
            Dictionary<string, GameObject> rawPrefabs = new Dictionary<string, GameObject>();
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo contentField = FindField(entry.GetType(), "m_content");
                FieldInfo prefabField = FindField(entry.GetType(), "m_prefab");
                if (contentField == null || prefabField == null) continue;
                object content = contentField.GetValue(entry);
                if (content == null) continue;
                if (content.GetType().Name.IndexOf("Cooked") >= 0) continue;
                string rawKind = SushiKind(content);
                if (rawKind == null) continue;
                GameObject rawPrefab = prefabField.GetValue(entry) as GameObject;
                if (rawPrefab != null)
                    rawPrefabs[rawKind] = rawPrefab;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo contentField = FindField(entry.GetType(), "m_content");
                FieldInfo prefabField = FindField(entry.GetType(), "m_prefab");
                if (contentField == null || prefabField == null) continue;
                object content = contentField.GetValue(entry);
                if (content == null) continue;
                if (content.GetType().Name.IndexOf("Cooked") < 0) continue;
                string which = SushiKind(content);
                if (which == null) continue;
                GameObject cur = prefabField.GetValue(entry) as GameObject;
                if (cur != null && rawPrefabs.ContainsKey(which) && cur != rawPrefabs[which])
                    continue;
                GameObject orig = LoadPanStandIn(which);
                if (orig != null) prefabField.SetValue(entry, orig);
            }
        }

        static void ReplaceCombo(object lookup)
        {
            if (lookup == null) return;
            ReplacePrefabArray(lookup, "m_lookupArray");
            ReplacePrefabArray(lookup, "m_fallbackArray");
            FieldInfo fallback = FindField(lookup.GetType(), "m_defaultFallback");
            if (fallback == null) return;
            GameObject cur = fallback.GetValue(lookup) as GameObject;
            GameObject orig = OriginalForName(cur);
            if (orig != null) fallback.SetValue(lookup, orig);
        }

        static void ReplacePrefabArray(object lookup, string fieldName)
        {
            FieldInfo arrField = FindField(lookup.GetType(), fieldName);
            if (arrField == null) return;
            Array arr = arrField.GetValue(lookup) as Array;
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                object entry = arr.GetValue(i);
                if (entry == null) continue;
                FieldInfo prefabField = FindField(entry.GetType(), "m_prefab");
                if (prefabField == null) continue;
                GameObject orig = OriginalForName(prefabField.GetValue(entry) as GameObject);
                if (orig != null) prefabField.SetValue(entry, orig);
            }
        }

        static GameObject OriginalForName(GameObject cur)
        {
            if (cur == null) return null;
            string name = cur.name;
            if (name == "plated_pasta_01") return LoadOriginal("assets/prefabs/plated/plated_pasta_01.prefab");
            if (name == "plated_pasta_fish_01")
            {
                // 意面+鱼是**整盘**造型（原版 Prefabs/Plated 同族资产，bundle47）——
                // 不能只换鱼模型（会把意面部分丢掉）；加载失败回落单鱼模型兜底。
                GameObject go = LoadOriginal("assets/prefabs/plated/plated_pasta_fish_01.prefab");
                return go != null ? go : LoadSushiStandIn("fish");
            }
            if (name == "plated_pasta_prawn_01")
            {
                // 同上：意面+虾整盘造型（此前误换 prawn_fried_01 单虾，鲜虾意面没意面）。
                GameObject go = LoadOriginal("assets/prefabs/plated/plated_pasta_prawn_01.prefab");
                return go != null ? go : LoadSushiStandIn("prawn");
            }
            if (name == "recipe_seafood_pasta_01") return LoadOriginal("assets/prefabs/meals/recipe_seafood_pasta_01.prefab");
            return null;
        }

        static string SushiKind(object node)
        {
            int uid = ReadUid(node);
            if (uid == UidSushiFish) return "fish";
            if (uid == UidSushiPrawn) return "prawn";
            FieldInfo comp = FindField(node.GetType(), "m_composition");
            if (comp == null) return null;
            Array arr = comp.GetValue(node) as Array;
            if (arr == null) return null;
            for (int i = 0; i < arr.Length; i++)
            {
                object child = arr.GetValue(i);
                if (child == null) continue;
                int childUid = ReadUid(child);
                if (childUid == UidSushiFish) return "fish";
                if (childUid == UidSushiPrawn) return "prawn";
            }
            return null;
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

        /// <summary>实例字段在基类上时，GetField 不会往上找。</summary>
        static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
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
