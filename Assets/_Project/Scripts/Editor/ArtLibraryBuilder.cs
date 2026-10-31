using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Lingjing.View;
using UnityEditor;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 把 Assets/_Project/Art 下的逐帧 PNG 收成一张 ArtLibrary 资产。
    /// 索引来源是各实体的 manifest.json（fps / loop / 朝向顺序 / 镜像），
    /// 文件名提供实体、状态、朝向、帧号，二者不重复维护。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.ArtLibraryBuilder.Run
    /// </summary>
    public static class ArtLibraryBuilder
    {
        public const string AssetPath = "Assets/_Project/Art/ArtLibrary.asset";
        static readonly string[] Groups = { "Characters", "Enemies", "Bosses" };

        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            var entities = new List<ArtLibrary.EntityClips>();

            foreach (string group in Groups)
            {
                string root = $"Assets/_Project/Art/{group}";
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }

                foreach (string entityDir in AssetDatabase.GetSubFolders(root))
                {
                    string entityName = Path.GetFileName(entityDir);
                    ArtLibrary.EntityClips clips = BuildEntity(entityDir, entityName, group == "Bosses");
                    if (clips != null)
                    {
                        entities.Add(clips);
                    }
                }
            }

            library.entities = entities.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            int states = 0;
            int frames = 0;
            foreach (ArtLibrary.EntityClips e in library.entities)
            {
                states += e.states.Length;
                foreach (ArtLibrary.StateClips s in e.states)
                {
                    foreach (ArtLibrary.Clip c in s.rows)
                    {
                        frames += c.frames.Length;
                    }
                }
            }

            Debug.Log($"[art] library built: entities={library.entities.Length} states={states} frames={frames} -> {AssetPath}");
        }

        static ArtLibrary.EntityClips BuildEntity(string dir, string entityName, bool isBoss)
        {
            string manifestPath = $"{dir}/manifest.json";
            var stateMeta = new Dictionary<string, (float fps, bool loop)>();
            var dirOrder = new List<string>();
            var mirrorFrom = new List<string>();
            var mirrorTo = new List<string>();

            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);

                Match dirMatch = Regex.Match(json, "\"directions\"\\s*:\\s*\\{([^}]*)\\}");
                if (dirMatch.Success)
                {
                    var pairs = new List<(string name, int index)>();
                    foreach (Match m in Regex.Matches(dirMatch.Groups[1].Value, "\"([a-z]+)\"\\s*:\\s*(\\d+)"))
                    {
                        pairs.Add((m.Groups[1].Value, int.Parse(m.Groups[2].Value)));
                    }

                    pairs.Sort((a, b) => a.index.CompareTo(b.index));
                    foreach ((string name, int _) in pairs)
                    {
                        dirOrder.Add(name);
                    }
                }

                Match mirrorMatch = Regex.Match(json, "\"mirror\"\\s*:\\s*\\{([^}]*)\\}");
                if (mirrorMatch.Success)
                {
                    foreach (Match m in Regex.Matches(mirrorMatch.Groups[1].Value, "\"([a-z]+)\"\\s*:\\s*\"([a-z]+)\""))
                    {
                        mirrorFrom.Add(m.Groups[1].Value);
                        mirrorTo.Add(m.Groups[2].Value);
                    }
                }

                foreach (Match m in Regex.Matches(json,
                    "\"(\\w+)\"\\s*:\\s*\\{\\s*\"frames\"\\s*:\\s*(\\d+)\\s*,\\s*\"dirs\"\\s*:\\s*(\\d+)\\s*,\\s*\"fps\"\\s*:\\s*(\\d+)\\s*,\\s*\"loop\"\\s*:\\s*(true|false)"))
                {
                    stateMeta[m.Groups[1].Value] = (float.Parse(m.Groups[4].Value), m.Groups[5].Value == "true");
                }
            }

            var states = new List<ArtLibrary.StateClips>();
            foreach (string stateDir in AssetDatabase.GetSubFolders(dir))
            {
                string stateName = Path.GetFileName(stateDir);
                var byDir = new Dictionary<string, List<(int frame, string path)>>();

                foreach (string file in Directory.GetFiles(stateDir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    // {entity}_{state}_{dir}_{frame:00}
                    string[] parts = name.Split('_');
                    if (parts.Length < 4)
                    {
                        continue;
                    }

                    string dirName = parts[parts.Length - 2];
                    if (!int.TryParse(parts[parts.Length - 1], out int frame))
                    {
                        continue;
                    }

                    if (!byDir.TryGetValue(dirName, out var list))
                    {
                        list = new List<(int, string)>();
                        byDir[dirName] = list;
                    }

                    list.Add((frame, file.Replace('\\', '/')));
                }

                if (byDir.Count == 0)
                {
                    continue;
                }

                var names = new List<string>(byDir.Keys);
                // 有 manifest 时按它声明的顺序，否则按字母序保证稳定。
                names.Sort((a, b) =>
                {
                    int ia = dirOrder.IndexOf(a);
                    int ib = dirOrder.IndexOf(b);
                    if (ia < 0) ia = 99;
                    if (ib < 0) ib = 99;
                    return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a, b);
                });

                var rows = new List<ArtLibrary.Clip>();
                foreach (string dirName in names)
                {
                    var frames = byDir[dirName];
                    frames.Sort((a, b) => a.frame.CompareTo(b.frame));

                    var clip = new ArtLibrary.Clip { frames = new Sprite[frames.Count] };
                    for (int i = 0; i < frames.Count; i++)
                    {
                        clip.frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(frames[i].path);
                    }

                    rows.Add(clip);
                }

                (float fps, bool loop) meta = stateMeta.TryGetValue(stateName, out var found)
                    ? found
                    : (isBoss ? 10f : 12f, stateName != "death");

                states.Add(new ArtLibrary.StateClips
                {
                    state = stateName,
                    fps = meta.fps,
                    loop = meta.loop,
                    dirNames = names.ToArray(),
                    rows = rows.ToArray()
                });
            }

            if (states.Count == 0)
            {
                return null;
            }

            return new ArtLibrary.EntityClips
            {
                entity = entityName,
                states = states.ToArray(),
                mirrorFrom = mirrorFrom.ToArray(),
                mirrorTo = mirrorTo.ToArray()
            };
        }
    }
}
