using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autofac;

namespace eyecandy
{
    public partial class Form1 : Form
    {
        public struct Config
        {
            public bool StartVisible;
            public bool PeriodicUpdate;
        }

        private delegate void UpdateLogMessagesDelegate();
        private delegate void UpdateButtonDelegate();

        private readonly Runner runner;
        private readonly Config config;

        public Form1(Config config) : base()
        {
            this.config = config;
            InitializeComponent();
        
            /*
             * TODO: Using a DI framework:
             * 
             *   - instead of storing runner, I'll need to store the lifetime scope
             *   - hook up the Dispose() method to dispose the scope (but this is what I should do even using poorman's DI. FIXME)
             */

            IWallpaperDataRepository repo = new WallpaperDataRepository(
                new WallpaperDataRepository.Config { Root = Path.Combine(Path.GetTempPath(), "eyecandy") },
                this.Log);
        
            this.runner = new Runner(
                new Runner.Config
                {
                    UpdateInterval = TimeSpan.FromMinutes(10),
                    NumberOfRetries = 10,
                    RetryInterval = TimeSpan.FromSeconds(30),
                },
                repo,
                new WindowsWallpaperSetter(repo, this.Log),
                new ApodWallpaperIdGenerator(new ApodWallpaperIdGenerator.Config { Seed = DateTime.Now.Millisecond }),
                new CachedWallpaperProvider(
                    repo,
                    new ApodWallpaperProvider(this.Log),
                    this.Log),
                this.DoUpdate,
                this.Log,
                new CancellationTokenSource());
        }

        private void Log(string msg)
        {
            this.Invoke(new UpdateLogMessagesDelegate(() =>
            {
                this.logMessages.AppendText(msg);
                this.logMessages.AppendText(Environment.NewLine);
            }));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (this.config.StartVisible)
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

            if (this.config.PeriodicUpdate)
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
            this.Log("updating wallpaper");
            Task.Run(() => this.DoUpdate(this.runner.UpdateWallpaper));
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }

        private void button1_Click(object sender, EventArgs e) => this.Hide();
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) => runner.Stop();
        private void Form1_FormClosed(object sender, FormClosedEventArgs e) => Application.Exit();
    }
}
