using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarchingCubesPlanet.VoxelEngine.Runtime
{
    public static class FileManager
    {
        public static byte[] GetFile(string url)
        {
            string path = ResolvePersistentPath(url);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public static void SaveFile(string url, byte[] binary)
        {
            string path = ResolvePersistentPath(url);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, binary ?? Array.Empty<byte>());
        }

        public static bool DeleteDirectory(string url)
        {
            string path = ResolvePersistentPath(url);
            if (!Directory.Exists(path))
            {
                return false;
            }

            Directory.Delete(path, true);
            return true;
        }

        public static string CombineUrl(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleanedParts = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                {
                    continue;
                }

                cleanedParts.Add(parts[i].Trim().Trim('/', '\\'));
            }

            return string.Join("/", cleanedParts);
        }

        private static string ResolvePersistentPath(string url)
        {
            string root = Path.GetFullPath(Application.persistentDataPath);
            string relativeUrl = (url ?? string.Empty).Trim().Trim('/', '\\');
            string relativePath = relativeUrl.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string combinedPath = string.IsNullOrEmpty(relativePath)
                ? root
                : Path.Combine(root, relativePath);
            string fullPath = Path.GetFullPath(combinedPath);

            string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Persistent file url escapes Application.persistentDataPath: {url}");
            }

            return fullPath;
        }
    }
}
