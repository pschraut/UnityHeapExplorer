# Introduction

Heap Explorer is a Memory Profiler, Debugger and Analyzer for Unity. This repository hosts Heap Explorer for Unity 2019.3 and newer. For older versions, please visit the now obsolete repository on Bitbucket instead ([link](https://bitbucket.org/pschraut/unityheapexplorer/)).

I spent a significant amount of time identifying and fixing memory leaks, as well as looking for memory optimization opportunities in Unity applications in the past. During this time, I often used Unity's [old Memory Profiler](https://bitbucket.org/Unity-Technologies/memoryprofiler) and while it's an useful tool, I never was entirely happy with it.

This lead me to write my own memory profiler where I have the opportunity to make all the things I didn't like about Unity's Memory Profiler better.



# Installation

In order to use the Heap Explorer, you have to add the package to your project. As of Unity 2019.3, Unity supports to add packages from git through the Package Manager window.

In Unity's Package Manager, choose "Add package from git URL" and insert one of the Package URL's you can find below. Once Heap Explorer is installed, you can open it from Unity's main menu under "Window > Analysis > Heap Explorer".

## Package URL's

| Version  |     Link      |
|----------|---------------|
| 3.2.0 | https://github.com/pschraut/UnityHeapExplorer.git#3.2.0 |



# Target audience

Heap Explorer is a tool for programmers and people with a strong technical background, who are looking for a tool that helps them identifying memory issues and memory optimization opportunities in Unity applications.

Heap Explorer is not fixing memory leaks, nor optimizing content for you automatically. Heap Explorer is a tool where you have to understand the presented data and draw your own conclusions from.



# Contact

The easiest way to get in touch with me, if you already have an Unity forums account, is to post in the Heap Explorer forum thread:
https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/

You could also use the "Start a Conversation" functionality to send me a private message via the Unity forums: https://forum.unity.com/members/peter77.308146/

And last but not least, you can send me an email. Please find the contact information on my website:
http://www.console-dev.de




# Can I use this tool when I work on a commercial project?

Yes. You can use Heap Explorer to debug, profile and analyze your hobby-, Indie- and commercial applications for free. You do not have to pay me anything.

If, however, Heap Explorer helped you, I would appreciate a mentioning in your credits screen.

Something like "Heap Explorer by Peter Schraut" would be very much appreciated from my side, but is not required. You can use Heap Explorer for free, without having to give me credit or mention you used the tool at all.



# How to capture a memory snapshot

Heap Explorer displays the connected Player in the "Capture" drop-down, which you can find in the toolbar. The button is located under a drop-down menu, to avoid clicking it by accident. 
If no Player is connected, Heap Explorer displays "Editor". Clicking the "Editor" button then captures a memory snapshot of the Unity editor.

![alt text](Documentation~/images/capture_dropdown_01.png "Capture Memory Snapshot Dropdown")

If a Player is connected, Heap Explorer displays the Player name, rather than "Editor". It's the same name that appears in Unity's Profiler window as well.

| Item  |     Description      |
|----------|---------------|
| Capture and Save | Prompts for a save location before the memory snapshot is captured. This feature has been added to allow you  to quickly capture a memory snapshot that you can analyze later, without Heap Explorer analyzing the snapshot, which can be an expensive operation. |
| Capture and Analyze | Captures a memory snapshot and immediately analyzes it. |
| Open Profiler | Opens Unity's Profiler window. In order to connect to a certain target, you have to use Unity's Profiler. As you select a different target (Editor, WindowsPlayer, ...) in Unity's Profiler window, Heap Explorer will update its entry in the "Capture" drop-down accordingly, depending on what is selected in Unity's Profiler. |


# Brief Overview

The Brief Overview page shows the most important "quick info" in a simple to read fashion, such as the top 20 object types that consume the most memory.

![alt text](Documentation~/images/brief_overview_01.png "Brief Overview Window")


# Compare Memory Snapshots

Heap Explorer supports to compare two memory snapshots and show the difference between those. This is an useful tool to find memory leaks.

![alt text](Documentation~/images/compare_snapshot_01.png "Compare Memory Snapshot")

"A" and "B" represent two different memory snapshots.

The "delta" columns indicate changes. The "C# Objects" and "C++ Objects" nodes can be expanded to see which objects specifically cause the difference.

Snapshot "A" is always the one you loaded using "File > Open Snapshot" or captured. While "B" is the memory snapshot that is used for comparison and can be replaced using the "Load..." button in the Compare Snapshot view.


# C# Objects

The C# Objects view displays managed objects found in a memory snapshot. Object instances are grouped by type. Grouping object instances by type allows to see how much memory a certain type is using.

![alt text](Documentation~/images/cs_view_01.png "C# Objects View")

| Location  | Description      |
|----------|---------------|
| Top-left panel | The main list that shows all managed objects found in the snapshot. |
| Top-right panel | An Inspector that displays fields and their corresponding values of the selected object. |
| Bottom-right panel | One or multiple paths to root of the selected object. |
| Bottom-left panel | Objects that hold a reference to the selected object. |

You can left-click on a column to sort and right-click on a column header to toggle individual columns:

| Column  | Description      |
|----------|---------------|
| C# Type | The managed type of the object instance, such as System.String. |
| C++ Name | If the C# object has a C++ counter-part, basically C# types that derive from UnityEngine.Object have, the name of the C++ native object is displayed in this column (UnityEngine.Object.name). |
| Size | The amount of memory a managed object or group of managed objects is using. | 
| Count | The number of managed objects in a group. |
| Address | The memory address of a managed object. |
| Assembly | The assembly (DLL) name in which the type lives. |


# C# Object Inspector

The C# Object Inspector displays fields of a managed object, along with the field type and value. I tried to mimic the feel of Visual Studio's Watch window.

![alt text](Documentation~/images/cs_inspector_01.png "C# Object Inspector")

The arrow in-front of the Name indicates the field provides further fields itself, or in the case of an array, provides array elements. Click the arrow to expand, as shown below.

![alt text](Documentation~/images/cs_inspector_02.png "C# Object Inspector")

The icon in-front of the Name represents the "high-level type" of a field, such as: ReferenceType, ValueType, Enum and Delegate. If the field is a ReferenceType, a button is shown next to the Name, which can be used to jump to the object instance.

![alt text](Documentation~/images/cs_inspector_03.png "C# Object Inspector")

A magnification icon appears next to the value, if the type provides a specific "Data Visualizer". A data visualizer allows Heap Explorer to display the value in a more specific way, tailored to the type, as shown below.

![alt text](Documentation~/images/cs_inspector_04.png "C# Object Inspector")

If a field is a pointer-type (ReferenceType, IntPtr, UIntPtr), but it points to null, the field is grayed-out. I found this very useful, because you often ignore null-values and having those grayed-out, makes it easier to skip them mentally.

![alt text](Documentation~/images/cs_inspector_05.png "C# Object Inspector")

The eye-like icon in the top-right corner of the Inspector can be used to toggle between the field- and raw-memory mode. I don't know how useful the raw-memory mode is for you, but it helped me to understand object memory, field layouts, etc while I was developing Heap Explorer. I thought there is no need to remove it.

![alt text](Documentation~/images/cs_hexview_01.png "C# Object Inspector")



# References / Referenced by

The "References" and "Referenced by" panels show what objects are connected.

![alt text](Documentation~/images/references_referencedby_01.png "References and Referenced by")

"References" shows the objects that are referenced by the selected object. "Referenced by" is basically the inverse, it shows what other objects hold a reference to the selected object.


# Paths to Root

The Root Path panel is used to show the root paths of an object instance.

A root path is the path of referrers from a specific instance to a root. A root can, for example, be a Static Field, a ScriptableObject, an AssetBundle or a GameObject.

The root path can be useful for identifying memory leaks. The root path can be used to derive why an instance has not been garbage collected, it shows what other objects hold the instance in memory.

The Root Path View lists paths to static fields first, because those are often the cause why an instance has not been garbage collected. It then lists all paths to non-static fields. The list is sorted by depth, meaning shorter paths appear in the list first. Therefore, the "Shortest Path to Root" is shown at the top of the list.

![alt text](Documentation~/images/cs_paths_to_root_01.png "Paths to root")

In the example above, Dictionary<Int32,Boolean> is kept in memory, because PerkManagerInternal holds a reference to it. And the static field PerkManager, holds a reference to the PerkManagerInternal object.

If you select a root path, the reason whether an object is kept in memory, is shown in the info message field at the bottom of the Root Path View.

Some types display a warning icon in the Root Path View. This is an indicator that the object is not automatically unloaded by Unity during a scene change for example.

Unity allows to mark UnityEngine.Object objects to prevent the engine from unloading objects automatically. This is can be done, for example, using [HideFlags](https://docs.unity3d.com/ScriptReference/HideFlags.html) or [DontDestroyOnLoad](https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html). The Root Path view displays a warning icon next to the type name, if an object is either a static field or uses one of Unity's mechanism to prevent it from being unloaded automatically.


# C# Object Duplicates

The C# Object Duplicates View analyzes managed objects for equality. If at least two objects have identical content, those objects are considered duplicates.

![alt text](Documentation~/images/cs_duplicates_01.png "C# Object Duplicates")

The view groups duplicates by type. If a type, or group, occurs more than once in this view, it means it's the same type, but different content.

For example, if you have ten "Banana" strings and ten "Apple" strings, these would be shown as two "System.String" groups. Two string groups, because both are of the same type, but with different content.

The view can be sorted by various columns. The most interesting ones likely being "Size" and "Count".  Sorting the view by "Size" allows to quickly see where most memory is wasted due duplicated objects.


# C# Delegates

Delegate's often seem to be the cause of a memory leak. I found it useful to have a dedicated view that shows all object instances that are of type System.Delegate.

The C# Delegates View is doing exactly this and behaves just like the regular C# Objects view. It lists all object instances that are a sub-class of the System.Delegate type.

![alt text](Documentation~/images/cs_delegates_01.png "C# Delegates")

If you select a delegate, its fields are displayed in the inspector (top-right corner of the window) as shown in the image below.

![alt text](Documentation~/images/cs_delegates_02.png "C# Delegates")

"m_target" is a reference to the object instance that contains the method that is being called by the delegate. "m_target" can be null, if the delegate points a static method.

Want to help me out?
I would really like to display the actual method name of the field "method". However, I didn't find a way how I would look up the name using just its address. It would be a very useful feature. If you know how to do that, please let me know!
https://forum.unity.com/threads/packedmemorysnapshot-how-to-resolve-system-delegate-method-name.516967/



# C# Delegate Targets

The C# Delegate Targets View displays managed objects that are referenced by the "m_target" field of a System.Delegate. The view behaves just like the regular C# Objects view.

Having a dedicated Delegate Targets view allows to quickly see what objects are held by delegates.

![alt text](Documentation~/images/cs_delegate_targets_01.png "C# Delegate Targets")



# C# Static Fields

The C# Static Fields view displays managed types that contain at least one static field. Selecting a type displays all of its static fields in the inspector (top-right corner).

![alt text](Documentation~/images/cs_static_fields_01.png "C# Static Fields")

| Question  | Answer      |
|----------|---------------|
| Why is a static type missing? | According to my tests, static field memory is initialized when you first access a static type. If you have a static class in your code, but it is missing in the memory snapshot, it’s likely that your application did not access this class yet. |
| Why does it not display an "Address" column? | Unity’s MemorySnapshot API does not provide at which memory address static field data is located. |
| Where is the Root Path view? | Static fields itself represent a root. There is no need for the Root Path view, because every static type is a root object. |
| Where is the "Referenced By" view? | You can’t reference a static field. However, static fields can reference other objects, that’s why it shows the “References” view. |



# C# Memory Sections

The C# Memory Sections view displays heap sections found in the memory snapshot. 

This view shows how many of those memory sections exist, which gives you an idea of how memory is fragmented. Select a memory section in the left list, to see what objects the sections contains, which are shown in the list on the right.

![alt text](Documentation~/images/cs_memory_sections_01.png "C# Memory Sections")











