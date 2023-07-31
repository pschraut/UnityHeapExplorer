//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
#pragma warning disable 0414
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Threading;
#if UNITY_2022_2_OR_NEWER
using Unity.Profiling.Memory;
#else
using UnityEngine.Profiling.Memory.Experimental;
#endif

namespace HeapExplorer
{
    public class HeapExplorerWindow : EditorWindow
    {
        /// <summary>
        /// Path of the active memory snapshot or an empty string if no snapshot is active.
        /// </summary>
        public string snapshotPath
        {
            get;
            set;
        }

        /// <summary>
        /// The active memory snapshot or null if no snapshot is active.
        /// </summary>
        public PackedMemorySnapshot snapshot
        {
            get
            {
                return m_Heap;
            }
            set
            {
                m_Heap = value;
            }
        }

        [NonSerialized] TestVariables m_TestVariables = new TestVariables(); // Allows me to easily check/test various types when capturing a snapshot in the editor.
        [NonSerialized] bool m_IsCapturing; // Whether the tool is currently capturing a memory snapshot
        [NonSerialized] GotoHistory m_GotoHistory = new GotoHistory();
        [NonSerialized] PackedMemorySnapshot m_Heap; // The active memory snapshot
        [NonSerialized] List<HeapExplorerView> m_Views = new List<HeapExplorerView>();
        [NonSerialized] HeapExplorerView m_ActiveView; // The view that is currently active
        [NonSerialized] System.Threading.Thread m_Thread; // The tool uses a thread to execute various jobs to not block the main UI.
        [NonSerialized] Rect m_FileToolbarButtonRect; // The rect of the File button in the toolbar menu. Used as position to open its popup menu.
        [NonSerialized] Rect m_ViewToolbarButtonRect; // The rect of the View button in the toolbar menu. Used as position to open its popup menu.
        [NonSerialized] Rect m_CaptureToolbarButtonRect; // The rect of the Capture button in the toolbar menu. Used as position to open its popup menu.
        [NonSerialized] List<AbstractThreadJob> m_ThreadJobs = new List<AbstractThreadJob>(); // Jobs to run on a background thread
        [NonSerialized] List<AbstractThreadJob> m_IntegrationJobs = new List<AbstractThreadJob>(); // These are completed thread jobs that are not being integrated on the main-thread
        [NonSerialized] bool m_Repaint; // Threads write to this variable rather than calling window.Repaint()
        [NonSerialized] string m_ErrorMsg = ""; // If an error occured, threads write to this string.
        [NonSerialized] string m_AutoSavePath = ""; //
        [NonSerialized] string m_StatusBarString = "";
        [NonSerialized] string m_BusyString = "";
        [NonSerialized] int m_BusyDraws;
        [NonSerialized] List<Exception> m_Exceptions = new List<Exception>(); // If exception occur in threaded jobs, these are collected and logged on the main thread
        [NonSerialized] bool m_CloseDueToError; // If set to true, will close the editor during the next Update
        [NonSerialized] double m_LastRepaintTimestamp; // The EditorApplication.timeSinceStartup when a Repaint() was issued

        static List<System.Type> s_ViewTypes = new List<Type>();

        public bool isClosing { get; private set; }

        bool useThreads
        {
            get
            {
                return EditorPrefs.GetBool("HeapExplorerWindow.m_UseThread", true);
            }
            set
            {
                EditorPrefs.SetBool("HeapExplorerWindow.m_UseThread", value);
            }
        }

        bool debugViewMenu
        {
            get
            {
                return EditorPrefs.GetBool("HeapExplorerWindow.debugViewMenu", false);
            }
            set
            {
                EditorPrefs.SetBool("HeapExplorerWindow.debugViewMenu", value);
            }
        }

        string autoSavePath
        {
            get
            {
                return EditorPrefs.GetString("HeapExplorerWindow.autoSavePath", "");
            }
            set
            {
                EditorPrefs.SetString("HeapExplorerWindow.autoSavePath", value);
            }
        }

