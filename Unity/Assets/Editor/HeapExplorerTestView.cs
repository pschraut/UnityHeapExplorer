using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HeapExplorer;

public class HeapExplorerTestView : HeapExplorerView
{
    RichManagedObject m_BiggestManagedObject;
    RichNativeObject m_BiggestNativeObject;

    [InitializeOnLoadMethod]
    static void Register()
    {
        // Register this class to appear in heap explorers 'Views' menu
        HeapExplorerWindow.Register<HeapExplorerTestView>();
    }

    public override void Awake()
    {
        base.Awake();

        this.titleContent = new GUIContent("Heap Explorer Test View");
    }

    // OnCreate is called once per snapshot, to initialize the view.
    protected override void OnCreate()
    {
        base.OnCreate();

        // Find the biggest managed object
        m_BiggestManagedObject = RichManagedObject.invalid;
        foreach (var mo in snapshot.managedObjects)
        {
            if (mo.size > m_BiggestManagedObject.size)
                m_BiggestManagedObject = new RichManagedObject(snapshot, mo.managedObjectsArrayIndex);
        }

        // Find the biggest native object
        m_BiggestNativeObject = RichNativeObject.invalid;
        foreach (var no in snapshot.nativeObjects)
        {
            if (no.size > m_BiggestNativeObject.size)
                m_BiggestNativeObject = new RichNativeObject(snapshot, no.nativeObjectsArrayIndex);
        }
    }

    // OnGUI is called to draw the specific UI for this view.
    public override void OnGUI()
    {
        base.OnGUI();

        EditorGUILayout.LabelField("This is the HeapExplorerTestView class.");
        GUILayout.Space(32);

        EditorGUILayout.HelpBox(string.Format("The single biggest managed object, with a size of {0}, is of type {1}.",
            EditorUtility.FormatBytes(m_BiggestManagedObject.size),
            m_BiggestManagedObject.type.name), MessageType.Info);

        GUILayout.Space(16);

        EditorGUILayout.HelpBox(string.Format("The single biggest native object, with a size of {0}, is of type {1}.",
            EditorUtility.FormatBytes(m_BiggestNativeObject.size),
            m_BiggestNativeObject.type.name), MessageType.Info);
    }
}
