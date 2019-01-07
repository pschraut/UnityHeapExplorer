using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using HeapExplorer;

public class Test_DI_120_Scene_Changes
{
    const string kSnapshotPath = "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\Empty_120SceneChanges.heap";
    PackedMemorySnapshot m_snapshot;

    PackedMemorySnapshot snapshot
    {
        get
        {
            if (m_snapshot == null)
            {
                m_snapshot = new PackedMemorySnapshot();
                m_snapshot.LoadFromFile(kSnapshotPath);
                m_snapshot.Initialize();
            }
            return m_snapshot;
        }
    }

    [Test]
    public void NativeObjectsArrayLength()
    {
        Assert.AreEqual(14500, snapshot.nativeObjects.Length);
    }

    [Test]
    public void NativeTypesArrayLength()
    {
        Assert.AreEqual(321, snapshot.nativeTypes.Length);
    }

    [Test]
    public void ManagedObjectsArrayLength()
    {
        Assert.AreEqual(171247, snapshot.managedObjects.Length);
    }

    [Test]
    public void ManagedTypesArrayLength()
    {
        Assert.AreEqual(5607, snapshot.managedTypes.Length);
    }

    [Test]
    public void GCHandlesArrayLength()
    {
        Assert.AreEqual(11850, snapshot.gcHandles.Length);
    }

    [Test]
    public void ManagedHeapSectionsArrayLength()
    {
        Assert.AreEqual(582, snapshot.managedHeapSections.Length);
    }

    [Test]
    public void ManagedStaticFieldsArrayLength()
    {
        //MemoryProfiler: Assert.AreEqual(1078, snapshot.managedStaticFields.Length);
        //Assert.AreEqual(2147, snapshot.managedStaticFields.Length);
        Assert.AreEqual(2138, snapshot.managedStaticFields.Length);
    }

    [Test]
    public void GCHandle_108c58f60_Fabric_Internal_Crashlytics_CrashlyticsInit()
    {
        var gcHandleIndex = snapshot.FindGCHandleOfTargetAddress(0x108c58f60);
        Assert.AreNotEqual(-1, gcHandleIndex);

        var managedObjIndex = snapshot.gcHandles[gcHandleIndex].managedObjectsArrayIndex;
        Assert.AreNotEqual(-1, managedObjIndex);

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[managedObjIndex]));

        var path = finder.shortestPath;
        Assert.AreEqual(2, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("Fabric.Internal.Crashlytics.CrashlyticsInit", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].staticField.isValid);
        Assert.AreEqual("Fabric.Internal.Crashlytics.CrashlyticsInit", path[1].staticField.classType.name);
    }

    [Test]
    public void CompareManagedObjectsWithMemoryProfiler()
    {
        TestUtility.CompareManagedObjectsWithMemoryProfiler(snapshot, "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\Empty_120SceneChanges_managedobjects.csv");
    }

    [Test]
    public void ManagedObject_String_111CCB060()
    {
        var managedObjIndex = snapshot.FindManagedObjectOfAddress(0x111CCB060);
        Assert.AreNotEqual(-1, managedObjIndex);

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[managedObjIndex]));
        var path = finder.shortestPath;

        Assert.AreEqual(2, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("System.String", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].staticField.isValid);
        Assert.AreEqual("UIModelPreviewRigManager", path[1].staticField.classType.name);
    }
}