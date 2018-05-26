using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    abstract public class AbstractDataVisualizer
    {
        static Dictionary<string, System.Type> s_visualizers = new Dictionary<string, System.Type>();

        public static void RegisterVisualizer(string typeName, System.Type visualizerType)
        {
            s_visualizers[typeName] = visualizerType;
        }

        public static bool HasVisualizer(string typeName)
        {
            var value = s_visualizers.ContainsKey(typeName);
            return value;
        }

        public static AbstractDataVisualizer CreateVisualizer(string typeName)
        {
            System.Type type;
            if (!s_visualizers.TryGetValue(typeName, out type))
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

        protected PackedMemorySnapshot m_snapshot;
        protected System.UInt64 m_address;
        protected PackedManagedType m_type;
        protected AbstractMemoryReader m_memoryReader;
        protected string m_title = "Visualizer";
        protected GenericMenu m_menu;

        public bool isFallback
        {
            get;
            protected set;
        }

        public string title
        {
            get
            {
                return m_title;
            }
        }

        public bool hasMenu
        {
            get
            {
                return m_menu != null;
            }
        }

        public void Initialize(PackedMemorySnapshot snapshot, AbstractMemoryReader memoryReader, System.UInt64 address, PackedManagedType type)
        {
            m_snapshot = snapshot;
            m_address = address;
            m_type = type;
            m_memoryReader = memoryReader;
            m_title = string.Format("{0} Visualizer", type.name);

            OnInitialize();
        }

        public void GUI()
        {
            OnGUI();
        }

        public void ShowMenu()
        {
            m_menu.ShowAsContext();
        }

        abstract protected void OnInitialize();
        abstract protected void OnGUI();
    }

    public class ColorDataVisualizer : AbstractDataVisualizer
    {
        Color m_color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color", typeof(ColorDataVisualizer));
        }

        protected override void OnInitialize()
        {
            int sizeOfSingle = m_snapshot.managedTypes[m_snapshot.coreTypes.systemSingle].size;

            m_color.r = m_memoryReader.ReadSingle(m_address + (uint)(sizeOfSingle * 0));
            m_color.g = m_memoryReader.ReadSingle(m_address + (uint)(sizeOfSingle * 1));
            m_color.b = m_memoryReader.ReadSingle(m_address + (uint)(sizeOfSingle * 2));
            m_color.a = m_memoryReader.ReadSingle(m_address + (uint)(sizeOfSingle * 3));
        }

        protected override void OnGUI()
        {
            EditorGUILayout.ColorField(m_color);
        }
    }

    public class Color32DataVisualizer : AbstractDataVisualizer
    {
        Color32 m_color;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Color32", typeof(Color32DataVisualizer));
        }

        protected override void OnInitialize()
        {
            int sizeOfByte = m_snapshot.managedTypes[m_snapshot.coreTypes.systemByte].size;

            m_color.r = m_memoryReader.ReadByte(m_address + (uint)(sizeOfByte * 0));
            m_color.g = m_memoryReader.ReadByte(m_address + (uint)(sizeOfByte * 1));
            m_color.b = m_memoryReader.ReadByte(m_address + (uint)(sizeOfByte * 2));
            m_color.a = m_memoryReader.ReadByte(m_address + (uint)(sizeOfByte * 3));
        }

        protected override void OnGUI()
        {
            EditorGUILayout.ColorField(m_color);
        }
    }

    public class Matrix4x4DataVisualizer : AbstractDataVisualizer
    {
        Matrix4x4 m_matrix;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Matrix4x4", typeof(Matrix4x4DataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_matrix = m_memoryReader.ReadMatrix4x4(m_address);
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
                            EditorGUILayout.TextField(m_matrix[y, x].ToString());
                        }
                    }
                }
            }
        }
    }

    public class QuaternionDataVisualizer : AbstractDataVisualizer
    {
        Quaternion m_quaternion;

        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("UnityEngine.Quaternion", typeof(QuaternionDataVisualizer));
        }

        protected override void OnInitialize()
        {
            m_quaternion = m_memoryReader.ReadQuaternion(m_address);
        }

        protected override void OnGUI()
        {
            var eulerAngles = m_quaternion.eulerAngles;
            EditorGUILayout.Vector3Field("Euler Angles", eulerAngles);
            //EditorGUILayout.Vector4Field("Quaternion", new Vector4(m_quaternion.x, m_quaternion.y, m_quaternion.z, m_quaternion.w));
        }
    }

    public class StringDataVisualizer : AbstractDataVisualizer
    {
        [InitializeOnLoadMethod]
        static void RegisterVisualizer()
        {
            RegisterVisualizer("System.String", typeof(StringDataVisualizer));
        }

        const int kMaxStringLength = 1024 * 4;

        string m_string;
        string m_shortString;

        protected override void OnInitialize()
        {
            var pointer = m_address;// m_memoryReader.ReadPointer(m_address);
            if (pointer == 0)
                m_string = "null";
            else
                m_string = m_memoryReader.ReadString(pointer + (ulong)m_snapshot.virtualMachineInformation.objectHeaderSize);

            if (m_string == null)
                m_string = "<null>";

            if (m_string.Length > kMaxStringLength)
                m_shortString = m_string.Substring(0, kMaxStringLength);

            m_menu = new GenericMenu();
            m_menu.AddItem(new GUIContent("Open in Text Editor"), false, OnMenuOpenInTextEditor);
        }

        protected override void OnGUI()
        {
            var text = m_string;

            if (m_shortString != null)
            {
                text = m_shortString;
                EditorGUILayout.HelpBox(string.Format("Displaying {0} chars only!", kMaxStringLength), MessageType.Info);
            }

            EditorGUILayout.TextArea(text, EditorStyles.wordWrappedLabel);
        }

        void OnMenuOpenInTextEditor()
        {
            var path = FileUtil.GetUniqueTempPathInProject() + ".txt";
            System.IO.File.WriteAllText(path, m_string);
            EditorUtility.OpenWithDefaultApp(path);
        }
    }

    sealed public class DataVisualizerWindow : EditorWindow
    {
        AbstractDataVisualizer m_visualizer;
        Vector2 m_scrollPosition;

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
            m_visualizer = dataVisualizer;
            if (m_visualizer == null)
                return;

            titleContent = new GUIContent(m_visualizer.title);
        }

        void OnEnable()
        {
        }

        void OnGUI()
        {
            if (m_visualizer == null)
            {
                EditorGUILayout.HelpBox("DataVisualizer is null.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_scrollPosition))
                {
                    m_scrollPosition = scrollView.scrollPosition;

                    GUILayout.Space(2);

                    m_visualizer.GUI();

                    GUILayout.Space(2);
                }
            }
        }
    }
}
