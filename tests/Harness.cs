using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NoActivateClick.Diagnostics
{
    /// <summary>Window used as a click target. Never activates itself when shown.</summary>
    internal sealed class TestTarget : Form
    {
        private int _downs;
        private int _ups;
        private int _moves;
        private int _wheels;
        private int _doubles;
        private int _tagged;
        private int _lastDownExtra;
        private int _lastDownX;
        private int _lastDownY;
        private int _movesWithLeft;
        private int _buttonClicks;

        public readonly Panel Surface = new Panel();
        public readonly Button ClickButton = new Button();
        public readonly TextBox Field = new TextBox();

        public int Downs { get { return Volatile.Read(ref _downs); } }
        public int Ups { get { return Volatile.Read(ref _ups); } }
        public int Moves { get { return Volatile.Read(ref _moves); } }
        public int Wheels { get { return Volatile.Read(ref _wheels); } }
        public int Doubles { get { return Volatile.Read(ref _doubles); } }
        public int TaggedDowns { get { return Volatile.Read(ref _tagged); } }
        public int LastDownExtra { get { return Volatile.Read(ref _lastDownExtra); } }
        public int LastDownX { get { return Volatile.Read(ref _lastDownX); } }
        public int LastDownY { get { return Volatile.Read(ref _lastDownY); } }
        public int MovesWithLeft { get { return Volatile.Read(ref _movesWithLeft); } }
        public int TaggedUps { get { return Volatile.Read(ref _taggedUps); } }
        public int HandoverDowns { get { return Volatile.Read(ref _handoverDowns); } }
        private int _handoverDowns;
        public int Drops { get { return Volatile.Read(ref _drops); } }
        public bool StartOleDrag;
        private int _taggedUps;
        private int _drops;
        private bool _dragging;
        private Point _dragOrigin;
        public int ButtonClicks { get { return Volatile.Read(ref _buttonClicks); } }

        public int Activations { get { return Volatile.Read(ref _activations); } }
        public int PosChanges { get { return Volatile.Read(ref _posChanges); } }
        private int _activations;
        private int _posChanges;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0006 && (m.WParam.ToInt64() & 0xFFFF) != 0) Interlocked.Increment(ref _activations);
            if (m.Msg == 0x0047) Interlocked.Increment(ref _posChanges);
            base.WndProc(ref m);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public TestTarget(Rectangle bounds, bool border)
        {
            FormBorderStyle = border ? FormBorderStyle.FixedSingle : FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            ShowInTaskbar = false;
            Text = "nac test";
            BackColor = Color.FromArgb(45, 55, 70);
            Surface.Dock = DockStyle.Fill;
            Surface.BackColor = Color.FromArgb(45, 55, 70);
            Controls.Add(Surface);

            ClickButton.Text = "target";
            ClickButton.Bounds = new Rectangle(bounds.Width - 150, 20, 110, 50);
            ClickButton.Click += delegate { Interlocked.Increment(ref _buttonClicks); };
            Controls.Add(ClickButton);
            ClickButton.BringToFront();

            Field.Bounds = new Rectangle(430, 258, 110, 26);
            Field.Text = "";
            Controls.Add(Field);
            Field.BringToFront();

            Surface.AllowDrop = true;
            Surface.DragEnter += delegate(object sender, DragEventArgs e) { e.Effect = DragDropEffects.Copy; };
            Surface.DragDrop += delegate(object sender, DragEventArgs e) { Interlocked.Increment(ref _drops); };

            Surface.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                _dragOrigin = e.Location;
                int extra = Native.GetMessageExtraInfo().ToInt32();
                Volatile.Write(ref _lastDownExtra, extra);
                Volatile.Write(ref _lastDownX, e.X);
                Volatile.Write(ref _lastDownY, e.Y);
                if (extra == Harness.InjectedTag) Interlocked.Increment(ref _tagged);
                if (extra == MouseHook.HandoverSignature) Interlocked.Increment(ref _handoverDowns);
                Interlocked.Increment(ref _downs);
            };
            Surface.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (Native.GetMessageExtraInfo().ToInt32() == Harness.InjectedTag) Interlocked.Increment(ref _taggedUps);
                Interlocked.Increment(ref _ups);
            };
            Surface.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Interlocked.Increment(ref _movesWithLeft);
                Interlocked.Increment(ref _moves);

                if (!StartOleDrag || _dragging || e.Button != MouseButtons.Left) return;
                int dx = e.X - _dragOrigin.X;
                int dy = e.Y - _dragOrigin.Y;
                if (dx * dx + dy * dy < 25 * 25) return;

                _dragging = true;
                try
                {
                    DataObject data = new DataObject(DataFormats.FileDrop, new string[] { Harness.DragFile });
                    DoDragDrop(data, DragDropEffects.Copy);
                }
                finally
                {
                    _dragging = false;
                }
            };
            Surface.MouseWheel += delegate { Interlocked.Increment(ref _wheels); };
            Surface.DoubleClick += delegate { Interlocked.Increment(ref _doubles); };
        }

        public void ResetCounters()
        {
            Volatile.Write(ref _downs, 0);
            Volatile.Write(ref _ups, 0);
            Volatile.Write(ref _moves, 0);
            Volatile.Write(ref _wheels, 0);
            Volatile.Write(ref _doubles, 0);
            Volatile.Write(ref _tagged, 0);
            Volatile.Write(ref _lastDownExtra, 0);
            Volatile.Write(ref _lastDownX, 0);
            Volatile.Write(ref _lastDownY, 0);
            Volatile.Write(ref _movesWithLeft, 0);
            Volatile.Write(ref _taggedUps, 0);
            Volatile.Write(ref _handoverDowns, 0);
            Volatile.Write(ref _drops, 0);
            Volatile.Write(ref _buttonClicks, 0);
        }
    }

    internal static class Harness
    {
        public const int InjectedTag = 0x4E41434C;
        public static readonly string DragFile =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nac_drag_test.txt");
        private const uint NoAct = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;

        private static TestTarget _front;
        private static TestTarget _back;
        private static TestTarget _side;
        private static Form _sync;
        private static ClickEngine _engine;
        private static MouseHook _hook;
        private static ForegroundBorrower _borrower = new ForegroundBorrower();
        private static KeepFrontPinner _pinner = new KeepFrontPinner();
        private static int _exitCode = 3;
        private static int _pass;
        private static int _fail;
        private static int _skip;
        private static readonly List<string> _failures = new List<string>();
        private static readonly object _traceLock = new object();
        private static readonly List<int> _trace = new List<int>();
        private static IntPtr _userForeground;
        private static int _installError;

        private static Point _panelPoint;
        private static Point _buttonPoint;
        private static Point _captionPoint;

        [STAThread]
        private static int Main()
        {
            Native.TryEnableDpiAwareness();
            Application.EnableVisualStyles();

            _sync = new Form();
            _sync.ShowInTaskbar = false;
            IntPtr unused = _sync.Handle;

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Console.WriteLine("== NoActivateClick acceptance test ==");
            Console.WriteLine("screen=" + Screen.PrimaryScreen.Bounds + " work=" + work);

            int W = work.Right;
            int H = work.Bottom;
            _front = new TestTarget(new Rectangle(W - 1200, H - 650, 420, 300), true);
            _back = new TestTarget(new Rectangle(W - 900, H - 500, 560, 360), false);
            _side = new TestTarget(new Rectangle(W - 1650, H - 650, 380, 300), true);

            _side.Show();
            _back.Show();
            _front.Show();
            RepairOrder();

            _panelPoint = new Point(_back.Left + 480, _back.Top + 300);
            _buttonPoint = new Point(_back.Left + (_back.Width - 95), _back.Top + 45);

            Native.RECT wr;
            Native.GetWindowRect(_side.Handle, out wr);
            Native.RECT sc = Native.ClientScreenRect(_side.Handle);
            _captionPoint = new Point((wr.Left + wr.Right) / 2, wr.Top + Math.Max(2, (sc.Top - wr.Top) / 2));

            ClickTargetPolicy policy = new ClickTargetPolicy();
            policy.ExcludeOwnProcess = false;
            policy.ExtraFilter = delegate(IntPtr h)
            {
                IntPtr root = Native.Root(h);
                return root == _front.Handle || root == _back.Handle || root == _side.Handle;
            };
            _engine = new ClickEngine(policy);
            _hook = new MouseHook(_engine);

            _hook.Observer = delegate(int message)
            {
                lock (_traceLock) { _trace.Add(message); }
            };
            _hook.Diagnostics = delegate(string text) { Console.WriteLine("   [hook] " + text); };

            Thread worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Start();
            Application.Run();
            return _exitCode;
        }

        private static void Run()
        {
            try
            {
                Thread.Sleep(500);
                _userForeground = Native.GetForegroundWindow();
                Console.WriteLine("front=" + Native.Describe(_front.Handle));
                Console.WriteLine("back =" + Native.Describe(_back.Handle));
                Console.WriteLine("side =" + Native.Describe(_side.Handle));
                Console.WriteLine("user foreground=" + Native.Describe(_userForeground));
                WritePointInfo("panel", _panelPoint, _back);
                WritePointInfo("button", _buttonPoint, _back);
                WritePointInfo("caption", _captionPoint, _side);

                Check("test windows did not steal focus",
                    _userForeground != _front.Handle && _userForeground != _back.Handle &&
                    _userForeground != _side.Handle, "fg=" + Native.Describe(_userForeground));
                Check("layout: front above back", FrontAboveBack(), OrderText());

                // ---- 0. control group: without the hook a plain click DOES raise the window ----
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "control group")) { Skip("control: plain click raises the window"); }
                else
                {
                    Click(_panelPoint);
                    Thread.Sleep(450);
                    bool raised = !FrontAboveBack();
                    IntPtr fgAfter = Native.GetForegroundWindow();
                    Check("control: plain click raises the background window", raised, OrderText());
                    Check("control: plain click takes the foreground", fgAfter == _back.Handle,
                        "fg=" + Native.Describe(fgAfter));
                }

                RepairOrder();
                if (Native.GetForegroundWindow() != _userForeground)
                {
                    Native.SetForegroundWindow(_userForeground);
                    Thread.Sleep(300);
                }
                Check("reset: user foreground restored", Native.GetForegroundWindow() == _userForeground,
                    "fg=" + Native.Describe(Native.GetForegroundWindow()));
                Check("reset: front above back", FrontAboveBack(), OrderText());

                // ---- 1. direct engine call ----
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "engine direct")) { Skip("engine direct click"); }
                else
                {
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    IntPtr target;
                    ClickOutcome outcome = _engine.MouseDown(Native.WM_LBUTTONDOWN, _panelPoint.X, _panelPoint.Y, out target);
                    if (outcome == ClickOutcome.Posted) _engine.PostTo(target, Native.WM_LBUTTONUP, _panelPoint.X, _panelPoint.Y, 0);
                    Thread.Sleep(250);
                    Check("engine: posts instead of activating", outcome == ClickOutcome.Posted, "outcome=" + outcome);
                    Check("engine: down and up delivered",
                        _back.Downs == 1 && _back.Ups == 1, "downs=" + _back.Downs + " ups=" + _back.Ups);
                    Check("engine: click is not tagged as hardware",
                        _back.LastDownExtra != InjectedTag, "extra=0x" + _back.LastDownExtra.ToString("X"));
                    Point expected = _back.Surface.PointToClient(_panelPoint);
                    Check("engine: client coordinates are correct",
                        _back.LastDownX == expected.X && _back.LastDownY == expected.Y,
                        "got=(" + _back.LastDownX + "," + _back.LastDownY + ") want=(" + expected.X + "," + expected.Y + ")");
                    Check("engine: front stayed above back", FrontAboveBack(), OrderText());
                    Check("engine: foreground unchanged", fgBefore == Native.GetForegroundWindow(),
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                }

                // ---- 2. hook, single click ----
                _sync.Invoke(new MethodInvoker(delegate { _hook.Install(out _installError); }));
                Check("low level mouse hook installed", _hook.IsInstalled,
                    "error=" + _installError + " installed=" + _hook.IsInstalled);
                if (!_hook.IsInstalled) { Finish(1); return; }

                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "hook single click")) { Skip("hook: single click"); }
                else
                {
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    Click(_panelPoint);
                    Thread.Sleep(300);
                    Check("hook: click reaches the background window",
                        _back.Downs == 1 && _back.Ups == 1, "downs=" + _back.Downs + " ups=" + _back.Ups);
                    Check("hook: physical click was swallowed",
                        _back.TaggedDowns == 0, "tagged=" + _back.TaggedDowns);
                    Check("hook: front stayed above back", FrontAboveBack(), OrderText());
                    Check("hook: foreground unchanged", fgBefore == Native.GetForegroundWindow(),
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                }

                // ---- 2b. direct posted click on the button, activation accounting ----
                _back.ResetCounters();
                RepairOrder();
                {
                    int actBefore = _back.Activations;
                    int posBefore = _back.PosChanges;
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    IntPtr btnTarget;
                    ClickOutcome btnOutcome = _engine.MouseDown(Native.WM_LBUTTONDOWN, _buttonPoint.X, _buttonPoint.Y, out btnTarget);
                    IntPtr fgMid = Native.GetForegroundWindow();
                    if (btnOutcome == ClickOutcome.Posted) _engine.PostTo(btnTarget, Native.WM_LBUTTONUP, _buttonPoint.X, _buttonPoint.Y, 0);
                    Thread.Sleep(250);
                    Console.WriteLine("   [diag] posted button: outcome=" + btnOutcome +
                                      " clicks=" + _back.ButtonClicks +
                                      " act=" + actBefore + "->" + _back.Activations +
                                      " pos=" + posBefore + "->" + _back.PosChanges +
                                      " fg=" + Native.ClassName(fgBefore) + "->" + Native.ClassName(fgMid) +
                                      "->" + Native.ClassName(Native.GetForegroundWindow()) +
                                      " frontAboveBack=" + FrontAboveBack());
                }

                // ---- 3. double click ----
                _back.ResetCounters();
                RepairOrder();
                ClearTrace();
                if (!Guarded(_panelPoint, _back, "hook double click")) { Skip("hook: double click"); }
                else
                {
                    Click(_panelPoint);
                    Thread.Sleep(70);
                    Click(_panelPoint);
                    Thread.Sleep(350);
                    Console.WriteLine("   hook trace: " + TraceText());
                    Check("hook: double click arrives as a double click",
                        _back.Downs == 2 && _back.Doubles == 1,
                        "downs=" + _back.Downs + " doubles=" + _back.Doubles);
                }

                // ---- 4. button ----
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_buttonPoint, _back, "hook button")) { Skip("hook: button click"); }
                else
                {
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    Click(_buttonPoint);
                    Thread.Sleep(300);
                    Check("hook: button on background window clicked",
                        _back.ButtonClicks == 1, "clicks=" + _back.ButtonClicks);
                    Check("hook: button click used posted messages",
                        _back.TaggedDowns == 0, "tagged=" + _back.TaggedDowns);
                    Check("hook: button click kept order", FrontAboveBack(), OrderText());
                    Check("hook: button click kept foreground", fgBefore == Native.GetForegroundWindow(),
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                }

                // ---- 5a. synthetic drag (handover off): no activation ----
                _hook.RealDragHandover = false;
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "synthetic drag")) { Skip("hook: synthetic drag"); }
                else
                {
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    Drag(_panelPoint, 0, 70, 6);
                    Thread.Sleep(350);
                    Check("hook: synthetic drag reaches the window",
                        _back.Downs == 1 && _back.Ups == 1 && _back.Moves >= 2 && _back.TaggedDowns == 0,
                        "downs=" + _back.Downs + " ups=" + _back.Ups + " moves=" + _back.Moves +
                        " tagged=" + _back.TaggedDowns);
                    Check("hook: synthetic drag kept order", FrontAboveBack(), OrderText());
                    Check("hook: synthetic drag kept foreground", fgBefore == Native.GetForegroundWindow(),
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                }

                // ---- 5b. drag handover: real input, window is activated (expected) ----
                _hook.RealDragHandover = true;
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "handover drag")) { Skip("hook: drag handover"); }
                else
                {
                    Drag(_panelPoint, 0, 70, 6);
                    Thread.Sleep(400);
                    Check("hook: drag turns into real input",
                        _back.HandoverDowns >= 1 && _back.TaggedUps >= 1,
                        "handoverDowns=" + _back.HandoverDowns + " taggedUps=" + _back.TaggedUps);
                    Check("hook: handover activates the dragged window",
                        Native.GetForegroundWindow() == _back.Handle,
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                    Native.SetForegroundWindow(_userForeground);
                    Thread.Sleep(250);
                }

                // ---- 5c. real OLE file drop started from a background window ----
                _back.ResetCounters();
                _side.ResetCounters();
                RepairOrder();
                _back.StartOleDrag = true;
                if (!Guarded(_panelPoint, _back, "ole drag")) { Skip("hook: OLE file drop"); }
                else
                {
                    Point dropPoint = new Point(_side.Left + _side.Width / 2, _side.Top + _side.Height / 2);
                    OleDrag(_panelPoint, dropPoint);
                    Thread.Sleep(600);
                    Check("hook: OLE file drop from a background window works",
                        _side.Drops >= 1, "drops=" + _side.Drops +
                        " taggedDowns=" + _back.TaggedDowns + " fg=" + Native.ClassName(Native.GetForegroundWindow()));
                }
                _back.StartOleDrag = false;
                Native.SetForegroundWindow(_userForeground);
                Thread.Sleep(200);

                // ---- 6. wheel ----
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "wheel")) { Skip("hook: wheel"); }
                else
                {
                    _hook.WheelRedirect = true;
                    Wheel(_panelPoint, 120);
                    Thread.Sleep(300);
                    Check("hook: wheel redirect reaches the window", _back.Wheels >= 1, "wheels=" + _back.Wheels);
                    _hook.WheelRedirect = false;
                }

                // ---- 7. caption of an inactive window ----
                RepairOrder();
                IntPtr target2;
                ClickOutcome captionOutcome = ClickOutcome.PassThrough;
                if (!Guarded(_captionPoint, _side, "caption")) { Skip("engine: caption is ignored"); }
                else
                {
                    IntPtr fgBefore = Native.GetForegroundWindow();
                    captionOutcome = _engine.MouseDown(Native.WM_LBUTTONDOWN, _captionPoint.X, _captionPoint.Y, out target2);
                    Check("engine: caption of inactive window is ignored",
                        captionOutcome == ClickOutcome.Blocked, "outcome=" + captionOutcome);
                    Click(_captionPoint);
                    Thread.Sleep(300);
                    Check("hook: caption click changes nothing",
                        fgBefore == Native.GetForegroundWindow() && FrontAboveBack(),
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                }

                // ---- 7b. borrow the foreground (type into a background window) ----
                _back.ResetCounters();
                RepairOrder();
                if (!Guarded(_panelPoint, _back, "borrow")) { Skip("borrow foreground"); }
                else
                {
                    IntPtr borrowTarget;
                    ClickOutcome oc = _engine.MouseDown(Native.WM_LBUTTONDOWN, _panelPoint.X, _panelPoint.Y, out borrowTarget);
                    if (oc == ClickOutcome.Posted)
                    {
                        _engine.PostTo(borrowTarget, Native.WM_LBUTTONUP, _panelPoint.X, _panelPoint.Y, 0);
                    }
                    Thread.Sleep(200);
                    Check("engine: remembers the last clicked window",
                        _engine.LastClickedRoot == _back.Handle,
                        "last=0x" + _engine.LastClickedRoot.ToInt64().ToString("X"));

                    bool borrowed = false;
                    string info = "";
                    _sync.Invoke(new MethodInvoker(delegate
                    {
                        borrowed = _borrower.Borrow(_engine.LastClickedRoot, _engine.LastClickedRoot,
                            _engine.LastClickPoint, out info);
                    }));
                    Thread.Sleep(350);
                    Check("borrow: target becomes the foreground window",
                        borrowed && Native.GetForegroundWindow() == _back.Handle,
                        "borrowed=" + borrowed + " info=" + info +
                        " fg=" + Native.Describe(Native.GetForegroundWindow()));

                    _sync.Invoke(new MethodInvoker(delegate { _borrower.Restore(); }));
                    Thread.Sleep(450);
                    Check("borrow: previous foreground restored",
                        Native.GetForegroundWindow() == _userForeground,
                        "fg=" + Native.Describe(Native.GetForegroundWindow()));
                    Check("borrow: front is above back again", FrontAboveBack(), OrderText());
                }

                // ---- 7c. borrow then really type into the background field ----
                _back.ResetCounters();
                RepairOrder();
                {
                    Point fieldPoint = new Point(_back.Left + 480, _back.Top + 271);
                    if (!Guarded(fieldPoint, _back, "borrow typing")) { Skip("borrow: typing"); }
                    else
                    {
                        _back.Field.Text = "";
                        IntPtr fieldTarget;
                        ClickOutcome oc = _engine.MouseDown(Native.WM_LBUTTONDOWN, fieldPoint.X, fieldPoint.Y, out fieldTarget);
                        if (oc == ClickOutcome.Posted)
                        {
                            _engine.PostTo(fieldTarget, Native.WM_LBUTTONUP, fieldPoint.X, fieldPoint.Y, 0);
                        }
                        Thread.Sleep(200);

                        bool borrowed = false;
                        string info = "";
                        _sync.Invoke(new MethodInvoker(delegate
                        {
                            borrowed = _borrower.Borrow(_engine.LastClickedRoot, out info);
                        }));
                        Thread.Sleep(300);
                        Console.WriteLine("   borrow typing: borrowed=" + borrowed + " fg=" +
                                          Native.Describe(Native.GetForegroundWindow()));

                        SendKeys("abc");
                        Thread.Sleep(350);
                        Check("borrow: typing lands in the background field",
                            _back.Field.Text.Contains("abc"),
                            "text='" + _back.Field.Text + "'");

                        _sync.Invoke(new MethodInvoker(delegate { _borrower.Restore(); }));
                        Thread.Sleep(350);
                    }
                }

                // ---- 7d. keep-front pinning with three windows ----
                {
                    Form winA = new Form();
                    Form winB = new Form();
                    Form winC = new Form();
                    try
                    {
                        winA.FormBorderStyle = FormBorderStyle.None;
                        winA.StartPosition = FormStartPosition.Manual;
                        winA.Bounds = new Rectangle(80, 120, 380, 240);
                        winA.ShowInTaskbar = false;
                        winA.Show();

                        winB.FormBorderStyle = FormBorderStyle.None;
                        winB.StartPosition = FormStartPosition.Manual;
                        winB.Bounds = new Rectangle(260, 240, 380, 240);
                        winB.ShowInTaskbar = false;
                        winB.Show();

                        winC.FormBorderStyle = FormBorderStyle.None;
                        winC.StartPosition = FormStartPosition.Manual;
                        winC.Bounds = new Rectangle(440, 360, 380, 240);
                        winC.ShowInTaskbar = false;
                        winC.Show();

                        Native.SetWindowPos(winC.Handle, Native.HWND_TOP, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                        Native.SetWindowPos(winB.Handle, Native.HWND_TOP, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                        Native.SetWindowPos(winA.Handle, Native.HWND_TOP, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                        Thread.Sleep(250);

                        bool orderBefore = Above(winA.Handle, winB.Handle) && Above(winB.Handle, winC.Handle);
                        int pinned = _pinner.PinAbove(winC.Handle, delegate(IntPtr h)
                        {
                            return h == winA.Handle || h == winB.Handle || h == winC.Handle;
                        });
                        bool pinnedBoth = IsTopmost(winA.Handle) && IsTopmost(winB.Handle);

                        Native.ForceForeground(winC.Handle);
                        Thread.Sleep(300);
                        bool orderAfter = Above(winA.Handle, winB.Handle) && Above(winB.Handle, winC.Handle);

                        Check("keep front: every window above the target is pinned",
                            pinned >= 2 && pinnedBoth, "pinned=" + pinned + " both=" + pinnedBoth);
                        Check("keep front: three windows keep their order (middle stays in front)",
                            orderBefore && orderAfter, "before=" + orderBefore + " after=" + orderAfter);

                        _pinner.UnpinAll();
                        Check("keep front: release restores normal stacking",
                            !IsTopmost(winA.Handle) && !IsTopmost(winB.Handle),
                            "topmostA=" + IsTopmost(winA.Handle) + " topmostB=" + IsTopmost(winB.Handle));
                    }
                    finally
                    {
                        try { winA.Close(); } catch { }
                        try { winB.Close(); } catch { }
                        try { winC.Close(); } catch { }
                        Native.ForceForeground(_userForeground);
                        Thread.Sleep(200);
                    }
                }

                // ---- 8. foreground rule ----
                IntPtr fg = Native.GetForegroundWindow();
                Check("engine: active window is exempt",
                    fg == IntPtr.Zero || _engine.IsForegroundTarget(fg), "fg=" + Native.Describe(fg));
                Check("engine: background window is not exempt",
                    !_engine.IsForegroundTarget(_back.Handle), "back=" + Native.Describe(_back.Handle));

                // ---- 9. DPI mapping ----
                Native.POINT mapped = _engine.ToClient(_back.Handle, new Native.POINT(_panelPoint.X, _panelPoint.Y));
                Point winForms = _back.PointToClient(_panelPoint);
                Check("engine: DPI conversion matches WinForms",
                    mapped.X == winForms.X && mapped.Y == winForms.Y,
                    "engine=(" + mapped.X + "," + mapped.Y + ") winforms=(" + winForms.X + "," + winForms.Y + ")");
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex);
                _fail++;
                _failures.Add("exception: " + ex.Message);
            }
            finally
            {
                Finish(_fail == 0 ? 0 : 1);
            }
        }

        private static void RepairOrder()
        {
            if (_hook != null) _hook.ResetDownState();
            Native.SetWindowPos(_side.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, NoAct);
            Native.SetWindowPos(_front.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, NoAct);
            Native.SetWindowPos(_back.Handle, _front.Handle, 0, 0, 0, 0, NoAct);
            Thread.Sleep(90);
        }

        private static bool Guarded(Point p, TestTarget target, string label)
        {
            IntPtr under = Native.WindowFromPoint(new Native.POINT(p.X, p.Y));
            bool ok = under != IntPtr.Zero && Native.Root(under) == target.Handle;
            if (!ok) Console.WriteLine("   [covered] " + label + ": " + p + " -> " + Native.Describe(under));
            return ok;
        }

        private static void WritePointInfo(string label, Point p, TestTarget expected)
        {
            IntPtr hwnd = Native.WindowFromPoint(new Native.POINT(p.X, p.Y));
            bool ok = hwnd != IntPtr.Zero && Native.Root(hwnd) == expected.Handle;
            Console.WriteLine("   " + label + " " + p + " -> " + Native.Describe(hwnd) + (ok ? "" : "  [NOT OUR WINDOW]"));
        }

        private static void SendKeys(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                ushort vk = (ushort)char.ToUpperInvariant(text[i]);
                Native.INPUT[] inputs = new Native.INPUT[2];
                inputs[0].type = 1;
                inputs[0].u.ki.wVk = vk;
                inputs[1].type = 1;
                inputs[1].u.ki.wVk = vk;
                inputs[1].u.ki.dwFlags = 0x0002; // KEYEVENTF_KEYUP
                Native.SendInput(2, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
                Thread.Sleep(40);
            }
        }

        private static void Click(Point p)
        {
            Native.SendMouse(p.X, p.Y, Native.MOUSEEVENTF_LEFTDOWN, new IntPtr(InjectedTag));
            Thread.Sleep(35);
            Native.SendMouse(p.X, p.Y, Native.MOUSEEVENTF_LEFTUP, new IntPtr(InjectedTag));
        }

        private static void Drag(Point start, int dx, int dy, int steps)
        {
            uint r = Native.SendMouse(start.X, start.Y, Native.MOUSEEVENTF_LEFTDOWN, new IntPtr(InjectedTag));
            if (r == 0) Console.WriteLine("   [drag] down injection failed");
            Thread.Sleep(60);
            for (int i = 1; i <= steps; i++)
            {
                Native.SendMouse(start.X + (dx * i) / steps, start.Y + (dy * i) / steps, 0, new IntPtr(InjectedTag));
                Thread.Sleep(25);
            }
            uint up = Native.SendMouse(start.X + dx, start.Y + dy, Native.MOUSEEVENTF_LEFTUP, new IntPtr(InjectedTag));
            Thread.Sleep(30);
            bool stillDown = (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0;
            Console.WriteLine("   [drag] upInjected=" + up + " stillDown=" + stillDown +
                              " cursor=" + Cursor.Position);
            if (stillDown)
            {
                Native.SendMouse(start.X + dx, start.Y + dy, Native.MOUSEEVENTF_LEFTUP, new IntPtr(InjectedTag));
            }
        }

        /// <summary>Press on the source, drag across to the target and release there.</summary>
        private static void OleDrag(Point from, Point to)
        {
            Native.SendMouse(from.X, from.Y, Native.MOUSEEVENTF_LEFTDOWN, new IntPtr(InjectedTag));
            Thread.Sleep(80);
            for (int i = 1; i <= 12; i++)
            {
                int x = from.X + ((to.X - from.X) * i) / 12;
                int y = from.Y + ((to.Y - from.Y) * i) / 12;
                Native.SendMouse(x, y, 0, new IntPtr(InjectedTag));
                Thread.Sleep(45);
            }
            Thread.Sleep(250);
            Native.SendMouse(to.X, to.Y, Native.MOUSEEVENTF_LEFTUP, new IntPtr(InjectedTag));
        }

        private static void Wheel(Point p, int delta)
        {
            Native.SendWheel(p.X, p.Y, delta, new IntPtr(InjectedTag));
        }

        private static List<IntPtr> Snapshot()
        {
            List<IntPtr> list = new List<IntPtr>();
            IntPtr h = Native.GetTopWindow(IntPtr.Zero);
            int guard = 0;
            while (h != IntPtr.Zero && guard++ < 3000)
            {
                list.Add(h);
                h = Native.GetWindow(h, Native.GW_HWNDNEXT);
            }
            return list;
        }

        private static int Index(List<IntPtr> list, IntPtr hwnd)
        {
            int i = list.IndexOf(hwnd);
            return i < 0 ? int.MaxValue : i;
        }

        private static bool Above(IntPtr upper, IntPtr lower)
        {
            List<IntPtr> z = Snapshot();
            return Index(z, upper) < Index(z, lower);
        }

        private static bool IsTopmost(IntPtr hwnd)
        {
            return (Native.GetExStyle(hwnd) & Native.WS_EX_TOPMOST) != 0;
        }

        private static bool FrontAboveBack()
        {
            List<IntPtr> z = Snapshot();
            return Index(z, _front.Handle) < Index(z, _back.Handle);
        }

        private static string OrderText()
        {
            List<IntPtr> list = Snapshot();
            return "front@" + Index(list, _front.Handle) + " back@" + Index(list, _back.Handle) +
                   " fg=" + Native.ClassName(Native.GetForegroundWindow());
        }

        private static void ClearTrace()
        {
            lock (_traceLock) { _trace.Clear(); }
        }

        private static string TraceText()
        {
            lock (_traceLock)
            {
                string[] parts = new string[_trace.Count];
                for (int i = 0; i < _trace.Count; i++) parts[i] = _trace[i].ToString("X4");
                return string.Join(" ", parts);
            }
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                _pass++;
                Console.WriteLine("[PASS] " + name);
            }
            else
            {
                _fail++;
                _failures.Add(name + " (" + detail + ")");
                Console.WriteLine("[FAIL] " + name + "  " + detail);
            }
        }

        private static void Skip(string name)
        {
            _skip++;
            Console.WriteLine("[SKIP] " + name);
        }

        private static void Finish(int code)
        {
            _exitCode = code;
            try { if (_hook != null) _hook.Dispose(); } catch { }
            if (_userForeground != IntPtr.Zero && Native.GetForegroundWindow() != _userForeground)
            {
                try { Native.SetForegroundWindow(_userForeground); } catch { }
            }
            try
            {
                const uint flags = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
                Native.SetWindowPos(_side.Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                Native.SetWindowPos(_front.Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                Native.SetWindowPos(_back.Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            }
            catch { }
            try
            {
                if (_front != null) _front.Close();
                if (_back != null) _back.Close();
                if (_side != null) _side.Close();
            }
            catch { }
            Console.WriteLine("== " + _pass + " passed, " + _fail + " failed, " + _skip + " skipped ==");
            for (int i = 0; i < _failures.Count; i++) Console.WriteLine("   FAILED: " + _failures[i]);
            try
            {
                _sync.BeginInvoke(new MethodInvoker(delegate { Application.ExitThread(); }));
            }
            catch { }
        }
    }
}