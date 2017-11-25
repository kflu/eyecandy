using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace eyecandy
{
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

    public static class Extensions
    {
        public static WallpaperData Get(this IWallpaperDataRepository repo, string id)
        {
            var (found, data) = repo.TryGet(id);
            if (!found) throw new ArgumentException($"{id} is not found in repo");
            return data;
        }
    }

    #region IMPL

    public class WallpaperDataRepository : IWallpaperDataRepository
    {
        private readonly string root;
        private readonly Action<string> log;

        public WallpaperDataRepository(string root, Action<string> log)
        {
            this.log = log;
            this.root = root;

            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }
        }

        public (bool found, WallpaperData data) TryGet(string id)
        {
            string dataFile = Path.Combine(root, id);
            if (!File.Exists(dataFile)) return (false, null);

            using (StreamReader reader = new StreamReader(dataFile))
            {
                return (true, JsonConvert.DeserializeObject<WallpaperData>(reader.ReadToEnd()));
            }
        }

        public void Store(WallpaperData data)
        {
            string dataFile = Path.Combine(root, data.Id);
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
        private readonly Action<string> log;

        public WindowsWallpaperSetter(IWallpaperDataRepository repo, Action<string> log)
        {
            this.repo = repo;
            this.log = log;
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
        private readonly IWallpaperProvider provider;
        private readonly int numberOfRetries;
        private readonly TimeSpan? retryInterval;

        public RobustWallpaperProvider(
            IWallpaperProvider provider,
            int numberOfRetries,
            TimeSpan? retryInterval)
        {
            this.provider = provider;
            this.numberOfRetries = numberOfRetries;
            this.retryInterval = retryInterval;
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
                    if (i == numberOfRetries) throw; // re-throw if exceeds number of retries
                    if (retryInterval != null)
                    {
                        Thread.Sleep(retryInterval.Value);
                    }
                }
            }
        }
    }

    public class CachedWallpaperProvider : IWallpaperProvider
    {
        private readonly IWallpaperDataRepository repo;
        private readonly IWallpaperProvider provider;
        private readonly Action<string> log;

        public CachedWallpaperProvider(
            IWallpaperDataRepository repo,
            IWallpaperProvider provider,
            Action<string> log)
        {
            this.repo = repo;
            this.provider = provider;
            this.log = log;
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
        private readonly Action<string> log;

        public ApodWallpaperProvider(Action<string> log)
        {
            this.log = log;
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
        Random rand;
        public ApodWallpaperIdGenerator(int seed)
        {
            this.rand = new Random(seed);
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
