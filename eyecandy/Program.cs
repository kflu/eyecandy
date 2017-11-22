using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;

namespace eyecandy
{
    class Program
    {
        static void Main(string[] args)
        {
            int seed = DateTime.UtcNow.Millisecond;
            IWallpaperDataRepository repo = new WallpaperDataRepository(Path.Combine(Path.GetTempPath(), "eyecandy"));
            IWallpaperSetter setter = new WindowsWallpaperSetter(repo);
            IWallpaperIdGenerator idGen = new ApodWallpaperIdGenerator(seed);
            IWallpaperProvider provider = new CachedWallpaperProvider(repo, new ApodWallpaperProvider());

            TimeSpan interval = TimeSpan.FromMinutes(10);

            while (true)
            {
                var wallpaper = provider.Download(idGen.GenerateId());
                setter.Set(wallpaper);

                Console.WriteLine($"Sleeping {interval}");
                Thread.Sleep(interval);
            }
        }
    }

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

        public WallpaperDataRepository(string root)
        {
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            this.root = root;
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
            Console.WriteLine($"Storing wallpaper data to {dataFile}");
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

        public WindowsWallpaperSetter(IWallpaperDataRepository repo)
        {
            this.repo = repo;
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

            Console.WriteLine($"Setting wallpaper to {fn}");
            if (!SystemParametersInfo(0x14, 0, fn, 0x14))
            {
                Console.WriteLine($"setting wallpaper {fn} failed");
            }
        }
    }

    public class CachedWallpaperProvider : IWallpaperProvider
    {
        private readonly IWallpaperDataRepository repo;
        private readonly IWallpaperProvider provider;

        public CachedWallpaperProvider(
            IWallpaperDataRepository repo,
            IWallpaperProvider provider)
        {
            this.repo = repo;
            this.provider = provider;
        }

        public WallpaperData Download(string id)
        {
            var result = repo.TryGet(id);
            if (result.found) return result.data;
            Console.WriteLine($"cache miss, downloading using {this.provider.GetType().Name}");
            var data = this.provider.Download(id);

            try
            {
                this.repo.Store(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error storing data to repo: {e}");
            }

            return data;
        }
    }

    public class ApodWallpaperProvider : IWallpaperProvider
    {
        public class ApodData
        {
            public string title;
            public string explanation;
            public string date;
            public string hdurl;
            public string url;
        }

        public WallpaperData Download(string id)
        {
            string url = $"https://api.nasa.gov/planetary/apod?api_key=DEMO_KEY&date={id}&hd=True";
            Console.WriteLine($"Downloading from {url}");
            using (var client = new WebClient())
            {
                string content = client.DownloadString(url);
                Console.WriteLine($"APOD data: {content}");
                ApodData data = JsonConvert.DeserializeObject<ApodData>(content);
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
