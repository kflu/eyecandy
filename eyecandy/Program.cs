using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace eyecandy
{
    class Program
    {
        static void Main(string[] args)
        {
            int seed = DateTime.UtcNow.Millisecond;
            var gen = new ApodWallpaperGenerator(seed);
            byte[] wallpaper = gen.WallpaperProvider.Download(gen.IdGenerator.GenerateId());

            IWallpaperSetter setter = new WindowsWallpaperSetter();
            setter.Set(wallpaper);
        }
    }

    public class WindowsWallpaperSetter : IWallpaperSetter
    {
        // For setting a string parameter
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, uint fWinIni);

        public void Set(byte[] wallpaper)
        {
            string fn = Path.GetTempFileName();
            using (Stream str = File.OpenWrite(fn))
            {
                str.Write(wallpaper, 0, wallpaper.Length);
            }

            if(!SystemParametersInfo(0x14, 0, fn, 0x14))
            {
                Console.WriteLine($"setting wallpaper ${fn} failed");
            }

            // TODO can I delete the file now?
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

        public byte[] Download(string id)
        {
            string url = $"https://api.nasa.gov/planetary/apod?api_key=DEMO_KEY&date={id}&hd=True";
            Console.WriteLine($"Downloading from {url}");
            using (var client = new WebClient())
            {
                string content = client.DownloadString(url);
                ApodData data = JsonConvert.DeserializeObject<ApodData>(content);
                if (data.hdurl == null)
                {
                    throw new InvalidDataException($"HD link is not available: {content}");
                }

                return client.DownloadData(data.hdurl);
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

    public class ApodWallpaperGenerator : IWallpaperGenerator
    {
        public ApodWallpaperGenerator(int seed)
        {
            IdGenerator = new ApodWallpaperIdGenerator(seed);
            WallpaperProvider = new ApodWallpaperProvider();
        }

        public IWallpaperIdGenerator IdGenerator { get; }

        public IWallpaperProvider WallpaperProvider { get; }
    }

    public interface IWallpaperProvider
    {
        byte[] Download(string id);
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
        void Set(byte[] wallpaper);
    }
}
