using Lingjing.View;
using UnityEditor;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 动画无头检查：库是否建全、8 向量化与镜像是否按规格走。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.AnimProbe.Run
    /// </summary>
    public static class AnimProbe
    {
        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(ArtLibraryBuilder.AssetPath);
            if (library == null)
            {
                Debug.Log("[anim] PROBE FAIL: 没有 ArtLibrary 资产");
                EditorApplication.Exit(1);
                return;
            }

            ArtLibrary.EntityClips eous = library.EntityOf("eous");
            ArtLibrary.EntityClips tail = library.EntityOf("taillight");
            ArtLibrary.EntityClips chain = library.EntityOf("chain");

            int eousIdleRows = eous != null ? eous.StateOf("idle").rows.Length : 0;
            int eousWalkFrames = eous != null ? eous.StateOf("walk").rows[0].frames.Length : 0;
            bool tailDeathNoLoop = tail != null && !tail.StateOf("death").loop;
            bool chainExists = chain != null;

            string east = ActorAnimator.DirectionName(1, 0);
            string southEast = ActorAnimator.DirectionName(1, -1);
            string west = ActorAnimator.DirectionName(-1, 0);

            string mirrorWest = eous != null ? eous.MirrorOf("w") : null;

            Debug.Log($"[anim] entities={library.entities.Length} eousIdleRows={eousIdleRows} eousWalkFrames={eousWalkFrames} tailDeathLoop={tailDeathNoLoop} chain={chainExists}");
            Debug.Log($"[anim] dir(e)={east} dir(se)={southEast} dir(w)={west} mirror(w)->{mirrorWest}");

            bool pass = eousIdleRows == 5
                        && eousWalkFrames == 4
                        && tailDeathNoLoop
                        && chainExists
                        && east == "e"
                        && southEast == "se"
                        && west == "w"
                        && mirrorWest == "e";

            Debug.Log(pass ? "[anim] PROBE PASS" : "[anim] PROBE FAIL");
            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
