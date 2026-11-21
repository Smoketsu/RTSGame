// rts/src/Game/Assets/Scripts/Bootstrap/ReplayFiles.cs
// Where session replays live, shared by the host that writes them and the replay list that reads them (player_interface_2.md
// UI-15): <repo>/rts/build/replays in the editor (git-ignored build output), <persistentDataPath>/replays in a player. File names are
// local timestamps (yyyyMMdd-HHmmss), so the newest file has the greatest name. File IO for presentation only, never in the
// simulation. Part of Rts.Game.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rts.Game.Bootstrap
{
    /// <summary>The replay folder and its files.</summary>
    public static class ReplayFiles
    {
        /// <summary>File extension of a replay, with the dot.</summary>
        public const string Extension = ".rtsreplay";

        /// <summary>The replay folder (it may not exist yet). Unit: absolute folder path.</summary>
        public static string Folder
        {
            get
            {
#if UNITY_EDITOR
                // rts/build/replays as seen from rts/src/Game/Assets.
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "build", "replays"));
#else
                return Path.Combine(Application.persistentDataPath, "replays");
#endif
            }
        }

        /// <summary>Every replay file in the folder, newest first (descending file name). Empty when the folder does not exist.</summary>
        public static List<string> NewestFirst()
        {
            List<string> files = new List<string>();
            if (Directory.Exists(Folder))
            {
                files.AddRange(Directory.GetFiles(Folder, "*" + Extension));
            }
            files.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));
            return files;
        }

        /// <summary>A new replay path named after the local time. The wall clock only names the file.</summary>
        public static string NewPath()
        {
            Directory.CreateDirectory(Folder);
            return Path.Combine(Folder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + Extension);
        }
    }
}
