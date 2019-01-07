using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using HeapExplorer;

public class Test_Wolf4_2018_IL2Cpp
{
    const string kSnapshotPath = "C:\\Users\\crash\\Documents\\unityheapexplorer\\Backup\\HeapDumps\\wolf4_2018_il2cpp.heap";
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
    public void ManagedObjectDuplicate()
    {
        RunTest<ManagedObjectDuplicateTestConfig>("ed1fa2da215673343a0621f17ebd7e30");
    }

    void RunTest<T>(string guid) where T : ScriptableObject, ITestConfig
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var test = AssetDatabase.LoadAssetAtPath<T>(path) as ITestConfig;
        test.RunTest(snapshot);
    }
}
