# Changelog
All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [4.0.0] - 2021-05-23
### Fixed
 - Fixed search regression introduced in 3.9.0. Thanks to jojo59516 for the fix. See [PR#10](https://github.com/pschraut/UnityHeapExplorer/pull/10) for details.
 - Fixed "ArgumentException: Getting control 2's position in a group with only 2 controls when doing repaint" error that occurred very often when loading a memory snapshot.
 - Fixed high CPU utilization caused by repainting the "analyzing in progress" GUI. Thanks to jojo59516 for the fix. See [PR#11](https://github.com/pschraut/UnityHeapExplorer/pull/11) for details.
 - Fixed that closing the Heap Explorer window while analyzing a memory snapshot is in progress sometimes didn't close the window immediately. Thanks to jojo59516 for the fix. See [PR#11](https://github.com/pschraut/UnityHeapExplorer/pull/11) for details.

## [3.9.0] - 2021-04-11
### Added
 - Added ability to search for a specific type in any list that provides a search-field. Use ```t:type``` just like in Unity's search-fields too. 
If you want to search for ```RenderTexture``` types, enter ```t:RenderTexture``` in the search-field.
Thanks to jojo59516 for the implementation. See [PR#7](https://github.com/pschraut/UnityHeapExplorer/pull/7) for details.
### Fixed
 - Fixed UnityEngine.RectTransform handled as UnityEngine.Transform. Thanks to jojo59516 for the fix. See [PR#6](https://github.com/pschraut/UnityHeapExplorer/pull/6) for details.
 - Fixed sorting by size in native objects list is incorrect when searching by string. Thanks to patrickdevarney for the bug-report. See [Issue #4](https://github.com/pschraut/UnityHeapExplorer/issues/4) for details.
 - Fixed sorting by name in native objects list resulting in semi-random order.
 
## [3.8.0] - 2021-04-07
### Fixed
 - Fixed object connections lost when loading a snapshot from file. Please see [Issue #5](https://github.com/pschraut/UnityHeapExplorer/issues/5) for details. Thanks to jojo59516 for the report.
 

## [3.7.0] - 2021-03-27
### Fixed
 - Fixed "Count" display in Compare Snapshots not updating correctly after swapping two snapshots. Thanks to niqibiao for the fix. See [PR#3](https://github.com/pschraut/UnityHeapExplorer/pull/3) for details.
 - Fixed slow performance and high memory usage in rendering large lists, in particular when resizing a column or the window.  Thanks to niqibiao for the fix. See [PR#3](https://github.com/pschraut/UnityHeapExplorer/pull/3) for details.


## [3.6.0] - 2021-01-09
### Changed
 - Heap Explorer tests now run only if the ```HEAPEXPLORER_ENABLE_TESTS``` define is set. In order to run the Heap Explorer tests, you need Unity's [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) package and set the ```HEAPEXPLORER_ENABLE_TESTS``` define either [Scripting Define Symbols](https://docs.unity3d.com/Manual/class-PlayerSettingsStandalone.html#Other) in Player Settings, or in a [csc.rsp file](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html). This is a workaround for [issue #2](https://github.com/pschraut/UnityHeapExplorer/issues/2).


## [3.5.0] - 2020-10-23
### Added
 - Added CHANGELOG.md to repository
 - Added link to CHANGELOG.md in Heap Explorer start view
### Fixed
 - fix for CaputureAndSaveHeap fails in 3.4.0 due to sharing violation (thanks chris, see [PR](https://github.com/pschraut/UnityHeapExplorer/pull/1) for details)
### Removed
 - Removed item from "File > Settings" popup menu named "Settings/Ignore nested structs (workaround for bug Case 1104590)". Unity Technologies fixed this issue already and the menu option wasn't doing anything useful anymore.

## [3.4.0] - 2020-08-09
### Changed
 - updated Heap Explorer to use the new Memory Profiling API, see [here](https://forum.unity.com/threads/heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-5#post-6185146) for details.


## [3.3.0] - 2020-08-03
### Fixed
 - "The type or namespace name 'NUnit' could not be found" (thanks Sohaib and andre)
 - "You need Unity 2017.4 or newer" dialog when running Heap Explorer in Unity 2020.2 (thanks Martin)


## [3.2.0] - 2020-02-13
### Fixed
 - Fixed "Unable to find style 'TL SelectionBarCloseButton' in skin 'LightSkin' Layout" warning when opening Heap Explorer.
 - Fixed per frame warning "null texture passed to GUI.DrawTexture" when the C# Hex Viewer was open.
 - Fixed "Open Profiler" button not working due to error "ExecuteMenuItem failed because there is no menu named 'Window/Profiler'".
### Changed
 - Moved Heap Explorer menu item from "Window > Heap Explorer" to "Window > Analysis > Heap Explorer"
 - Moved documentation from PDF to [github](https://github.com/pschraut/UnityHeapExplorer). It's not complete yet, but I wanted to get this release out.
 - Moved repository from bitbucket to [github](https://github.com/pschraut/UnityHeapExplorer)
### Removed
 - Removed "The MemoryProfiling API does not work with .NET 4.x Scripting Runtime" warning, because apparently Unity Technologies fixed this issue in the meanwhile.
 - Removed Beta notice and note about Heap Explorer not working with .NET 3.5, because there is no .NET3.5 in Unity 2019.3 anymore.


## [3.1.0] - 2019-02-13
### Added
 - The bottom status bar now display the loaded snapshot filepath, as suggested [here](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-4212100).


## [3.0.0] - 2019-01-07
### Added
 - Added option to hide internal managed managed sections (sections that are not 4096bytes aligned), as explained [here](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3902371). You can toggle this behavior in the toolbar “File > Settings > Show Internal Memory Sections”.
 - Added functionality to substitute managed object addresses from a text file to Heap Explorer. This was an attempt to debug why a managed object exists in memory at run-time, but is not included in a memory snapshot. You can follow the problem [here](https://forum.unity.com/threads/is-it-possible-the-snapshots-are-missing-some-objects.607300/).


## [2.9.0] - 2018-11-28
### Added
 - Added workaround for Unity bug [Case 1104590](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3934645). Heap Explorer now ignored what appears as nested struct instances, which was causing an endless loop. This issue occurs with .NET4 ScriptingRuntime only. You can find the option to toggle this behavior in HeapExplorer toolbar > File > Settings > Ignore Nested Structs. Based on [this feedback](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3917245).
 - Added functionality to cancel a “finding root paths” operation. A “Cancel” button is now available in the root paths panel.
### Changed
 - Increased loop guard limit, when trying to find root paths from 100000 to 300000 iterations. This avoids [this issue](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3934858).
 - When changing the selection in the C#/C++ objects view, any running “finding root paths” job is aborted, rather than waited for completion. This makes the UI feel more responsive.
 - Changed order in which various jobs run in Heap Explorer, which makes the UI slightly more responsive.


## [2.8.0] - 2018-11-25
### Added
 - Added functionality to stop/cancel a processing step when Heap Explorer is analyzing a memory snapshot. This is actually a workaround for an issue that sounds like an infinite loop as [reported here](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3917245). I’m still interested in the memory snapshot that causes the issue to actually fix it though.
 - C# Memory Sections View: Added raw memory view to inspect the actual bytes of a memory section.
 - Added functionality to export parts of the memory snapshot to CSV. You find it in the “Views” popupmenu, named “CSV Export”. Based on [this feedback](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3902371).
### Changed
 - C# Memory Sections View: Changed phrase "N memory sections fragmented across X.XXGB" to "Nmemory sections within an X.XXGB address space", based on [this feedback](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3919363).
### Removed
 - Overview View: Removed memory sections graph, based on [this feedback](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-3#post-3902371).


## [2.7.0] - 2018-09-09
### Added
 - Added warning if project uses .NET 4.x Scripting Runtime, because Unity’s MemoryProfiling API does not work with that at the time of writing. [See here for details](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-2#post-3655840).
 - Added better error messages for known memory snapshot issues, like empty typeDescriptions array.
 - Added error message dialog and then quit Heap Explorer if an unrecoverable error occurrs.
 - Added error message if Heap Explorer is unable to parse the editor version, to get at the bottom of [this problem](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-2#post-3635089).


## [2.6.0] - 2018-09-02
### Added
 - Added “Exclude NativeObject connections when capturing a memory snapshot” option to Heap Explorers “File > Settings” menu. Please read the “Exclude NativeObject connections” documentation when you might want to activate it.


## [2.5.0] - 2018-08-28
### Added
 - Added Debug.Log if the PackedConnection array creation would cause an out-of-memory exception. [See forum post](https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/page-2#post-3615523)


## [2.4.0] - 2018-08-11
### Added
 - Loading memory snapshots displays more information about the progress now.
### Fixed
 - Overview View: Fixed  negative size display if object group exceeded the maximum int limit.
 - C++ Objects View: Fixed negative size display, if an object group exceeded the maximum int limit.
 - C# Objects View: Fixed negative size display, if an object group exceeded the maximum int limit.
 - Memory Sections: Fixed saving memory sections as file which are larger than 2gb.
 

## [2.3.0] - 2018-07-07
### Added
 - C++ Objects View: Added asset preview. Memory snapshots do not contain asset memory, thus the preview is generated from the asset in the project instead rather than the memory snapshot!
 - C++ Objects View: Reworked list filtering. It allows to change filter settings without the list being recreated every time you change a single option. You have to apply those changes now. This helps to save time when filtering big memory snapshots.
 - C++ Objects View: If any list filtering is active, the “Filter” button in the toolbar uses a different color, to make it obvious filtering is in place.
### Changed
 - Brief Overview: Changing wording of the Memory Section description.
### Fixed
 - C++ Objects View: Fixed “Type” display of MonoBehaviour, which now displays the actual derived type rather than just MonoBehaviour always.
 - C++ Objects View: Fixed “Type” column sometimes showing the object name rather than type.


## [2.2.0] - 2018-06-23
### Added
 - C++ Objects View: Added popup menu to toolbar to exclude native objects from the list, depending on whether the native object is an asset, a scene-object or a run-time-object.
 - C++ Objects View: Added popup menu to toolbar to exclude native objects from the list, depending on whether the native object is marked as “Destroy on load” or “Do NOT destroy on load”.
### Changed
 - C++ Objects View: Replaced generic “C++” icon with icons that indicate whether it’s an asset, scene-object or run-time-object.
 - C++ Objects View: Display icon in object inspector in the top-right corner.
### Fixed
 - C++ Objects View: Fixed count and size display in bottom status bar.


## [2.1.0] - 2018-06-16
### Added
 - Added “C++ Asset Duplicates” view
 - Added context menu if you right-click the “Name” column in the C++ objects view. It provides functionality to find assets of the C++ object/asset name in the project.


## [2.0.0] - 2018-05-25
### Added
 - C# Memory Sections: Added visualization of managed memory section fragmentation


## [1.9.0] - 2018-05-21
### Added
 - Overview: Brought back “GCHandles” and “Virtual Machine Information”
### Changed
 - When loading a snapshot from the “Start Page”, it opens the “Brief Overview” afterwards, rather thanthe “C# Objects” view.
 - 

## [1.8.0] - 2018-05-21
### Added
 - Overview: Added percentage % next to the size
 - Overview: Added visualization of managed heap fragmentation in the operating system memory
### Fixed
 - Overview: Fixed some layout issues
### Removed
 - Overview: Removed GCHandles and VirtualMachine Information from the overview, as this wasn’t very interesting nor something that is normally using much memory.


## [1.7.0] - 2018-05-19
### Added
 - Compare Snapshots: Added “Swap” button to swap snapshot A <> B
 - Compare Snapshots: Loading Snapshot B now displays the same loading status information as when loading Snapshot A.
 - C# Static Fields: Added toolbar menu with functionality to save the memory of a selected static field as a file
 - C# Memory Sections: Added toolbar menu with functionality to save the all C# memory sections as a file.
 - Debugged why some memory sections do not contain references to managed objects, but contain non-zero bytes. See “Empty C# Memory Sections” in under Known Issues.
### Fixed
 - Compare Snapshots: Snapshot B doesn’t get unloaded anymore, when loading or capturing snapshot A, as suggested multiple times.
### Changed
 - Moved statics to bottom status bar.

## [1.6.0] - 2018-05-08
### Changed
 - “Compare Snapshots”, changed diff from “A - B” to “B - A” comparison.
 - Loading a snapshot restores the previously active view when done.
### Fixed
 - Fixed being unable to select a memory section by clicking on its address in the “C# Memory Sections” view.


## [1.5.0] - 2018-05-07
### Fixed
 - Added another workaround for “Array out of index” exception.


## [1.4.0] - 2018-05-07
### Fixed
 - Added workaround for “Array out of index” exception, if the element-type of an array could not be detected. In this case this object outputs an error during heap reconstruction and from then on it’s ignored. Please send me the memory snapshot if you run into this error.


## [1.3.0] - 2018-05-06
### Added
 - In Heap Explorer toolbar, added “Capture > Open Profiler”. This opens the Unity profiler window, which allows you to connect to a different target. It’s simply a convenience feature, if you figure the editor is not connected to the correct player. This saves a few mouse clicks to get to the Unity Profilerto connect to a different player.
 - In Heap Explorer toolbar, added “File > Recent”, which allows to re-open the “most recently used” snapshots.
### Changed
 - The “Load” button in the “Compare Snapshot” view now features a “most recently used” snapshot list. This allows you to switch between previously saved snapshots with fewer mouse clicks.


## [1.2.0] - 2018-05-05
### Added
 - In Heap Explorer toolbar, added “Capture and Save”, which just saves the captured snapshot without automatically analyzing it.
### Changed
 - In Heap Explorer toolbar, renamed “Capture” to “Capture and Analyze”.
 

## [1.1.0] - 2018-05-02
### Added
 - Added "Capture > Open Profiler". This opens the Unity profiler window, which allows you to connect to a different target. It’s simply a convenience feature, if you figure the editor is not connected to the correct player. This saves a few mouse clicks to get to the Unity Profiler to connect to a different player.
 