        bool showInternalMemorySections
        {
            get
            {
                return EditorPrefs.GetBool("HeapExplorerWindow.showInternalMemorySections", true);
            }
            set
            {
                ManagedHeapSectionsView.s_ShowInternalSections = value;
                EditorPrefs.SetBool("HeapExplorerWindow.showInternalMemorySections", value);
            }
        }

        HeapExplorerView welcomeView
        {
            get
            {
                return FindView<WelcomeView>();
            }
        }

        /// <summary>
        /// Register a view to be added to the Heap Explorer 'Views' menu.
        /// </summary>
        /// <typeparam name="T">The type of the HeapExplorerView to register.</typeparam>
        public static void Register<T>() where T : HeapExplorerView
        {
            var type = typeof(T);
            if (s_ViewTypes.Contains(type))
                return;

            s_ViewTypes.Add(type);
        }

        [MenuItem("Window/Analysis/Heap Explorer", priority = 5)]
        static void Create()
        {
            if (!HeEditorUtility.IsVersionOrNewer(2019, 3))
            {
                if (EditorUtility.DisplayDialog(HeGlobals.k_Title,
                        $"{HeGlobals.k_Title} requires Unity 2019.3 or newer.", "Forum", "Close"))
                    Application.OpenURL(HeGlobals.k_ForumUrl);
                return;
            }

            EditorWindow.GetWindow<HeapExplorerWindow>();
        }

        void OnEnable()
        {
            isClosing = false;
            titleContent = new GUIContent(HeGlobals.k_Title);
            minSize = new Vector2(800, 600);
            snapshotPath = "";
            showInternalMemorySections = showInternalMemorySections;
            m_LastRepaintTimestamp = 0;
            m_Repaint = true;

            m_ThreadJobs = new List<AbstractThreadJob>();
            m_Thread = new System.Threading.Thread(ThreadLoop);
            m_Thread.Start();

            CreateViews();

            EditorApplication.update += OnEditorApplicationUpdate;
        }

        void OnDisable()
        {
            lock (m_ThreadJobs)
            {
                // ask thread to exit
                isClosing = true;
                if (m_Heap != null && m_Heap.isProcessing)
                    m_Heap.abortActiveStepRequested = true;
                Monitor.Pulse(m_ThreadJobs);
            }
            m_Thread.Join(); // wait for thread exit
            m_Thread = null;

            EditorApplication.update -= OnEditorApplicationUpdate;

            DestroyViews();
        }

        void OnEditorApplicationUpdate()
        {
            if (m_Repaint || (m_Heap != null && m_Heap.isBusy))
            {
                if (m_LastRepaintTimestamp+0.05f < EditorApplication.timeSinceStartup)
                {
                    m_Repaint = false;
                    m_LastRepaintTimestamp = EditorApplication.timeSinceStartup;
                    Repaint();
                }
            }

            if (m_CloseDueToError)
            {
                EditorUtility.DisplayDialog("Heap Explorer - ERROR", "An error occured. Please check Unity's Debug Console for more information.", "OK");
                Close();
            }
        }

        void CreateViews()
        {
            foreach (var type in s_ViewTypes)
            {
                CreateView(type);
            }

            ActivateView(welcomeView);
        }

        public T FindView<T>() where T : HeapExplorerView
        {
            foreach (var view in m_Views)
            {
                var v = view as T;
                if (v != null)
                    return v;
            }

            return null;
        }

        void ResetViews()
        {
            if (m_ActiveView != null)
            {
                m_ActiveView.Hide();
                m_ActiveView = null;
            }

            for (var n = 0; n < m_Views.Count; ++n)
                m_Views[n].EvictHeap();
        }

        void DestroyViews()
        {
            if (m_ActiveView != null)
            {
                m_ActiveView.Hide();
                m_ActiveView = null;
            }

            for (var n = 0; n < m_Views.Count; ++n)
                m_Views[n].OnDestroy();
            m_Views.Clear();
        }

        HeapExplorerView CreateView(System.Type type)
        {
            var view = (HeapExplorerView)Activator.CreateInstance(type);
            view.window = this;
            view.Awake();
            m_Views.Add(view);
            return view;
        }

