using System.Collections.Generic;
using System.Reflection;
using Lingjing.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 伤害数字静态池的跨场景回归。
    /// 关键前提：编辑模式下 DOTween 不推进、OnComplete 不触发，正常流程走不到 Despawn，静态 Pool 会一直为空；
    /// 所以这里用反射调用私有 Despawn()，先把实例真正放回 Pool，让切场景时池里确实残留已销毁引用——
    /// 这才是修复前会抛 MissingReferenceException 的真实条件。随后再 Spawn，验证「池非空 + 跨场景」下能重建。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.DamagePoolProbe.Run
    /// </summary>
    public static class DamagePoolProbe
    {
        public static void Run()
        {
            FieldInfo poolField = typeof(DamageNumber).GetField("Pool", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo despawnMethod = typeof(DamageNumber).GetMethod("Despawn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (poolField == null || despawnMethod == null)
            {
                Fail("反射找不到 DamageNumber.Pool 或 Despawn，探针与实现不同步");
                return;
            }

            var pool = poolField.GetValue(null) as Stack<DamageNumber>;
            if (pool == null)
            {
                Fail("DamageNumber.Pool 不是 Stack<DamageNumber>，探针与实现不同步");
                return;
            }

            // 第一步：Spawn 一个实例，再反射调它的私有 Despawn() 放回池。
            // 不制造「Pool 非空」，下面切场景后的失效引用处理与重建逻辑根本不会被执行（假覆盖）。
            if (!TrySpawn(10))
            {
                return;
            }

            GameObject firstRoot = GameObject.Find("DamageNumbers");
            GameObject firstInstance = FindActiveInstance(firstRoot, "10");
            if (firstInstance == null)
            {
                Fail("首次 Spawn 后找不到 active 的伤害数字实例（根节点或文本 10）");
                return;
            }

            DamageNumber firstNumber = firstInstance.GetComponent<DamageNumber>();
            if (firstNumber == null)
            {
                Fail("首次 Spawn 的实例上没有 DamageNumber 组件");
                return;
            }

            try
            {
                despawnMethod.Invoke(firstNumber, null);
            }
            catch (TargetInvocationException e)
            {
                Fail($"反射调用 Despawn 抛异常：{e.InnerException?.GetType().Name} {e.InnerException?.Message}");
                return;
            }

            int poolBefore = pool.Count;
            if (poolBefore != 1)
            {
                Fail($"Despawn 后 Pool.Count == {poolBefore}，期望 1（池非空前提没建立）");
                return;
            }

            Debug.Log($"[dmgpool] 前提建立：Pool.Count={poolBefore}（实例已回池），准备切场景");

            // 第二步：切空场景。根节点与池里的实例一起被销毁，但静态 Pool 不会被场景销毁自动清空。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (GameObject.Find("DamageNumbers") != null)
            {
                Fail("切场景后旧 DamageNumbers 根节点仍在，没能复现静态池失效条件");
                return;
            }

            if (pool.Count != poolBefore)
            {
                Fail($"切场景后 Pool.Count == {pool.Count}，期望 {poolBefore}（静态池不会被场景销毁自动清空）");
                return;
            }

            if (pool.Peek() != null)
            {
                Fail("切场景后 Pool 里的引用仍是活的，没能复现 destroyed 引用");
                return;
            }

            Debug.Log($"[dmgpool] 条件复现：Pool.Count={pool.Count}，栈顶为 Unity 假 null（destroyed）");

            // 第三步：再次 Spawn。修复前这里 Pool.Pop() 会拿到 destroyed 对象，在 number.transform 上抛异常。
            if (!TrySpawn(20))
            {
                return;
            }

            GameObject newRoot = GameObject.Find("DamageNumbers");
            if (newRoot == null)
            {
                Fail("跨场景后 Spawn 没有重建 DamageNumbers 根节点");
                return;
            }

            GameObject newInstance = FindActiveInstance(newRoot, "20");
            if (newInstance == null)
            {
                Fail("跨场景后 Spawn 重建的实例不是 active 或文本不是 20");
                return;
            }

            int poolAfter = pool.Count;
            if (poolAfter > 1 || (poolAfter == 1 && pool.Peek() == null))
            {
                Fail($"跨场景 Spawn 后 Pool.Count == {poolAfter}，失效引用没有被清掉");
                return;
            }

            Debug.Log($"[dmgpool] poolBefore={poolBefore} poolAfter={poolAfter}");

            // 附加覆盖：根节点仍存活、池里某个对象被单独销毁时，Spawn 的 Pool.Pop() 假 null 判空分支。
            // （上面的场景切换路径会被根节点重建时的整池清理短路，这条补上 Pop 判空本身。）
            DamageNumber second = newInstance.GetComponent<DamageNumber>();
            if (second == null)
            {
                Fail("跨场景后 Spawn 的实例上没有 DamageNumber 组件");
                return;
            }

            try
            {
                despawnMethod.Invoke(second, null);
            }
            catch (TargetInvocationException e)
            {
                Fail($"第二次反射调用 Despawn 抛异常：{e.InnerException?.GetType().Name} {e.InnerException?.Message}");
                return;
            }

            if (pool.Count != 1)
            {
                Fail($"第二次 Despawn 后 Pool.Count == {pool.Count}，期望 1");
                return;
            }

            // 只销毁池里那个对象、保留根节点：下一次 Spawn 会走 Pool.Pop() → 假 null → Create 分支。
            Object.DestroyImmediate(second.gameObject);
            if (pool.Peek() != null)
            {
                Fail("单独销毁池内对象后，池里没有留下 destroyed 引用，Pop 判空分支的前提没建立");
                return;
            }

            if (!TrySpawn(40))
            {
                return;
            }

            GameObject popRoot = GameObject.Find("DamageNumbers");
            if (FindActiveInstance(popRoot, "40") == null)
            {
                Fail("根节点存活时 Pop 拿到 destroyed 对象后没有重建出可用实例（文本 40）");
                return;
            }

            int poolAfterPop = pool.Count;
            if (poolAfterPop > 1 || (poolAfterPop == 1 && pool.Peek() == null))
            {
                Fail($"Pop 判空后 Pool.Count == {poolAfterPop}，失效引用没有被清掉");
                return;
            }

            Debug.Log($"[dmgpool] Pop 判空覆盖：poolBefore=1 poolAfter={poolAfterPop}");

            Debug.Log($"[dmgpool] PROBE PASS: 池非空条件下跨场景重建成功（poolBefore={poolBefore}, poolAfter={poolAfter}）");
        }

        /// <summary>Spawn 并统一接住异常：三类异常都算 FAIL。</summary>
        static bool TrySpawn(double amount)
        {
            try
            {
                DamageNumber.Spawn(Vector3.zero, amount);
                return true;
            }
            catch (MissingReferenceException e)
            {
                Fail($"Spawn({amount}) 抛 MissingReferenceException：{e.Message}");
                return false;
            }
            catch (System.NullReferenceException e)
            {
                Fail($"Spawn({amount}) 抛 NullReferenceException：{e.Message}");
                return false;
            }
            catch (System.Exception e)
            {
                Fail($"Spawn({amount}) 抛 {e.GetType().Name}：{e.Message}");
                return false;
            }
        }

        /// <summary>在根节点下找 active、文本匹配的伤害数字实例；找不到返回 null。</summary>
        static GameObject FindActiveInstance(GameObject root, string expectedText)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.transform.childCount; i++)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                if (!child.activeSelf)
                {
                    continue;
                }

                TextMesh mesh = child.GetComponent<TextMesh>();
                if (mesh != null && mesh.text == expectedText)
                {
                    return child;
                }
            }

            return null;
        }

        static void Fail(string reason)
        {
            Debug.LogError($"[dmgpool] FAIL: {reason}");
            EditorApplication.Exit(1);
        }
    }
}
