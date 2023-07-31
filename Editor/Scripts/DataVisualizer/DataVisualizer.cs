//
// Heap Explorer for Unity. Copyright (c) 2019-2022 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System;
using System.Collections.Generic;
using System.Globalization;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    public abstract class AbstractDataVisualizer
    {
        public bool isFallback
        {
            get;
            protected set;
        }

        public string title
        {
            get
            {
                return m_Title;
            }
        }

        public bool hasMenu
        {
            get
            {
                return m_Menu != null;
            }
        }

        protected PackedMemorySnapshot m_Snapshot;
        protected UInt64 m_Address;
        protected PackedManagedType m_Type;
        protected AbstractMemoryReader m_MemoryReader;
        protected string m_Title = "Visualizer";
        protected GenericMenu m_Menu;

        static readonly Dictionary<string, Type> s_Visualizers = new Dictionary<string, Type>();

        public void Initialize(PackedMemorySnapshot snapshot, AbstractMemoryReader memoryReader, UInt64 address, PackedManagedType type)
        {
            m_Snapshot = snapshot;
            m_Address = address;
            m_Type = type;
            m_MemoryReader = memoryReader;
            m_Title = $"{type.name} Visualizer";

            OnInitialize();
        }

        public void GUI()
        {
            OnGUI();
        }

        public void ShowMenu()
        {
            m_Menu.ShowAsContext();
        }

        protected abstract void OnInitialize();
        protected abstract void OnGUI();

        public static void RegisterVisualizer(string typeName, Type visualizerType)
        {
            s_Visualizers[typeName] = visualizerType;
        }

        public static bool HasVisualizer(string typeName)
        {
            var value = s_Visualizers.ContainsKey(typeName);
            return value;
        }

        public static AbstractDataVisualizer CreateVisualizer(string typeName)
        {
            if (!s_Visualizers.TryGetValue(typeName, out var type))
                type = typeof(FallbackDataVisualizer);

            var value = Activator.CreateInstance(type) as AbstractDataVisualizer;
            return value;
        }

        class FallbackDataVisualizer : AbstractDataVisualizer
        {
            protected override void OnInitialize()
            {
                isFallback = true;
            }

            protected override void OnGUI()
            {
            }
        }
    }

    class ColorDataVisualizer : AbstractDataVisualizer
    {
        Option<Color> m_Color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color", typeof(ColorDataVisualizer));
        }

        protected override void OnInitialize() {
            m_Color = m_MemoryReader.ReadColor(m_Address);
        }

        protected override void OnGUI()
        {
            if (m_Color.valueOut(out var color)) EditorGUILayout.ColorField(color);
            else EditorGUILayout.LabelField($"Couldn't read `Color` at {m_Address:X}");
        }
    }

    class Color32DataVisualizer : AbstractDataVisualizer
    {
        Option<Color32> m_Color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color32", typeof(Color32DataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_Color = m_MemoryReader.ReadColor32(m_Address);
        }

        protected override void OnGUI()
        {
            if (m_Color.valueOut(out var color)) EditorGUILayout.ColorField(color);
            else EditorGUILayout.LabelField($"Couldn't read `Color32` at {m_Address:X}");
        }
    }

    class Matrix4x4DataVisualizer : AbstractDataVisualizer
    {
        Option<Matrix4x4> m_Matrix;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Matrix4x4", typeof(Matrix4x4DataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_Matrix = m_MemoryReader.ReadMatrix4x4(m_Address);
        }

        protected override void OnGUI()
        {
            if (m_Matrix.valueOut(out var matrix)) {
                using (new EditorGUILayout.VerticalScope())
                {
                    for (var y = 0; y < 4; ++y)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (var x = 0; x < 4; ++x)
                            {
                                EditorGUILayout.TextField(matrix[y, x].ToString(CultureInfo.InvariantCulture));
                            }
                        }
                    }
                }
            }
            else EditorGUILayout.LabelField($"Couldn't read `Matrix4x4` at {m_Address:X}");
        }
    }

    class QuaternionDataVisualizer : AbstractDataVisualizer
    {
        Option<Quaternion> m_Quaternion;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Quaternion", typeof(QuaternionDataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_Quaternion = m_MemoryReader.ReadQuaternion(m_Address);
        }

        protected override void OnGUI()
        {
            if (m_Quaternion.valueOut(out var quaternion)) {
                var eulerAngles = quaternion.eulerAngles;
                EditorGUILayout.Vector3Field("Euler Angles", eulerAngles);
                //EditorGUILayout.Vector4Field("Quaternion", new Vector4(m_quaternion.x, m_quaternion.y, m_quaternion.z, m_quaternion.w));
            }
            else EditorGUILayout.LabelField($"Couldn't read `Quaternion` at {m_Address:X}");
        }
    }

    class StringDataVisualizer : AbstractDataVisualizer
    {
        string m_String;
        string m_ShortString;
        const int k_MaxStringLength = 1024 * 4;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("System.String", typeof(StringDataVisualizer));
        }

        protected override void OnInitialize()
        {
            var pointer = m_Address;
            if (pointer == 0)
                m_String = "null";
            else {
                m_String = 
                    m_MemoryReader
                        .ReadString(pointer + m_Snapshot.virtualMachineInformation.objectHeaderSize)
                        .getOrElse("<Error while reading>");
            }

            if (m_String == null)
                m_String = "<null>";

            if (m_String.Length > k_MaxStringLength)
                m_ShortString = m_String.Substring(0, k_MaxStringLength);

            m_Menu = new GenericMenu();
            m_Menu.AddItem(new GUIContent("Open in Text Editor"), false, OnMenuOpenInTextEditor);
        }

        protected override void OnGUI()
        {
            var text = m_String;

            if (m_ShortString != null)
            {
                text = m_ShortString;
                EditorGUILayout.HelpBox($"Displaying {k_MaxStringLength} chars only!", MessageType.Info);
            }

            EditorGUILayout.TextArea(text, EditorStyles.wordWrappedLabel);
        }

        void OnMenuOpenInTextEditor()
        {
            var path = FileUtil.GetUniqueTempPathInProject() + ".txt";
            System.IO.File.WriteAllText(path, m_String);
            EditorUtility.OpenWithDefaultApp(path);
        }
    }

    class DateTimeDataVisualizer : AbstractDataVisualizer
    {
        Option<DateTime> m_DateTime;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("System.DateTime", typeof(DateTimeDataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_DateTime = m_MemoryReader.ReadInt64(m_Address).map(ticks => new DateTime(ticks));
        }

        protected override void OnGUI()
        {
            EditorGUILayout.LabelField(m_DateTime.fold(
                "<error while reading>", _ => _.ToString(DateTimeFormatInfo.InvariantInfo)
            ));
        }
    }

    public sealed class DataVisualizerWindow : EditorWindow
    {
        AbstractDataVisualizer m_Visualizer;
        Vector2 m_ScrollPosition;

        public static EditorWindow CreateWindow(PackedMemorySnapshot snapshot, AbstractMemoryReader memoryReader, UInt64 address, PackedManagedType type)
        {
            var visualizer = AbstractDataVisualizer.CreateVisualizer(type.name);
            if (visualizer == null)
            {
                Debug.LogWarningFormat("Could not create DataVisualizer for type '{0}'", type.name);
                return null;
            }
            visualizer.Initialize(snapshot, memoryReader, address, type);

            var window = CreateInstance<DataVisualizerWindow>();
            window.SetVisualizer(visualizer);
            window.ShowUtility();
            return window;
        }

        void SetVisualizer(AbstractDataVisualizer dataVisualizer)
        {
            m_Visualizer = dataVisualizer;
            if (m_Visualizer == null)
                return;

            titleContent = new GUIContent(m_Visualizer.title);
        }

        void OnGUI()
        {
            if (m_Visualizer == null)
            {
                EditorGUILayout.HelpBox("DataVisualizer is null.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;

                    GUILayout.Space(2);

                    m_Visualizer.GUI();

                    GUILayout.Space(2);
                }
            }
        }
    }
}
