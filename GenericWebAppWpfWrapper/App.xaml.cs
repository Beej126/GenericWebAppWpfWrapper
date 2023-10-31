using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Linq;
using System.Collections.Generic;

namespace GenericWebAppWpfWrapper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            /*
                if (e.Args.Count() > 0)
                {
                    MessageBox.Show("You have the latest version.");
                    Shutdown();
                    return;
                }

                JumpTask task = new JumpTask
                {
                    Title = "Exit Google Chat",
                    Description = "Closes the App",
                    CustomCategory = "Actions",
                    IconResourcePath = Assembly.GetEntryAssembly().Location,
                    ApplicationPath = "pwsh.exe",
                    Arguments = "-WindowStyle Hidden -Command taskkill.exe /f /im GmailZero.exe",
                    //Assembly.GetEntryAssembly().Location
                };

                JumpList jumpList = new JumpList();
                jumpList.JumpItems.Add(task);
                jumpList.ShowFrequentCategory = false;
                jumpList.ShowRecentCategory = false;

                JumpList.SetJumpList(Application.Current, jumpList);
            */



            IConfigurationRoot configuration = new ConfigurationBuilder()
                //.SetBasePath(Directory.GetCurrentDirectory())
                .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Url", getArg(e.Args, 0) },
                    { "AppName", getArg(e.Args, 1) },
                    { "SeparateUserData", getArg(e.Args, 2) },
                    { "BlockExternalLinks", getArg(e.Args, 3) },
                    { "OnlyAllowScripts", getArg(e.Args, 4) },
                    { "AspectRatio", getArg(e.Args, 5) },
                })
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();

            services.AddHttpClient();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<MainWindow>();

            var serviceProvider = services.BuildServiceProvider();

            var mainWin = serviceProvider.GetRequiredService<MainWindow>();
            mainWin.Show();
        }
        private string getArg(string[] args, int index) => args.Length > index ? args[index] : null;
    }

}
