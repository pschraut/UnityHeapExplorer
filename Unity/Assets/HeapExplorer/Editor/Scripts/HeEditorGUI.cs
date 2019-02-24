//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public static class HeEditorGUILayout
    {
        static bool s_SplitterActive;
        static int s_SplitterActiveId = -1;
        static Vector2 s_SplitterMousePosition;

        public static Rect GetLargeRect()
        {
            return GUILayoutUtility.GetRect(50, 100000, 50, 100000);
        }

        public static bool LinkButton(GUIContent content, GUIStyle guiStyle = null, params GUILayoutOption[] options)
        {
            var color = GUI.color;
            //GUI.color = new Color(1, 0, 0, 1);
            var result = GUILayout.Button(content, guiStyle == null ? HeEditorStyles.hyperlink : guiStyle, options);
            GUI.color = color;

            if (Event.current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

            return result;
        }

        public static float VerticalSplitter(int id, float value, float min, float max, EditorWindow editorWindow)
        {
            return Splitter(id, ref value, min, max, editorWindow, true);
        }

        public static float HorizontalSplitter(int id, float value, float min, float max, EditorWindow editorWindow)
        {
            return Splitter(id, ref value, min, max, editorWindow, false);
        }

        static float Splitter(int id, ref float value, float min, float max, EditorWindow editorWindow, bool vertical)
        {
            Rect position = new Rect();

            if (vertical)
            {
                position = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));

                var oldColor = GUI.color;
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, GUI.color.a * 0.25f);
                GUI.DrawTexture(position, HeEditorStyles.splitterImage, ScaleMode.StretchToFill);
                GUI.color = oldColor;

                position.y -= 2;
                position.height += 4;
                EditorGUIUtility.AddCursorRect(position, MouseCursor.SplitResizeUpDown);
            }
            else
            {
                position = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandHeight(true));

                var oldColor = GUI.color;
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, GUI.color.a * 0.25f);
                GUI.DrawTexture(position, HeEditorStyles.splitterImage, ScaleMode.StretchToFill);
                GUI.color = oldColor;

                position.x -= 2;
                position.width += 4;
                EditorGUIUtility.AddCursorRect(position, MouseCursor.SplitResizeLeftRight);
            }



            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    if (position.Contains(Event.current.mousePosition))
                    {
                        s_SplitterActive = true;
                        s_SplitterActiveId = id;
                        s_SplitterMousePosition = Event.current.mousePosition;
                    }
                    break;

                case EventType.MouseUp:
                case EventType.MouseLeaveWindow:
                    s_SplitterActive = false;
                    s_SplitterActiveId = -1;
                    editorWindow.Repaint();
                    break;

                case EventType.MouseDrag:
                    if (s_SplitterActive && s_SplitterActiveId == id)
                    {
                        var delta = Event.current.mousePosition - s_SplitterMousePosition;
                        s_SplitterMousePosition = Event.current.mousePosition;

                        if (vertical)
                            value = Mathf.Clamp(value - delta.y / editorWindow.position.height, min, max);
                        else
                            value = Mathf.Clamp(value - delta.x / editorWindow.position.width, min, max);

                        editorWindow.Repaint();
                    }
                    break;
            }

            return value;
        }

        public static bool DelayedSearchField(SearchField searchField, ref string searchString)
        {
            // check keydown before processing the search field, because it swallows them
            var isEnter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            var isESC = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;

            searchString = searchField.OnToolbarGUI(searchString);
            if (!searchField.HasFocus())
                return false;

            return isEnter || isESC;
        }
    }

    public class HeSearchField : SearchField
    {
        EditorWindow m_EditorWindow;
        float m_FinishTime;
        string m_SearchString = "";

        public float delay
        {
            get;
            set;
        }

        public string text
        {
            get;
            private set;
        }

        public HeSearchField(EditorWindow editorWindow)
        {
            autoSetFocusOnFindCommand = false;
            delay = 1.0f;
            text = m_SearchString;
            m_EditorWindow = editorWindow;
        }

        public bool OnToolbarGUI()
        {
            var isEnter = HasFocus() && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            var isESC = HasFocus() && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;

            var newString = OnToolbarGUI(m_SearchString);
            if (newString != m_SearchString)
            {
                m_SearchString = newString;
                m_FinishTime = Time.realtimeSinceStartup + delay;
            }

            if (isEnter || isESC)
                m_FinishTime = 0;

            if (m_FinishTime > Time.realtimeSinceStartup)
            {
                m_EditorWindow.Repaint();
                return false;
            }

            if (m_SearchString != text)
            {
                text = m_SearchString;
                m_EditorWindow.Repaint();
                return true;
            }

            return false;
        }
    }

    public static class HeEditorGUI
    {
        static System.Text.StringBuilder s_StringBuilder = new System.Text.StringBuilder(256);

        static string SignString(long value)
        {
            if (value == 0)
                return "";

            return (value < 0) ? "-" : "+";
        }

        public static void Size(Rect position, long size)
        {
            var text = EditorUtility.FormatBytes(System.Math.Abs(size));
            if (size < 0)
                text = "-" + text;

            GUI.Label(position, text);
        }

        public static void SizeDiff(Rect position, long size)
        {
            GUI.Label(position, SignString(size) + EditorUtility.FormatBytes(System.Math.Abs(size)));
        }

        public static void Count(Rect position, long count)
        {
            GUI.Label(position, count.ToString());
        }

        public static void CountDiff(Rect position, long count)
        {
            GUI.Label(position, SignString(count) + System.Math.Abs(count).ToString());
        }

        public static void Address(Rect position, ulong address)
        {
            var stringAddress = string.Format(StringFormat.Address, address);
            EditorGUI.LabelField(position, stringAddress, EditorStyles.label);

            var e = Event.current;
            if (e.type == EventType.ContextClick && position.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy address"), false, (GenericMenu.MenuFunction2)delegate (object userData)
                {
                    EditorGUIUtility.systemCopyBuffer = userData as string;
                }, stringAddress);
                menu.ShowAsContext();
            }
            //EditorGUI.TextField(position, string.Format(StringFormat.Address, address), EditorStyles.label);
        }

        public static void TypeName(Rect position, string text, string tooltip = "")
        {
            var orgtext = text;

            var trycount = 0;
            while (trycount++ < 5 && EditorStyles.label.CalcSize(new GUIContent(text)).x > position.width)
            {
                text = ShortenTypeName(text);
            }

            var ttip = (trycount > 1) ? orgtext + "\n\n" + tooltip : tooltip;
            var content = new GUIContent(text, ttip.Trim());
            GUI.Label(position, content);
        }

        public static void AssemblyName(Rect position, string text, string tooltip = "")
        {
            TypeName(position, text, tooltip);
        }

        static string ShortenTypeName(string typeName)
        {
            s_StringBuilder.Length = 0;

            for (int n = 0; n < typeName.Length; ++n)
            {
                if (typeName[n] != '.')
                    continue;

                // Skip the ... sequence
                if (typeName[n] == '.' && n + 1 < typeName.Length && typeName[n + 1] == '.')
                {
                    while (typeName[n] == '.' && n + 1 < typeName.Length && typeName[n + 1] == '.')
                        ++n;
                    ++n;
                    continue;
                }

                ++n; // skip the '.'

                // Copy chars over to dest
                while (n < typeName.Length)
                {
                    s_StringBuilder.Append(typeName[n]);
                    ++n;
                }

                s_StringBuilder.Insert(0, "...");
                return s_StringBuilder.ToString();
            }

            // We are here if the string could not be shortened
            s_StringBuilder.Append(typeName);
            return s_StringBuilder.ToString();
        }

        public static bool Link(Rect position, GUIContent content)
        {
            var pressed = GUI.Button(position, content, HeEditorStyles.hyperlink);

            if (Event.current.type == EventType.Repaint)
                EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            return pressed;
        }

        public static bool CsButton(Rect position)
        {
            if (GUI.Button(position, new GUIContent("", HeEditorStyles.csImage, "C# Object"), HeEditorStyles.gotoStyle))
                return true;

            return false;
        }

        public static bool CsStaticButton(Rect position)
        {
            if (GUI.Button(position, new GUIContent("", HeEditorStyles.csStaticImage, "Static C# Field"), HeEditorStyles.gotoStyle))
                return true;

            return false;
        }

        public static bool CppButton(Rect position)
        {
            if (GUI.Button(position, new GUIContent("", HeEditorStyles.cppImage, "C++ Object"), HeEditorStyles.gotoStyle))
                return true;

            return false;
        }

        public static void ManagedTypeIcon(Rect position, PackedManagedType type)
        {
            var isValueType = type.isValueType;
            var icon = isValueType ? HeEditorStyles.csValueTypeImage : HeEditorStyles.csReferenceTypeImage;
            var content = new GUIContent(icon, isValueType ? "ValueType" : "ReferenceType");
            GUI.Box(HeEditorGUI.SpaceL(ref position, position.height), content, HeEditorStyles.iconStyle);
        }

        public static void NativeObjectIcon(Rect position, PackedNativeUnityEngineObject obj)
        {
            var warningMsg = "";
            var showWarning = false;
            if (obj.isDontDestroyOnLoad || obj.isManager || ((obj.hideFlags & HideFlags.DontUnloadUnusedAsset) != 0))
            {
                warningMsg = "\n\nThe object does not unload automatically during scene changes, because of 'isDontDestroyOnLoad' or 'isManager' or 'hideFlags'.";
                showWarning = true;
            }

            if (obj.isPersistent)
            {
                GUI.Box(position, new GUIContent(HeEditorStyles.assetImage, "Object is an asset." + warningMsg), HeEditorStyles.iconStyle);
            }
            else if (obj.instanceId < 0)
            {
                var c = GUI.color;
                GUI.color = new Color(1, 0.75f, 1, c.a);
                GUI.Box(position, new GUIContent(HeEditorStyles.instanceImage, "Object created at runtime." + warningMsg), HeEditorStyles.iconStyle);
                GUI.color = c;
            }
            else
            {
                GUI.Box(position, new GUIContent(HeEditorStyles.sceneImage, "Object is stored in scene." + warningMsg), HeEditorStyles.iconStyle);
            }

            if (showWarning)
            {
                var r = position;
                r.x += 5;
                r.y += 4;
                GUI.Box(r, new GUIContent(HeEditorStyles.warnImage), HeEditorStyles.iconStyle);
            }
        }


        public static bool GCHandleButton(Rect position)
        {
            if (GUI.Button(position, new GUIContent("", HeEditorStyles.gcHandleImage, "GCHandle"), HeEditorStyles.gotoStyle))
                return true;

            return false;
        }

        public static Rect SpaceL(ref Rect position, float pixels)
        {
            var r = position;
            r.width = Mathf.Min(pixels, r.width);
            r.y += 1;
            position.x += r.width;
            position.width -= r.width;
            return r;
        }

        public static Rect SpaceR(ref Rect position, float pixels)
        {
            var r = position;
            r.x = r.xMax;
            r.y += 1;
            r.width = Mathf.Min(pixels, r.width); //pixels;
            r.x -= (r.width + 2);
            position.width -= (r.width + 2);
            return r;
        }
    }
}
