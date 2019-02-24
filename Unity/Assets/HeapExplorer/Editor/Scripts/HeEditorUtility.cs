//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using System.Reflection;

namespace HeapExplorer
{
    public static class HeEditorUtility
    {
        static int s_Major;
        static int s_Minor;

        static HeEditorUtility()
        {
            var splits = Application.version.Split(new[] { '.' });
            if (splits.Length >= 1) int.TryParse(splits[0], out s_Major);
            if (splits.Length >= 2) int.TryParse(splits[1], out s_Minor);

            if (splits.Length < 2)
                Debug.LogErrorFormat("HeapExplorer was unable to parse the editor version '{0}'. Could you please post this message in the Heap Explorer forum thread, so I can look at the issue: {1}", Application.version, "https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/");
        }

        /// <summary>
        /// Gets whether the Unity editor is the specified version or newer.
        /// </summary>
        public static bool IsVersionOrNewer(int major, int minor)
        {
            if (s_Major < major) return false;
            if (s_Minor < minor) return false;
            return true;
        }

        /// <summary>
        /// Opens the Unity Profiler window.
        /// </summary>
        public static void OpenProfiler()
        {
            if (IsVersionOrNewer(2018, 2))
                EditorApplication.ExecuteMenuItem("Window/Debug/Profiler");
            else
                EditorApplication.ExecuteMenuItem("Window/Profiler");
        }

        /// <summary>
        /// Search the project window using the specified filter.
        /// </summary>
        public static void SearchProjectBrowser(string filter)
        {
            foreach (var type in typeof(EditorWindow).Assembly.GetTypes())
            {
                if (type.Name != "ProjectBrowser")
                    continue;

                var window = EditorWindow.GetWindow(type);
                if (window != null)
                {
                    var method = type.GetMethod("SetSearch", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                    if (method != null)
                        method.Invoke(window, new System.Object[] { filter });
                }

                return;
            }
        }
    }
}