using System.Collections;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 世界地图装饰强制展开（CustomStub 版，接替原 LayoutRuntimeWorldMapDressing）。
    ///
    /// 背景：dlc08 map_* 装饰（现迁 commonW1/prefabs/dlc08/art/dlc08_circus/）的
    /// prefab 带 WorldMapSceneryOptimizer：Awake() 把整个可视 Mesh 子树
    /// SetActive(false)，只有世界地图的展开流程会重新激活——关卡场景里展开永不
    /// 触发，表现为编辑器里可见、进 Play/游戏包后整件消失。
    ///
    /// 本组件等 child 实例化（其 Awake 已跑、Mesh 已隐藏）后对每个 optimizer 调
    /// End(FlipDirection.Unfold)：Mesh 重新激活、等价世界地图上展开完成的终态；
    /// 随后关闭 child 下全部 Collider（map 家族自带腰高碰撞盒会在关卡里形成
    /// 看不见的空气墙）。无 optimizer 的 map 件自然空转。
    ///
    /// 数据载体：SpecificPseudoPrefabTag.prefabTag = "WorldMapDressing|"。
    /// </summary>
    public class WorldMapDressing : MonoBehaviour
    {
        /// <summary>tag 载体前缀。</summary>
        public const string TagPrefix = "WorldMapDressing|";

        private static bool s_loggedSelfCheck;

        private IEnumerator Start()
        {
            var inner = RunInner();
            while (true)
            {
                object current;
                bool hasNext;
                try
                {
                    hasNext = inner.MoveNext();
                    current = hasNext ? inner.Current : null;
                }
                catch (System.Exception ex)
                {
                    StubLog.LogWarn("[WorldMapDressing] 协程异常退出: " + name + "\n" + ex);
                    yield break;
                }
                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        private IEnumerator RunInner()
        {
            if (!s_loggedSelfCheck)
            {
                s_loggedSelfCheck = true;
                StubLog.Log("[WorldMapDressing] 反射自检: Optimizer=" + (GameApi.WorldMapOptimizerType != null)
                    + " End=" + (GameApi.OptimizerEndMethod != null) + " Unfold=" + (GameApi.FlipUnfold != null));
            }

            // 等 child 出现（宿主/模组实例化 bundle 装饰）
            float waited = 0f;
            while (transform.childCount == 0)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 20f)
                {
                    StubLog.LogWarn("[WorldMapDressing] 等待 child 超时（20s）: " + name);
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }
            // 再等一拍让 child 的 Awake（隐藏 Mesh）跑完
            yield return new WaitForSeconds(0.5f);

            try
            {
                int unfolded = 0;
                if (GameApi.WorldMapOptimizerType != null && GameApi.OptimizerEndMethod != null
                    && GameApi.FlipUnfold != null)
                {
                    var optimizers = GameApi.GetComponentsInChildren(gameObject, GameApi.WorldMapOptimizerType, true);
                    for (int i = 0; i < optimizers.Length; i++)
                    {
                        var opt = optimizers[i];
                        if (opt == null)
                            continue;
                        var mesh = GameApi.OptimizerMeshProperty != null
                            ? GameApi.OptimizerMeshProperty.GetValue(opt, null) as GameObject
                            : null;
                        if (mesh != null && !mesh.activeSelf)
                        {
                            GameApi.OptimizerEndMethod.Invoke(opt, new[] { GameApi.FlipUnfold });
                            unfolded++;
                        }
                    }
                }

                // 展开后 bundle 自带碰撞盒会生效（空气墙）——关卡内一律关闭
                var colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                        colliders[i].enabled = false;
                }

                StubLog.Log("[WorldMapDressing] 处理完成: " + name + "（展开 " + unfolded
                    + " 个 optimizer，关闭 " + colliders.Length + " 个碰撞体）");
            }
            catch (System.Exception ex)
            {
                StubLog.LogWarn("[WorldMapDressing] 处理失败: " + name + " " + ex.Message);
            }
        }
    }
}