        public void OnGoto(GotoCommand command)
        {
            command.fromView = m_ActiveView;
            m_GotoHistory.Add(command, command);

            GotoInternal(command, false);
        }

        void GotoInternal(GotoCommand command, bool restoreFromView)
        {
            if (restoreFromView)
            {
                if (command.fromView != null)
                {
                    ActivateView(command.fromView);
                    command.fromView.RestoreCommand(command);
                }
                return;
            }

            if (command.toView != null)
            {
                ActivateView(command.toView);
                command.toView.RestoreCommand(command);
                return;
            }

            var sortedViews = new List<HeapExplorerView>(m_Views);
            sortedViews.Sort(delegate (HeapExplorerView x, HeapExplorerView y)
            {
                var xx = x.CanProcessCommand(command);
                var yy = y.CanProcessCommand(command);
                return yy.CompareTo(xx);
            });

            ActivateView(sortedViews[0]);
            sortedViews[0].RestoreCommand(command);
        }

        void OnGUI()
        {
            if (!string.IsNullOrEmpty(m_ErrorMsg))
            {
                if (Event.current.type != EventType.Layout)
                {
                    m_Repaint = true;
                    return;
                }

                EditorUtility.DisplayDialog(HeGlobals.k_Title + " - ERROR", m_ErrorMsg, "OK");
                Close();
                return;
            }

            for (var n = 0; n < m_Exceptions.Count; ++n)
                Debug.LogException(m_Exceptions[n]);
            m_Exceptions.Clear();

            if (m_IntegrationJobs.Count > 0 && Event.current.type == EventType.Layout)
            {
                lock (m_IntegrationJobs)
                {
                    if (m_IntegrationJobs.Count > 0)
                    {
                        m_IntegrationJobs[0].IntegrateFunc();
                        m_IntegrationJobs.RemoveAt(0);
                    }
                }
            }

            var abortButton = false;

            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(position.width), GUILayout.MaxHeight(position.height)))
            {
                using (new EditorGUI.DisabledGroupScope(m_IsCapturing || (m_Heap != null && m_Heap.isBusy) || (m_BusyDraws > 0)))
                {
                    m_BusyDraws--;
                    m_BusyString = "";

                    DrawToolbar();

                    if (m_Heap != null)
                    {
                        if (m_Heap.isBusy)
                        {
                            SetBusy(m_Heap.busyString);
                            abortButton = true;
                        }

                        if (!m_Heap.isBusy && m_ActiveView == null && Event.current.type == EventType.Layout)
                            RestoreView();
                    }

                    DrawView();
                    GUILayout.FlexibleSpace();
                    DrawStatusBar();
                }
            }

