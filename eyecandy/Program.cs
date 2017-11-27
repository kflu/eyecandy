using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autofac;

namespace eyecandy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[STAThread]
        //static void Main_Autofac()
        //{
        //    Application.EnableVisualStyles();
        //    Application.SetCompatibleTextRenderingDefault(false);
        //
        //    var builder = new Autofac.ContainerBuilder();
        //
        //    builder.Register(c => new WallpaperDataRepository.Config { Root = Path.Combine(Path.GetTempPath(), "eyecandy") }).SingleInstance();
        //    builder.Register(c => new RobustWallpaperProvider.Config { NumberOfRetries = 10, RetryInterval = TimeSpan.FromSeconds(30) }).SingleInstance();
        //    builder.Register(c => new ApodWallpaperIdGenerator.Config { Seed = DateTime.UtcNow.Millisecond }).SingleInstance();
        //    builder.Register(c => new Runner.Config { Interval = TimeSpan.FromMinutes(10) }).SingleInstance();
        //    builder.Register(c => new CancellationTokenSource()).SingleInstance();
        //
        //    builder.RegisterType<WallpaperDataRepository>().As<IWallpaperDataRepository>().SingleInstance();
        //    builder.RegisterType<WindowsWallpaperSetter>().As<IWallpaperSetter>().SingleInstance();
        //    builder.RegisterType<ApodWallpaperIdGenerator>().As<IWallpaperIdGenerator>().SingleInstance();
        //    builder.RegisterType<ApodWallpaperProvider>().SingleInstance();
        //    builder.Register<RobustWallpaperProvider>(c => new RobustWallpaperProvider(c.Resolve<ApodWallpaperProvider>(), c.Resolve<RobustWallpaperProvider.Config>()));
        //    builder.Register<IWallpaperProvider>(c => new CachedWallpaperProvider(
        //        c.Resolve<IWallpaperDataRepository>(),
        //        c.Resolve<RobustWallpaperProvider>(),
        //        c.Resolve<LogDelegate>()));
        //
        //    builder.Register<Func<Form1, LogDelegate>>(c => Form1.CreateLog);
        //    builder.RegisterType<Runner>();
        //    builder.RegisterType<Form1>();
        //    builder.Register<LogDelegate>(c => c.Resolve<Form1>().Log);
        //    builder.Register<Runner.DoUpdateDelegate>(c => c.Resolve<Form1>().DoUpdate);
        //
        //    using (var container = builder.Build())
        //    using (var scope = container.BeginLifetimeScope())
        //    {
        //        Application.Run(scope.Resolve<Form1>());
        //    }
        //}

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form1 form = null;
            Func<LogDelegate> getLog = () => form.Log;

            IWallpaperDataRepository repo = new WallpaperDataRepository(
                new WallpaperDataRepository.Config { Root = Path.Combine(Path.GetTempPath(), "eyecandy") }, 
                getLog);

            form = new Form1(
                new Runner(
                    new Runner.Config { Interval = TimeSpan.FromMinutes(10) },
                    repo,
                    new WindowsWallpaperSetter(repo, getLog),
                    new ApodWallpaperIdGenerator(new ApodWallpaperIdGenerator.Config { Seed = DateTime.Now.Millisecond }),
                    new CachedWallpaperProvider(
                        repo,
                        new RobustWallpaperProvider(
                            new ApodWallpaperProvider(getLog),
                            new RobustWallpaperProvider.Config
                            {
                                NumberOfRetries = 10,
                                RetryInterval = TimeSpan.FromSeconds(30),
                            }),
                        getLog),
                    () => form.DoUpdate,
                    getLog,
                    new CancellationTokenSource()),
                Form1.CreateLog);

            Application.Run(form);
        }
    }
}
