using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;
using System.Management;

class Program {
    static Program() {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
            string assemblyName = new System.Reflection.AssemblyName(args.Name).Name;
            string assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin\\" + assemblyName + ".dll");
            if (File.Exists(assemblyPath)) {
                return System.Reflection.Assembly.LoadFrom(assemblyPath);
            }
            return null;
        };
    }
    // ──────────────────────────────────────────────
    // Win32 API structures and constants
    // ──────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS_EX {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS_EX2 {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
        public UIntPtr PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MEMORYSTATUSEX {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LUID {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LUID_AND_ATTRIBUTES {
        public LUID Luid;
        public uint Attributes;
    }

    struct TOKEN_PRIVILEGES {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    // Windows API Imports
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessWorkingSetSizeEx(IntPtr hProcess, UIntPtr dwMinimumWorkingSetSize, UIntPtr dwMaximumWorkingSetSize, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessWorkingSetSize(IntPtr hProcess, UIntPtr dwMinimumWorkingSetSize, UIntPtr dwMaximumWorkingSetSize);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_PRIORITY_INFORMATION {
        public uint MemoryPriority;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref MEMORY_PRIORITY_INFORMATION ProcessInformation,
        uint ProcessInformationSize
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern int NtSuspendProcess(IntPtr hProcess);

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern int NtResumeProcess(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint GetPriorityClass(IntPtr hProcess);

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    [DllImport("shell32.dll", SetLastError = true)]
    static extern bool IsUserAnAdmin();

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX ppsmemCounters, uint cb);

    [DllImport("psapi.dll", EntryPoint = "GetProcessMemoryInfo", SetLastError = true)]
    static extern bool GetProcessMemoryInfoEx2(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX2 ppsmemCounters, uint cb);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    // Access rights
    const uint PROCESS_TERMINATE = 0x0001;
    const uint PROCESS_VM_OPERATION = 0x0008;
    const uint PROCESS_VM_READ = 0x0010;
    const uint PROCESS_SET_INFORMATION = 0x0200;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_SET_QUOTA = 0x0100;
    const uint PROCESS_SUSPEND_RESUME = 0x0800;
    const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    // Priorities
    const uint IDLE_PRIORITY_CLASS = 0x00000040;
    const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    const uint HIGH_PRIORITY_CLASS = 0x00000080;

    // Job Object Constants
    const uint JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001;
    const int JobObjectExtendedLimitInformation = 9;

    // ──────────────────────────────────────────────
    // Core Application State
    // ──────────────────────────────────────────────
    static string AppDir;
    static string FrontendDir;
    static string ConfigPath;

    // Settings
    static bool AutoOptimize = false;
    static int AutoIntervalMinutes = 10;
    static string AutoFlushTier = "safe";
    static string DefaultTier = "safe";
    static int AutoResumeMinutes = 10;
    static bool GlobalLimitEnabled = false;
    static int GlobalLimitMb = 600;
    static string UpdateServerUrl = "";

    // Stats
    static double TotalSavedMb = 0.0;
    static int TotalOptCount = 0;
    static DateTime ServerStartTime = DateTime.Now;

    // Active client structures
    static Dictionary<int, IntPtr> JobHandles = new Dictionary<int, IntPtr>();
    static Dictionary<int, int> RamLimits = new Dictionary<int, int>();
    static HashSet<int> SeenPids = new HashSet<int>();
    
    // Priorities
    static Dictionary<int, string> OriginalPriorities = new Dictionary<int, string>();
    static Dictionary<int, int> IoPriorities = new Dictionary<int, int>();
    
    // Ghost process detection state
    struct GhostIoItem {
        public ulong ReadBytes;
        public ulong WriteBytes;
    }
    static Dictionary<int, GhostIoItem> LastGhostIo = new Dictionary<int, GhostIoItem>();
    static Dictionary<int, int> GhostConfirmations = new Dictionary<int, int>();
    
    // Suspend Timers
    static Dictionary<int, System.Threading.Timer> SuspendTimers = new Dictionary<int, System.Threading.Timer>();
    static HashSet<int> SuspendedPids = new HashSet<int>();

    // Process Caching
    static Dictionary<int, string> ProcessLabels = new Dictionary<int, string>();
    static int ProcessCounter = 0;
    static Dictionary<int, CpuCacheItem> CpuCache = new Dictionary<int, CpuCacheItem>();
    static Dictionary<int, IoCacheItem> IoCache = new Dictionary<int, IoCacheItem>();
    static Dictionary<int, PageFaultCacheItem> PageFaultCache = new Dictionary<int, PageFaultCacheItem>();
    static Dictionary<int, double> LastTrimTimes = new Dictionary<int, double>();

    // Server State
    static HttpListener Listener;
    static int ServerPort = 5000;
    static List<WebSocketSession> WebSockets = new List<WebSocketSession>();
    static NotifyIcon TrayIcon;
    static bool Running = true;

    // Synchronization locks
    static readonly object ConsoleLock = new object();
    static readonly object StateLock = new object();

    struct CpuCacheItem {
        public TimeSpan ProcessorTime;
        public DateTime Time;
        public double LastValue;
    }

    struct IoCacheItem {
        public ulong TotalBytes;
        public DateTime Time;
        public double LastValue;
    }

    struct PageFaultCacheItem {
        public uint Faults;
        public DateTime Time;
        public double LastValue;
    }

    class ProcessInfo {
        public int Pid;
        public string Label;
        public double RamMb;
        public ulong RamBytes;
        public double VmsMb;
        public double CpuPercent;
        public string Status;
        public int UptimeSeconds;
        public string UptimeFormatted;
        public double PageFaultRate;
        public double IoRateKbps;
        public int Threads;
        public ulong ReadBytes;
        public ulong WriteBytes;
        public string Priority;
        public int RamLimitMb;
        public IntPtr MainWindowHandle;
    }

    // ──────────────────────────────────────────────
    // Entry Point (Main)
    // ──────────────────────────────────────────────
    [STAThread]
    static void Main(string[] args) {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            try {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.ExceptionObject.ToString());
            } catch {}
        };

        AppDir = AppDomain.CurrentDomain.BaseDirectory;

        // Load native WebView2Loader.dll from bin folder if it exists
        string binDir = Path.Combine(AppDir, "bin");
        string loaderPath = Path.Combine(binDir, "WebView2Loader.dll");
        if (File.Exists(loaderPath)) {
            LoadLibrary(loaderPath);
        }

        if (Directory.Exists(binDir)) {
            FrontendDir = Path.Combine(binDir, "frontend");
            ConfigPath = Path.Combine(binDir, "config.json");
        } else {
            FrontendDir = Path.Combine(AppDir, "frontend");
            ConfigPath = Path.Combine(AppDir, "config.json");
        }

        // Elevate privileges to Administrator
        if (!IsUserAnAdmin() && !Array.Exists(args, x => x == "--elevated")) {
            try {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Application.ExecutablePath;
                info.Arguments = string.Join(" ", args) + " --elevated";
                info.Verb = "runas";
                Process.Start(info);
                return;
            } catch {
                MessageBox.Show("Ứng dụng cần chạy dưới quyền Administrator để tối ưu hóa và quản lý giới hạn RAM Roblox.", "Yêu cầu Quyền Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        LoadSettings();
        ExportManifestFile();

        DeleteOldExecutable();
        PerformStartupUpdateCheck();

        // Start WebServer
        StartWebServer();

        // Start Monitor & Optimizer Loops
        Thread monitorThread = new Thread(BackgroundMonitorLoop);
        monitorThread.IsBackground = true;
        monitorThread.Start();

        // Setup System Tray Icon directly on the Main STA thread
        TrayIcon = new NotifyIcon();
        TrayIcon.Text = "RobloxRAM Optimizer C#";
        TrayIcon.Icon = CreateTrayIcon();
        
        ContextMenu menu = new ContextMenu();
        menu.MenuItems.Add("Mở Dashboard", (s, e) => OpenDashboard());
        menu.MenuItems.Add("-");
        menu.MenuItems.Add("Thoát", (s, e) => Shutdown());
        TrayIcon.ContextMenu = menu;
        TrayIcon.Visible = true;
        
        TrayIcon.DoubleClick += (s, e) => OpenDashboard();

        // Open dashboard on start
        OpenDashboard();

        // Keep main thread alive and process messages natively
        Application.Run();

        // Cleanup
        if (TrayIcon != null) {
            TrayIcon.Visible = false;
            TrayIcon.Dispose();
        }
        CleanupAllLimits();
    }

    static DashboardForm activeForm = null;
    static readonly object formLock = new object();

    static void OpenDashboard() {
        string url = string.Format("http://127.0.0.1:{0}", ServerPort);
        lock (formLock) {
            if (activeForm != null && !activeForm.IsDisposed) {
                try {
                    activeForm.Invoke((MethodInvoker)delegate {
                        if (activeForm.WindowState == FormWindowState.Minimized) {
                            activeForm.WindowState = FormWindowState.Normal;
                        }
                        activeForm.Activate();
                    });
                } catch {
                    activeForm = null;
                    OpenDashboard();
                }
                return;
            }

            Thread thread = new Thread(() => {
                try {
                    activeForm = new DashboardForm(url);
                    Application.Run(activeForm);
                } catch {
                    try {
                        ProcessStartInfo edgeApp = new ProcessStartInfo();
                        edgeApp.FileName = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                        edgeApp.Arguments = string.Format("--app=\"{0}\"", url);
                        edgeApp.UseShellExecute = false;
                        Process.Start(edgeApp);
                    } catch {
                        Process.Start(url);
                    }
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    internal static void Shutdown() {
        Running = false;
        if (Listener != null) {
            try { Listener.Stop(); } catch {}
        }
        Application.Exit();
    }

    // Helper to generate purple game icon in-memory without external files
    public static Icon CreateTrayIcon() {
        using (Bitmap bmp = new Bitmap(16, 16)) {
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(Color.FromArgb(139, 92, 246))) {
                    g.FillEllipse(brush, 1, 1, 14, 14);
                }
                using (Brush whiteBrush = new SolidBrush(Color.White)) {
                    g.FillEllipse(whiteBrush, 5, 5, 6, 6);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ──────────────────────────────────────────────
    // Settings logic (Manual JSON Parsing)
    // ──────────────────────────────────────────────
    static void LoadSettings() {
        if (File.Exists(ConfigPath)) {
            try {
                string json = File.ReadAllText(ConfigPath);
                AutoOptimize = ParseBoolKey(json, "auto_optimize");
                int val = ParseIntKey(json, "auto_interval_minutes");
                if (val >= 5 && val <= 60) AutoIntervalMinutes = val;
                
                string tier = ParseStringKey(json, "auto_flush_tier");
                if (!string.IsNullOrEmpty(tier)) AutoFlushTier = tier;

                string defTier = ParseStringKey(json, "default_tier");
                if (!string.IsNullOrEmpty(defTier)) DefaultTier = defTier;

                int resMin = ParseIntKey(json, "auto_resume_minutes");
                if (resMin > 0) AutoResumeMinutes = resMin;

                GlobalLimitEnabled = ParseBoolKey(json, "global_limit_enabled");
                int limMb = ParseIntKey(json, "global_limit_mb");
                if (limMb >= 100 && limMb <= 3000) GlobalLimitMb = limMb;

                string updUrl = ParseStringKey(json, "update_server_url");
                if (updUrl != null) UpdateServerUrl = updUrl;
            } catch {}
        }
    }

    static void SaveSettings() {
        try {
            string json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\n" +
                "  \"auto_optimize\": {0},\n" +
                "  \"auto_interval_minutes\": {1},\n" +
                "  \"auto_flush_tier\": \"{2}\",\n" +
                "  \"default_tier\": \"{3}\",\n" +
                "  \"auto_resume_minutes\": {4},\n" +
                "  \"global_limit_enabled\": {5},\n" +
                "  \"global_limit_mb\": {6},\n" +
                "  \"update_server_url\": \"{7}\"\n" +
                "}}",
                AutoOptimize.ToString().ToLower(),
                AutoIntervalMinutes,
                AutoFlushTier,
                DefaultTier,
                AutoResumeMinutes,
                GlobalLimitEnabled.ToString().ToLower(),
                GlobalLimitMb,
                EscapeJson(UpdateServerUrl)
            );
            File.WriteAllText(ConfigPath, json);
        } catch {}
    }

    static bool ParseBoolKey(string json, string key) {
        string pattern = "\"" + key + "\"";
        int idx = json.IndexOf(pattern);
        if (idx == -1) return false;
        int colon = json.IndexOf(":", idx);
        if (colon == -1) return false;
        int end = json.IndexOfAny(new char[] { ',', '}', '\n' }, colon);
        if (end == -1) end = json.Length;
        string val = json.Substring(colon + 1, end - colon - 1).Trim().Replace("\"", "");
        return val == "true" || val == "1";
    }

    static int ParseIntKey(string json, string key) {
        string pattern = "\"" + key + "\"";
        int idx = json.IndexOf(pattern);
        if (idx == -1) return 0;
        int colon = json.IndexOf(":", idx);
        if (colon == -1) return 0;
        int end = json.IndexOfAny(new char[] { ',', '}', '\n' }, colon);
        if (end == -1) end = json.Length;
        string val = json.Substring(colon + 1, end - colon - 1).Trim().Replace("\"", "");
        int res;
        int.TryParse(val, out res);
        return res;
    }

    static string ParseStringKey(string json, string key) {
        string pattern = "\"" + key + "\"";
        int idx = json.IndexOf(pattern);
        if (idx == -1) return null;
        int colon = json.IndexOf(":", idx);
        if (colon == -1) return null;
        int end = json.IndexOfAny(new char[] { ',', '}', '\n' }, colon);
        if (end == -1) end = json.Length;
        return json.Substring(colon + 1, end - colon - 1).Trim().Replace("\"", "");
    }

    static string EscapeJson(string s) {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "").Replace("\t", " ");
    }

    static string GetSystemDriveLetter() {
        try {
            string sysDir = Environment.SystemDirectory;
            string root = Path.GetPathRoot(sysDir);
            if (!string.IsNullOrEmpty(root) && root.Length >= 2 && root[1] == ':') {
                return root.Substring(0, 1).ToUpper();
            }
        } catch {}
        return "C";
    }

    static bool SetMemoryPriority(IntPtr hProcess, uint priority) {
        try {
            MEMORY_PRIORITY_INFORMATION info = new MEMORY_PRIORITY_INFORMATION();
            info.MemoryPriority = priority;
            return SetProcessInformation(
                hProcess,
                39, // ProcessMemoryPriority
                ref info,
                (uint)Marshal.SizeOf(typeof(MEMORY_PRIORITY_INFORMATION))
            );
        } catch {
            return false;
        }
    }

    // ──────────────────────────────────────────────
    // WebServer (HttpListener)
    // ──────────────────────────────────────────────
    static void StartWebServer() {
        List<string> errors = new List<string>();
        
        // Loop to find an available port starting from 5000 to 5100
        for (int port = 5000; port < 5100; port++) {
            try {
                if (Listener != null) {
                    try { Listener.Close(); } catch {}
                }
                
                Listener = new HttpListener();
                Listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
                Listener.Start();
                ServerPort = port;
                break;
            } catch (Exception ex) {
                errors.Add(string.Format("Cổng {0}: {1}", port, ex.Message));
                if (Listener != null) {
                    try { Listener.Close(); } catch {}
                    Listener = null;
                }
            }
        }

        if (Listener == null || !Listener.IsListening) {
            string detailedError = string.Join("\n", errors.ToArray());
            if (detailedError.Length > 300) detailedError = detailedError.Substring(0, 300) + "...";
            MessageBox.Show("Không thể khởi động WebServer trên bất kỳ cổng nào từ 5000 đến 5100.\n\nChi tiết lỗi:\n" + detailedError, "Lỗi Khởi Động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Shutdown();
            return;
        }

        try {
            Task.Run(async () => {
                while (Running) {
                    try {
                        HttpListenerContext context = await Listener.GetContextAsync();
                        Forget(Task.Run(() => HandleRequest(context)));
                    } catch {
                        if (!Running) break;
                    }
                }
            });
        } catch (Exception ex) {
            MessageBox.Show("Lỗi khởi chạy luồng xử lý WebServer: " + ex.Message, "Lỗi Khởi Động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Shutdown();
        }
    }

    static async Task HandleRequest(HttpListenerContext context) {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        if (request.RawUrl == "/ws") {
            if (request.IsWebSocketRequest) {
                try {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    RegisterWebSocket(wsContext.WebSocket);
                } catch {
                    response.StatusCode = 500;
                    response.Close();
                }
            } else {
                response.StatusCode = 400;
                response.Close();
            }
            return;
        }

        // CORS Headers
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, DELETE");

        if (request.HttpMethod == "OPTIONS") {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        string path = request.RawUrl.Split('?')[0];

        if (path.StartsWith("/api/")) {
            response.ContentType = "application/json; charset=utf-8";
            string responseJson = "";
            try {
                responseJson = await HandleApiCall(path, request);
            } catch (Exception ex) {
                response.StatusCode = 500;
                responseJson = "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
            byte[] bytes = Encoding.UTF8.GetBytes(responseJson);
            response.ContentLength64 = bytes.Length;
            try {
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            } catch {}
            response.OutputStream.Close();
        } else {
            await ServeStaticFile(response, path);
        }
    }

    static async Task ServeStaticFile(HttpListenerResponse response, string rawPath) {
        string cleanPath = rawPath == "/" ? "index.html" : rawPath.Replace("/", "\\").TrimStart('\\').TrimStart('/');
        string filePath = Path.Combine(FrontendDir, cleanPath);

        if (File.Exists(filePath)) {
            try {
                byte[] bytes = File.ReadAllBytes(filePath);
                string ext = Path.GetExtension(filePath).ToLower();
                string contentType = "application/octet-stream";
                if (ext == ".html" || ext == ".htm") contentType = "text/html; charset=utf-8";
                else if (ext == ".css") contentType = "text/css";
                else if (ext == ".js") contentType = "application/javascript";
                else if (ext == ".json") contentType = "application/json";
                else if (ext == ".png") contentType = "image/png";
                else if (ext == ".ico") contentType = "image/x-icon";

                response.ContentType = contentType;
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            } catch (Exception ex) {
                response.StatusCode = 500;
                byte[] err = Encoding.UTF8.GetBytes("Error: " + ex.Message);
                try { response.OutputStream.Write(err, 0, err.Length); } catch {}
            }
        } else {
            response.StatusCode = 404;
            byte[] err = Encoding.UTF8.GetBytes("File Not Found: " + rawPath);
            try { await response.OutputStream.WriteAsync(err, 0, err.Length); } catch {}
        }
        response.OutputStream.Close();
    }

    static void RegisterWebSocket(WebSocket ws) {
        var session = new WebSocketSession(ws);
        lock (WebSockets) {
            WebSockets.Add(session);
        }
        
        // Read loop (to keep it open and detect disconnect)
        Task.Run(async () => {
            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open) {
                try {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                } catch {
                    break;
                }
            }
            lock (WebSockets) {
                WebSockets.Remove(session);
            }
        });
    }

    static async Task BroadcastMessage(string eventName, string payloadJson) {
        string json = string.Format("{{\"event\":\"{0}\",\"payload\":{1}}}", eventName, payloadJson);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);

        List<WebSocketSession> sessions;
        lock (WebSockets) {
            sessions = new List<WebSocketSession>(WebSockets);
        }

        List<WebSocketSession> failedSessions = new List<WebSocketSession>();
        List<Task> sendTasks = new List<Task>();

        foreach (var session in sessions) {
            if (session.Socket.State == WebSocketState.Open) {
                var currentSession = session;
                sendTasks.Add(Task.Run(async () => {
                    try {
                        await currentSession.SendAsync(buffer, WebSocketMessageType.Text, true);
                    } catch {
                        lock (failedSessions) {
                            failedSessions.Add(currentSession);
                        }
                    }
                }));
            } else {
                failedSessions.Add(session);
            }
        }

        if (sendTasks.Count > 0) {
            await Task.WhenAll(sendTasks);
        }

        if (failedSessions.Count > 0) {
            lock (WebSockets) {
                foreach (var session in failedSessions) {
                    WebSockets.Remove(session);
                }
            }
        }
    }

    class WebSocketSession {
        public WebSocket Socket;
        private readonly SemaphoreSlim SendSemaphore = new SemaphoreSlim(1, 1);

        public WebSocketSession(WebSocket socket) {
            Socket = socket;
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage) {
            await SendSemaphore.WaitAsync();
            try {
                if (Socket.State == WebSocketState.Open) {
                    await Socket.SendAsync(buffer, messageType, endOfMessage, CancellationToken.None);
                }
            } finally {
                SendSemaphore.Release();
            }
        }
    }

    // ──────────────────────────────────────────────
    // REST API Endpoint router
    // ──────────────────────────────────────────────
    static async Task<string> HandleApiCall(string path, HttpListenerRequest request) {
        string body = "";
        if (request.HttpMethod == "POST" || request.HttpMethod == "PUT") {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding)) {
                body = await reader.ReadToEndAsync();
            }
        }

        // GET /api/processes
        if (path == "/api/processes" && request.HttpMethod == "GET") {
            var processes = ScanRobloxProcesses();
            return SerializeProcesses(processes);
        }

        // GET /api/system
        if (path == "/api/system" && request.HttpMethod == "GET") {
            double total, used, avail;
            double percent = GetSystemMemoryPercent(out total, out used, out avail);
            var processes = ScanRobloxProcesses();
            double robloxTotal = 0;
            foreach (var p in processes) robloxTotal += p.RamMb;

            int autoRemaining = GetAutoFlushRemainingSeconds();
            return SerializeSystemInfo(total, used, avail, percent, robloxTotal, processes.Count, IsUserAnAdmin(), autoRemaining);
        }

        // GET /api/settings
        if (path == "/api/settings" && request.HttpMethod == "GET") {
            return GetSettingsJson();
        }

        // POST/PUT /api/settings
        if (path == "/api/settings" && (request.HttpMethod == "POST" || request.HttpMethod == "PUT")) {
            if (body.Contains("\"auto_optimize\"")) {
                AutoOptimize = ParseBoolKey(body, "auto_optimize");
                if (AutoOptimize) {
                    StartAutoScheduler();
                } else {
                    StopAutoScheduler();
                }
            }
            if (body.Contains("\"auto_interval_minutes\"")) {
                int val = ParseIntKey(body, "auto_interval_minutes");
                if (val >= 5 && val <= 60) {
                    AutoIntervalMinutes = val;
                    if (AutoOptimize) ResetAutoSchedulerInterval();
                }
            }
            if (body.Contains("\"auto_flush_tier\"")) {
                string tier = ParseStringKey(body, "auto_flush_tier");
                if (!string.IsNullOrEmpty(tier)) AutoFlushTier = tier;
            }
            if (body.Contains("\"default_tier\"")) {
                string tier = ParseStringKey(body, "default_tier");
                if (!string.IsNullOrEmpty(tier)) DefaultTier = tier;
            }
            if (body.Contains("\"auto_resume_minutes\"")) {
                int val = ParseIntKey(body, "auto_resume_minutes");
                if (val > 0) AutoResumeMinutes = val;
            }
            if (body.Contains("\"global_limit_enabled\"")) {
                GlobalLimitEnabled = ParseBoolKey(body, "global_limit_enabled");
            }
            if (body.Contains("\"global_limit_mb\"")) {
                int val = ParseIntKey(body, "global_limit_mb");
                if (val >= 100 && val <= 3000) GlobalLimitMb = val;
            }
            if (body.Contains("\"update_server_url\"")) {
                string val = ParseStringKey(body, "update_server_url");
                if (val != null) UpdateServerUrl = val;
            }

            SaveSettings();
            return GetSettingsJson();
        }

        // GET /api/history
        if (path == "/api/history" && request.HttpMethod == "GET") {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"history\":[],\"total_saved_mb\":{0:F1},\"success\":true}}",
                TotalSavedMb
            );
        }

        // POST /api/optimize/all
        if (path == "/api/optimize/all" && request.HttpMethod == "POST") {
            string tier = ParseStringKey(body, "tier") ?? "safe";
            var pids = GetRobloxPids();
            double saved = 0.0;
            int count = 0;
            
            foreach (int pid in pids) {
                var json = OptimizeProcess(pid, tier, force: true);
                if (json.Contains("\"success\":true")) {
                    double savedMb = double.Parse(ParseStringKey(json, "saved_mb") ?? "0", CultureInfo.InvariantCulture);
                    saved += savedMb;
                    count++;
                }
                Thread.Sleep(200); // Dynamic stagger
            }

            string res = string.Format(CultureInfo.InvariantCulture, "{{\"success\":true,\"count\":{0},\"total_saved_mb\":{1:F1}}}", count, saved);
            await BroadcastMessage("optimize_all_result", res);
            return res;
        }

        // POST /api/optimize/<pid>
        if (path.StartsWith("/api/optimize/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(14));
            string tier = ParseStringKey(body, "tier") ?? "safe";
            var result = OptimizeProcess(pid, tier, force: true);
            await BroadcastMessage("optimize_result", result);
            return result;
        }

        if (path == "/api/system/clear-standby" && request.HttpMethod == "POST") {
            if (!IsUserAnAdmin()) return "{\"success\":false,\"error\":\"Requires Administrator privileges\"}";
            try {
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                int val = 5; // Low priority standby list
                int status = NtSetSystemInformation(80, ref val, sizeof(int));
                if (status != 0) {
                    val = 4; // Full standby list
                    status = NtSetSystemInformation(80, ref val, sizeof(int));
                }
                if (status == 0) return "{\"success\":true}";
                else return "{\"success\":false,\"error\":\"NtSetSystemInformation returned " + status + "\"}";
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        // POST /api/system/flush-modified
        if (path == "/api/system/flush-modified" && request.HttpMethod == "POST") {
            if (!IsUserAnAdmin()) return "{\"success\":false,\"error\":\"Requires Administrator privileges\"}";
            try {
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                int val = 3; // Flush modified pages list
                int status = NtSetSystemInformation(80, ref val, sizeof(int));
                if (status == 0) return "{\"success\":true}";
                else return "{\"success\":false,\"error\":\"NtSetSystemInformation returned " + status + "\"}";
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        // POST /api/suspend/<pid>
        if (path.StartsWith("/api/suspend/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(13));
            int resMin = ParseIntKey(body, "auto_resume_minutes");
            if (resMin == 0) resMin = AutoResumeMinutes;
            
            bool ok = SuspendProcess(pid, resMin);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1},\"action\":\"suspend\"}}", pid, ok.ToString().ToLower());
        }

        // POST /api/resume/<pid>
        if (path.StartsWith("/api/resume/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(12));
            bool ok = ResumeProcess(pid);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1},\"action\":\"resume\"}}", pid, ok.ToString().ToLower());
        }

        // POST /api/priority/<pid>
        if (path.StartsWith("/api/priority/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(14));
            string level = ParseStringKey(body, "level") ?? "normal";
            bool ok = SetCpuPriority(pid, level);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1},\"priority\":\"{2}\"}}", pid, ok.ToString().ToLower(), level);
        }

        // POST /api/kill/all
        if (path == "/api/kill/all" && request.HttpMethod == "POST") {
            var pids = GetRobloxPids();
            int count = 0;
            foreach (int pid in pids) {
                if (KillProcess(pid)) count++;
            }
            return string.Format(CultureInfo.InvariantCulture, "{{\"success\":true,\"count\":{0}}}", count);
        }

        // POST /api/kill/<pid>
        if (path.StartsWith("/api/kill/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(10));
            bool ok = KillProcess(pid);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1}}}", pid, ok.ToString().ToLower());
        }

        // POST /api/limit/remove/<pid>
        if (path.StartsWith("/api/limit/remove/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(18));
            bool ok = RemoveRamLimit(pid);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1}}}", pid, ok.ToString().ToLower());
        }

        // POST /api/limit/<pid>
        if (path.StartsWith("/api/limit/") && request.HttpMethod == "POST") {
            int pid = int.Parse(path.Substring(11));
            int limitMb = ParseIntKey(body, "limit_mb");
            bool ok = SetRamLimit(pid, limitMb);
            return string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"success\":{1},\"limit_mb\":{2}}}", pid, ok.ToString().ToLower(), limitMb);
        }

        // GET /api/system/pagefile
        if (path == "/api/system/pagefile" && request.HttpMethod == "GET") {
            return GetPagefileJson();
        }

        // POST /api/system/pagefile
        if (path == "/api/system/pagefile" && request.HttpMethod == "POST") {
            int sizeGb = ParseIntKey(body, "size_gb");
            try {
                ConfigurePagefile(sizeGb);
                return string.Format(CultureInfo.InvariantCulture, "{{\"success\":true,\"size_gb\":{0}}}", sizeGb);
            } catch (Exception ex) {
                return string.Format(CultureInfo.InvariantCulture, "{{\"success\":false,\"error\":\"{0}\"}}", EscapeJson(ex.Message));
            }
        }

        // GET /api/update/manifest
        if (path == "/api/update/manifest" && request.HttpMethod == "GET") {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"success\":true,\"files\":{");
            for (int i = 0; i < UpdateWhitelist.Length; i++) {
                string file = UpdateWhitelist[i];
                string hash = GetFileHash(file);
                sb.Append(string.Format("\"{0}\":\"{1}\"", file, hash));
                if (i < UpdateWhitelist.Length - 1) {
                    sb.Append(",");
                }
            }
            sb.Append("}}");
            return sb.ToString();
        }

        // GET /api/update/file
        if (path == "/api/update/file" && request.HttpMethod == "GET") {
            string relPath = request.QueryString["path"];
            if (string.IsNullOrEmpty(relPath)) {
                return "{\"success\":false,\"error\":\"Path parameter required\"}";
            }
            string normalized = relPath.Replace("\\", "/").TrimStart('/');
            bool whitelisted = false;
            foreach (string f in UpdateWhitelist) {
                if (f.Equals(normalized, StringComparison.OrdinalIgnoreCase)) {
                    whitelisted = true;
                    break;
                }
            }
            if (!whitelisted) {
                return "{\"success\":false,\"error\":\"Access denied to this file\"}";
            }
            string fullPath = Path.Combine(AppDir, normalized.Replace("/", "\\"));
            if (File.Exists(fullPath)) {
                try {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    string base64 = Convert.ToBase64String(bytes);
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"success\":true,\"path\":\"{0}\",\"content\":\"{1}\"}}",
                        EscapeJson(normalized),
                        base64
                    );
                } catch (Exception ex) {
                    return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
                }
            }
            return "{\"success\":false,\"error\":\"File not found\"}";
        }

        // POST /api/update/export-manifest
        if (path == "/api/update/export-manifest" && request.HttpMethod == "POST") {
            try {
                ExportManifestFile();
                return "{\"success\":true,\"path\":\"update_manifest.json\"}";
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        // POST /api/update/check
        if (path == "/api/update/check" && request.HttpMethod == "POST") {
            if (string.IsNullOrEmpty(UpdateServerUrl)) {
                return "{\"success\":false,\"error\":\"Chưa cấu hình địa chỉ máy chủ cập nhật trong phần Cài đặt.\"}";
            }
            string url = UpdateServerUrl.TrimEnd('/');
            string manifestUrl = url + "/api/update/manifest";
            string json = "";
            try {
                json = DownloadStringWithTimeout(manifestUrl, 3000);
            } catch {
                try {
                    manifestUrl = url + "/update_manifest.json";
                    json = DownloadStringWithTimeout(manifestUrl, 3000);
                } catch (Exception ex) {
                    return "{\"success\":false,\"error\":\"Không thể kết nối đến máy chủ: " + EscapeJson(ex.Message) + "\"}";
                }
            }
            try {
                var remoteHashes = ParseManifestJson(json);
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"success\":true,\"update_available\":false,\"files\":[");
                int count = 0;
                foreach (var file in UpdateWhitelist) {
                    string remoteHash = "";
                    if (remoteHashes.TryGetValue(file, out remoteHash)) {
                        string localHash = GetFileHash(file);
                        if (remoteHash != localHash && !string.IsNullOrEmpty(remoteHash)) {
                            if (count > 0) sb.Append(",");
                            sb.Append(string.Format("{{\"path\":\"{0}\",\"remote_hash\":\"{1}\",\"local_hash\":\"{2}\"}}", file, remoteHash, localHash));
                            count++;
                        }
                    }
                }
                sb.Append("]");
                if (count > 0) {
                    sb.Append(",\"update_available\":true");
                }
                sb.Append("}");
                return sb.ToString();
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"Lỗi xử lý manifest: " + EscapeJson(ex.Message) + "\"}";
            }
        }

        // POST /api/update/apply
        if (path == "/api/update/apply" && request.HttpMethod == "POST") {
            if (string.IsNullOrEmpty(UpdateServerUrl)) {
                return "{\"success\":false,\"error\":\"Chưa cấu hình địa chỉ máy chủ cập nhật.\"}";
            }
            string url = UpdateServerUrl.TrimEnd('/');
            string manifestUrl = url + "/api/update/manifest";
            bool isStatic = false;
            string json = "";
            try {
                json = DownloadStringWithTimeout(manifestUrl, 3000);
            } catch {
                try {
                    manifestUrl = url + "/update_manifest.json";
                    json = DownloadStringWithTimeout(manifestUrl, 3000);
                    isStatic = true;
                } catch (Exception ex) {
                    return "{\"success\":false,\"error\":\"Lỗi kết nối máy chủ: " + EscapeJson(ex.Message) + "\"}";
                }
            }
            try {
                var remoteHashes = ParseManifestJson(json);
                bool needRestart = false;
                List<string> updatedFiles = new List<string>();
                foreach (var file in UpdateWhitelist) {
                    string remoteHash = "";
                    if (remoteHashes.TryGetValue(file, out remoteHash)) {
                        string localHash = GetFileHash(file);
                        if (remoteHash != localHash && !string.IsNullOrEmpty(remoteHash)) {
                            byte[] fileData = null;
                            if (isStatic) {
                                string fileUrl = url + "/" + file.Replace("\\", "/");
                                fileData = DownloadFileWithTimeout(fileUrl, 15000);
                            } else {
                                string fileUrl = url + "/api/update/file?path=" + Uri.EscapeDataString(file);
                                byte[] jsonBytes = DownloadFileWithTimeout(fileUrl, 15000);
                                if (jsonBytes != null) {
                                    string jsonStr = Encoding.UTF8.GetString(jsonBytes);
                                    if (jsonStr.Contains("\"success\":true")) {
                                        string base64 = ParseStringKey(jsonStr, "content");
                                        if (!string.IsNullOrEmpty(base64)) {
                                            fileData = Convert.FromBase64String(base64);
                                        }
                                    }
                                }
                            }
                            if (fileData != null && fileData.Length > 0) {
                                string fullPath = Path.Combine(AppDir, file.Replace("/", "\\"));
                                string dir = Path.GetDirectoryName(fullPath);
                                if (!Directory.Exists(dir)) {
                                    Directory.CreateDirectory(dir);
                                }
                                if (file.Equals("RobloxRAM_Optimizer.exe", StringComparison.OrdinalIgnoreCase)) {
                                    string newExePath = fullPath + ".new";
                                    File.WriteAllBytes(newExePath, fileData);
                                    needRestart = true;
                                } else {
                                    File.WriteAllBytes(fullPath, fileData);
                                }
                                updatedFiles.Add(file);
                            }
                        }
                    }
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("{\"success\":true,\"restart_required\":" + needRestart.ToString().ToLower() + ",\"updated_files\":[");
                for (int i = 0; i < updatedFiles.Count; i++) {
                    sb.Append("\"" + EscapeJson(updatedFiles[i]) + "\"");
                    if (i < updatedFiles.Count - 1) sb.Append(",");
                }
                sb.Append("]}");
                return sb.ToString();
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"Lỗi tải cập nhật: " + EscapeJson(ex.Message) + "\"}";
            }
        }

        // POST /api/update/restart
        if (path == "/api/update/restart" && request.HttpMethod == "POST") {
            try {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string oldExePath = exePath + ".old";
                string newExePath = exePath + ".new";
                if (File.Exists(newExePath)) {
                    if (File.Exists(oldExePath)) {
                        try { File.Delete(oldExePath); } catch {}
                    }
                    File.Move(exePath, oldExePath);
                    File.Move(newExePath, exePath);
                    Process.Start(exePath);
                    Shutdown();
                    return "{\"success\":true}";
                } else {
                    return "{\"success\":false,\"error\":\"Không tìm thấy tệp tin cập nhật mới .new\"}";
                }
            } catch (Exception ex) {
                return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        return "{\"success\":false,\"error\":\"Endpoint not found\"}";
    }

    // ──────────────────────────────────────────────
    // Roblox Process Scanner
    // ──────────────────────────────────────────────
    static List<ProcessInfo> ScanRobloxProcesses() {
        var results = new List<ProcessInfo>();
        var activePids = new List<int>();

        try {
            var processes = Process.GetProcesses();
            foreach (var p in processes) {
                try {
                    string name = p.ProcessName.ToLower();
                    if (name.Contains("robloxplayerbeta") || name.Contains("windows10universal")) {
                        int pid = p.Id;
                        activePids.Add(pid);

                        // Calculate RAM usage (Private Working Set matching Task Manager)
                        double ramMb = 0.0;
                        ulong ramBytes = 0;
                        double vmsMb = 0.0;
                        try {
                            ramMb = GetPrivateWorkingSetMb(pid, p);
                            ramBytes = (ulong)(ramMb * 1024.0 * 1024.0);
                            vmsMb = p.VirtualMemorySize64 / (1024.0 * 1024.0);
                        } catch {}

                        // CPU percentage calculation
                        double cpuPercent = GetCpuUsage(p);

                        // Uptime calculation
                        int uptimeSecs = 0;
                        try {
                            uptimeSecs = (int)(DateTime.Now - p.StartTime).TotalSeconds;
                        } catch {}

                        // Thread count
                        int threads = 0;
                        try {
                            threads = p.Threads.Count;
                        } catch {}

                        // Priority string
                        string priority = GetCpuPriorityString(pid);

                        // I/O rate calculation
                        ulong readBytes;
                        ulong writeBytes;
                        double ioRate = GetIoRate(p, out readBytes, out writeBytes);

                        // Page fault rate calculation
                        double pageFaultRate = GetPageFaultRate(p);

                        // Status setting
                        string status = "running";
                        lock (StateLock) {
                            if (SuspendedPids.Contains(pid)) status = "suspended";
                            else if (pid == GetForegroundPid()) status = "active";
                        }

                        // Get custom label
                        string label = GetProcessLabel(pid);

                        IntPtr winHandle = IntPtr.Zero;
                        try { winHandle = p.MainWindowHandle; } catch {}

                        int limitMb = 0;
                        lock (StateLock) {
                            if (RamLimits.ContainsKey(pid)) limitMb = RamLimits[pid];
                        }

                        results.Add(new ProcessInfo {
                            Pid = pid,
                            Label = label,
                            RamMb = ramMb,
                            RamBytes = ramBytes,
                            VmsMb = vmsMb,
                            CpuPercent = cpuPercent,
                            Status = status,
                            UptimeSeconds = uptimeSecs,
                            UptimeFormatted = FormatUptime(uptimeSecs),
                            PageFaultRate = pageFaultRate,
                            IoRateKbps = ioRate,
                            Threads = threads,
                            ReadBytes = readBytes,
                            WriteBytes = writeBytes,
                            Priority = priority,
                            RamLimitMb = limitMb,
                            MainWindowHandle = winHandle
                        });
                    }
                } catch {}
            }
        } catch {}

        // Cleanup lists
        CleanupDeadPids(activePids);

        // Sort results by label number
        results.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));
        return results;
    }

    static List<int> GetRobloxPids() {
        var pids = new List<int>();
        try {
            var processes = Process.GetProcesses();
            foreach (var p in processes) {
                string name = p.ProcessName.ToLower();
                if (name.Contains("robloxplayerbeta") || name.Contains("windows10universal")) {
                    pids.Add(p.Id);
                }
            }
        } catch {}
        return pids;
    }

    static string GetProcessLabel(int pid) {
        lock (StateLock) {
            if (!ProcessLabels.ContainsKey(pid)) {
                ProcessCounter++;
                ProcessLabels[pid] = "Roblox #" + ProcessCounter;
            }
            return ProcessLabels[pid];
        }
    }

    static string FormatUptime(int seconds) {
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        if (hours > 0) return string.Format("{0}h {1}m", hours, minutes);
        return string.Format("{0}m", minutes);
    }

    // CPU Percent Cache implementation
    static double GetCpuUsage(Process p) {
        int pid = p.Id;
        DateTime now = DateTime.UtcNow;
        TimeSpan currentCpuTime;
        try {
            currentCpuTime = p.TotalProcessorTime;
        } catch {
            return 0.0;
        }

        lock (StateLock) {
            if (CpuCache.ContainsKey(pid)) {
                var cached = CpuCache[pid];
                double timeDiff = (now - cached.Time).TotalMilliseconds;
                if (timeDiff < 500) {
                    return cached.LastValue;
                }

                double cpuDiff = (currentCpuTime - cached.ProcessorTime).TotalMilliseconds;
                double percent = (cpuDiff / (timeDiff * Environment.ProcessorCount)) * 100.0;
                percent = Math.Max(0.0, Math.Min(100.0, percent));
                
                CpuCache[pid] = new CpuCacheItem {
                    ProcessorTime = currentCpuTime,
                    Time = now,
                    LastValue = percent
                };
                return percent;
            } else {
                CpuCache[pid] = new CpuCacheItem {
                    ProcessorTime = currentCpuTime,
                    Time = now,
                    LastValue = 0.0
                };
                return 0.0;
            }
        }
    }

    // I/O rate calculation
    static double GetIoRate(Process p, out ulong readBytes, out ulong writeBytes) {
        int pid = p.Id;
        readBytes = 0;
        writeBytes = 0;
        
        IO_COUNTERS counters;
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return 0.0;
        
        try {
            if (GetProcessIoCounters(hProcess, out counters)) {
                readBytes = counters.ReadTransferCount;
                writeBytes = counters.WriteTransferCount;
                ulong total = readBytes + writeBytes;
                
                DateTime now = DateTime.UtcNow;
                lock (StateLock) {
                    if (IoCache.ContainsKey(pid)) {
                        var cached = IoCache[pid];
                        double timeDiff = (now - cached.Time).TotalSeconds;
                        if (timeDiff < 0.5) return cached.LastValue;
                        
                        double diffBytes = (double)(total - cached.TotalBytes);
                        double kbps = (diffBytes / timeDiff) / 1024.0;
                        kbps = Math.Max(0.0, kbps);
                        
                        IoCache[pid] = new IoCacheItem {
                            TotalBytes = total,
                            Time = now,
                            LastValue = kbps
                        };
                        return kbps;
                    } else {
                        IoCache[pid] = new IoCacheItem {
                            TotalBytes = total,
                            Time = now,
                            LastValue = 0.0
                        };
                        return 0.0;
                    }
                }
            }
        } finally {
            CloseHandle(hProcess);
        }
        return 0.0;
    }

    // Page fault rate calculation
    static double GetPageFaultRate(Process p) {
        int pid = p.Id;
        PROCESS_MEMORY_COUNTERS_EX pmc;
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (hProcess == IntPtr.Zero) return 0.0;
        
        try {
            pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));
            if (GetProcessMemoryInfo(hProcess, out pmc, pmc.cb)) {
                uint faults = pmc.PageFaultCount;
                DateTime now = DateTime.UtcNow;
                lock (StateLock) {
                    if (PageFaultCache.ContainsKey(pid)) {
                        var cached = PageFaultCache[pid];
                        double timeDiff = (now - cached.Time).TotalSeconds;
                        if (timeDiff < 0.5) return cached.LastValue;
                        
                        double diffFaults = (double)(faults - cached.Faults);
                        double rate = diffFaults / timeDiff;
                        rate = Math.Max(0.0, rate);
                        
                        PageFaultCache[pid] = new PageFaultCacheItem {
                            Faults = faults,
                            Time = now,
                            LastValue = rate
                        };
                        return rate;
                    } else {
                        PageFaultCache[pid] = new PageFaultCacheItem {
                            Faults = faults,
                            Time = now,
                            LastValue = 0.0
                        };
                        return 0.0;
                    }
                }
            }
        } finally {
            CloseHandle(hProcess);
        }
        return 0.0;
    }

    // ──────────────────────────────────────────────
    // Real-Time Background Monitor Loop (Every 2s)
    // ──────────────────────────────────────────────
    static void BackgroundMonitorLoop() {
        CpuThrottleLoop(); // Start CPU background state machine thread
        
        while (Running) {
            try {
                var processes = ScanRobloxProcesses();
                
                // Ghost process detector logic
                foreach (var p in processes) {
                    bool isCandidate = (p.UptimeSeconds > 20) && (p.Status != "suspended") && 
                                       ((p.RamMb < 35.0 && p.CpuPercent < 0.1) || (p.MainWindowHandle == IntPtr.Zero && p.CpuPercent < 3.0));
                    if (isCandidate) {
                        lock (StateLock) {
                            if (LastGhostIo.ContainsKey(p.Pid)) {
                                GhostIoItem prevIo = LastGhostIo[p.Pid];
                                if (p.ReadBytes == prevIo.ReadBytes && p.WriteBytes == prevIo.WriteBytes) {
                                    int confirmations = GhostConfirmations.ContainsKey(p.Pid) ? GhostConfirmations[p.Pid] : 0;
                                    confirmations++;
                                    GhostConfirmations[p.Pid] = confirmations;
                                    
                                    if (confirmations >= 3) {
                                        int pid = p.Pid;
                                        string label = p.Label;
                                        Forget(BroadcastMessage("ghost_killed", string.Format(CultureInfo.InvariantCulture, "{{\"pid\":{0},\"label\":\"{1}\"}}", pid, label.Replace("\"", "\\\""))));
                                        KillProcess(pid);
                                        
                                        LastGhostIo.Remove(pid);
                                        GhostConfirmations.Remove(pid);
                                    }
                                } else {
                                    LastGhostIo[p.Pid] = new GhostIoItem { ReadBytes = p.ReadBytes, WriteBytes = p.WriteBytes };
                                    GhostConfirmations[p.Pid] = 0;
                                }
                            } else {
                                LastGhostIo[p.Pid] = new GhostIoItem { ReadBytes = p.ReadBytes, WriteBytes = p.WriteBytes };
                                GhostConfirmations[p.Pid] = 0;
                            }
                        }
                    } else {
                        lock (StateLock) {
                            if (LastGhostIo.ContainsKey(p.Pid)) {
                                LastGhostIo.Remove(p.Pid);
                                GhostConfirmations.Remove(p.Pid);
                            }
                        }
                    }
                }

                // Silence check for extra Roblox processes (crash handlers, launcher)
                try {
                    var allProcs = Process.GetProcesses();
                    foreach (var p in allProcs) {
                        try {
                            string name = p.ProcessName.ToLower();
                            if (name.Contains("robloxcrashhandler") || name.Contains("robloxplayerlauncher")) {
                                int pid = p.Id;
                                int uptimeSecs = 0;
                                try { uptimeSecs = (int)(DateTime.Now - p.StartTime).TotalSeconds; } catch {}
                                if (uptimeSecs > 15) {
                                    IntPtr winHandle = IntPtr.Zero;
                                    try { winHandle = p.MainWindowHandle; } catch {}
                                    if (winHandle == IntPtr.Zero) {
                                        p.Kill();
                                    }
                                }
                            }
                        } catch {}
                    }
                } catch {}

                string payload = SerializeProcesses(processes);
                Forget(BroadcastMessage("process_update", payload));

                // Update system memory
                double total, used, avail;
                double percent = GetSystemMemoryPercent(out total, out used, out avail);
                double robloxTotal = 0;
                foreach (var p in processes) robloxTotal += p.RamMb;

                int autoRemaining = GetAutoFlushRemainingSeconds();
                string sysPayload = SerializeSystemInfo(total, used, avail, percent, robloxTotal, processes.Count, IsUserAnAdmin(), autoRemaining);
                Forget(BroadcastMessage("system_update", sysPayload));

                // Auto-limit logic for newly seen tabs and periodic gentle trim for background limited tabs
                var activePids = GetRobloxPids();
                int fgPid = GetForegroundPid();
                foreach (int pid in activePids) {
                    bool containsPid;
                    lock (StateLock) {
                        containsPid = SeenPids.Contains(pid);
                    }

                    if (!containsPid) {
                        lock (StateLock) {
                            SeenPids.Add(pid);
                        }
                        if (GlobalLimitEnabled) {
                            int limit = GlobalLimitMb;
                            Task.Run(() => {
                                SetRamLimit(pid, limit);
                            });
                        }
                    } else {
                        bool hasLimit = false;
                        int limitVal = 0;
                        lock (StateLock) {
                            hasLimit = RamLimits.ContainsKey(pid);
                            if (hasLimit) limitVal = RamLimits[pid];
                        }
                        if (pid != fgPid && hasLimit) {
                            double curRamMb = GetPrivateWorkingSetMb(pid, null);
                            if (curRamMb > (limitVal + 50)) {
                                // Re-apply soft working set limit to gently page out memory via pagefile/virtual memory without lag spikes
                                RestoreRamLimit(pid);
                            }
                        }
                    }
                }

                // System high memory emergency response (>90%)
                if (percent > 90) {
                    EmergencyFlush(activePids, processes);
                }
            } catch {}
            Thread.Sleep(1000);
        }
    }

    // GCOptimizer - Lower background CPU & I/O priorities
    static int ActiveProcessPid = 0;
    static void CpuThrottleLoop() {
        Thread thread = new Thread(() => {
            while (Running) {
                try {
                    var pids = GetRobloxPids();
                    if (pids.Count > 0) {
                        int foregroundPid = GetForegroundPid();
                        int currentActive = pids.Contains(foregroundPid) ? foregroundPid : 0;
                        
                        foreach (int pid in pids) {
                            if (pid == currentActive) {
                                if (ActiveProcessPid != pid) {
                                    RestoreActivePriorities(pid);
                                    SuspendRamLimit(pid);
                                }
                            } else {
                                bool needThrottle = false;
                                lock (StateLock) {
                                    needThrottle = (ActiveProcessPid == pid || !IoPriorities.ContainsKey(pid) || IoPriorities[pid] != 1);
                                }
                                if (needThrottle) {
                                    ThrottleBackgroundPriorities(pid);
                                    RestoreRamLimit(pid);
                                }
                            }
                        }
                        ActiveProcessPid = currentActive;
                    } else {
                        ActiveProcessPid = 0;
                    }
                } catch {}
                Thread.Sleep(2000);
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    static int GetForegroundPid() {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        return (int)pid;
    }

    static void ThrottleBackgroundPriorities(int pid) {
        try {
            // CPU to Below Normal
            lock (StateLock) {
                if (!OriginalPriorities.ContainsKey(pid)) {
                    OriginalPriorities[pid] = GetCpuPriorityString(pid);
                }
            }
            SetCpuPriority(pid, "below_normal", saveOriginal: false);
            
            // I/O to Low (1)
            SetIoPriority(pid, 1);
            lock (StateLock) {
                IoPriorities[pid] = 1;
            }
        } catch {}
    }

    static void RestoreActivePriorities(int pid) {
        try {
            // CPU to Normal
            string original = "normal";
            lock (StateLock) {
                if (OriginalPriorities.ContainsKey(pid)) {
                    original = OriginalPriorities[pid];
                    OriginalPriorities.Remove(pid);
                }
            }
            SetCpuPriority(pid, original, saveOriginal: false);

            // I/O to Normal (2)
            SetIoPriority(pid, 2);
            lock (StateLock) {
                IoPriorities[pid] = 2;
            }
        } catch {}
    }

    static bool SetIoPriority(int pid, int level) {
        IntPtr hProcess = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try {
            int priority = level;
            // 33 represents ProcessIoPriority
            int status = NtSetInformationProcess(hProcess, 33, ref priority, sizeof(int));
            return status == 0;
        } finally {
            CloseHandle(hProcess);
        }
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    static extern int NtSetInformationProcess(IntPtr hProcess, int ProcessInformationClass, ref int ProcessInformation, int ProcessInformationLength);

    static string GetCpuPriorityString(int pid) {
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return "normal";
        try {
            uint pclass = GetPriorityClass(hProcess);
            if (pclass == IDLE_PRIORITY_CLASS) return "idle";
            if (pclass == BELOW_NORMAL_PRIORITY_CLASS) return "below_normal";
            if (pclass == NORMAL_PRIORITY_CLASS) return "normal";
            if (pclass == ABOVE_NORMAL_PRIORITY_CLASS) return "above_normal";
            if (pclass == HIGH_PRIORITY_CLASS) return "high";
            return "normal";
        } finally {
            CloseHandle(hProcess);
        }
    }

    static bool SetCpuPriority(int pid, string level, bool saveOriginal = true) {
        uint pclass = NORMAL_PRIORITY_CLASS;
        if (level == "idle") pclass = IDLE_PRIORITY_CLASS;
        else if (level == "below_normal") pclass = BELOW_NORMAL_PRIORITY_CLASS;
        else if (level == "normal") pclass = NORMAL_PRIORITY_CLASS;
        else if (level == "above_normal") pclass = ABOVE_NORMAL_PRIORITY_CLASS;
        else if (level == "high") pclass = HIGH_PRIORITY_CLASS;

        IntPtr hProcess = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try {
            if (saveOriginal) {
                lock (StateLock) {
                    if (!OriginalPriorities.ContainsKey(pid)) {
                        OriginalPriorities[pid] = GetCpuPriorityString(pid);
                    }
                }
            }
            return SetPriorityClass(hProcess, pclass);
        } finally {
            CloseHandle(hProcess);
        }
    }

    // Emergency memory trim
    static DateTime LastEmergencyFlushTime = DateTime.MinValue;
    static void EmergencyFlush(List<int> activePids, List<ProcessInfo> scannerData) {
        if ((DateTime.Now - LastEmergencyFlushTime).TotalSeconds < 60) return;
        LastEmergencyFlushTime = DateTime.Now;

        Forget(BroadcastMessage("emergency_flush_triggered", "{\"percent\":90}"));
        
        Task.Run(() => {
            foreach (int pid in activePids) {
                // Skip foreground active tab
                if (pid == GetForegroundPid()) continue;
                
                // Do a moderate trim
                OptimizeProcess(pid, "moderate", force: false);
                Thread.Sleep(300);
            }
        });
    }

    static void CleanupDeadPids(List<int> activePids) {
        lock (StateLock) {
            // Limits
            var deadLimitPids = new List<int>();
            foreach (var key in JobHandles.Keys) {
                if (!activePids.Contains(key)) deadLimitPids.Add(key);
            }
            foreach (int pid in deadLimitPids) RawRemoveRamLimit(pid);

            // Seen list
            SeenPids.IntersectWith(activePids);

            // Priorities
            var deadOriginalPids = new List<int>();
            foreach (var key in OriginalPriorities.Keys) {
                if (!activePids.Contains(key)) deadOriginalPids.Add(key);
            }
            foreach (int pid in deadOriginalPids) OriginalPriorities.Remove(pid);

            // I/O Priorities
            var deadIoPids = new List<int>();
            foreach (var key in IoPriorities.Keys) {
                if (!activePids.Contains(key)) deadIoPids.Add(key);
            }
            foreach (int pid in deadIoPids) IoPriorities.Remove(pid);

            // Suspend timers
            var deadSuspendPids = new List<int>();
            foreach (var key in SuspendTimers.Keys) {
                if (!activePids.Contains(key)) deadSuspendPids.Add(key);
            }
            foreach (int pid in deadSuspendPids) RawCancelSuspendTimer(pid);

            // Caching dicts cleanup
            var deadLabels = new List<int>();
            foreach (var key in ProcessLabels.Keys) {
                if (!activePids.Contains(key)) deadLabels.Add(key);
            }
            foreach (int pid in deadLabels) ProcessLabels.Remove(pid);

            var deadCpu = new List<int>();
            foreach (var key in CpuCache.Keys) {
                if (!activePids.Contains(key)) deadCpu.Add(key);
            }
            foreach (int pid in deadCpu) CpuCache.Remove(pid);

            var deadIo = new List<int>();
            foreach (var key in IoCache.Keys) {
                if (!activePids.Contains(key)) deadIo.Add(key);
            }
            foreach (int pid in deadIo) IoCache.Remove(pid);

            var deadFault = new List<int>();
            foreach (var key in PageFaultCache.Keys) {
                if (!activePids.Contains(key)) deadFault.Add(key);
            }
            foreach (int pid in deadFault) PageFaultCache.Remove(pid);

            var deadTrim = new List<int>();
            foreach (var key in LastTrimTimes.Keys) {
                if (!activePids.Contains(key)) deadTrim.Add(key);
            }
            foreach (int pid in deadTrim) LastTrimTimes.Remove(pid);

            var deadGhostIo = new List<int>();
            foreach (var key in LastGhostIo.Keys) {
                if (!activePids.Contains(key)) deadGhostIo.Add(key);
            }
            foreach (int pid in deadGhostIo) LastGhostIo.Remove(pid);

            var deadGhostConf = new List<int>();
            foreach (var key in GhostConfirmations.Keys) {
                if (!activePids.Contains(key)) deadGhostConf.Add(key);
            }
            foreach (int pid in deadGhostConf) GhostConfirmations.Remove(pid);

            var deadCounters = new List<int>();
            foreach (var key in PrivateWsCounters.Keys) {
                if (!activePids.Contains(key)) deadCounters.Add(key);
            }
            foreach (int pid in deadCounters) {
                try { PrivateWsCounters[pid].Dispose(); } catch {}
                PrivateWsCounters.Remove(pid);
            }
        }
    }

    // ──────────────────────────────────────────────
    // Memory Limit via Working Set Soft Flags
    // ──────────────────────────────────────────────
    static bool SetRamLimit(int pid, int limitMb) {
        if (limitMb < 50 || limitMb > 8192) return false;
        lock (StateLock) {
            try {
                ulong bytes = (ulong)limitMb * 1024 * 1024;
                bool isActive = (pid == GetForegroundPid());

                // 1. Apply Memory Priority & Soft Working Set Limit directly on the process
                IntPtr hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_SET_INFORMATION | PROCESS_QUERY_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero) {
                    try {
                        // Set Memory Priority (5 = Normal, 1 = Very Low)
                        SetMemoryPriority(hProcess, isActive ? 5U : 1U);
                        
                        // Set Working Set Size with soft flags
                        // QUOTA_LIMITS_HARDWS_MIN_DISABLE = 0x2, QUOTA_LIMITS_HARDWS_MAX_DISABLE = 0x8
                        uint flags = 0x00000002 | 0x00000008;
                        if (isActive) {
                            SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)ulong.MaxValue, flags);
                        } else {
                            SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)bytes, flags);
                        }
                    } finally {
                        CloseHandle(hProcess);
                    }
                }

                // 2. Keep Job Object for tracking (LimitFlags = 0 to avoid hard constraints)
                if (JobHandles.ContainsKey(pid)) {
                    RamLimits[pid] = limitMb;
                    return true;
                }

                // Create new Job
                string jobName = "RobloxRAM_Limit_Job_" + pid;
                IntPtr jobHandle = CreateJobObjectW(IntPtr.Zero, jobName);
                if (jobHandle != IntPtr.Zero) {
                    try {
                        IntPtr hProcForJob = OpenProcess(PROCESS_SET_QUOTA | PROCESS_TERMINATE, false, pid);
                        if (hProcForJob != IntPtr.Zero) {
                            try {
                                if (AssignProcessToJobObject(jobHandle, hProcForJob)) {
                                    JobHandles[pid] = jobHandle;
                                } else {
                                    CloseHandle(jobHandle);
                                }
                            } finally {
                                CloseHandle(hProcForJob);
                            }
                        } else {
                            CloseHandle(jobHandle);
                        }
                    } catch {
                        CloseHandle(jobHandle);
                    }
                }

                RamLimits[pid] = limitMb;
                return true;
            } catch {}
            return false;
        }
    }

    static bool RemoveRamLimit(int pid) {
        lock (StateLock) {
            return RawRemoveRamLimit(pid);
        }
    }

    static bool RawRemoveRamLimit(int pid) {
        IntPtr hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_SET_INFORMATION, false, pid);
        if (hProcess != IntPtr.Zero) {
            try {
                // Restore default Memory Priority (5) and clear Working Set limits
                SetMemoryPriority(hProcess, 5U);
                SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)ulong.MaxValue, 0x00000002 | 0x00000008);
            } finally {
                CloseHandle(hProcess);
            }
        }

        if (JobHandles.ContainsKey(pid)) {
            IntPtr hJob = JobHandles[pid];
            CloseHandle(hJob);
            JobHandles.Remove(pid);
        }
        RamLimits.Remove(pid);
        return true;
    }

    static void SuspendRamLimit(int pid) {
        lock (StateLock) {
            IntPtr hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_SET_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero) {
                try {
                    // Active foreground process: Normal memory priority & unlimited Working Set
                    SetMemoryPriority(hProcess, 5U);
                    SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)ulong.MaxValue, 0x00000002 | 0x00000008);
                } finally {
                    CloseHandle(hProcess);
                }
            }
        }
    }

    static void RestoreRamLimit(int pid) {
        lock (StateLock) {
            if (RamLimits.ContainsKey(pid)) {
                int limitMb = RamLimits[pid];
                ulong bytes = (ulong)limitMb * 1024 * 1024;
                
                IntPtr hProcess = OpenProcess(PROCESS_SET_QUOTA | PROCESS_SET_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero) {
                    try {
                        // Background process: Very Low memory priority & soft Working Set limit
                        SetMemoryPriority(hProcess, 1U);
                        SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)bytes, 0x00000002 | 0x00000008);
                    } finally {
                        CloseHandle(hProcess);
                    }
                }
            }
        }
    }

    static void CleanupAllLimits() {
        lock (StateLock) {
            var pids = new List<int>(JobHandles.Keys);
            foreach (int pid in pids) RawRemoveRamLimit(pid);
        }
    }

    // ──────────────────────────────────────────────
    // Suspend & Auto Resume
    // ──────────────────────────────────────────────
    static bool SuspendProcess(int pid, int minutes) {
        IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try {
            int status = NtSuspendProcess(hProcess);
            if (status == 0) {
                lock (StateLock) {
                    SuspendedPids.Add(pid);
                    RawCancelSuspendTimer(pid);

                    if (minutes > 0) {
                        var timer = new System.Threading.Timer((state) => {
                            ResumeProcess((int)state);
                        }, pid, minutes * 60 * 1000, Timeout.Infinite);
                        SuspendTimers[pid] = timer;
                    }
                }
                return true;
            }
        } finally {
            CloseHandle(hProcess);
        }
        return false;
    }

    static bool ResumeProcess(int pid) {
        IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (hProcess == IntPtr.Zero) return false;
        try {
            int status = NtResumeProcess(hProcess);
            if (status == 0) {
                lock (StateLock) {
                    SuspendedPids.Remove(pid);
                    RawCancelSuspendTimer(pid);
                }
                return true;
            }
        } finally {
            CloseHandle(hProcess);
        }
        return false;
    }

    static void CancelSuspendTimer(int pid) {
        lock (StateLock) {
            RawCancelSuspendTimer(pid);
        }
    }

    static void RawCancelSuspendTimer(int pid) {
        if (SuspendTimers.ContainsKey(pid)) {
            SuspendTimers[pid].Dispose();
            SuspendTimers.Remove(pid);
        }
    }

    // ──────────────────────────────────────────────
    // Pagefile Configuration (Registry & WMI)
    // ──────────────────────────────────────────────
    static string GetPagefileJson() {
        string sysDrive = GetSystemDriveLetter();
        try {
            // Read directly from Registry first (100% reliable)
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management")) {
                if (key != null) {
                    string[] pagingFiles = key.GetValue("PagingFiles") as string[];
                    bool auto = true;
                    double sizeGb = 0.0;

                    if (pagingFiles != null && pagingFiles.Length > 0) {
                        foreach (string pf in pagingFiles) {
                            if (string.IsNullOrEmpty(pf)) continue;
                            string[] parts = pf.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0) {
                                string path = parts[0];
                                if (path.StartsWith("?", StringComparison.OrdinalIgnoreCase)) {
                                    auto = true;
                                } else {
                                    auto = false;
                                    if (parts.Length > 1) {
                                        double initMb;
                                        if (double.TryParse(parts[1], out initMb)) {
                                            sizeGb = initMb / 1024.0;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (auto || sizeGb == 0.0) {
                        string pfPath = sysDrive + ":\\pagefile.sys";
                        if (File.Exists(pfPath)) {
                            try {
                                FileInfo info = new FileInfo(pfPath);
                                sizeGb = (double)info.Length / (1024.0 * 1024.0 * 1024.0);
                            } catch {}
                        }
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"success\":true,\"auto\":{0},\"size_gb\":{1:F1},\"drive\":\"{2}\"}}",
                        auto.ToString().ToLower(),
                        sizeGb,
                        sysDrive
                    );
                }
            }
        } catch {}

        // Fallback to WMI if Registry fails
        try {
            using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem")) {
                using (var results = searcher.Get()) {
                    bool auto = true;
                    foreach (ManagementObject item in results) {
                        try {
                            auto = (bool)item["AutomaticManagedPagefile"];
                        } finally {
                            item.Dispose();
                        }
                        break;
                    }

                    double sizeGb = 0.0;
                    using (var fileSearcher = new ManagementObjectSearcher("Select * from Win32_PageFileSetting")) {
                        using (var fileResults = fileSearcher.Get()) {
                            foreach (ManagementObject item in fileResults) {
                                try {
                                    sizeGb = Convert.ToDouble(item["InitialSize"]) / 1024.0;
                                } finally {
                                    item.Dispose();
                                }
                                break;
                            }
                        }
                    }

                    if (auto && sizeGb == 0.0) {
                        string path = sysDrive + ":\\pagefile.sys";
                        if (File.Exists(path)) {
                            FileInfo info = new FileInfo(path);
                            sizeGb = info.Length / (1024.0 * 1024.0 * 1024.0);
                        }
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"success\":true,\"auto\":{0},\"size_gb\":{1:F1},\"drive\":\"{2}\"}}",
                        auto.ToString().ToLower(),
                        sizeGb,
                        sysDrive
                    );
                }
            }
        } catch {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"success\":true,\"auto\":true,\"size_gb\":0.0,\"drive\":\"{0}\"}}",
                sysDrive
            );
        }
    }

    static void ConfigurePagefile(int sizeGb) {
        if (!IsUserAnAdmin()) {
            throw new Exception("Yêu cầu quyền Administrator để thay đổi RAM ảo hệ thống.");
        }

        if (sizeGb < 0 || sizeGb > 128) {
            throw new ArgumentException("Dung lượng RAM ảo không hợp lệ (phải từ 0 đến 128 GB).");
        }

        string sysDrive = GetSystemDriveLetter();

        if (sizeGb == 0) {
            // Auto Managed
            try {
                // Try WMI first
                using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem")) {
                    using (var results = searcher.Get()) {
                        foreach (ManagementObject item in results) {
                            try {
                                if (!(bool)item["AutomaticManagedPagefile"]) {
                                    using (var fileSearcher = new ManagementObjectSearcher("Select * from Win32_PageFileSetting")) {
                                        using (var fileResults = fileSearcher.Get()) {
                                            foreach (ManagementObject fileSetting in fileResults) {
                                                try { fileSetting.Delete(); } catch {}
                                                finally { fileSetting.Dispose(); }
                                            }
                                        }
                                    }
                                    item["AutomaticManagedPagefile"] = true;
                                    item.Put();
                                }
                            } finally {
                                item.Dispose();
                            }
                            break;
                        }
                    }
                }
            } catch (Exception wmiEx) {
                // Registry Fallback if WMI fails (common on debloated OS)
                try {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true)) {
                        if (key != null) {
                            key.SetValue("PagingFiles", new string[] { "?:\\pagefile.sys" }, Microsoft.Win32.RegistryValueKind.MultiString);
                        } else {
                            throw new Exception("Không thể mở registry key SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management");
                        }
                    }
                } catch (Exception regEx) {
                    throw new Exception(string.Format("Lỗi khôi phục RAM ảo tự động (WMI và Registry đều thất bại).\nLỗi WMI: {0}\nLỗi Registry: {1}", wmiEx.Message, regEx.Message));
                }
            }
        } else {
            // Check free space on the detected system drive
            DriveInfo drive;
            try {
                drive = new DriveInfo(sysDrive);
            } catch (Exception ex) {
                throw new Exception(string.Format("Không thể truy cập ổ đĩa {0} để kiểm tra dung lượng: {1}", sysDrive, ex.Message));
            }

            long freeSpaceBytes = drive.AvailableFreeSpace;
            long requestedBytes = (long)sizeGb * 1024 * 1024 * 1024;
            
            // Get current pagefile size to account for replacement
            double currentSizeGb = 0.0;
            try {
                // Try Registry first
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management")) {
                    if (key != null) {
                        string[] pagingFiles = key.GetValue("PagingFiles") as string[];
                        if (pagingFiles != null && pagingFiles.Length > 0) {
                            foreach (string pf in pagingFiles) {
                                if (string.IsNullOrEmpty(pf)) continue;
                                string[] parts = pf.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 1 && !parts[0].StartsWith("?", StringComparison.OrdinalIgnoreCase)) {
                                    double initMb;
                                    if (double.TryParse(parts[1], out initMb)) {
                                        currentSizeGb = initMb / 1024.0;
                                    }
                                }
                            }
                        }
                    }
                }
            } catch {}

            if (currentSizeGb == 0.0) {
                try {
                    // Try WMI fallback
                    using (var fileSearcher = new ManagementObjectSearcher("Select * from Win32_PageFileSetting")) {
                        using (var fileResults = fileSearcher.Get()) {
                            foreach (ManagementObject item in fileResults) {
                                try {
                                    string name = item["Name"] as string;
                                    if (name != null && name.StartsWith(sysDrive + ":", StringComparison.OrdinalIgnoreCase)) {
                                        currentSizeGb = Convert.ToDouble(item["InitialSize"]) / 1024.0;
                                        break;
                                    }
                                } finally {
                                    item.Dispose();
                                }
                            }
                        }
                    }
                } catch {}
            }
            
            long currentBytes = (long)(currentSizeGb * 1024.0 * 1024.0 * 1024.0);
            long netRequiredBytes = requestedBytes - currentBytes;

            if (netRequiredBytes > 0 && freeSpaceBytes < (netRequiredBytes + 2L * 1024L * 1024L * 1024L)) {
                double freeGb = freeSpaceBytes / (1024.0 * 1024.0 * 1024.0);
                double reqGb = netRequiredBytes / (1024.0 * 1024.0 * 1024.0);
                throw new Exception(string.Format(
                    CultureInfo.InvariantCulture,
                    "Ổ đĩa {0}: không đủ dung lượng. Cần thêm {1:F1} GB nhưng ổ {0} chỉ còn trống {2:F1} GB.",
                    sysDrive,
                    reqGb + 2.0, // includes 2GB safety buffer
                    freeGb
                ));
            }

            int sizeMb = sizeGb * 1024;
            
            try {
                // Try WMI first
                using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem")) {
                    using (var results = searcher.Get()) {
                        foreach (ManagementObject item in results) {
                            try {
                                if ((bool)item["AutomaticManagedPagefile"]) {
                                    item["AutomaticManagedPagefile"] = false;
                                    item.Put();
                                }
                            } finally {
                                item.Dispose();
                            }
                            break;
                        }
                    }
                }

                bool found = false;
                using (var fileSearcher = new ManagementObjectSearcher("Select * from Win32_PageFileSetting")) {
                    using (var fileResults = fileSearcher.Get()) {
                        foreach (ManagementObject item in fileResults) {
                            try {
                                string name = item["Name"] as string;
                                if (name != null && name.StartsWith(sysDrive + ":", StringComparison.OrdinalIgnoreCase)) {
                                    item["InitialSize"] = sizeMb;
                                    item["MaximumSize"] = sizeMb;
                                    item.Put();
                                    found = true;
                                    break;
                                }
                            } finally {
                                item.Dispose();
                            }
                        }
                    }
                }

                if (!found) {
                    using (var mgmtClass = new ManagementClass("Win32_PageFileSetting")) {
                        using (var newInstance = mgmtClass.CreateInstance()) {
                            newInstance["Name"] = sysDrive + ":\\pagefile.sys";
                            newInstance["InitialSize"] = sizeMb;
                            newInstance["MaximumSize"] = sizeMb;
                            newInstance.Put();
                        }
                    }
                }
            } catch (Exception wmiEx) {
                // Registry Fallback if WMI fails
                try {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true)) {
                        if (key != null) {
                            string pagefileConfig = string.Format("{0}:\\pagefile.sys {1} {1}", sysDrive, sizeMb);
                            key.SetValue("PagingFiles", new string[] { pagefileConfig }, Microsoft.Win32.RegistryValueKind.MultiString);
                        } else {
                            throw new Exception("Không thể mở registry key SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management");
                        }
                    }
                } catch (Exception regEx) {
                    throw new Exception(string.Format("Lỗi cấu hình RAM ảo (WMI và Registry đều thất bại).\nLỗi WMI: {0}\nLỗi Registry: {1}", wmiEx.Message, regEx.Message));
                }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Process RAM Trimming Logic
    // ──────────────────────────────────────────────
    static string OptimizeProcess(int pid, string tier, bool force) {
        try {
            var processes = Process.GetProcesses();
            Process targetProc = null;
            foreach (var p in processes) {
                if (p.Id == pid) { targetProc = p; break; }
            }
            if (targetProc == null) return "{\"success\":false,\"error\":\"Process not found\"}";

            double beforeMb = targetProc.WorkingSet64 / (1024.0 * 1024.0);
            
            // Checks for Auto Trim cooldowns (if not forced)
            if (!force) {
                bool isCoolingDown = false;
                lock (StateLock) {
                    if (LastTrimTimes.ContainsKey(pid)) {
                        if ((DateTime.UtcNow - DateTimeFromUnix(LastTrimTimes[pid])).TotalSeconds < 60) {
                            isCoolingDown = true;
                        }
                    }
                }
                if (isCoolingDown) {
                    return "{\"success\":true,\"skipped\":true,\"reason\":\"cooldown\",\"pid\":" + pid + "}";
                }
                
                // Active foreground window safety
                if (pid == GetForegroundPid()) {
                    return "{\"success\":true,\"skipped\":true,\"reason\":\"active_foreground\",\"pid\":" + pid + "}";
                }

                // Startup protection guard (60s)
                try {
                    double uptime = (DateTime.Now - targetProc.StartTime).TotalSeconds;
                    if (uptime < 60.0) {
                        return "{\"success\":true,\"skipped\":true,\"reason\":\"startup_protection\",\"pid\":" + pid + "}";
                    }
                } catch {}
            }

            // Execute trimming
            double ratio = 0.8;
            if (tier == "moderate") ratio = 0.5;
            else if (tier == "aggressive") ratio = 0.3;

            long minWs = 200 * 1024 * 1024; // 200MB floor
            long memBefore = targetProc.WorkingSet64;
            
            if (memBefore <= minWs && !force) {
                return "{\"success\":true,\"skipped\":true,\"reason\":\"already_min_footprint\",\"pid\":" + pid + "}";
            }

            long target = Math.Max((long)(memBefore * ratio), minWs);
            long minSize = Math.Max(minWs, target / 2);
            long maxSize = target;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, pid);
            if (hProcess != IntPtr.Zero) {
                try {
                    // Try SetProcessWorkingSetSizeEx
                    bool success = SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)minSize, (UIntPtr)maxSize, 0x00000002 | 0x00000008); // soft flags
                    if (!success) {
                        // Fallback
                        SetProcessWorkingSetSize(hProcess, (UIntPtr)minSize, (UIntPtr)maxSize);
                    }
                    Thread.Sleep(300);
                    // Reset working set limit back to the configured soft limit if one exists, otherwise set to unlimited
                    bool hasLimit;
                    int limitMb = 0;
                    lock (StateLock) {
                        hasLimit = RamLimits.ContainsKey(pid);
                        if (hasLimit) limitMb = RamLimits[pid];
                    }
                    if (hasLimit) {
                        ulong bytes = (ulong)limitMb * 1024 * 1024;
                        SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)(16 * 1024 * 1024), (UIntPtr)bytes, 0x00000002 | 0x00000008);
                    } else {
                        SetProcessWorkingSetSizeEx(hProcess, (UIntPtr)ulong.MaxValue, (UIntPtr)ulong.MaxValue, 0x00000002 | 0x00000008);
                    }
                } finally {
                    CloseHandle(hProcess);
                }
            }

            // Page faults trigger update
            double afterMb = 0.0;
            try {
                targetProc.Refresh();
                afterMb = targetProc.WorkingSet64 / (1024.0 * 1024.0);
            } catch {}

            double savedMb = Math.Max(0.0, beforeMb - afterMb);
            TotalSavedMb += savedMb;
            TotalOptCount++;
            lock (StateLock) {
                LastTrimTimes[pid] = DateTimeToUnix(DateTime.UtcNow);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\n" +
                "  \"success\": true,\n" +
                "  \"pid\": {0},\n" +
                "  \"tier\": \"{1}\",\n" +
                "  \"before_mb\": {2:F1},\n" +
                "  \"after_mb\": {3:F1},\n" +
                "  \"saved_mb\": {4:F1}\n" +
                "}}",
                pid,
                tier,
                beforeMb,
                afterMb,
                savedMb
            );

        } catch (Exception ex) {
            return "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
        }
    }

    // ──────────────────────────────────────────────
    // Auto Optimizer Scheduler
    // ──────────────────────────────────────────────
    static System.Threading.Timer AutoSchedulerTimer;
    static DateTime NextAutoFlushTime = DateTime.MinValue;

    static void StartAutoScheduler() {
        StopAutoScheduler();
        int ms = AutoIntervalMinutes * 60 * 1000;
        NextAutoFlushTime = DateTime.Now.AddMilliseconds(ms);
        AutoSchedulerTimer = new System.Threading.Timer(AutoSchedulerCallback, null, ms, ms);
    }

    static void StopAutoScheduler() {
        if (AutoSchedulerTimer != null) {
            AutoSchedulerTimer.Dispose();
            AutoSchedulerTimer = null;
        }
        NextAutoFlushTime = DateTime.MinValue;
    }

    static void ResetAutoSchedulerInterval() {
        StartAutoScheduler();
    }

    static int GetAutoFlushRemainingSeconds() {
        if (NextAutoFlushTime == DateTime.MinValue) return -1;
        double diff = (NextAutoFlushTime - DateTime.Now).TotalSeconds;
        return Math.Max(0, (int)diff);
    }

    static void AutoSchedulerCallback(object state) {
        NextAutoFlushTime = DateTime.Now.AddMinutes(AutoIntervalMinutes);
        
        var pids = GetRobloxPids();
        if (pids.Count == 0) {
            Forget(BroadcastMessage("auto_optimize_tick", "{\"reason\":\"no_roblox_processes\",\"processes\":0,\"saved_mb\":0}"));
            return;
        }

        double totalSaved = 0.0;
        int count = 0;
        
        foreach (int pid in pids) {
            var res = OptimizeProcess(pid, AutoFlushTier, force: false);
            if (res.Contains("\"success\":true") && !res.Contains("\"skipped\":true")) {
                double savedMb = double.Parse(ParseStringKey(res, "saved_mb") ?? "0", CultureInfo.InvariantCulture);
                totalSaved += savedMb;
                count++;
            }
            Thread.Sleep(300);
        }

        string payload = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"reason\":\"timer_expired\",\"processes\":{0},\"saved_mb\":{1:F1},\"skipped\":{2}}}",
            count,
            totalSaved,
            pids.Count - count
        );
        Forget(BroadcastMessage("auto_optimize_tick", payload));
    }

    static void Forget(Task task) { }

    static bool EnablePrivilege(string privilegeName) {
        IntPtr hToken;
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out hToken)) {
            return false;
        }
        try {
            LUID luid;
            if (!LookupPrivilegeValue(null, privilegeName, out luid)) {
                return false;
            }
            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
            tp.PrivilegeCount = 1;
            tp.Privilege.Luid = luid;
            tp.Privilege.Attributes = 0x00000002; // SE_PRIVILEGE_ENABLED
            return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        } finally {
            CloseHandle(hToken);
        }
    }

    static bool KillProcess(int pid) {
        try {
            var proc = Process.GetProcessById(pid);
            proc.Kill();
            return true;
        } catch {
            return false;
        }
    }

    static double GetSystemMemoryPercent(out double totalMb, out double usedMb, out double availMb) {
        totalMb = 0;
        usedMb = 0;
        availMb = 0;
        MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
        memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        if (GlobalMemoryStatusEx(ref memStatus)) {
            totalMb = memStatus.ullTotalPhys / (1024.0 * 1024.0);
            availMb = memStatus.ullAvailPhys / (1024.0 * 1024.0);
            usedMb = totalMb - availMb;
            return memStatus.dwMemoryLoad;
        }
        return 0.0;
    }

    static string SerializeProcesses(List<ProcessInfo> processes) {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < processes.Count; i++) {
            var p = processes[i];
            sb.Append(string.Format(
                CultureInfo.InvariantCulture,
                "{{" +
                "\"pid\":{0}," +
                "\"label\":\"{1}\"," +
                "\"ram_mb\":{2:F1}," +
                "\"ram_bytes\":{3}," +
                "\"vms_mb\":{4:F1}," +
                "\"cpu_percent\":{5:F1}," +
                "\"status\":\"{6}\"," +
                "\"uptime_seconds\":{7}," +
                "\"uptime_formatted\":\"{8}\"," +
                "\"page_fault_rate\":{9:F1}," +
                "\"io_rate_kbps\":{10:F1}," +
                "\"threads\":{11}," +
                "\"read_bytes\":{12}," +
                "\"write_bytes\":{13}," +
                "\"priority\":\"{14}\"," +
                "\"ram_limit_mb\":{15}" +
                "}}",
                p.Pid,
                p.Label.Replace("\"", "\\\""),
                p.RamMb,
                p.RamBytes,
                p.VmsMb,
                p.CpuPercent,
                p.Status,
                p.UptimeSeconds,
                p.UptimeFormatted,
                p.PageFaultRate,
                p.IoRateKbps,
                p.Threads,
                p.ReadBytes,
                p.WriteBytes,
                p.Priority,
                p.RamLimitMb
            ));
            if (i < processes.Count - 1) {
                sb.Append(",");
            }
        }
        sb.Append("]");
        return sb.ToString();
    }

    static string SerializeSystemInfo(double totalMb, double usedMb, double availMb, double percent, double robloxTotalMb, int robloxCount, bool isAdmin, int autoFlushRemainingSeconds) {
        double totalGb = totalMb / 1024.0;
        double usedGb = usedMb / 1024.0;
        double availGb = availMb / 1024.0;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{{" +
            "\"total_mb\":{0:F1}," +
            "\"used_mb\":{1:F1}," +
            "\"available_mb\":{2:F1}," +
            "\"percent\":{3:F1}," +
            "\"total_gb\":{4:F1}," +
            "\"used_gb\":{5:F1}," +
            "\"available_gb\":{6:F1}," +
            "\"roblox_total_mb\":{7:F1}," +
            "\"roblox_count\":{8}," +
            "\"is_admin\":{9}," +
            "\"auto_flush_remaining_seconds\":{10}" +
            "}}",
            totalMb,
            usedMb,
            availMb,
            percent,
            totalGb,
            usedGb,
            availGb,
            robloxTotalMb,
            robloxCount,
            isAdmin.ToString().ToLower(),
            autoFlushRemainingSeconds
        );
    }

    static string GetSettingsJson() {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{{" +
            "\"auto_optimize\":{0}," +
            "\"auto_interval_minutes\":{1}," +
            "\"auto_flush_tier\":\"{2}\"," +
            "\"default_tier\":\"{3}\"," +
            "\"auto_resume_minutes\":{4}," +
            "\"global_limit_enabled\":{5}," +
            "\"global_limit_mb\":{6}," +
            "\"enabled\":{7}," +
            "\"interval_seconds\":{8}," +
            "\"interval_minutes\":{9:F1}," +
            "\"tier\":\"{10}\"," +
            "\"total_saved_mb\":{11:F1}," +
            "\"history_count\":{12}," +
            "\"seconds_remaining\":{13}," +
            "\"update_server_url\":\"{14}\"" +
            "}}",
            AutoOptimize.ToString().ToLower(),
            AutoIntervalMinutes,
            AutoFlushTier,
            DefaultTier,
            AutoResumeMinutes,
            GlobalLimitEnabled.ToString().ToLower(),
            GlobalLimitMb,
            AutoOptimize.ToString().ToLower(),
            AutoIntervalMinutes * 60,
            (double)AutoIntervalMinutes,
            AutoFlushTier,
            TotalSavedMb,
            TotalOptCount,
            GetAutoFlushRemainingSeconds(),
            EscapeJson(UpdateServerUrl)
        );
    }

    // ──────────────────────────────────────────────
    // Helper conversions
    // ──────────────────────────────────────────────
    static DateTime DateTimeFromUnix(double unix) {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
    }

    static double DateTimeToUnix(DateTime dt) {
        return (dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    static readonly string[] UpdateWhitelist = new string[] {
        "RobloxRAM_Optimizer.exe",
        "bin/Microsoft.Web.WebView2.Core.dll",
        "bin/Microsoft.Web.WebView2.WinForms.dll",
        "bin/WebView2Loader.dll",
        "bin/app.manifest",
        "bin/frontend/index.html",
        "bin/frontend/index.css",
        "bin/frontend/app.js"
    };

    static void DeleteOldExecutable() {
        try {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string oldPath = exePath + ".old";
            if (File.Exists(oldPath)) {
                Task.Run(() => {
                    for (int i = 0; i < 5; i++) {
                        try {
                            if (File.Exists(oldPath)) {
                                File.Delete(oldPath);
                            }
                            break;
                        } catch {
                            Thread.Sleep(1000);
                        }
                    }
                });
            }
        } catch {}
    }

    static void ExportManifestFile() {
        try {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"files\": {\n");
            for (int i = 0; i < UpdateWhitelist.Length; i++) {
                string file = UpdateWhitelist[i];
                string hash = GetFileHash(file);
                sb.Append(string.Format("    \"{0}\": \"{1}\"", file, hash));
                if (i < UpdateWhitelist.Length - 1) {
                    sb.Append(",\n");
                }
            }
            sb.Append("\n  }\n}");
            string manifestPath = Path.Combine(AppDir, "update_manifest.json");
            File.WriteAllText(manifestPath, sb.ToString(), Encoding.UTF8);
        } catch {}
    }

    static string GetFileHash(string relativePath) {
        string fullPath = Path.Combine(AppDir, relativePath.Replace("/", "\\"));
        if (!File.Exists(fullPath)) return "";
        try {
            using (var md5 = System.Security.Cryptography.MD5.Create()) {
                using (var stream = File.OpenRead(fullPath)) {
                    byte[] hash = md5.ComputeHash(stream);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) {
                        sb.Append(hash[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
        } catch {
            return "";
        }
    }

    static string DownloadStringWithTimeout(string url, int timeoutMs) {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Timeout = timeoutMs;
        request.Method = "GET";
        request.UserAgent = "RobloxRAM_Optimizer_Updater";
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
            if (response.StatusCode == HttpStatusCode.OK) {
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) {
                    return reader.ReadToEnd();
                }
            }
        }
        throw new Exception("Response status is not OK");
    }

    static byte[] DownloadFileWithTimeout(string url, int timeoutMs) {
        try {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = timeoutMs;
            request.Method = "GET";
            request.UserAgent = "RobloxRAM_Optimizer_Updater";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
                if (response.StatusCode == HttpStatusCode.OK) {
                    using (MemoryStream ms = new MemoryStream()) {
                        response.GetResponseStream().CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
        } catch {}
        return null;
    }

    static Dictionary<string, string> ParseManifestJson(string json) {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try {
            int filesIdx = json.IndexOf("\"files\"");
            if (filesIdx == -1) return dict;
            int startBrace = json.IndexOf("{", filesIdx);
            if (startBrace == -1) return dict;
            int endBrace = json.IndexOf("}", startBrace);
            if (endBrace == -1) return dict;
            string filesContent = json.Substring(startBrace + 1, endBrace - startBrace - 1);
            string[] pairs = filesContent.Split(',');
            foreach (string pair in pairs) {
                string[] kv = pair.Split(new char[] { ':' }, 2);
                if (kv.Length == 2) {
                    string key = kv[0].Trim().Replace("\"", "").Replace("\\\\", "/").Replace("\\", "/").Trim();
                    string val = kv[1].Trim().Replace("\"", "").Trim();
                    dict[key] = val;
                }
            }
        } catch {}
        return dict;
    }

    static void PerformStartupUpdateCheck() {
        if (string.IsNullOrEmpty(UpdateServerUrl)) return;
        string url = UpdateServerUrl.TrimEnd('/');
        string manifestUrl = url + "/api/update/manifest";
        bool isStatic = false;
        string json = "";
        try {
            json = DownloadStringWithTimeout(manifestUrl, 3000);
        } catch {
            try {
                manifestUrl = url + "/update_manifest.json";
                json = DownloadStringWithTimeout(manifestUrl, 3000);
                isStatic = true;
            } catch {
                return;
            }
        }
        try {
            var remoteHashes = ParseManifestJson(json);
            bool needRestart = false;
            foreach (var file in UpdateWhitelist) {
                string remoteHash = "";
                if (remoteHashes.TryGetValue(file, out remoteHash)) {
                    string localHash = GetFileHash(file);
                    if (remoteHash != localHash && !string.IsNullOrEmpty(remoteHash)) {
                        byte[] fileData = null;
                        if (isStatic || url.Contains("githubusercontent.com") || url.Contains("raw.githubusercontent.com")) {
                            string fileUrl = url + "/" + file.Replace("\\", "/");
                            fileData = DownloadFileWithTimeout(fileUrl, 15000);
                        } else {
                            string fileUrl = url + "/api/update/file?path=" + Uri.EscapeDataString(file);
                            byte[] jsonBytes = DownloadFileWithTimeout(fileUrl, 15000);
                            if (jsonBytes != null) {
                                string jsonStr = Encoding.UTF8.GetString(jsonBytes);
                                if (jsonStr.Contains("\"success\":true")) {
                                    string base64 = ParseStringKey(jsonStr, "content");
                                    if (!string.IsNullOrEmpty(base64)) {
                                        fileData = Convert.FromBase64String(base64);
                                    }
                                }
                            }
                        }
                        if (fileData != null && fileData.Length > 0) {
                            string fullPath = Path.Combine(AppDir, file.Replace("/", "\\"));
                            string dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) {
                                Directory.CreateDirectory(dir);
                            }
                            if (file.Equals("RobloxRAM_Optimizer.exe", StringComparison.OrdinalIgnoreCase)) {
                                string newExePath = fullPath + ".new";
                                File.WriteAllBytes(newExePath, fileData);
                                needRestart = true;
                            } else {
                                File.WriteAllBytes(fullPath, fileData);
                            }
                        }
                    }
                }
            }
            if (needRestart) {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string oldExePath = exePath + ".old";
                string newExePath = exePath + ".new";
                if (File.Exists(newExePath)) {
                    if (File.Exists(oldExePath)) {
                        try { File.Delete(oldExePath); } catch {}
                    }
                    File.Move(exePath, oldExePath);
                    File.Move(newExePath, exePath);
                    Process.Start(exePath);
                    Environment.Exit(0);
                }
            }
        } catch {}
    }

    static Dictionary<int, PerformanceCounter> PrivateWsCounters = new Dictionary<int, PerformanceCounter>();

    static string GetProcessInstanceName(int pid) {
        try {
            var cat = new PerformanceCounterCategory("Process");
            string[] instances = cat.GetInstanceNames();
            foreach (string instance in instances) {
                if (instance.StartsWith("roblox", StringComparison.OrdinalIgnoreCase) || 
                    instance.StartsWith("windows10universal", StringComparison.OrdinalIgnoreCase)) {
                    using (var cnt = new PerformanceCounter("Process", "ID Process", instance, true)) {
                        try {
                            if ((int)cnt.RawValue == pid) return instance;
                        } catch {}
                    }
                }
            }
        } catch {}
        return null;
    }

    static double GetPrivateWorkingSetMb(int pid, Process p) {
        // Method 1: Try PROCESS_MEMORY_COUNTERS_EX2 (Modern Windows 10/11)
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (hProcess != IntPtr.Zero) {
            try {
                PROCESS_MEMORY_COUNTERS_EX2 pmc;
                pmc.cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX2));
                if (GetProcessMemoryInfoEx2(hProcess, out pmc, pmc.cb)) {
                    double bytes = (double)pmc.PrivateWorkingSetSize.ToUInt64();
                    if (bytes > 0) return bytes / (1024.0 * 1024.0);
                }
            } catch {}
            finally {
                CloseHandle(hProcess);
            }
        }

        // Method 2: Fallback to Performance Counter
        try {
            lock (StateLock) {
                if (!PrivateWsCounters.ContainsKey(pid)) {
                    string instanceName = GetProcessInstanceName(pid);
                    if (!string.IsNullOrEmpty(instanceName)) {
                        var counter = new PerformanceCounter("Process", "Working Set - Private", instanceName, true);
                        counter.NextValue(); // Prime the counter
                        PrivateWsCounters[pid] = counter;
                    }
                }
                if (PrivateWsCounters.ContainsKey(pid)) {
                    double bytes = PrivateWsCounters[pid].NextValue();
                    if (bytes > 0) return bytes / (1024.0 * 1024.0);
                }
            }
        } catch {}

        // Method 3: Ultimate Fallback (Total Working Set)
        try {
            return p.WorkingSet64 / (1024.0 * 1024.0);
        } catch {
            return 0.0;
        }
    }
}

class DashboardForm : System.Windows.Forms.Form {
    private Microsoft.Web.WebView2.WinForms.WebView2 WebView;

    public DashboardForm(string url) {
        this.Text = "Roblox RAM Optimizer Dashboard";
        this.Size = new System.Drawing.Size(1150, 800);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        
        try {
            this.Icon = Program.CreateTrayIcon();
        } catch {}

        this.FormClosed += (s, e) => {
            Program.Shutdown();
        };

        WebView = new Microsoft.Web.WebView2.WinForms.WebView2();
        WebView.Dock = System.Windows.Forms.DockStyle.Fill;
        this.Controls.Add(WebView);

        this.Load += async (s, e) => {
            try {
                string dataFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, System.AppDomain.CurrentDomain.FriendlyName + ".WebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, dataFolder);
                await WebView.EnsureCoreWebView2Async(env);
                
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                WebView.Source = new System.Uri(url);
            } catch (System.Exception ex) {
                System.Windows.Forms.MessageBox.Show("Không thể khởi chạy WebView2. Đang mở dashboard trên trình duyệt của bạn.\n\nChi tiết lỗi: " + ex.Message, "Lỗi WebView2", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                try {
                    System.Diagnostics.ProcessStartInfo edgeApp = new System.Diagnostics.ProcessStartInfo();
                    edgeApp.FileName = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                    edgeApp.Arguments = string.Format("--app=\"{0}\"", url);
                    edgeApp.UseShellExecute = false;
                    System.Diagnostics.Process.Start(edgeApp);
                } catch {
                    try {
                        System.Diagnostics.Process.Start(url);
                    } catch {}
                }
                try {
                    this.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate {
                        this.Close();
                    });
                } catch {
                    this.Close();
                }
            }
        };
    }
}
