using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class OverviewView : HeapExplorerView
    {
        const int k_ListItemCount = 20;
        const int k_ColumnPercentageWidth = 60;
        const int k_ColumnSizeWidth = 70;

        struct Entry
        {
            public int typeIndex;
            public long size;
        }
        Entry[] m_nativeMemory;
        Entry[] m_managedMemory;
        Entry[] m_staticMemory;
        long m_nativeMemoryTotal;
        long m_managedMemoryTotal;
        long m_staticMemoryTotal;
        Texture2D m_heapFragTexture;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<OverviewView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("Overview", "");
            viewMenuOrder = 150;
        }

        public override void OnDestroy()
        {
            if (m_heapFragTexture != null)
            {
                Texture2D.DestroyImmediate(m_heapFragTexture);
                m_heapFragTexture = null;
            }

            base.OnDestroy();
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            if (m_heapFragTexture != null) Texture2D.DestroyImmediate(m_heapFragTexture);
            m_heapFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_heapFragTexture.name = "HeapExplorer-HeapFragmentation-Texture";
            ScheduleJob(new HeapFragmentationJob() { snapshot = snapshot, texture = m_heapFragTexture });

            m_nativeMemory = null;
            m_managedMemory = null;
            m_staticMemory = null;
            m_nativeMemoryTotal = 0;
            m_managedMemoryTotal = 0;
            m_staticMemoryTotal = 0;

            AnalyzeNative();
            AnalyzeManaged();
            AnalyzeStatic();
        }

        void AnalyzeNative()
        {
            m_nativeMemory = new Entry[snapshot.nativeTypes.LongLength];
            for (long n = 0, nend = snapshot.nativeObjects.LongLength; n < nend; ++n)
            {
                var obj = snapshot.nativeObjects[n];

                m_nativeMemoryTotal += obj.size;
                m_nativeMemory[obj.nativeTypesArrayIndex].size += obj.size;
                m_nativeMemory[obj.nativeTypesArrayIndex].typeIndex = obj.nativeTypesArrayIndex;
            }
            System.Array.Sort(m_nativeMemory, delegate (Entry x, Entry y)
            {
                return y.size.CompareTo(x.size);
            });
        }

        void AnalyzeManaged()
        {
            m_managedMemory = new Entry[snapshot.managedTypes.LongLength];
            for (long n = 0, nend = snapshot.managedObjects.LongLength; n < nend; ++n)
            {
                var obj = snapshot.managedObjects[n];
                var type = snapshot.managedTypes[obj.managedTypesArrayIndex];

                m_managedMemoryTotal += obj.size;
                m_managedMemory[type.managedTypesArrayIndex].size += obj.size;
                m_managedMemory[type.managedTypesArrayIndex].typeIndex = obj.managedTypesArrayIndex;
            }
            System.Array.Sort(m_managedMemory, delegate (Entry x, Entry y)
            {
                return y.size.CompareTo(x.size);
            });
        }

        void AnalyzeStatic()
        {
            m_staticMemory = new Entry[snapshot.managedTypes.LongLength];
            for (long n = 0, nend = snapshot.managedTypes.LongLength; n < nend; ++n)
            {
                var type = snapshot.managedTypes[n];

                if (type.staticFieldBytes != null)
                {
                    m_staticMemoryTotal += type.staticFieldBytes.Length;
                    m_staticMemory[type.managedTypesArrayIndex].size += type.staticFieldBytes.Length;
                }

                m_staticMemory[type.managedTypesArrayIndex].typeIndex = type.managedTypesArrayIndex;
            }

            System.Array.Sort(m_staticMemory, delegate (Entry x, Entry y)
            {
                return y.size.CompareTo(x.size);
            });
        }

        void DrawStats(string field1, string field2, string field3)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(field1, GUILayout.Width(k_ColumnPercentageWidth));
                GUILayout.Label(field2, GUILayout.Width(k_ColumnSizeWidth));
                HeEditorGUI.TypeName(GUILayoutUtility.GetRect(10, GUI.skin.label.CalcHeight(new GUIContent("Wg"), 32), GUILayout.ExpandWidth(true)), field3);
            }
        }

        public override void OnGUI()
        {
            base.OnGUI();


            float k_CellWidth = window.position.width * 0.328f;

            GUILayout.Label("Brief Overview", HeEditorStyles.heading2);
            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Native Memory
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(k_CellWidth)))
                {
                    EditorGUILayout.LabelField(new GUIContent(string.Format("Top {0} Native Memory Usage", k_ListItemCount), HeEditorStyles.cppImage), EditorStyles.boldLabel);
                    GUILayout.Space(8);

                    for (var n = 0; n < Mathf.Min(k_ListItemCount, m_nativeMemory.Length); ++n)
                    {
                        var type = snapshot.nativeTypes[m_nativeMemory[n].typeIndex];
                        var size = m_nativeMemory[n].size;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(string.Format("{0:F2}%", (size / (float)m_nativeMemoryTotal) * 100), GUILayout.Width(k_ColumnPercentageWidth));
                            GUILayout.Label(EditorUtility.FormatBytes(size), GUILayout.Width(k_ColumnSizeWidth));
                            HeEditorGUI.TypeName(GUILayoutUtility.GetRect(10, GUI.skin.label.CalcHeight(new GUIContent("Wg"), 32), GUILayout.ExpandWidth(true)), type.name);
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label("...", GUILayout.Width(k_ColumnSizeWidth));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Total", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label(EditorUtility.FormatBytes(m_nativeMemoryTotal), EditorStyles.boldLabel, GUILayout.Width(k_ColumnSizeWidth));
                        if (GUILayout.Button("Investigate"))
                            window.OnGoto(new GotoCommand(new RichNativeObject(snapshot, 0)));
                    }
                }

                // Managed Memory
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(k_CellWidth)))
                {
                    EditorGUILayout.LabelField(new GUIContent(string.Format("Top {0} Managed Memory Usage", k_ListItemCount), HeEditorStyles.csImage), EditorStyles.boldLabel);
                    GUILayout.Space(8);

                    for (var n = 0; n < Mathf.Min(k_ListItemCount, m_managedMemory.Length); ++n)
                    {
                        var type = snapshot.managedTypes[m_managedMemory[n].typeIndex];
                        var size = m_managedMemory[n].size;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(string.Format("{0:F2}%", (size / (float)m_managedMemoryTotal) * 100), GUILayout.Width(k_ColumnPercentageWidth));
                            GUILayout.Label(EditorUtility.FormatBytes(size), GUILayout.Width(k_ColumnSizeWidth));
                            HeEditorGUI.TypeName(GUILayoutUtility.GetRect(10, GUI.skin.label.CalcHeight(new GUIContent("Wg"), 32), GUILayout.ExpandWidth(true)), type.name);
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label("...", GUILayout.Width(k_ColumnSizeWidth));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Total", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label(EditorUtility.FormatBytes(m_managedMemoryTotal), EditorStyles.boldLabel, GUILayout.Width(k_ColumnSizeWidth));
                        if (GUILayout.Button("Investigate"))
                            window.OnGoto(new GotoCommand(new RichManagedObject(snapshot, 0)));
                    }
                }

                // Static Memory
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(k_CellWidth)))
                {
                    EditorGUILayout.LabelField(new GUIContent(string.Format("Top {0} Static Memory Usage", k_ListItemCount), HeEditorStyles.csStaticImage), EditorStyles.boldLabel);
                    GUILayout.Space(8);

                    for (var n = 0; n < Mathf.Min(k_ListItemCount, m_staticMemory.Length); ++n)
                    {
                        var type = snapshot.managedTypes[m_staticMemory[n].typeIndex];
                        var size = m_staticMemory[n].size;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(string.Format("{0:F2}%", (size / (float)m_staticMemoryTotal) * 100), GUILayout.Width(k_ColumnPercentageWidth));
                            GUILayout.Label(EditorUtility.FormatBytes(size), GUILayout.Width(k_ColumnSizeWidth));
                            HeEditorGUI.TypeName(GUILayoutUtility.GetRect(10, GUI.skin.label.CalcHeight(new GUIContent("Wg"), 32), GUILayout.ExpandWidth(true)), type.name);
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label("...", GUILayout.Width(k_ColumnSizeWidth));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Total", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label(EditorUtility.FormatBytes(m_staticMemoryTotal), EditorStyles.boldLabel, GUILayout.Width(k_ColumnSizeWidth));
                        if (GUILayout.Button("Investigate"))
                            window.OnGoto(new GotoCommand(new RichStaticField(snapshot, 0)));
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // GC Handles
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(k_CellWidth)))
                {
                    EditorGUILayout.LabelField(new GUIContent("GC handles Memory Usage", HeEditorStyles.gcHandleImage), EditorStyles.boldLabel);
                    GUILayout.Space(8);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Total", GUILayout.Width(k_ColumnPercentageWidth));
                        GUILayout.Label(EditorUtility.FormatBytes(snapshot.virtualMachineInformation.pointerSize * snapshot.gcHandles.Length), EditorStyles.boldLabel, GUILayout.Width(k_ColumnSizeWidth));
                        if (GUILayout.Button("Investigate"))
                            window.OnGoto(new GotoCommand(new RichGCHandle(snapshot, 0)));
                    }
                }

                // VirtualMachine Information
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(k_CellWidth)))
                {
                    GUILayout.Label("Virtual Machine Information", EditorStyles.boldLabel);
                    GUILayout.Space(8);

                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.pointerSize), "Pointer Size");
                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.objectHeaderSize), "Object Header Size");
                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.arrayHeaderSize), "Array Header Size");
                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.arrayBoundsOffsetInHeader), "Array Bounds Offset In Header");
                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.arraySizeOffsetInHeader), "Array Size Offset In Header");
                    DrawStats("", EditorUtility.FormatBytes(snapshot.virtualMachineInformation.allocationGranularity), "Allocation Granularity");
                    DrawStats("", string.Format("{0}", snapshot.virtualMachineInformation.heapFormatVersion), "Heap Format Version");
                }
            }

            DrawHeapFragmentation();
        }

        void DrawHeapFragmentation()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var text = string.Format("{0} managed heap sections, making a total of {1}, fragmented across {2} from the operating system", snapshot.managedHeapSections.Length, EditorUtility.FormatBytes((long)snapshot.managedHeapSize), EditorUtility.FormatBytes((long)snapshot.managedHeapAddressSpace));
                GUILayout.Label(text, EditorStyles.boldLabel);

                GUI.DrawTexture(GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true)), m_heapFragTexture, ScaleMode.StretchToFill);

                if (HeEditorGUILayout.LinkButton(new GUIContent("Understanding the managed heap", "https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity4-1.html"), HeEditorStyles.miniHyperlink))
                    EditorUtility.OpenWithDefaultApp("https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity4-1.html");

                GUILayout.Label(string.Format("Red represents the {0} address space allocated from the operating system. Green repesents the {1} allocated managed heap sections within this address space.",
                    EditorUtility.FormatBytes((long)snapshot.managedHeapAddressSpace),
                    snapshot.managedHeapSections.LongLength), EditorStyles.miniLabel);
            }
        }

        class HeapFragmentationJob : AbstractThreadJob
        {
            public Texture2D texture;
            public PackedMemorySnapshot snapshot;

            Color32[] m_data;

            public override void ThreadFunc()
            {
                m_data = ManagedHeapSectionsUtility.GetManagedHeapUsageAsTextureData(snapshot);
            }

            public override void IntegrateFunc()
            {
                if (texture != null && m_data != null)
                {
                    texture.SetPixels32(m_data);
                    texture.Apply(false);
                }
            }
        }
    }
}
