using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace CustomStub
{
    /// <summary>
    /// 在背包子物体上注册 Server/Client PlacementContainer 的 Allow 回调。
    /// </summary>
    public class WebBackpackPlacementBridge : MonoBehaviour
    {
        private WebBackpackStorage _storage;
        private readonly List<Registration> _registrations = new List<Registration>();

        private struct Registration
        {
            public object Target;
            public Delegate Callback;
            public MethodInfo Unregister;
        }

        public static void EnsureOn(GameObject backpackGo, WebBackpackStorage storage)
        {
            if (backpackGo == null || storage == null)
                return;
            var existing = backpackGo.GetComponent<WebBackpackPlacementBridge>();
            if (existing != null)
            {
                existing._storage = storage;
                return;
            }
            var c = backpackGo.AddComponent<WebBackpackPlacementBridge>();
            c._storage = storage;
        }

        private void Update()
        {
            if (_registrations.Count == 0)
                TryRegisterAll();
        }

        private void OnDestroy()
        {
            UnregisterAll();
        }

        private void TryRegisterAll()
        {
            if (_storage == null)
                return;
            TryRegisterOn(GameApi.ServerPlacementContainerType);
            TryRegisterOn(GameApi.Find("ClientPlacementContainer"));
        }

        private void TryRegisterOn(Type syncPlacementType)
        {
            if (syncPlacementType == null)
                return;
            for (int i = 0; i < _registrations.Count; i++)
            {
                if (_registrations[i].Target != null
                    && _registrations[i].Target.GetType() == syncPlacementType)
                    return;
            }

            var comp = GetComponent(syncPlacementType);
            if (comp == null)
                return;

            var register = syncPlacementType.GetMethod("RegisterAllowItemPlacement",
                BindingFlags.Public | BindingFlags.Instance);
            var unregister = syncPlacementType.GetMethod("UnregisterAllowItemPlacement",
                BindingFlags.Public | BindingFlags.Instance);
            if (register == null || unregister == null)
                return;

            var callbackType = register.GetParameters()[0].ParameterType;
            var contextType = GameApi.Find("PlacementContext");
            if (contextType == null)
                return;

            var storageField = typeof(WebBackpackPlacementBridge).GetField("_storage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var canAccept = typeof(WebBackpackStorage).GetMethod("CanAcceptPlacementItem",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (storageField == null || canAccept == null)
                return;

            var self = Expression.Constant(this);
            var itemParam = Expression.Parameter(typeof(GameObject), "item");
            var ctxParam = Expression.Parameter(contextType, "context");
            var storageExpr = Expression.Field(self, storageField);
            var call = Expression.Call(storageExpr, canAccept, itemParam);
            var lambda = Expression.Lambda(callbackType, call, itemParam, ctxParam);
            var callback = lambda.Compile();
            register.Invoke(comp, new object[] { callback });
            _registrations.Add(new Registration
            {
                Target = comp,
                Callback = callback,
                Unregister = unregister
            });
        }

        private void UnregisterAll()
        {
            for (int i = 0; i < _registrations.Count; i++)
            {
                var r = _registrations[i];
                if (r.Target == null || r.Callback == null || r.Unregister == null)
                    continue;
                try
                {
                    r.Unregister.Invoke(r.Target, new object[] { r.Callback });
                }
                catch
                {
                    // ignored
                }
            }
            _registrations.Clear();
        }
    }
}
