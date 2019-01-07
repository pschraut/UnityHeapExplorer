using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using HeapExplorer;

public class Test_Wolf4
{
    //const string kSnapshotPath = "C:\\Users\\crash\\Documents\\HeapExplorer\\Unity\\Build\\wolf4.heap";
    const string kSnapshotPath = "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\wolf4.heap";
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
    public void CompareStaticFieldsWithMemoryProfiler()
    {
        TestUtility.CompareManagedStaticTypesWithMemoryProfiler(snapshot, "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\wolf4_staticfields.csv");
    }

    [Test]
    public void NativeObjectsArrayLength()
    {
        Assert.AreEqual(16749, snapshot.nativeObjects.Length);
    }

    [Test]
    public void NativeTypesArrayLength()
    {
        Assert.AreEqual(219, snapshot.nativeTypes.Length);
    }

    [Test]
    public void ManagedObjectsArrayLength()
    {
        Assert.AreEqual(22236, snapshot.managedObjects.Length);
    }

    [Test]
    public void ManagedTypesArrayLength()
    {
        Assert.AreEqual(1671, snapshot.managedTypes.Length);
    }

    [Test]
    public void GCHandlesArrayLength()
    {
        Assert.AreEqual(11249, snapshot.gcHandles.Length);
    }

    [Test]
    public void ManagedHeapSectionsArrayLength()
    {
        Assert.AreEqual(845, snapshot.managedHeapSections.Length);
    }

    [Test]
    public void ManagedStaticFieldsArrayLength()
    {
        Assert.AreEqual(386, snapshot.managedStaticFields.Length);
        Assert.AreEqual(157, snapshot.managedStaticTypes.Length);
    }

    [Test]
    public void FindHeapOfAddress()
    {
        Assert.AreNotEqual(-1, snapshot.FindHeapOfAddress(0x1DB27A09000));
        Assert.AreNotEqual(-1, snapshot.FindHeapOfAddress(0x1db2c0f0000));
        Assert.AreNotEqual(-1, snapshot.FindHeapOfAddress(0x1DB20462450));
        Assert.AreNotEqual(-1, snapshot.FindHeapOfAddress(0x1DB388FFEB8));
    }

    [Test]
    public void FindNativeObjectOfAddress()
    {
        var value = snapshot.FindNativeObjectOfAddress(0x1db1cec89b0); // chubgothic_1
        Assert.AreNotEqual(-1, value);
    }

    [Test]
    public void FindManagedTypeOfAddress()
    {
        Assert.AreNotEqual(-1, snapshot.FindManagedTypeOfTypeInfoAddress(snapshot.managedTypes[0].typeInfoAddress));
        Assert.AreNotEqual(-1, snapshot.FindManagedTypeOfTypeInfoAddress(snapshot.managedTypes[snapshot.managedTypes.Length / 2].typeInfoAddress));
        Assert.AreNotEqual(-1, snapshot.FindManagedTypeOfTypeInfoAddress(snapshot.managedTypes[snapshot.managedTypes.Length - 1].typeInfoAddress));
    }

    [Test]
    public void FindManagedObjectOfNativeObject()
    {
        var value = snapshot.FindManagedObjectOfNativeObject(0x1db1cec89b0); // chubgothic_1
        Assert.AreNotEqual(-1, value);
    }

