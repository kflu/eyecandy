using System;
using System.Linq;
using System.Windows.Forms;

namespace eyecandy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            /*
             * There is a reason I don't compose dependencies in the Main - Primarily to avoid circular dependencies.
             * Problem is the logger `LogDelegate` implementation logs to the main form's message box. So the logger
             * depends on the form being instantiated first. But many of the form's dependencies (which must be instantiated
             * before the form) depend on the logger, and hence the circular dependency.
             * 
             * It is possible to break the circle, by, e.g., let the logger dependency on each component to be lazily evaluated:
             * 
             *     Func<LogDelegate> getLogger;
             *     
             * Then use the logger as:
             * 
             *     this.getLogger()("hello world");
             * 
             * Too ugly to be used.
             * 
             * Another option is to make the Form's dependency resolved lazily. For example, instead of:
             * 
             *     private readonly Runner runner;
             * 
             * use:
             * 
             *     private readonly Func<Runner> getRunner;
             * 
             * And only get the runner when it should be used. But again that's still feels over fitting for just the circular dependency
             * problem.
             */
            string[] args = Environment.GetCommandLineArgs();
            Application.Run(new Form1(new Form1.Config
            {
                StartVisible = args.Contains("--start-visible"),
                PeriodicUpdate = !args.Contains("--no-periodic-update"),
            }));
        }
    }
}
