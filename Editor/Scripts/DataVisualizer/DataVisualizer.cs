//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    abstract public class AbstractDataVisualizer
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
        protected System.UInt64 m_Address;
        protected PackedManagedType m_Type;
        protected AbstractMemoryReader m_MemoryReader;
        protected string m_Title = "Visualizer";
        protected GenericMenu m_Menu;

        static Dictionary<string, System.Type> s_Visualizers = new Dictionary<string, System.Type>();
        
        public void Initialize(PackedMemorySnapshot snapshot, AbstractMemoryReader memoryReader, System.UInt64 address, PackedManagedType type)
        {
            m_Snapshot = snapshot;
            m_Address = address;
            m_Type = type;
            m_MemoryReader = memoryReader;
            m_Title = string.Format("{0} Visualizer", type.name);

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

        abstract protected void OnInitialize();
        abstract protected void OnGUI();

        public static void RegisterVisualizer(string typeName, System.Type visualizerType)
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
            System.Type type;
            if (!s_Visualizers.TryGetValue(typeName, out type))
                type = typeof(FallbackDataVisualizer);

            var value = System.Activator.CreateInstance(type) as AbstractDataVisualizer;
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
        Color m_Color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color", typeof(ColorDataVisualizer));
        }

        protected override void OnInitialize()
        {
            int sizeOfSingle = m_Snapshot.managedTypes[m_Snapshot.coreTypes.systemSingle].size;

            m_Color.r = m_MemoryReader.ReadSingle(m_Address + (uint)(sizeOfSingle * 0));
            m_Color.g = m_MemoryReader.ReadSingle(m_Address + (uint)(sizeOfSingle * 1));
            m_Color.b = m_MemoryReader.ReadSingle(m_Address + (uint)(sizeOfSingle * 2));
            m_Color.a = m_MemoryReader.ReadSingle(m_Address + (uint)(sizeOfSingle * 3));
        }

        protected override void OnGUI()
        {
            EditorGUILayout.ColorField(m_Color);
        }
    }

    class Color32DataVisualizer : AbstractDataVisualizer
    {
        Color32 m_Color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color32", typeof(Color32DataVisualizer));
        }

        protected override void OnInitialize()
        {
            int sizeOfByte = m_Snapshot.managedTypes[m_Snapshot.coreTypes.systemByte].size;

            m_Color.r = m_MemoryReader.ReadByte(m_Address + (uint)(sizeOfByte * 0));
            m_Color.g = m_MemoryReader.ReadByte(m_Address + (uint)(sizeOfByte * 1));
            m_Color.b = m_MemoryReader.ReadByte(m_Address + (uint)(sizeOfByte * 2));
            m_Color.a = m_MemoryReader.ReadByte(m_Address + (uint)(sizeOfByte * 3));
        }

        protected override void OnGUI()
        {
            EditorGUILayout.ColorField(m_Color);
        }
    }

    class Matrix4x4DataVisualizer : AbstractDataVisualizer
    {
        Matrix4x4 m_Matrix;

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
            using (new EditorGUILayout.VerticalScope())
            {
                for (var y = 0; y < 4; ++y)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (var x = 0; x < 4; ++x)
                        {
                            EditorGUILayout.TextField(m_Matrix[y, x].ToString());
                        }
                    }
                }
            }
        }
    }

    class QuaternionDataVisualizer : AbstractDataVisualizer
    {
        Quaternion m_Quaternion;

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
            var eulerAngles = m_Quaternion.eulerAngles;
            EditorGUILayout.Vector3Field("Euler Angles", eulerAngles);
            //EditorGUILayout.Vector4Field("Quaternion", new Vector4(m_quaternion.x, m_quaternion.y, m_quaternion.z, m_quaternion.w));
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
            else
                m_String = m_MemoryReader.ReadString(pointer + (ulong)m_Snapshot.virtualMachineInformation.objectHeaderSize);

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
                EditorGUILayout.HelpBox(string.Format("Displaying {0} chars only!", k_MaxStringLength), MessageType.Info);
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

    sealed public class DataVisualizerWindow : EditorWindow
    {
        AbstractDataVisualizer m_Visualizer;
        Vector2 m_ScrollPosition;

        public static EditorWindow CreateWindow(PackedMemorySnapshot snapshot, AbstractMemoryReader memoryReader, System.UInt64 address, PackedManagedType type)
        {
            var visualizer = AbstractDataVisualizer.CreateVisualizer(type.name);
            if (visualizer == null)
            {
                Debug.LogWarningFormat("Could not create DataVisualizer for type '{0}'", type.name);
                return null;
            }
            visualizer.Initialize(snapshot, memoryReader, address, type);

            var window = DataVisualizerWindow.CreateInstance<DataVisualizerWindow>();
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

        void OnEnable()
        {
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
