using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using eyecandy;

namespace eyecandy
{
    public partial class Form1 : Form
    {
        private delegate void UpdateLogMessagesDelegate();
        private delegate void UpdateButtonDelegate();

        private readonly LogDelegate log;
        private Runner runner = null;

        public Form1(
            Runner runner,
            Func<Form1, LogDelegate> createLog) 
            : base()
        {
            this.runner = runner;
            this.log = createLog(this);
        }

        private Form1()
        {
            InitializeComponent();
        }

        public static LogDelegate CreateLog(Form1 form)
        {
            return msg =>
            {
                form.Invoke(new UpdateLogMessagesDelegate(() =>
                {
                    form.logMessages.AppendText(msg);
                    form.logMessages.AppendText(Environment.NewLine);
                }));
            };
        }

        public void Log(string msg)
        {
            this.log(msg);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Contains("--start-visible"))
            {
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.Show();
            }
            else
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }

            this.notifyIcon.Visible = true;

            TimeSpan interval = TimeSpan.FromMinutes(10);

            if (!args.Contains("--no-auto-start"))
            {
                this.runner.Start();
            }
        }

        public void DoUpdate(Action update)
        {
            this.Invoke(new UpdateButtonDelegate(() =>
            {
                this.buttonChangeWallpaper.Enabled = false;
            }));

            update();

            this.Invoke(new UpdateButtonDelegate(() =>
            {
                this.buttonChangeWallpaper.Enabled = true;
            }));
        }

        private void buttonChangeWallpaper_Click(object sender, EventArgs e)
        {
            this.log("updating wallpaper");
            Task.Run(() => this.DoUpdate(this.runner.UpdateWallpaper));
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }

        private void button1_Click(object sender, EventArgs e) => this.Hide();

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            runner.Stop();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e) => Application.Exit();
    }

    public class Runner
    {
        public delegate void DoUpdateDelegate(Action update);

        public struct Config
        {
            public TimeSpan Interval;
        }

        private readonly Config config;
        private readonly IWallpaperDataRepository repo;
        private readonly IWallpaperSetter setter;
        private readonly IWallpaperIdGenerator idGen;
        private readonly IWallpaperProvider provider;
        private readonly LogDelegate log;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly DoUpdateDelegate doUpdate;

        private readonly CancellationToken cancellationToken;
        private Task task = null;

        public Runner(
            Config config,
            IWallpaperDataRepository repo,
            IWallpaperSetter setter,
            IWallpaperIdGenerator idGen,
            IWallpaperProvider provider,
            Func<DoUpdateDelegate> getUpdater,
            Func<LogDelegate> getLog,
            CancellationTokenSource cancellationTokenSource)
        {
            this.config = config;
            this.repo = repo;
            this.setter = setter;
            this.idGen = idGen;
            this.provider = provider;
            this.doUpdate = getUpdater();
            this.log = getLog();
            this.cancellationTokenSource = cancellationTokenSource;
            this.cancellationToken = cancellationTokenSource.Token;
        }

        public void UpdateWallpaper()
        {
            var wallpaper = provider.Download(idGen.GenerateId());
            setter.Set(wallpaper);
        }

        public void Start()
        {
            this.task = Task.Run(StartAsync);
        }

        public void Stop()
        {
            try
            {
                cancellationTokenSource.Cancel();
                if (task != null)
                {
                    // only wait this much time to prevent hang, which can occur when window is closed while a download is going on
                    Task.WaitAll(new[] { task }, TimeSpan.FromMilliseconds(100));
                }
            }
            catch (Exception ex) when (IsTaskCancellationException(ex)) { /* task is cancelled this is normal */ }
            catch (Exception ex) { this.log($"Error during shutting down: {ex}"); }
        }

        private async Task StartAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                doUpdate(UpdateWallpaper);
                log($"Sleeping {config.Interval}");
                await Task.Delay(this.config.Interval, this.cancellationToken);
            }
        }

        bool IsTaskCancellationException(Exception e)
        {
            if (e is TaskCanceledException) return true;
            if (e is AggregateException) return ((AggregateException)e).InnerExceptions.All(ie => ie is TaskCanceledException);
            return false;
        }
    }
}
