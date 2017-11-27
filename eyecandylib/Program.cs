using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace eyecandy
{
    public delegate void LogDelegate(string msg);

    public class WallpaperData
    {
        public string Id;
        public byte[] Wallpaper;
        public string Explanation;
    }

    public interface IWallpaperDataRepository
    {
        void Store(WallpaperData data);
        (bool found, WallpaperData data) TryGet(string id);
    }

    public interface IWallpaperProvider
    {
        WallpaperData Download(string id);
    }

    public interface IWallpaperIdGenerator
    {
        string GenerateId();
    }

    public interface IWallpaperGenerator
    {
        IWallpaperIdGenerator IdGenerator { get; }

        IWallpaperProvider WallpaperProvider { get; }
    }

    public interface IWallpaperSetter
    {
        void Set(WallpaperData wallpaper);
    }

    public static class Utils
    {
        public static WallpaperData Get(this IWallpaperDataRepository repo, string id)
        {
            var (found, data) = repo.TryGet(id);
            if (!found) throw new ArgumentException($"{id} is not found in repo");
            return data;
        }

        public static LogDelegate CreateLoggerWithTimestamp(LogDelegate innerLog)
        {
            return msg =>
            {
                innerLog($"{DateTime.Now.ToString("s")}: {msg}");
            };
        }
    }

    #region IMPL

    public class WallpaperDataRepository : IWallpaperDataRepository
    {
        public struct Config
        {
            public string Root;
        }

        private readonly Config config;
        private readonly LogDelegate log;

        public WallpaperDataRepository(Config config, Func<LogDelegate> getLog)
        {
            this.log = getLog();
            this.config = config;

            if (!Directory.Exists(config.Root))
            {
                Directory.CreateDirectory(config.Root);
            }
        }

        public (bool found, WallpaperData data) TryGet(string id)
        {
            string dataFile = Path.Combine(config.Root, id);
            if (!File.Exists(dataFile)) return (false, null);

            using (StreamReader reader = new StreamReader(dataFile))
            {
                return (true, JsonConvert.DeserializeObject<WallpaperData>(reader.ReadToEnd()));
            }
        }

        public void Store(WallpaperData data)
        {
            string dataFile = Path.Combine(config.Root, data.Id);
            log($"Storing wallpaper data to {dataFile}");
            using (StreamWriter wr = new StreamWriter(dataFile, append: false))
            {
                wr.Write(JsonConvert.SerializeObject(data));
            }
        }
    }

    public class WindowsWallpaperSetter : IWallpaperSetter
    {
        // For setting a string parameter
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, uint fWinIni);

        private readonly IWallpaperDataRepository repo;
        private readonly LogDelegate log;

        public WindowsWallpaperSetter(IWallpaperDataRepository repo, Func<LogDelegate> getLog)
        {
            this.repo = repo;
            this.log = getLog();
        }

        public void Set(WallpaperData wallpaper)
        {
            var data = repo.Get(wallpaper.Id);
            string fn = Path.Combine(Path.GetTempPath(), "eyecandy.img");

            using (var str = File.OpenWrite(fn))
            {
                str.Seek(0, SeekOrigin.Begin);
                str.Write(data.Wallpaper, 0, data.Wallpaper.Length);
                str.SetLength(str.Position + 1);
            }

            log($"Setting wallpaper to {fn}");
            if (!SystemParametersInfo(0x14, 0, fn, 0x14))
            {
                log($"setting wallpaper {fn} failed");
            }
        }
    }

    public class RobustWallpaperProvider : IWallpaperProvider
    {
        public struct Config
        {
            public int NumberOfRetries;
            public TimeSpan? RetryInterval;
        }

        private readonly IWallpaperProvider provider;
        private readonly Config config;

        public RobustWallpaperProvider(
            IWallpaperProvider provider,
            Config config)
        {
            this.provider = provider;
            this.config = config;
        }

        public WallpaperData Download(string id)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    return this.provider.Download(id);
                }
                catch (DownloadFailedException)
                {
                    if (i == config.NumberOfRetries) throw; // re-throw if exceeds number of retries
                    if (config.RetryInterval != null)
                    {
                        Thread.Sleep(config.RetryInterval.Value);
                    }
                }
            }
        }
    }

    public class CachedWallpaperProvider : IWallpaperProvider
    {
        private readonly IWallpaperDataRepository repo;
        private readonly IWallpaperProvider provider;
        private readonly LogDelegate log;

        public CachedWallpaperProvider(
            IWallpaperDataRepository repo,
            IWallpaperProvider provider,
            Func<LogDelegate> getLog)
        {
            this.repo = repo;
            this.provider = provider;
            this.log = getLog();
        }

        public WallpaperData Download(string id)
        {
            var result = repo.TryGet(id);
            if (result.found) return result.data;
            log($"cache miss, downloading using {this.provider.GetType().Name}");
            var data = this.provider.Download(id);

            try
            {
                this.repo.Store(data);
            }
            catch (Exception e)
            {
                log($"Error storing data to repo: {e}");
            }

            return data;
        }
    }

    public class DownloadFailedException : Exception
    {
        public DownloadFailedException() : base() { }
        public DownloadFailedException(string msg) : base(msg) { }
    }

    public class ApodWallpaperProvider : IWallpaperProvider
    {
        private readonly LogDelegate log;

        public ApodWallpaperProvider(Func<LogDelegate> getLog)
        {
            this.log = getLog();
        }

        public class ApodData
        {
            public string title;
            public string explanation;
            public string date;
            public string hdurl;
            public string url;
            public string media_type;
        }

        public WallpaperData Download(string id)
        {
            string url = $"https://api.nasa.gov/planetary/apod?api_key=DEMO_KEY&date={id}&hd=True";
            log($"Downloading from {url}");
            using (var client = new WebClient())
            {
                string content = client.DownloadString(url);
                log($"APOD data: {content}");
                ApodData data = JsonConvert.DeserializeObject<ApodData>(content);
                if (data.media_type != "image") throw new DownloadFailedException();

                string picUrl = data.hdurl ?? data.url;
                if (picUrl == null) throw new InvalidDataException($"picture url is not available: {content}");

                return new WallpaperData
                {
                    Id = id,
                    Wallpaper = client.DownloadData(picUrl),
                    Explanation = data.explanation,
                };
            }
        }
    }

    public class ApodWallpaperIdGenerator : IWallpaperIdGenerator
    {
        public struct Config
        {
            public int Seed;
        }

        private readonly Config config;
        private readonly Random rand;

        public ApodWallpaperIdGenerator(Config config)
        {
            this.rand = new Random(config.Seed);
        }

        public string GenerateId()
        {
            int daysAgo = this.rand.Next(0, 5 * 365);
            DateTime dt = DateTime.Today - TimeSpan.FromDays(daysAgo);
            return dt.ToString("yyyy-MM-dd");
        }
    }

    #endregion IMPL
}
