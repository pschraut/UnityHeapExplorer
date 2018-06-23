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
            int.TryParse(splits[0], out s_Major);
            int.TryParse(splits[1], out s_Minor);
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