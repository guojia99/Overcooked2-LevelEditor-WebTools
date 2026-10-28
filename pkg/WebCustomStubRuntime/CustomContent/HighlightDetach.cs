using System;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 销毁“挂在交互高亮链上的物体”之前，把它从祖先的
    /// ClientAnticipateInteractionHighlight.m_children 里摘掉。
    ///
    /// 背景：ClientPlate.StartSynchronising 会把自己注册到父级（工作台/台面）的
    /// 高亮列表；原版只在正常拾取/移除路径里注销。运行时被服务端直接销毁的物体
    /// （脏茶杯合并、未装盘内容物丢弃等）走的是 DestroyObject，不会触发注销，
    /// 父级列表里就留下一个“已被销毁”的引用。玩家一靠近该工作台，
    /// ClientAnticipateInteractionHighlight.IncrementHighlight 就会抛
    /// MissingReferenceException，PlayerControls.Update 每帧中断——
    /// 表现为“碰到杯堆的工作台就卡住/高亮失效/交互失灵”。
    ///
    /// 这里用反射调用（不强依赖游戏 Client 类，真机 stub DLL 也能用）。
    /// </summary>
    internal static class HighlightDetach
    {
        private const string HighlightTypeName = "ClientAnticipateInteractionHighlight";
        private const string RemoveChildName = "RemoveChild";

        /// <summary>销毁前调用；内部吞掉所有异常，绝不阻塞销毁流程。</summary>
        internal static void BeforeDestroy(GameObject go)
        {
            try
            {
                RemoveFromAncestors(go);
            }
            catch (Exception)
            {
                // 高亮清理失败不应影响合并/销毁本身
            }
        }

        private static void RemoveFromAncestors(GameObject go)
        {
            if (go == null)
                return;
            var child = FindHighlight(go);
            if (child == null)
                return;
            var parent = go.transform.parent;
            while (parent != null)
            {
                var parentHighlight = FindHighlight(parent.gameObject);
                if (parentHighlight != null && !ReferenceEquals(parentHighlight, child))
                {
                    var mi = parentHighlight.GetType().GetMethod(RemoveChildName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null)
                        mi.Invoke(parentHighlight, new object[] { child });
                    return;
                }
                parent = parent.parent;
            }
        }

        private static Component FindHighlight(GameObject go)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null && comps[i].GetType().Name == HighlightTypeName)
                    return comps[i];
            }
            return null;
        }
    }
}
