using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HeapExplorer;
using NUnit.Framework;
using UnityEngine.TestTools;

public interface ITestConfig
{
    void RunTest(PackedMemorySnapshot snapshot);
}

[CreateAssetMenu(menuName = "Tests/Managed Object Duplicate Config")]
public class ManagedObjectDuplicateTestConfig : ScriptableObject, ITestConfig
{
    [System.Serializable]
    public class Entry
    {
        public string comment;
        public List<string> addresses;
    }
    
    public List<Entry> Equal = new List<Entry>();
    public List<Entry> NotEqual = new List<Entry>();

    public void RunTest(PackedMemorySnapshot snapshot)
    {
        var reader = new MemoryReader(snapshot);

        foreach (var list in Equal)
        {
            Hash128 prevHash = new Hash128();

            for (int k = 0, kend = list.addresses.Count; k < kend; ++k)
            {
                var address = ulong.Parse(list.addresses[k], System.Globalization.NumberStyles.HexNumber);
                var index = snapshot.FindManagedObjectOfAddress(address);
                var obj = new RichManagedObject(snapshot, index);

                var hash = reader.ComputeObjectHash(obj.address, obj.type.packed);
                if (k > 0)
                    Assert.AreEqual(prevHash, hash);

                prevHash = hash;
            }
        }

        foreach (var list in NotEqual)
        {
            Hash128 prevHash = new Hash128();

            for (int k = 0, kend = list.addresses.Count; k < kend; ++k)
            {
                var address = ulong.Parse(list.addresses[k], System.Globalization.NumberStyles.HexNumber);
                var index = snapshot.FindManagedObjectOfAddress(address);
                var obj = new RichManagedObject(snapshot, index);

                var hash = reader.ComputeObjectHash(obj.address, obj.type.packed);
                if (k > 0)
                    Assert.AreNotEqual(prevHash, hash);

                prevHash = hash;
            }
        }
    }
}
