using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LevelEditorStub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStub
{
    // Built synchronously before entity scanning. Isolated paged tables leave the backpack's
    // native default/callback indices untouched and obey the four-bit SpawnEntityMessage ID.
    internal sealed class WebBackpackPrefabDirectory
    {
        internal sealed class Entry
        {
            internal string Key;
            internal GameObject Prefab;
            internal bool Storable;
            internal int Stage;
            internal GameObject Spawner;
            internal int Page;
            internal int LocalId;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        // Fixed entity topology even when one peer cannot resolve a source. Empty pages use
        // native ObjectContainer synchronizers; no prefab is required to scan those entities.
        private const int PageSize = 16;
        private const int PageCount = 8;
        private readonly Dictionary<GameObject, Entry> _byPrefab = new Dictionary<GameObject, Entry>();
        private bool _valid = true;
        internal string Digest = "invalid";
        private string _ownerKey;
        private readonly List<GameObject> _pages = new List<GameObject>();
        private static bool s_installed;
        private static bool s_active;
        private static GameObject s_captureOwner;
        private static GameObject s_capturedSpawn;
        private static readonly List<MethodBase> s_patchTargets = new List<MethodBase>();
        private static readonly Dictionary<GameObject, GameObject> s_origins = new Dictionary<GameObject, GameObject>();
        private static readonly Dictionary<string, float> s_diagnostics = new Dictionary<string, float>();

        internal static void Reset()
        {
            s_active = false;
            s_origins.Clear();
            s_captureOwner = null;
            s_capturedSpawn = null;
            s_diagnostics.Clear();
            WebBackpackDirectoryHandshake.Reset();
            if (s_installed)
            {
                Uninstall();
                s_installed = false;
            }
        }

        internal static void Diagnostic(string key, string message)
        {
            float now = Time.realtimeSinceStartup;
            float next;
            if (s_diagnostics.TryGetValue(key, out next) && now < next)
                return;
            s_diagnostics[key] = now + 5f;
            StubLog.Log("[BackpackNet] " + message);
        }

        internal static bool Install()
        {
            s_active = true;
            if (s_installed)
                return true;
            try
            {
                var harmony = new Harmony("oc2.customstub.backpack.directory");
                var flags = BindingFlags.Static | BindingFlags.NonPublic;
                var self = typeof(WebBackpackPrefabDirectory);
                var collection = GameApi.Find("SpawnableEntityCollection");
                var spawn = collection != null ? collection.GetMethod("SpawnEntity") : null;
                MethodInfo register = null;
                if (GameApi.NetworkUtilsType != null)
                    foreach (var method in GameApi.NetworkUtilsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        if (method.Name == "RegisterSpawnablePrefab" && method.GetParameters().Length == 3)
                            register = method;
                if (spawn == null || register == null)
                    throw new InvalidOperationException("spawn/registration API missing");
                s_patchTargets.Add(spawn);
                harmony.Patch(spawn, new HarmonyMethod(self.GetMethod("BeforeSpawn", flags)),
                    new HarmonyMethod(self.GetMethod("AfterSpawn", flags)));
                s_patchTargets.Add(register);
                harmony.Patch(register, new HarmonyMethod(self.GetMethod("BeforeRegister", flags)));
                var receiver = GameApi.Find("ClientSynchronisationReceiver");
                foreach (string name in new[] { "OnSpawnEntityMessageReceived", "OnSpawnPhysicalAttachmentMessageReceived" })
                {
                    var method = receiver != null ? receiver.GetMethod(name) : null;
                    if (method == null) throw new MissingMethodException(name);
                    s_patchTargets.Add(method);
                    harmony.Patch(method, new HarmonyMethod(self.GetMethod("BeforeReceiveSpawn", flags)));
                }
                WebBackpackRecoveryIsolation.Install(harmony, s_patchTargets);
                var controller = GameApi.Find("MultiplayerController");
                var startAll = controller != null ? controller.GetMethod("StartSynchronisation") : null;
                if (startAll == null)
                    throw new InvalidOperationException("MultiplayerController.StartSynchronisation missing");
                s_patchTargets.Add(startAll);
                harmony.Patch(startAll, new HarmonyMethod(self.GetMethod("BeforeStartAll", flags)));
                foreach (string typeName in new[] { "ServerPickupItemSpawner", "ClientPickupItemSpawner" })
                {
                    var type = GameApi.Find(typeName);
                    var start = type != null ? type.GetMethod("StartSynchronising") : null;
                    if (start == null)
                        throw new InvalidOperationException(typeName + ".StartSynchronising missing");
                    s_patchTargets.Add(start);
                    harmony.Patch(start, new HarmonyMethod(self.GetMethod("BeforeStart", flags)));
                }
                s_installed = true;
                StubLog.Log("[BackpackNet] revision=4.0.0.22 directory hooks installed (pre-scan pages, native defaults isolated)");
                return true;
            }
            catch (Exception ex)
            {
                Uninstall();
                Diagnostic("install", "directory hooks unavailable: " + ex.Message);
                return false;
            }
        }

        private static void Uninstall()
        {
            var harmony = new Harmony("oc2.customstub.backpack.directory");
            foreach (var target in s_patchTargets)
                harmony.Unpatch(target, HarmonyPatchType.All, "oc2.customstub.backpack.directory");
            s_patchTargets.Clear();
        }

        private static void BeforeStartAll()
        {
            if (!s_active)
                return;
            // Freeze all directories before native synchronizers create UI/reparent objects.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var storage in root.GetComponentsInChildren<WebBackpackStorage>(true))
                {
                    storage.CheckPrefabDirectory();
                }
        }

        private static void BeforeStart(Component __instance)
        {
            if (!s_active || __instance == null)
                return;
            var storage = WebBackpackStorage.FindForDispenser(__instance);
            if (storage != null)
                storage.CheckPrefabDirectory();
        }

        private static bool BeforeRegister(GameObject _gameObject, GameObject _prefab)
        {
            if (!s_active || _gameObject == null)
                return true;
            var page = _gameObject.GetComponent<WebBackpackSpawnPage>();
            return page == null || page.Storage == null
                || page.Storage.AllowPrefabRegistration(_gameObject, _prefab);
        }

        private static bool BeforeSpawn(Component __instance, int _id, out GameObject __state)
        {
            __state = null;
            if (!s_active || __instance == null)
                return true;
            var page = __instance.gameObject.GetComponent<WebBackpackSpawnPage>();
            // Client receiver is guarded BEFORE invoking SpawnEntity. Never return null here:
            // the native caller would register that null as a network object.
            if (page != null && (page.Storage == null || !page.Storage.CanDecodeSpawnPage(_id, __instance.gameObject)))
                throw new InvalidOperationException("Backpack page decoder unavailable; receiver must reject before spawning");
            var field = __instance.GetType().GetField("m_spawnables", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(__instance) as IList : null;
            if (list != null && _id >= 0 && _id < list.Count)
                __state = list[_id] as GameObject;
            if (page != null)
                Diagnostic("spawn:" + __instance.gameObject.name + ":" + _id,
                    "spawn role=" + (GameApi.IsServerMachine() ? "server" : "client")
                    + " page=" + __instance.gameObject.name + " entity=" + GameApi.GetEntityId(__instance.gameObject)
                    + " id=" + _id + " prefab=" + (__state != null ? __state.name : "unresolved"));
            return true;
        }

        private static bool BeforeReceiveSpawn(object message)
        {
            if (!s_active || message == null) return true;
            try
            {
                var nested = message.GetType().GetField("m_SpawnEntityData");
                var spawn = nested != null ? nested.GetValue(message) : message;
                var header = spawn.GetType().GetField("m_SpawnerHeader").GetValue(spawn);
                var id = (uint)header.GetType().GetField("m_uEntityID").GetValue(header);
                var registry = GameApi.Find("EntitySerialisationRegistry");
                var entry = registry.GetMethod("GetEntry", new[] { typeof(uint) }).Invoke(null, new object[] { id });
                if (entry == null) return true; // Native missing-source check consumes this case.
                var owner = entry.GetType().GetField("m_GameObject").GetValue(entry) as GameObject;
                var page = owner != null ? owner.GetComponent<WebBackpackSpawnPage>() : null;
                if (page == null) return true;
                int localId = (int)spawn.GetType().GetField("m_SpawnableID").GetValue(spawn);
                bool valid = page.Storage != null && page.Storage.CanDecodeSpawnPage(localId, owner);
                if (!valid) Diagnostic("receive-unverified", "entire native spawn receive rejected before null registration; directory unavailable");
                return valid;
            }
            catch (Exception ex)
            {
                Diagnostic("receive-guard", "spawn receive guard failed closed: " + ex.Message);
                return false;
            }
        }

        private static void AfterSpawn(Component __instance, GameObject __result, GameObject __state)
        {
            if (!s_active || __result == null || __state == null)
                return;
            // Exact reference captured before Instantiate, including workstation transformations.
            s_origins[__result] = __state;
            if (s_captureOwner != null && __instance.gameObject == s_captureOwner)
                s_capturedSpawn = __result;
            if ((s_origins.Count & 63) == 0)
            {
                var dead = new List<GameObject>();
                foreach (var pair in s_origins)
                    if (pair.Key == null)
                        dead.Add(pair.Key);
                foreach (var item in dead)
                    s_origins.Remove(item);
            }
        }

        internal static void BeginSpawnCapture(GameObject owner)
        {
            s_captureOwner = owner;
            s_capturedSpawn = null;
        }

        internal static GameObject EndSpawnCapture()
        {
            var result = s_capturedSpawn;
            s_captureOwner = null;
            s_capturedSpawn = null;
            return result;
        }

        internal Entry Find(GameObject prefab)
        {
            Entry entry;
            return prefab != null && _byPrefab.TryGetValue(prefab, out entry) ? entry : null;
        }

        internal Entry FindItem(GameObject item)
        {
            GameObject prefab;
            return item != null && s_origins.TryGetValue(item, out prefab) ? Find(prefab) : null;
        }

        internal bool BuildAndRegister(GameObject backpack)
        {
            try
            {
                // Allocate a fixed topology before any source lookup can fail on one peer.
                var storage = backpack.GetComponent<WebBackpackChild>().storage;
                _ownerKey = SceneManager.GetActiveScene().name + "/" + PathOf(backpack.transform);
                var containerType = GameApi.Find("ObjectContainer");
                if (containerType == null)
                    throw new InvalidOperationException("ObjectContainer missing");
                for (int pageIndex = 0; pageIndex < PageCount; pageIndex++)
                {
                    var page = new GameObject("WebBackpackSpawnPage_" + pageIndex.ToString("D3"));
                    page.transform.SetParent(backpack.transform, false);
                    var marker = page.AddComponent<WebBackpackSpawnPage>();
                    marker.Storage = storage;
                    page.AddComponent(containerType);
                    _pages.Add(page);
                }
                // Scene hierarchy paths and serialized array positions are the stable source keys.
                // No UID/name matching and no dependency on the order of FindObjects results.
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                var collectionType = GameApi.Find("SpawnableEntityCollection");
                var spawnablesField = collectionType != null ? collectionType.GetField("m_spawnables",
                    BindingFlags.NonPublic | BindingFlags.Instance) : null;
                for (int r = 0; r < roots.Length; r++)
                {
                    var transforms = roots[r].GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        var go = transforms[i].gameObject;
                        if (go.GetComponent<WebBackpackSpawnPage>() != null)
                            continue;
                        string path = PathOf(transforms[i]);
                        var spawner = GameApi.PickupItemSpawnerType != null ? go.GetComponent(GameApi.PickupItemSpawnerType) : null;
                        if (spawner != null && GameApi.PickupItemPrefabField != null)
                            AddChain(GameApi.PickupItemPrefabField.GetValue(spawner) as GameObject, path + "/spawner");
                        // TriggerAttachedSpawn registers its weighted source array in Awake,
                        // before synchronization. Include that native table in its original order.
                        var nativeTable = collectionType != null ? go.GetComponent(collectionType) : null;
                        var nativeSources = nativeTable != null && spawnablesField != null
                            ? spawnablesField.GetValue(nativeTable) as IList : null;
                        if (nativeSources != null)
                            for (int n = 0; n < nativeSources.Count; n++)
                                AddChain(nativeSources[n] as GameObject, path + "/native/" + n.ToString("D5"));
                        var components = go.GetComponents<Component>();
                        PseudoPrefabSO authored = null;
                        bool ambiguousSource = false;
                        foreach (var candidate in components)
                        {
                            var source = WebBackpackAuthoredSource.Source(candidate);
                            if (source == null)
                                continue;
                            if (authored != null && authored != source)
                                ambiguousSource = true;
                            authored = source;
                        }
                        if (authored != null && !ambiguousSource)
                        {
                            var physical = WebBackpackAuthoredSource.ExactChild(go);
                            var prefab = WebBackpackAuthoredSource.Load(authored);
                            if (physical != null && prefab != null)
                            {
                                AddChain(prefab, path + "/authored/" + authored.bundleName + "/" + authored.assetPath);
                                GameObject previous;
                                if (s_origins.TryGetValue(physical, out previous) && previous != prefab)
                                {
                                    _valid = false;
                                    Diagnostic("authored-conflict", "conflicting authored prefab source: " + path);
                                }
                                else
                                    s_origins[physical] = prefab;
                            }
                            else if (physical != null)
                                _valid = false;
                        }
                        else if (ambiguousSource)
                            Diagnostic("authored-ambiguous:" + path, "authored physical source ambiguous; item provenance not assigned: " + path);
                        for (int c = 0; c < components.Length; c++)
                        {
                            var component = components[c];
                            if (component == null)
                                continue;
                            string type = component.GetType().FullName;
                            if (type != "CustomStub.RandomCrate" && type != "CustomStub.WebBackpackStorage")
                                continue;
                            string fieldName = type == "CustomStub.RandomCrate" ? "m_itemSOs" : "m_initialFillSOs";
                            var field = component.GetType().GetField(fieldName);
                            var sources = field != null ? field.GetValue(component) as PseudoPrefabSO[] : null;
                            if (sources == null)
                                continue;
                            for (int n = 0; n < sources.Length; n++)
                            {
                                var so = sources[n];
                                if (so == null)
                                    continue;
                                // Avoid host LoadAsset's DeInit/Init fallback inside synchronization.
                                var direct = GameApi.GetPseudoPrefabDirectAsset(so);
                                if (direct != null && !(direct is GameObject))
                                    continue; // Existing node-only recipe initial fill, never a spawn source.
                                var prefab = direct as GameObject;
                                if (prefab == null)
                                {
                                    var bundle = GameApi.GetAssetBundle(so.bundleName);
                                    if (bundle == null)
                                    {
                                        _valid = false;
                                        continue;
                                    }
                                    var asset = bundle.LoadAsset(so.assetPath);
                                    prefab = asset as GameObject;
                                    if (asset == null)
                                        _valid = false;
                                    else if (prefab == null)
                                        continue;
                                }
                                AddChain(prefab, path + "/" + type + "/" + n.ToString("D5")
                                    + "/" + so.bundleName + "/" + so.assetPath);
                            }
                        }
                    }
                }
                var sorted = new List<Entry>(_byPrefab.Values);
                sorted.Sort(delegate(Entry a, Entry b) { return StringComparer.Ordinal.Compare(a.Key, b.Key); });
                for (int i = 1; i < sorted.Count; i++)
                    if (StringComparer.Ordinal.Equals(sorted[i - 1].Key, sorted[i].Key))
                        _valid = false; // Ambiguous duplicate scene source paths cannot define IDs.
                _entries.AddRange(sorted);
                if (spawnablesField == null)
                    throw new InvalidOperationException("native source/registration API missing");
                // SpawnEntityMessage serializes a 4-bit local ID. Pages must exist BEFORE
                // ScanEntities, so both peers scan the same additional native spawner entities.
                if (!_valid || _entries.Count == 0 || _entries.Count > PageCount * PageSize)
                    throw new InvalidOperationException("incomplete sources or directory exceeds " + PageCount * PageSize);
                for (int i = 0; i < _entries.Count; i++)
                {
                    var owner = _pages[i / PageSize];
                    _entries[i].Spawner = owner;
                    _entries[i].Page = i / PageSize;
                    _entries[i].LocalId = i % PageSize;
                    if (!GameApi.RegisterSpawnable(owner, _entries[i].Prefab))
                        throw new InvalidOperationException("registration failed at " + i);
                }
                if (!Verify(backpack))
                    throw new InvalidOperationException("registered IDs differ from directory");
                uint hash = 2166136261;
                var mapping = new List<string>();
                for (int i = 0; i < _entries.Count; i++)
                {
                    string row = (i / PageSize) + "/" + (i % PageSize) + "=" + _entries[i].Key + ":" + _entries[i].Prefab.name + ":" + _entries[i].Storable;
                    foreach (char ch in row + "\n")
                        hash = unchecked((hash ^ ch) * 16777619);
                    mapping.Add(row);
                }
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    Digest = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                        string.Join("\n", mapping.ToArray())))).Replace("-", "");
                StubLog.Log("[BackpackNet] directory role=" + (GameApi.IsServerMachine() ? "server" : "client")
                    + " owner=" + PathOf(backpack.transform) + " count=" + _entries.Count + " hash=" + hash.ToString("X8"));
                // Once per directory, bounded lines for comparing actual numeric IDs on both peers.
                for (int i = 0; i < mapping.Count; i += 8)
                    StubLog.Log("[BackpackNet] IDs " + string.Join(" | ", mapping.GetRange(i, Math.Min(8, mapping.Count - i)).ToArray()));
                return true;
            }
            catch (Exception ex)
            {
                _valid = false;
                Diagnostic("directory", "directory rejected, physical items retained: " + ex.Message);
                return false;
            }
        }

        internal bool Verify(GameObject backpack)
        {
            var type = GameApi.Find("SpawnableEntityCollection");
            var field = type != null ? type.GetField("m_spawnables", BindingFlags.Instance | BindingFlags.NonPublic) : null;
            if (!_valid || field == null || _pages.Count == 0)
                return false;
            for (int page = 0; page < _pages.Count; page++)
            {
                if (_pages[page] == null)
                    return false;
                var component = _pages[page].GetComponent(type);
                var list = component != null ? field.GetValue(component) as IList : null;
                int size = Math.Max(0, Math.Min(PageSize, _entries.Count - page * PageSize));
                if ((size != 0 && list == null) || (list != null && list.Count != size))
                    return false;
                for (int i = 0; i < size; i++)
                    if (!ReferenceEquals(list[i], _entries[page * PageSize + i].Prefab))
                        return false;
            }
            return true;
        }

        internal string HandshakeRow(GameObject backpack)
        {
            var ids = new List<string>();
            foreach (var page in _pages)
                ids.Add(GameApi.GetEntityId(page).ToString());
            return _ownerKey + "|" + Digest
                + "|" + string.Join(",", ids.ToArray());
        }

        internal bool PagesLinked()
        {
            if (_pages.Count != PageCount) return false;
            foreach (var page in _pages)
                if (page == null || GameApi.GetEntityId(page) == 0) return false;
            return true;
        }

        internal bool ContainsPageSlot(GameObject page, int localId)
        {
            foreach (var entry in _entries)
                if (entry.Spawner == page && entry.LocalId == localId) return true;
            return false;
        }

        internal Entry FindUniqueSimpleNode(object node)
        {
            if (!WebBackpackStorage.IsBareHandNodeType(node))
                return null;
            Entry found = null;
            foreach (var entry in _entries)
            {
                var prefabNode = WebBackpackStorage.SimpleNodeFromPrefab(entry.Prefab);
                if (!entry.Storable || prefabNode == null || !node.Equals(prefabNode))
                    continue;
                if (found != null && found.Prefab != entry.Prefab)
                {
                    Diagnostic("initial-ambiguous", "simple ingredient initial fill has multiple physical sources; no guess");
                    return null;
                }
                found = entry;
            }
            if (found == null)
                Diagnostic("initial-missing", "simple ingredient initial fill has no exact simple-node physical source");
            return found;
        }

        private void AddChain(GameObject root, string key)
        {
            bool workableSource = root != null && GameApi.WorkableItemType != null && root.GetComponent(GameApi.WorkableItemType) != null;
            var visited = new HashSet<GameObject>();
            int stage = 0;
            while (root != null && visited.Add(root))
            {
                string stableKey = key + "/stage/" + stage.ToString("D3");
                Entry entry;
                if (!_byPrefab.TryGetValue(root, out entry))
                {
                    entry = new Entry();
                    entry.Prefab = root;
                    entry.Key = stableKey;
                    entry.Stage = stage;
                    _byPrefab.Add(root, entry);
                }
                else if (StringComparer.Ordinal.Compare(stableKey, entry.Key) < 0)
                {
                    entry.Key = stableKey;
                    entry.Stage = stage;
                }
                // A native Workable transformation chain is the authority for processed food.
                // Cooked/mixed/assembled recipe products are not admitted by looking at their UID.
                entry.Storable |= WebBackpackStorage.IsStorablePhysicalPrefab(root, workableSource);
                var workable = GameApi.WorkableItemType != null ? root.GetComponent(GameApi.WorkableItemType) : null;
                root = workable != null && GameApi.WorkableNextPrefabField != null
                    ? GameApi.WorkableNextPrefabField.GetValue(workable) as GameObject : null;
                stage++;
            }
            if (root != null)
                _valid = false; // Cyclic transformation data cannot define a finite source closure.
        }

        private static string PathOf(Transform transform)
        {
            string path = "";
            while (transform != null)
            {
                // Runtime UI/managers may add unrelated roots in a different order per peer.
                // Root names identify source trees; sibling indices disambiguate descendants.
                path = (transform.parent != null ? transform.GetSiblingIndex().ToString("D5") + ":" : "")
                    + transform.name + "/" + path;
                transform = transform.parent;
            }
            return path;
        }
    }
}
