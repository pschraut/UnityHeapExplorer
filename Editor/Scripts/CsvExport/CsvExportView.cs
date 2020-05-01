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
    class CsvExportView : HeapExplorerView
    {
        string m_ExportFolder = "../export";
        string m_ExportName = "game";
        string m_Delimiter = ",";

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<CsvExportView>();
        }

        public override void Awake()
        {
            base.Awake();

            this.titleContent = new GUIContent("CSV Export");
        }
        
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ExportFolder = EditorPrefs.GetString(GetPrefsKey(() => m_ExportFolder), m_ExportFolder);
            m_ExportName = EditorPrefs.GetString(GetPrefsKey(() => m_ExportName), m_ExportName);
            m_Delimiter = EditorPrefs.GetString(GetPrefsKey(() => m_Delimiter), m_Delimiter);
        }

        protected override void OnHide()
        {
            base.OnHide();

            EditorPrefs.SetString(GetPrefsKey(() => m_ExportFolder), m_ExportFolder);
            EditorPrefs.SetString(GetPrefsKey(() => m_ExportName), m_ExportName);
            EditorPrefs.SetString(GetPrefsKey(() => m_Delimiter), m_Delimiter);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_ExportFolder = EditorGUILayout.TextField(new GUIContent("Export Folder", "Choose an export folder where the generated *.csv files are being saved. You can also enter a path that is relative to the project folder."), m_ExportFolder);

                    if (GUILayout.Button(new GUIContent("...", "Select export directory"), GUILayout.Width(100)))
                    {
                        var newFolder = EditorUtility.SaveFolderPanel("Select Csv export folder", m_ExportFolder, "");
                        if (!string.IsNullOrEmpty(newFolder))
                            m_ExportFolder = newFolder;
                    }
                }
                GUILayout.Space(4);

                m_ExportName = EditorGUILayout.TextField(new GUIContent("Export Name", "Choose an export prefix, that is used to save eacg *.csv file."), m_ExportName);
                GUILayout.Space(4);

                m_Delimiter = EditorGUILayout.TextField(new GUIContent("CSV Delimiter", "Choose a delimiter that is used to generate the csv data."), m_Delimiter);
                GUILayout.Space(4);
            }

            GUILayout.Space(8);

            var isExportable = true;
            if (string.IsNullOrEmpty(m_ExportFolder)) isExportable = false;
            if (string.IsNullOrEmpty(m_ExportName)) isExportable = false;
            if (string.IsNullOrEmpty(m_Delimiter)) isExportable = false;

            using (new EditorGUI.DisabledGroupScope(!isExportable))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Export", GUILayout.Width(100)))
                    {
                        Export();
                    }

                    if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                    {
                        EditorUtility.RevealInFinder(m_ExportFolder);
                    }
                }
            }
        }

        void Export()
        {
            var folder = m_ExportFolder;
            if (string.IsNullOrEmpty(folder))
                folder = ".";

            System.IO.Directory.CreateDirectory(folder);

            ExportNativeObjects(string.Format("{0}/{1}_native_objects.csv", folder, m_ExportName));
            ExportManagedObjects(string.Format("{0}/{1}_managed_objects.csv", folder, m_ExportName));
            //ExportManagedStaticFields(string.Format("{0}/{1}_managed_static_fields.csv", folder, m_ExportName));
        }

        void ExportNativeObjects(string filePath)
        {
            var sb = new System.Text.StringBuilder(1024 * 16);
            var objs = snapshot.nativeObjects;

            sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"{0}\"{4}\"{0}\"{5}\"{0}\"{6}\"{0}\"{7}\"{0}\"{8}\"{0}\"{9}\"{0}\"{10}\"\n",
                m_Delimiter,
                "C++ Type",
                "C++ Name",
                "Bytes",
                "DontDestroyOnLoad",
                "Persistent",
                "Address",
                "InstanceId",
                "Manager",
                "HideFlags",
                "C# Type");

            for (var n = 0; n < objs.Length; ++n)
            {
                var obj = new RichNativeObject(snapshot, objs[n].nativeObjectsArrayIndex);
                sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"{0}\"{4}\"{0}\"{5}\"{0}\"{6}\"{0}\"{7}\"{0}\"{8}\"{0}\"{9}\"{0}\"{10}\"\n",
                    m_Delimiter,
                    obj.type.name,
                    obj.name,
                    obj.size,
                    obj.isDontDestroyOnLoad,
                    obj.isPersistent,
                    obj.address,
                    obj.instanceId,
                    obj.isManager,
                    obj.hideFlags,
                    obj.managedObject.type.name);
            }

            System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        void ExportManagedObjects(string filePath)
        {
            var sb = new System.Text.StringBuilder(1024 * 16);
            var objs = snapshot.managedObjects;

            sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"{0}\"{4}\"{0}\"{5}\"{0}\"{6}\"\n",
                m_Delimiter,
                "C# Type",
                "C# Address",
                "C# Bytes",
                "C# Assembly",
                "C++ Address",
                "C++ Type");

            for (var n = 0; n < objs.Length; ++n)
            {
                var obj = new RichManagedObject(snapshot, objs[n].managedObjectsArrayIndex);
                sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"{0}\"{4}\"{0}\"{5}\"{0}\"{6}\"\n",
                    m_Delimiter,
                    obj.type.name,
                    obj.address,
                    obj.size,
                    obj.type.assemblyName,
                    obj.nativeObject.address,
                    obj.nativeObject.type.name);
            }

            System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        void ExportManagedStaticFields(string filePath)
        {
            var sb = new System.Text.StringBuilder(1024 * 16);
            var objs = snapshot.managedStaticFields;

            sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"\n",
                m_Delimiter,
                "C# Class Type",
                "C# Field Type",
                "C# Field Type Assembly");

            for (var n = 0; n < objs.Length; ++n)
            {
                var obj = new RichStaticField(snapshot, objs[n].staticFieldsArrayIndex);
                sb.AppendFormat("\"{1}\"{0}\"{2}\"{0}\"{3}\"\n",
                    m_Delimiter,
                    obj.classType.name,
                    obj.fieldType.name,
                    obj.fieldType.assemblyName);
            }

            System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }
    }
}