            if (!string.IsNullOrEmpty(m_BusyString))
            {
                m_Repaint = true;
                DrawBusy(abortButton);
            }
        }

        public void SetBusy(string text)
        {
            m_BusyString = text;
            m_BusyDraws = 3;
        }

        public void SetStatusbarString(string text)
        {
            m_StatusBarString = text;
        }

        void DrawBusy(bool drawAbortButton)
        {
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            var iconSize = 128;

            var pivotPoint = new Vector2(this.position.width / 2, this.position.height / 2);
            GUIUtility.RotateAroundPivot(Time.realtimeSinceStartup * 45, pivotPoint);

            var r = new Rect(pivotPoint - new Vector2(0.5f, 0.5f) * iconSize, Vector2.one * iconSize);
            GUI.color = new Color(1, 1, 1, 1.0f);
            GUI.DrawTexture(r, HeEditorStyles.loadingImageBig);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;

            r = new Rect(new Vector2(0, pivotPoint.y - iconSize), new Vector2(position.width, iconSize * 0.5f));
            GUI.Label(r, m_BusyString, HeEditorStyles.loadingLabel);

            r.x = pivotPoint.x - iconSize * 0.5f;
            r.y += iconSize * 1.5f;
            r.width = iconSize;
            r.height = 32;

            if (m_Heap != null && m_Heap.isProcessing)
            {
                using (new EditorGUI.DisabledGroupScope(m_Heap.abortActiveStepRequested))
                {
                    if (GUI.Button(r, "Cancel..."))
                    {
                        if (EditorUtility.DisplayDialog(HeGlobals.k_Title, "If you cancel the current processing step, the tool most likely shows incorrect or incomplete data.\n\nOnly the current step is being canceled, the tool then continues to move on to the next step.\n\nStopping might not take place immediately.", "Stop", "Keep going"))
                            m_Heap.abortActiveStepRequested = true;
                    }
                }
            }
        }

        void FreeMem()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        void ActivateView(object userData)
        {
            var view = userData as HeapExplorerView;
            if (m_ActiveView != null)
                m_ActiveView.Hide();

            m_StatusBarString = "";
            m_ActiveView = view;

            if (m_ActiveView != null)
                m_ActiveView.Show(m_Heap);

            m_Repaint = true;
        }

        void DrawView()
        {
            if (m_ActiveView == null || (m_Heap != null && m_Heap.isBusy))
                return;

            UnityEngine.Profiling.Profiler.BeginSample(m_ActiveView.GetType().Name);

            m_ActiveView.OnGUI();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        void DrawStatusBar()
        {
            using (new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(m_StatusBarString);
                GUILayout.FlexibleSpace();
                GUILayout.Label(snapshotPath ?? "");
            }

            // Darken the status area a little
            if (Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                r.height += 2; r.x -= 4; r.width += 8;
                var oldcolor = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.1f);
                GUI.DrawTexture(r, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = oldcolor;
            }
        }

        void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUI.BeginDisabledGroup(!m_GotoHistory.HasBack());
                if (GUILayout.Button(new GUIContent(HeEditorStyles.backwardImage, "Navigate Backward"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    var cmd = m_GotoHistory.Back();
                    if (cmd != null)
                        GotoInternal(cmd, true);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!m_GotoHistory.HasForward());
                if (GUILayout.Button(new GUIContent(HeEditorStyles.forwardImage, "Navigate Forward"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    var cmd = m_GotoHistory.Forward();
                    if (cmd != null)
                        GotoInternal(cmd, false);
                }
                EditorGUI.EndDisabledGroup();


                if (GUILayout.Button("File", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open..."), false, LoadFromFile);

                    for (int n = 0; n < HeMruFiles.count; ++n)
                    {
                        var path = HeMruFiles.GetPath(n);

                        if (string.IsNullOrEmpty(path))
                            continue;

                        if (!System.IO.File.Exists(path))
                            continue;

                        menu.AddItem(new GUIContent($"Recent/{(n + 1)}     {path.Replace('/', '\\')}"), false, delegate (System.Object obj)
                        {
                            var p = obj as string;
                            LoadFromFile(p);
                        }, path);
                    }
                    menu.AddSeparator("Recent/");
                    menu.AddItem(new GUIContent("Recent/Clear list"), false, delegate ()
                    {
                        if (EditorUtility.DisplayDialog("Clear list...", "Do you want to clear the most recently used files list?", "Clear", "Cancel"))
                            HeMruFiles.RemoveAll();
                    });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Close Snapshot"), false, CloseFile);
                    menu.AddSeparator("");
                    if (m_Heap == null)
                        menu.AddDisabledItem(new GUIContent("Save as..."));
                    else
                        menu.AddItem(new GUIContent("Save as..."), false, SaveToFile);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("New Window"), false, delegate ()
                    {
                        var wnd = EditorWindow.CreateInstance<HeapExplorerWindow>();
                        wnd.Show();
                    });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Settings/Use Multi-Threading"), useThreads, delegate ()
                    {
                        useThreads = !useThreads;
                    });
                    menu.AddItem(new GUIContent("Settings/Debug View Menu"), debugViewMenu, delegate ()
                    {
                        debugViewMenu = !debugViewMenu;
                    });
                    menu.AddItem(new GUIContent("Settings/Show unaligned memory sections (removes MonoMemPool sections)"), showInternalMemorySections, delegate ()
                    {
                        showInternalMemorySections = !showInternalMemorySections;
                    });
                    menu.DropDown(m_FileToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_FileToolbarButtonRect = GUILayoutUtility.GetLastRect();


                EditorGUI.BeginDisabledGroup(m_Heap == null || m_Heap.isBusy);
                if (GUILayout.Button("View", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
                {
                    m_Views.Sort(delegate (HeapExplorerView x, HeapExplorerView y)
                    {
                        var value = x.viewMenuOrder.CompareTo(y.viewMenuOrder);
                        if (value == 0)
                            value = string.Compare(x.titleContent.text, y.titleContent.text);
                        return value;
                    });

                    var prevOrder = -1;
                    var menu = new GenericMenu();
                    foreach (var view in m_Views)
                    {
                        if (view.viewMenuOrder < 0)
                            continue;
                        if (prevOrder == -1)
                            prevOrder = view.viewMenuOrder;

                        var p0 = prevOrder / 100;
                        var p1 = view.viewMenuOrder / 100;
                        if (p1 - p0 >= 1)
                        {
                            var i = view.titleContent.text.LastIndexOf("/");
                            if (i == -1)
                                menu.AddSeparator("");
                            else
                                menu.AddSeparator(view.titleContent.text.Substring(0, i));
                        }
                        prevOrder = view.viewMenuOrder;

                        var c = new GUIContent(view.titleContent);
                        if (debugViewMenu)
                            c.text = string.Format("{2}   [viewMenuOrder={0}, type={1}]", view.viewMenuOrder, view.GetType().Name, c.text);

                        menu.AddItem(c, m_ActiveView == view, (GenericMenu.MenuFunction2)delegate (System.Object o)
                        {
                            if (o == m_ActiveView)
                                return;

                            var v = o as HeapExplorerView;
                            var c0 = m_ActiveView.GetRestoreCommand(); c0.fromView = m_ActiveView;
                            var c1 = v.GetRestoreCommand(); c1.toView = v;
                            m_GotoHistory.Add(c0, c1);
                            ActivateView(v);
                        }, view);
                    }

                    menu.DropDown(m_ViewToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_ViewToolbarButtonRect = GUILayoutUtility.GetLastRect();
                EditorGUI.EndDisabledGroup();


                var connectedProfiler = UnityEditorInternal.ProfilerDriver.GetConnectionIdentifier(UnityEditorInternal.ProfilerDriver.connectedProfiler);
                if (GUILayout.Button(new GUIContent("Capture", HeEditorStyles.magnifyingGlassImage), EditorStyles.toolbarDropDown, GUILayout.Width(80)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent($"Capture and Save '{connectedProfiler}'..."), false, CaptureAndSaveHeap);
                    menu.AddItem(new GUIContent($"Capture and Analyze '{connectedProfiler}'"), false, CaptureAndAnalyzeHeap);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent(string.Format("Open Profiler")), false, delegate () { HeEditorUtility.OpenProfiler(); });
                    menu.DropDown(m_CaptureToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_CaptureToolbarButtonRect = GUILayoutUtility.GetLastRect();

                if (m_ActiveView != null)
                    m_ActiveView.OnToolbarGUI();
            }
        }

        void SaveToFile()
        {
            var path = EditorUtility.SaveFilePanel("Save", "", "memory", "heap");
            if (string.IsNullOrEmpty(path))
                return;
            HeMruFiles.AddPath(path);

            m_Heap.SaveToFile(path);
            snapshotPath = path;
        }

        void CloseFile()
        {
            snapshotPath = "";
            m_Heap = null;
            Reset(true);
            ActivateView(welcomeView);
            FreeMem();
        }

        void LoadFromFile()
        {
            var path = EditorUtility.OpenFilePanel("Load", "", "heap");
            if (string.IsNullOrEmpty(path))
                return;

            LoadFromFile(path);
        }

        public void LoadFromFile(string path)
        {
            SaveView();
            FreeMem();
            HeMruFiles.AddPath(path);
            Reset();
            m_Heap = null;

            if (useThreads)
            {
                var job = new LoadThreadJob
                {
                    path = path,
                    threadFunc = LoadFromFileThreaded
                };

                ScheduleJob(job);
            }
            else
            {
                LoadFromFileThreaded(path);
            }
        }

        void ThreadLoop()
        {
            while (!isClosing)
            {
                AbstractThreadJob job = null;

                lock (m_ThreadJobs)
                {
                    while (m_ThreadJobs.Count == 0)
                    {
                        Monitor.Wait(m_ThreadJobs); // block myself, waiting for jobs
                        if (isClosing)
                            return; // exit
                    }

                    job = m_ThreadJobs[0];
                    m_ThreadJobs.RemoveAt(0);
                }

                job.state = AbstractThreadJob.State.Running;
                try
                {
                    m_Repaint = true;
                    job.ThreadFunc();
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (System.Exception e)
                {
                    m_Exceptions.Add(e);
                }
                finally
                {
                    m_Repaint = true;
                    job.state = AbstractThreadJob.State.Completed;
                }

                lock (m_IntegrationJobs)
                {
                    m_IntegrationJobs.Add(job);
                }
            }
        }

        void LoadFromFileThreaded(object userData)
        {
            var filePath = userData as string;
            snapshotPath = filePath;

            m_Heap = new PackedMemorySnapshot();
            if (!m_Heap.LoadFromFile(filePath))
            {
                m_ErrorMsg = string.Format("Could not load memory snapshot.");
                return;
            }

            m_Heap.Initialize(filePath);
        }

        void SaveView()
        {
            EditorPrefs.SetString("HeapExplorerWindow.restoreView", "");
            if (m_ActiveView != null && m_ActiveView != welcomeView)
            {
                EditorPrefs.SetString("HeapExplorerWindow.restoreView", m_ActiveView.GetType().Name);
            }
        }

        void RestoreView()
        {
            var viewType = EditorPrefs.GetString("HeapExplorerWindow.restoreView", "");

            HeapExplorerView view = null;
            for (int n = 0; n < m_Views.Count; ++n)
            {
                if (m_Views[n].GetType().Name == viewType)
                {
                    view = m_Views[n];
                    break;
                }
            }

            if (view == null)
                view = FindView<OverviewView>();

            ActivateView(view);
        }

        void Reset(bool destroy = false)
        {
            if (destroy)
            {
                DestroyViews();
                CreateViews();
            }
            else
            {
                ResetViews();
            }

            m_GotoHistory = new GotoHistory();
            ActivateView(null);
        }

        /// <summary>
        /// Same flags as Unity memory profiler.
        /// </summary>
        const CaptureFlags CAPTURE_FLAGS =
            CaptureFlags.ManagedObjects
            | CaptureFlags.NativeObjects
            | CaptureFlags.NativeAllocations
            | CaptureFlags.NativeAllocationSites
            | CaptureFlags.NativeStackTraces;

        void CaptureAndSaveHeap()
        {
            if (string.IsNullOrEmpty(autoSavePath))
                autoSavePath = System.IO.Path.Combine(Application.persistentDataPath, "memory.heap");

            var path = EditorUtility.SaveFilePanel("Save snapshot as...", System.IO.Path.GetDirectoryName(autoSavePath), System.IO.Path.GetFileNameWithoutExtension(autoSavePath), "heap");
            if (string.IsNullOrEmpty(path))
                return;
            autoSavePath = path;

            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Receiving memory...", 0.0f);
            try
            {
                FreeMem();
                m_IsCapturing = true;

                string snapshotPath = System.IO.Path.ChangeExtension(path, "snapshot");
                MemoryProfiler.TakeSnapshot(snapshotPath, OnHeapReceivedSaveOnly, CAPTURE_FLAGS);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_Repaint = true;
            }
        }

        void OnHeapReceivedSaveOnly(string path, bool captureResult) {
            const float BASE_PROGRESS = 0.5f;
            const float PROGRESS_LEFT = 0.4f;
            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Saving memory...", BASE_PROGRESS);
            try
            {
                var args = new MemorySnapshotProcessingArgs(
                    UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot.Load(path),
                    maybeUpdateUI: (stepName, index, steps) => {
                        var percentage = (float) index / (steps - 1);
                        var totalPercentage = BASE_PROGRESS + percentage * PROGRESS_LEFT;
                        EditorUtility.DisplayProgressBar(HeGlobals.k_Title, stepName, totalPercentage);
                    }
                );

                var heap = PackedMemorySnapshot.FromMemoryProfiler(args);
                EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Saving memory to file...", 0.95f);
                heap.SaveToFile(autoSavePath);
                HeMruFiles.AddPath(autoSavePath);
                ShowNotification(new GUIContent($"Memory snapshot saved as\n'{autoSavePath}'"));
            }
            catch
            {
                m_CloseDueToError = true;
                throw;
            }
            finally
            {
                m_IsCapturing = false;
                m_Repaint = true;
                EditorUtility.ClearProgressBar();
            }
        }

        void CaptureAndAnalyzeHeap()
        {
            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Receiving memory...", 0.0f);
            try
            {
                SaveView();
                FreeMem();
                Reset();
                m_IsCapturing = true;

                var path = FileUtil.GetUniqueTempPathInProject();
                MemoryProfiler.TakeSnapshot(path, OnHeapReceived, CAPTURE_FLAGS);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_Repaint = true;
            }
        }

        void OnHeapReceived(string path, bool captureResult)
        {
            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Reading memory...", 0.5f);
            try
            {
                m_Heap = null;

                if (useThreads)
                {
                    var job = new ReceiveThreadJob {
                        threadFunc = () => ReceiveHeapThreaded(new MemorySnapshotProcessingArgs(
                            UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot.Load(path)
                        ))
                    };
                    ScheduleJob(job);
                }
                else
                {
                    const float BASE_PROGRESS = 0.75f;
                    const float PROGRESS_LEFT = 0.25f;
                    EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Analyzing memory...", BASE_PROGRESS);
                    var sshot = UnityEditor.Profiling.Memory.Experimental.PackedMemorySnapshot.Load(path);
                    ReceiveHeapThreaded(new MemorySnapshotProcessingArgs(
                        sshot,
                        maybeUpdateUI: (stepName, index, steps) => {
                            var percentage = (float) index / (steps - 1);
                            var totalPercentage = BASE_PROGRESS + percentage * PROGRESS_LEFT;
                            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, stepName, totalPercentage);
                        }
                    ));
                }
            }
            finally
            {
                snapshotPath = $"Captured Snapshot at {DateTime.Now.ToShortTimeString()}";
                m_IsCapturing = false;
                m_Repaint = true;
                EditorUtility.ClearProgressBar();
            }
        }

        void ReceiveHeapThreaded(MemorySnapshotProcessingArgs args) {
            try {
                m_Heap = PackedMemorySnapshot.FromMemoryProfiler(args);
                m_Heap.Initialize();
            }
            catch {
                m_CloseDueToError = true;
                throw;
            }
        }

        public void ScheduleJob(AbstractThreadJob job)
        {
            job.state = AbstractThreadJob.State.Queued;

            lock (m_ThreadJobs)
            {
                // Remove unstarted jobs of same type
                for (var n = m_ThreadJobs.Count - 1; n >= 0; --n)
                {
                    var j = m_ThreadJobs[n];
                    if (j.state == AbstractThreadJob.State.Queued && j.GetType() == job.GetType())
                    {
                        m_ThreadJobs.RemoveAt(n);
                        continue;
                    }
                }

                m_ThreadJobs.Add(job);
                Monitor.Pulse(m_ThreadJobs); // notify thread that jobs here
            }

            m_Repaint = true;
        }
    }

    class LoadThreadJob : AbstractThreadJob
    {
        public string path;
        public Action<object> threadFunc;

        public override void ThreadFunc()
        {
            threadFunc.Invoke(path);
        }
    }

    class ReceiveThreadJob : AbstractThreadJob
    {
        public Action threadFunc;

        public override void ThreadFunc() {
            threadFunc.Invoke();
        }
    }

    public abstract class AbstractThreadJob
    {
        public enum State
        {
            None,
            Queued,
            Running,
            Completed
        };

        public State state;
        public abstract void ThreadFunc();

        public virtual void IntegrateFunc()
        {

        }
    }
}
