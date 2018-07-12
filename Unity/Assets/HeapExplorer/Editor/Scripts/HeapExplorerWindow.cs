using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    public class HeapExplorerWindow : EditorWindow
    {
#pragma warning disable 0414
        [NonSerialized] bool m_isCapturing;
        [NonSerialized] GotoHistory m_gotoHistory = new GotoHistory();
        [NonSerialized] PackedMemorySnapshot m_heap;
        [NonSerialized] List<HeapExplorerView> m_views = new List<HeapExplorerView>();
        [NonSerialized] HeapExplorerView m_activeView;
        [NonSerialized] System.Threading.Thread m_thread;
        [NonSerialized] Rect m_fileToolbarButtonRect;
        [NonSerialized] Rect m_viewToolbarButtonRect;
        [NonSerialized] Rect m_captureToolbarButtonRect;
        [NonSerialized] Rect m_customToolbarButtonRect;
        [NonSerialized] List<AbstractThreadJob> m_threadJobs = new List<AbstractThreadJob>();
        [NonSerialized] List<AbstractThreadJob> m_integrationJobs = new List<AbstractThreadJob>();
        [NonSerialized] bool m_repaint;
        //[NonSerialized] Test_Editor m_testVariables = new Test_Editor();
        [NonSerialized] ManagedDelegatesView m_managedDelegatesView;
        [NonSerialized] string m_ErrorMsg;
        [NonSerialized] bool m_UseThread = true;
        [NonSerialized] string m_autoSavePath="";
        [NonSerialized] string m_statusBarString="";
        [NonSerialized] string m_busyString="";
        [NonSerialized] int m_busyDraws;
#pragma warning restore 0414

        public string snapshotPath
        {
            get;
            set;
        }

        public PackedMemorySnapshot snapshot
        {
            get
            {
                return m_heap;
            }
            set
            {
                m_heap = value;
            }
        }

        static List<System.Type> s_ViewTypes = new List<Type>();

        public static void Register<T>() where T : HeapExplorerView
        {
            var type = typeof(T);
            if (s_ViewTypes.Contains(type))
                return;

            s_ViewTypes.Add(type);
        }

        [MenuItem("Window/Heap Explorer")]
        static void Create()
        {
            var supported = true;

            var numbers = Application.unityVersion.Split('.');
            if (numbers != null && numbers.Length >= 2)
            {
                int major, minor;
                if (!int.TryParse(numbers[0], out major)) major = -1;
                if (!int.TryParse(numbers[1], out minor)) minor = -1;

                if (major < 2017) supported = false;
                if (major == 2017 && minor < 3) supported = false;
            }

            if (!supported)
            {
                if (EditorUtility.DisplayDialog(HeGlobals.k_Title, string.Format("{0} requires Unity 2017.3 or newer.", HeGlobals.k_Title), "Forum", "Close"))
                    Application.OpenURL(HeGlobals.k_ForumUrl);
                return;
            }

            if (DateTime.Now.Year > 2018)
            {
                if (EditorUtility.DisplayDialog(HeGlobals.k_Title, string.Format("The {0} {1} build expired.", HeGlobals.k_Title, HeGlobals.k_Version), "Forum", "Close"))
                    Application.OpenURL(HeGlobals.k_ForumUrl);
                return;
            }

            EditorWindow.GetWindow<HeapExplorerWindow>();
        }

        void OnEnable()
        {
            titleContent = new GUIContent(HeGlobals.k_Title);
            minSize = new Vector2(800, 600);
            snapshotPath = "";
            m_UseThread = EditorPrefs.GetBool("HeapExplorerWindow.m_UseThread", m_UseThread);

            CreateViews();

            m_threadJobs = new List<AbstractThreadJob>();
            m_thread = new System.Threading.Thread(ThreadLoop);
            m_thread.Start();

            EditorApplication.update += OnApplicationUpdate;
        }

        void OnDisable()
        {
            TryAbortThread();
            m_threadJobs = new List<AbstractThreadJob>();

            UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived -= OnHeapReceived;
            EditorApplication.update -= OnApplicationUpdate;

            DestroyViews();
        }

        void OnApplicationUpdate()
        {
            if (m_repaint)
            {
                m_repaint = false;
                Repaint();
            }
        }

        void CreateViews()
        {
            foreach(var type in s_ViewTypes)
            {
                CreateView(type);
            }

            ActivateView(welcomeView);
        }

        HeapExplorerView welcomeView
        {
            get
            {
                return FindView<WelcomeView>();
            }
        }

        public T FindView<T>() where T : HeapExplorerView
        {
            foreach(var view in m_views)
            {
                var v = view as T;
                if (v != null)
                    return v;
            }

            return null;
        }

        void ResetViews()
        {
            if (m_activeView != null)
            {
                m_activeView.Hide();
                m_activeView = null;
            }

            for (var n = 0; n < m_views.Count; ++n)
                m_views[n].EvictHeap();
        }

        void DestroyViews()
        {
            if (m_activeView != null)
            {
                m_activeView.Hide();
                m_activeView = null;
            }

            for (var n = 0; n < m_views.Count; ++n)
                m_views[n].OnDestroy();
            m_views.Clear();
        }

        HeapExplorerView CreateView(System.Type type)
        {
            var view = (HeapExplorerView)System.Activator.CreateInstance(type);
            view.window = this;
            //view.gotoCB += OnGoto;
            view.Awake();
            m_views.Add(view);
            return view;
        }

        public void OnGoto(GotoCommand command)
        {
            command.fromView = m_activeView;
            m_gotoHistory.Add(command, command);

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

            var sortedViews = new List<HeapExplorerView>(m_views);
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
                    m_repaint = true;
                    return;
                }

                EditorUtility.DisplayDialog(HeGlobals.k_Title + " - ERROR", m_ErrorMsg, "OK");
                Close();
                return;
            }

            for(var n=0; n< m_exceptions.Count; ++n)
                Debug.LogException(m_exceptions[n]);
            m_exceptions.Clear();

            if (m_integrationJobs.Count > 0 && Event.current.type == EventType.Layout)
            {
                lock(m_integrationJobs)
                {
                    if (m_integrationJobs.Count > 0)
                    {
                        m_integrationJobs[0].IntegrateFunc();
                        m_integrationJobs.RemoveAt(0);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(position.width), GUILayout.MaxHeight(position.height)))
            {
                using (new EditorGUI.DisabledGroupScope(m_isCapturing || (m_heap != null && !m_heap.isReady) || (m_busyDraws > 0)))
                {
                    m_busyDraws--;
                    m_busyString = "";

                    DrawToolbar();

                    if (m_heap != null)
                    {
                        if (!m_heap.isReady)
                            SetBusy(m_heap.stateString);

                        if (m_heap.isReady && m_activeView == null && Event.current.type == EventType.Layout)
                            RestoreView();
                    }

                    DrawView();
                    GUILayout.FlexibleSpace();
                    DrawStatusBar();
                }
            }

            if (!string.IsNullOrEmpty(m_busyString))
            {
                m_repaint = true;
                DrawBusy();
            }
        }

        public void SetBusy(string text)
        {
            m_busyString = text;
            m_busyDraws = 3;
        }

        public void SetStatusbarString(string text)
        {
            m_statusBarString = text;
        }

        void DrawBusy()
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

            r = new Rect(new Vector2(0, pivotPoint.y - iconSize), new Vector2(position.width, iconSize*0.5f));
            //GUI.Label(r, m_heap.stateString, HeEditorStyles.loadingLabel);
            GUI.Label(r, m_busyString, HeEditorStyles.loadingLabel);
        }

        void FreeMem()
        {
            System.GC.Collect();
        }

        void ActivateView(object userData)
        {
            var view = userData as HeapExplorerView;
            if (m_activeView != null)
                m_activeView.Hide();

            m_statusBarString = "";
            m_activeView = view;

            if (m_activeView != null)
                m_activeView.Show(m_heap);

            m_repaint = true;
        }

        void DrawView()
        {
            if (m_activeView == null || (m_heap != null && !m_heap.isReady))
                return;

            UnityEngine.Profiling.Profiler.BeginSample(m_activeView.GetType().Name);

            m_activeView.OnGUI();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        void DrawStatusBar()
        {
            var r = GUILayoutUtility.GetRect(10, 20, GUILayout.ExpandWidth(true));
            var oldcolor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.1f);
            GUI.DrawTexture(r, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = oldcolor;

            r.x += 4;
            r.y += 2;
            GUI.Label(r, m_statusBarString, EditorStyles.boldLabel);
        }

        void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                EditorGUI.BeginDisabledGroup(!m_gotoHistory.HasBack());
                if (GUILayout.Button(new GUIContent(HeEditorStyles.backwardImage, "Navigate Backward"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    var cmd = m_gotoHistory.Back();
                    if (cmd != null)
                        GotoInternal(cmd, true);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!m_gotoHistory.HasForward());
                if (GUILayout.Button(new GUIContent(HeEditorStyles.forwardImage, "Navigate Forward"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    var cmd = m_gotoHistory.Forward();
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

                        menu.AddItem(new GUIContent(string.Format("Recent/{0}     {1}", (n + 1), path.Replace('/', '\\'))), false, delegate(System.Object obj)
                        {
                            var p = obj as string;
                            LoadFromFile(p);
                        }, path);
                    }
                    menu.AddSeparator("Recent/");
                    menu.AddItem(new GUIContent("Recent/Clear list"), false, delegate() 
                    {
                        if (EditorUtility.DisplayDialog("Clear list...", "Do you want to clear the most recently used files list?", "Clear", "Cancel"))
                            HeMruFiles.RemoveAll();
                    });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Close Snapshot"), false, CloseFile);
                    menu.AddSeparator("");
                    if (m_heap == null)
                        menu.AddDisabledItem(new GUIContent("Save as..."));
                    else
                        menu.AddItem(new GUIContent("Save as..."), false, SaveToFile);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("New Window"), false, delegate()
                    {
                        var wnd = EditorWindow.CreateInstance<HeapExplorerWindow>();
                        wnd.Show();
                    });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Settings/Use Multi-Threading"), m_UseThread, delegate()
                    {
                        m_UseThread = !m_UseThread;
                        EditorPrefs.SetBool("HeapExplorerWindow.m_UseThread", m_UseThread);
                    });

                    menu.DropDown(m_fileToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_fileToolbarButtonRect = GUILayoutUtility.GetLastRect();


                EditorGUI.BeginDisabledGroup(m_heap == null || !m_heap.isReady);
                if (GUILayout.Button("View", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
                {
                    var menu = new GenericMenu();
                    foreach (var view in m_views)
                    {
                        menu.AddItem(new GUIContent(view.titleContent), m_activeView == view, (GenericMenu.MenuFunction2)delegate(System.Object o)
                        {
                            if (o == m_activeView)
                                return;

                            var v = o as HeapExplorerView;
                            var c0 = m_activeView.GetRestoreCommand(); c0.fromView = m_activeView;
                            var c1 = v.GetRestoreCommand(); c1.toView = v;
                            m_gotoHistory.Add(c0, c1);
                            ActivateView(v);
                        }, view);
                    }

                    menu.DropDown(m_viewToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_viewToolbarButtonRect = GUILayoutUtility.GetLastRect();
                EditorGUI.EndDisabledGroup();


                var connectedProfiler = UnityEditorInternal.ProfilerDriver.GetConnectionIdentifier(UnityEditorInternal.ProfilerDriver.connectedProfiler);
                if (GUILayout.Button(new GUIContent("Capture", HeEditorStyles.magnifyingGlassImage), EditorStyles.toolbarDropDown, GUILayout.Width(80)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(string.Format("Capture and Save '{0}'...", connectedProfiler)), false, CaptureAndSaveHeap);
                    menu.AddItem(new GUIContent(string.Format("Capture and Analyze '{0}'", connectedProfiler)), false, CaptureAndAnalyzeHeap);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent(string.Format("Open Profiler")), false, delegate () { HeEditorUtility.OpenProfiler(); });
                    menu.DropDown(m_captureToolbarButtonRect);
                }
                if (Event.current.type == EventType.Repaint)
                    m_captureToolbarButtonRect = GUILayoutUtility.GetLastRect();

                if (m_activeView != null)
                    m_activeView.OnToolbarGUI();
            }
        }

        void SaveToFile()
        {
            var path = EditorUtility.SaveFilePanel("Save", "", "memory", "heap");
            if (string.IsNullOrEmpty(path))
                return;
            HeMruFiles.AddPath(path);

            m_heap.SaveToFile(path);
            snapshotPath = path;
        }

        void CloseFile()
        {
            snapshotPath = "";
            m_heap = null;
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
        
        void TryAbortThread()
        {
            if (m_thread == null)
                return;

            var guard = 0;
            var flags = System.Threading.ThreadState.Stopped | System.Threading.ThreadState.Aborted;

            m_thread.Abort();
            while ((m_thread.ThreadState & flags) == 0)
            {
                System.Threading.Thread.Sleep(1);
                if (++guard > 1000)
                {
                    Debug.LogWarning("Waiting for thread abort");
                    break;
                }
            }

            m_thread = null;
        }

        public void LoadFromFile(string path)
        {
            SaveView();
            FreeMem();
            HeMruFiles.AddPath(path);
            Reset();
            m_heap = null;

            if (m_UseThread)
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
        List<Exception> m_exceptions = new List<Exception>();

        void ThreadLoop()
        {
            var keepRunning = true;
            while (keepRunning)
            {
                System.Threading.Thread.Sleep(10);
                switch (m_thread.ThreadState)
                {
                    case System.Threading.ThreadState.Aborted:
                    case System.Threading.ThreadState.AbortRequested:
                    case System.Threading.ThreadState.Stopped:
                    case System.Threading.ThreadState.StopRequested:
                        keepRunning = false;
                        break;
                }

                AbstractThreadJob job = null;

                lock (m_threadJobs)
                {
                    if (m_threadJobs.Count == 0)
                        continue;
     
                    job = m_threadJobs[0];
                    m_threadJobs.RemoveAt(0);
                }

                job.state = AbstractThreadJob.State.Running;
                try
                {
                    m_repaint = true;
                    job.ThreadFunc();
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    throw e;
                }
                catch (System.Exception e)
                {
                    m_exceptions.Add(e);
                    //Debug.LogException(e);
                }
                finally
                {
                    m_repaint = true;
                    job.state = AbstractThreadJob.State.Completed;
                }

                lock(m_integrationJobs)
                {
                    m_integrationJobs.Add(job);
                }
            }
        }

        void LoadFromFileThreaded(object userData)
        {
            var filePath = userData as string;
            snapshotPath = filePath;

            m_heap = new PackedMemorySnapshot();
            if (!m_heap.LoadFromFile(filePath))
            {
                m_ErrorMsg = string.Format("Could not load memory snapshot.");
                return;
            }

            m_heap.Initialize();
        }

        void SaveView()
        {
            EditorPrefs.SetString("HeapExplorerWindow.restoreView", "");
            if (m_activeView != null && m_activeView != welcomeView)
            {
                EditorPrefs.SetString("HeapExplorerWindow.restoreView", m_activeView.GetType().Name);
                //Debug.Log("Saving" + m_activeView.GetType().Name);
            }
        }

        void RestoreView()
        {
            var viewType = EditorPrefs.GetString("HeapExplorerWindow.restoreView", "");

            HeapExplorerView view = null;
            for (int n = 0; n < m_views.Count; ++n)
            {
                if (m_views[n].GetType().Name == viewType)
                {
                    view = m_views[n];
                    break;
                }
            }

            if (view == null)
                view = FindView<OverviewView>();// m_overviewView;

            //Debug.Log("Restoring" + view.GetType().Name);
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

            m_gotoHistory = new GotoHistory();
            ActivateView(null);
        }

        void CaptureAndSaveHeap()
        {
            var autoSavePath = EditorPrefs.GetString("HeapExplorerWindow.autoSavePath", "");
            if (string.IsNullOrEmpty(autoSavePath))
                autoSavePath = Application.dataPath + "/memory.heap";

            var path = EditorUtility.SaveFilePanel("Save snapshot as...", System.IO.Path.GetDirectoryName(autoSavePath), System.IO.Path.GetFileNameWithoutExtension(autoSavePath), "heap");
            if (string.IsNullOrEmpty(path))
                return;

            EditorPrefs.SetString("HeapExplorerWindow.autoSavePath", path);
            m_autoSavePath = path;

            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Receiving memory...", 0.0f);
            try
            {
                FreeMem();
                m_isCapturing = true;

                UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived += OnHeapReceivedSaveOnly;
                UnityEditor.MemoryProfiler.MemorySnapshot.RequestNewSnapshot();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_repaint = true;
            }
        }

        void OnHeapReceivedSaveOnly(UnityEditor.MemoryProfiler.PackedMemorySnapshot snapshot)
        {
            UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived -= OnHeapReceivedSaveOnly;

            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Saving memory...", 0.5f);
            try
            {
                var heap = PackedMemorySnapshot.FromMemoryProfiler(snapshot);
                heap.SaveToFile(m_autoSavePath);
                HeMruFiles.AddPath(m_autoSavePath);
                ShowNotification(new GUIContent(string.Format("Memory snapshot saved as\n'{0}'", m_autoSavePath)));
            }
            finally
            {
                m_isCapturing = false;
                m_repaint = true;
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
                m_isCapturing = true;

                UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived += OnHeapReceived;
                UnityEditor.MemoryProfiler.MemorySnapshot.RequestNewSnapshot();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_repaint = true;
            }
        }

        void OnHeapReceived(UnityEditor.MemoryProfiler.PackedMemorySnapshot snapshot)
        {
            UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived -= OnHeapReceived;

            EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Reading memory...", 0.5f);
            try
            {
                //Reset();
                m_heap = null;

                if (m_UseThread)
                {
                    var job = new ReceiveThreadJob
                    {
                        threadFunc = ReceiveHeapThreaded,
                        snapshot = snapshot
                    };
                    ScheduleJob(job);
                }
                else
                {
                    EditorUtility.DisplayProgressBar(HeGlobals.k_Title, "Analyzing memory...", 0.75f);
                    ReceiveHeapThreaded(snapshot);
                }
            }
            finally
            {
                snapshotPath = string.Format("Captured Snapshot at {0}", DateTime.Now.ToShortTimeString());
                m_isCapturing = false;
                m_repaint = true;
                EditorUtility.ClearProgressBar();
            }
        }

        void ReceiveHeapThreaded(object userData)
        {
            var snapshot = userData as UnityEditor.MemoryProfiler.PackedMemorySnapshot;
            m_heap = PackedMemorySnapshot.FromMemoryProfiler(snapshot);
            m_heap.Initialize();
        }

        public void ScheduleJob(AbstractThreadJob job)
        {
            job.state = AbstractThreadJob.State.Queued;

            lock (m_threadJobs)
            {
                // Remove unstarted jobs of same type
                for (var n = m_threadJobs.Count - 1; n >= 0; --n)
                {
                    var j = m_threadJobs[n];
                    if (j.state == AbstractThreadJob.State.Queued && j.GetType() == job.GetType())
                    {
                        m_threadJobs.RemoveAt(n);
                        continue;
                    }
                }

                m_threadJobs.Add(job);
            }

            m_repaint = true;
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
        public UnityEditor.MemoryProfiler.PackedMemorySnapshot snapshot;
        public Action<object> threadFunc;

        public override void ThreadFunc()
        {
            threadFunc.Invoke(snapshot);
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
