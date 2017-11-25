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
        private Runner runner = null;
        private Task RunnerTask = null;
        private CancellationTokenSource cancellationTokenSource = null;
        private readonly Action<string> log;
        private readonly Action<Action> doUpdate;

        delegate void UpdateLogMessagesDelegate();
        delegate void UpdateButtonDelegate();

        public Form1()
        {
            InitializeComponent();
            this.log = msg =>
            {
                this.Invoke(new UpdateLogMessagesDelegate(() =>
                {
                    this.logMessages.AppendText(msg);
                    this.logMessages.AppendText(Environment.NewLine);
                }));
            };

            this.doUpdate = update =>
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
            };
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--start-visible"))
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }

            this.notifyIcon.Visible = true;

            int seed = DateTime.UtcNow.Millisecond;
            IWallpaperDataRepository repo = new WallpaperDataRepository(Path.Combine(Path.GetTempPath(), "eyecandy"), log);
            IWallpaperSetter setter = new WindowsWallpaperSetter(repo, log);
            IWallpaperIdGenerator idGen = new ApodWallpaperIdGenerator(seed);

            IWallpaperProvider provider = new CachedWallpaperProvider(
                repo, 
                new RobustWallpaperProvider(
                    new ApodWallpaperProvider(log),
                    numberOfRetries: 10,
                    retryInterval: TimeSpan.FromSeconds(30)), 
                log);

            TimeSpan interval = TimeSpan.FromMinutes(10);
            cancellationTokenSource = new CancellationTokenSource();

            this.runner = new Runner(interval, seed, repo, setter, idGen, provider, doUpdate, log, cancellationTokenSource.Token);

            if (!args.Contains("--no-auto-start"))
            {
                RunnerTask = Task.Run(() => this.runner.Start());
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                cancellationTokenSource.Cancel();
                if (RunnerTask != null)
                {
                    Task.WaitAll(RunnerTask);
                }
            }
            catch (Exception ex) when (IsTaskCancellationException(ex))
            {
            }
        }

        bool IsTaskCancellationException(Exception e)
        {
            if (e is TaskCanceledException) return true;
            if (e is AggregateException) return ((AggregateException)e).InnerExceptions.All(ie => ie is TaskCanceledException);
            return false;
        }

        private void buttonChangeWallpaper_Click(object sender, EventArgs e)
        {
            this.log("updating wallpaper");
            Task.Run(() => this.doUpdate(this.runner.UpdateWallpaper));
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
    }

    public class Runner
    {
        private readonly TimeSpan interval;
        private readonly int seed;
        private readonly IWallpaperDataRepository repo;
        private readonly IWallpaperSetter setter;
        private readonly IWallpaperIdGenerator idGen;
        private readonly IWallpaperProvider provider;
        private readonly Action<string> log;
        private readonly CancellationToken cancellationToken;
        private readonly Action<Action> doUpdate;

        public Runner(
            TimeSpan interval,
            int seed,
            IWallpaperDataRepository repo,
            IWallpaperSetter setter,
            IWallpaperIdGenerator idGen,
            IWallpaperProvider provider,
            Action<Action> doUpdate,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            this.interval = interval;
            this.seed = seed;
            this.repo = repo;
            this.setter = setter;
            this.idGen = idGen;
            this.provider = provider;
            this.doUpdate = doUpdate;
            this.log = log;
            this.cancellationToken = cancellationToken;
        }

        public void UpdateWallpaper()
        {
            var wallpaper = provider.Download(idGen.GenerateId());
            setter.Set(wallpaper);
        }

        public async Task Start()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                doUpdate(UpdateWallpaper);
                log($"Sleeping {interval}");
                await Task.Delay(this.interval, this.cancellationToken);
            }
        }
    }
}
