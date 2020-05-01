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




# Can I use this tool even when I work on a commercial project?

Yes, you can use Heap Explorer to debug, profile and analyze your hobby-, Indie- and commercial applications for free. You do not have to pay me anything.

If however Heap Explorer helped you, I would appreciate a mentioning in your credits screen. Something like "Heap Explorer by Peter Schraut" would be very much appreciated from my side, but is not required.
You can use Heap Explorer for free, without having to give me credit or mention you used the tool at all.



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