    [Test]
    public void FindManagedObjectOfAddress()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2AD232E0); // chubgothic_1
        Assert.AreNotEqual(-1, value);
    }

    [Test]
    public void FindManagedObject_MonoEnumInfo_1DB20469FC0()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB20469FC0); // System.MonoEnumInfo
        Assert.AreNotEqual(-1, value);
    }

    [Test]
    public void FindManagedObject_SlotArray_1DB2A512EE0()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2A512EE0); // Slot[]
        Assert.AreNotEqual(-1, value);
    }

    [Test]
    public void GetConnections_chubgothic_1()
    {
        var references = new List<PackedConnection>();
        var referencedBy = new List<PackedConnection>();

        // managed object
        var managedObject = snapshot.FindManagedObjectOfNativeObject(0x1db1cec89b0); // chubgothic_1
        references = new List<PackedConnection>();
        referencedBy = new List<PackedConnection>();
        snapshot.GetConnections(snapshot.managedObjects[managedObject], references, referencedBy);
        Assert.AreEqual(1, references.Count);
        Assert.AreEqual(182, referencedBy.Count);

        // native object
        var nativeObject = snapshot.FindNativeObjectOfAddress(0x1db1cec89b0);
        references = new List<PackedConnection>();
        referencedBy = new List<PackedConnection>();
        snapshot.GetConnections(snapshot.nativeObjects[nativeObject], references, referencedBy);
        Assert.AreEqual(3, references.Count);
        Assert.AreEqual(405, referencedBy.Count);
    }

    [Test]
    public void GetConnections_UnityEngine_Color32_Array()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2A5AD000); // UnityEngine.Color32[]

        var references = new List<PackedConnection>();
        var referencedBy = new List<PackedConnection>();
        snapshot.GetConnections(snapshot.managedObjects[value], references, referencedBy);

        Assert.AreEqual(0, references.Count);
        Assert.AreEqual(1, referencedBy.Count);
    }

    [Test]
    public void GetConnections_MonoStateHandler()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1db2adcdc30);

        var references = new List<PackedConnection>();
        var referencedBy = new List<PackedConnection>();
        snapshot.GetConnections(snapshot.managedObjects[value], references, referencedBy);

        Assert.AreEqual(5, references.Count);
        Assert.AreEqual(14, referencedBy.Count);

        // References
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, references, PackedConnection.Kind.None, PackedConnection.Kind.Native, 0x1DB3013A6A0));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, references, PackedConnection.Kind.None, PackedConnection.Kind.Managed, 0x1DB2ADBDAE0));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, references, PackedConnection.Kind.None, PackedConnection.Kind.Managed, 0x1DB2A53A320));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, references, PackedConnection.Kind.None, PackedConnection.Kind.Managed, 0x1DB2AD32780));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, references, PackedConnection.Kind.None, PackedConnection.Kind.Managed, 0x1DB2ADBDAE0));

        // ReferencedBy
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.GCHandle, PackedConnection.Kind.None, 0x1DB2ADCDC30));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2A524000));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2A52BF00));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADE2500));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADD4300));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADE4C80));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADCF0A0));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADBDAE0));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADC3C40));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADB9C00));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADBAD80));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADBAF00));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB27A32800));
        Assert.AreEqual(true, TestUtility.CheckConnection(snapshot, referencedBy, PackedConnection.Kind.Managed, PackedConnection.Kind.None, 0x1DB2ADACEE0));
    }
    
    [Test]
    public void ManagedObjectsCompareWithMemoryProfiler_Wolf4()
    {
        TestUtility.CompareManagedObjectsWithMemoryProfiler(snapshot, "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\wolf4_managedobjects.csv");
    }

    [Test]
    public void ShortestPathToRoot_UnityEngine_Color32_Array()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2A5AD000); // UnityEngine.Color32[]

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(4, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("UnityEngine.Color32[]", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].managed.isValid);
        Assert.AreEqual("HUDMinimap", path[1].managed.type.name);

        Assert.AreEqual(true, path[2].gcHandle.isValid);

        Assert.AreEqual(true, path[3].native.isValid);
        Assert.AreEqual("MonoBehaviour", path[3].native.type.name);
        Assert.AreEqual("Minimap", path[3].native.name);
    }

    [Test]
    public void ShortestPathToRoot_MonoEnumInfo_StringArray()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB20443540); // string[]

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(5, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("System.String[]", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].managed.isValid);
        Assert.AreEqual("System.MonoEnumInfo", path[1].managed.type.name);

        Assert.AreEqual(true, path[2].managed.isValid);
        Assert.AreEqual("Slot[]", path[2].managed.type.name);

        Assert.AreEqual(true, path[3].managed.isValid);
        Assert.AreEqual("System.Collections.Hashtable", path[3].managed.type.name);

        Assert.AreEqual(true, path[4].staticField.isValid);
        Assert.AreEqual("System.MonoEnumInfo", path[4].staticField.classType.name);
        Assert.AreEqual("System.Collections.Hashtable", path[4].staticField.fieldType.name);
    }

    [Test]
    public void ShortestPathToRoot_AudioClipConfig()
    {
        var value = snapshot.FindNativeObjectOfAddress(0x1db2a2385c0); // AudioClipConfig

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.nativeObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(2, path.count);

        Assert.AreEqual(true, path[0].native.isValid);
        Assert.AreEqual("MonoBehaviour", path[0].native.type.name);
        Assert.AreEqual("dog_hurt_whimper_howl_08_Config", path[0].native.name);

        Assert.AreEqual(true, path[1].native.isValid);
        Assert.AreEqual("MonoBehaviour", path[1].native.type.name);
        Assert.AreEqual("Sprite", path[1].native.name);
        Assert.AreEqual(0x1DB3014DFF0, path[1].native.address);
    }

    [Test]
    public void ShortestPathToRoot_System_Globalization_NumberFormatInfo()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2044F8A0);

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(3, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("System.Globalization.NumberFormatInfo", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].managed.isValid);
        Assert.AreEqual("System.Globalization.CultureInfo", path[1].managed.type.name);

        Assert.AreEqual(true, path[2].staticField.isValid);
        Assert.AreEqual("System.Globalization.CultureInfo", path[2].staticField.fieldType.name);
    }

    [Test]
    public void ShortestPathToRoot_SceneObjectIDManager()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1DB2046CDC8);

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(2, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("SceneObjectIDManager", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].staticField.isValid);
        Assert.AreEqual("SceneObjectIDManager", path[1].staticField.fieldType.name);
        Assert.AreEqual("SceneObjectID", path[1].staticField.classType.name);
    }

    [Test]
    public void ShortestPathToRoot_MonoStateHandler()
    {
        var value = snapshot.FindManagedObjectOfAddress(0x1db2adcdc30);

        var finder = new RootPathUtility();
        finder.Find(new ObjectProxy(snapshot, m_snapshot.managedObjects[value]));
        var path = finder.shortestPath;

        Assert.AreEqual(3, path.count);

        Assert.AreEqual(true, path[0].managed.isValid);
        Assert.AreEqual("MonoStateHandler", path[0].managed.type.name);

        Assert.AreEqual(true, path[1].gcHandle.isValid);

        Assert.AreEqual(true, path[2].native.isValid);
        Assert.AreEqual("MonoBehaviour", path[2].native.type.name);
    }

    [Test]
    public void ManagedObject_ComputeHash_SingleArray_Equal()
    {
        TestUtility.ManagedObjectContentIsEqual(snapshot, new[] { 0x1DB27A0E000u, 0x1DB27A00000u });
    }

    [Test]
    public void ManagedObject_ComputeHash_SingleArray_NotEqual()
    {
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB27A0E000u, 0x1DB20479000u });
    }

    [Test]
    public void ManagedObject_ComputeHash_String_Equal()
    {
        TestUtility.ManagedObjectContentIsEqual(snapshot, new[] { 0x1DB2C18E400u, 0x1DB2C18E800u, 0x1DB2C18EA00u, 0x1DB2C193E00u });
    }

    [Test]
    public void ManagedObject_ComputeHash_String_NotEqual()
    {
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB27A0E000u, 0x1DB2C113800u });
    }

    [Test]
    public void ManagedObject_ComputeHash_String_02_Equal()
    {
        TestUtility.ManagedObjectContentIsEqual(snapshot, new[] { 0x1DB2AD38C78u, 0x1DB20455AB8u, 0x1DB388DD578u });
    }

    [Test]
    public void ManagedObject_ComputeHash_String_02_NotEqual()
    {
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB2AD38C78u, 0x1DB2AD38150u });
    }

    [Test]
    public void ManagedObject_ComputeHash_EventTask_Equal()
    {
        TestUtility.ManagedObjectContentIsEqual(snapshot, new[] { 0x1DB2A4DA910u, 0x1DB2AD34A28u });
    }

    [Test]
    public void ManagedObject_ComputeHash_EventTask_NotEqual()
    {
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB2A4DA910u, 0x1DB2AD75C58u });
    }

    [Test]
    public void ManagedObject_ComputeHash_FontData_Equal()
    {
        TestUtility.ManagedObjectContentIsEqual(snapshot, new[] { 0x1DB2A6082C0u, 0x1DB2A628E40u, 0x1DB2A608580u, 0x1DB2A6084C0u, 0x1DB2A628600u, 0x1DB2A628580u, 0x1DB2A628800u, 0x1DB2A6287C0u });
    }

    [Test]
    public void ManagedObject_ComputeHash_FontData_NotEqual()
    {
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB2A6082C0u, 0x1DB2AD75C58u });
        TestUtility.ManagedObjectContentIsNotEqual(snapshot, new[] { 0x1DB2A6082C0u, 0x1DB2A608180u });
    }
}