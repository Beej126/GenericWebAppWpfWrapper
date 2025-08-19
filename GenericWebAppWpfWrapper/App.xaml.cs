using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GenericWebAppWpfWrapper
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Parse command line arguments into a dictionary
            var argsDict = ParseCommandLineArgs(e.Args);

            // Check for required arguments
            if (string.IsNullOrWhiteSpace(argsDict["Url"]) || string.IsNullOrWhiteSpace(argsDict["AppName"]))
            {
                ShowUsageDialog();
                Shutdown();
                return;
            }

            if (!argsDict["Url"].StartsWith("http")) argsDict["Url"] = "https://" + argsDict["Url"];

            IConfigurationRoot configuration = new ConfigurationBuilder()
                //.SetBasePath(Directory.GetCurrentDirectory())
                //.SetBasePath(Directory.GetParent(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).FullName)
                .AddInMemoryCollection(argsDict)
                //not used yet:.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();

            //CookieContainer cookieContainer = new();
            //from: https://github.com/dotnet/extensions/issues/872#issuecomment-496419597
            services.AddHttpClient("default");
                //.ConfigureHttpMessageHandlerBuilder(builder =>
                //{
                //    if (builder.PrimaryHandler is HttpClientHandler handler)
                //    {
                //        //see WebResourceRequested in MainWindow.xaml.cs
                //        // apparently by setting HttpClientHandler.UseCookies false the HttpClient then allows cookies to be set manually
                //        // but these never got sent in the actual request!?!?
                //        handler.UseCookies = false;

                //        //trying this approach: https://stackoverflow.com/questions/56820489/httprequestmessage-doesnt-add-cookie-into-request/56822712#56822712
                //        //handler.CookieContainer = cookieContainer;

                //    }
                //});

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<MainWindow>();
            //services.AddSingleton<CookieContainer>(cookieContainer);

            var serviceProvider = services.BuildServiceProvider();

            var mainWin = serviceProvider.GetRequiredService<MainWindow>();
            mainWin.Show();
        }

        /// <summary>
        /// Displays usage information when the application is run without arguments
        /// </summary>
        private void ShowUsageDialog()
        {
            string usageText =
@"Command Line Arguments:

Required:
  -Url [url]                        The starting URL (e.g., mail.google.com), https:// will be added automatically if not present
  -Title [name]                     The application name (e.g., Gmail)
                                        Used for window title, .ico filename, and injected .js filename
                                        spaces are removed from the Title when mapped to filenames

Optional:
  -SeparateUserData [True/False]    Create separate folder for app storage (Default: False)
  -BlockExternalLinks [True/False]  Block external navigation (Default: False)
  -AllowedScripts [scripts]         Comma-delimited list of script resources to allow
  -AspectRatio [x:y]                Force window aspect ratio (e.g., 16:9.5)

Example:
  GenericWebAppWpfWrapper.exe -Url mail.google.com -Title Gmail -BlockExternalLinks True";

            // Use our custom dialog with monospaced font
            var dialog = new UsageDialog(usageText);
            dialog.ShowDialog();
        }

        /// <summary>
        /// Parses command line arguments into a dictionary
        /// </summary>
        private Dictionary<string, string> ParseCommandLineArgs(string[] args)
        {
            // Create a dictionary to hold our configuration settings
            var result = new Dictionary<string, string>
            {
                // Initialize with default keys to ensure they exist in the dictionary
                { "Url", null },
                { "AppName", null }, // Keep AppName for backward compatibility
                { "Title", null },
                { "SeparateUserData", null },
                { "BlockExternalLinks", null },
                { "AllowedScripts", null },
                { "AspectRatio", null }
            };

            // Parse named arguments in format: -Name value
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].StartsWith("-", StringComparison.OrdinalIgnoreCase))
                {
                    string key = args[i].Substring(1);
                    string value = args[i + 1];
                    
                    // Skip the next argument as we've already consumed it as a value
                    i++;
                    
                    // Map -Title to AppName for backward compatibility
                    if (string.Equals(key, "Title", StringComparison.OrdinalIgnoreCase))
                    {
                        result["AppName"] = value; // Set the AppName value
                        result["Title"] = value;   // Also set Title
                    }
                    // Only add it if it's one of our expected keys (case-insensitive)
                    else if (result.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                    {
                        result[key] = value;
                    }
                }
            }

            return result;
        }
    }
}
