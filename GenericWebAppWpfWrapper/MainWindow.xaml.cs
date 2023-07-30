using Microsoft.Extensions.Configuration;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenericWebAppWpfWrapper
{

    public partial class MainWindow : Window
    {
        private bool isReallyExit = false;
        private readonly IConfiguration config;

        public MainWindow(IConfiguration config)
        {
            this.config = config;
            this.Icon = new BitmapImage(new Uri(Path.Combine(Directory.GetCurrentDirectory(), config["AppName"].Replace(" ", "") + ".ico")));
            this.Title = config["AppName"];
            this.WindowState = WindowState.Minimized;

            InitializeComponent();
        }

        public override void OnApplyTemplate()
        {
            var oDep = GetTemplateChild("btnQuit");
            if (oDep != null)
            {
                ((Button)oDep).Click += this.btnQuit_Click;
            }

            base.OnApplyTemplate();
        }

        private void btnQuit_Click(System.Object sender, System.Windows.RoutedEventArgs e)
        {
            isReallyExit = true;
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled && e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                WindowState = WindowState.Minimized;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isReallyExit)
            {
                e.Cancel = true; // this will prevent to close
                WindowState = WindowState.Minimized;
            }
        }
        private void Wv2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string countText = e.TryGetWebMessageAsString();

            int iconWidth = 20;
            int iconHeight = 20;

            RenderTargetBitmap bmp = new RenderTargetBitmap(iconWidth, iconHeight, 96, 96, PixelFormats.Default);
            ContentControl root = new ContentControl();

            root.ContentTemplate = (DataTemplate)Resources["OverlayIcon"];
            root.Content = countText == "0" ? null : countText;

            root.Arrange(new Rect(0, 0, iconWidth, iconHeight));

            bmp.Render(root);

            TaskbarItemInfo.Overlay = bmp;
        }

        private string savedUserAgent;
        private void webView_Initialized(object wvInitSender, System.EventArgs e)
        {
            wv2.CoreWebView2InitializationCompleted += (object sender, CoreWebView2InitializationCompletedEventArgs e) =>
            {

                //https://stackoverflow.com/questions/57479245/how-to-add-support-for-html5-notifications-in-xaml-webview-in-a-uwp-app/57503529#57503529
                wv2.CoreWebView2.PermissionRequested += (object sender, CoreWebView2PermissionRequestedEventArgs e) =>
                {
                    //MessageBox.Show("wv2.CoreWebView2.PermissionRequested\r\n\r\nPermissionKind: " + e.PermissionKind + "\r\n\r\nUri: " + e.Uri);
                    e.State = CoreWebView2PermissionState.Allow;
                };

                wv2.CoreWebView2.NavigationStarting += (object sender, CoreWebView2NavigationStartingEventArgs e) =>
                {
                    //see massive thread on the need for user agent string with google auth: https://github.com/MicrosoftEdge/WebView2Feedback/issues/1647#issuecomment-1063861835
                    //important, it must be set in something later in the lifecycle like NavigationStarting 

                    //but fyi, stuff like whatsapp requires something valid
                    //example user agent strings: https://deviceatlas.com/blog/list-of-user-agent-strings#android

                    //need to flip user agent specifically when on google login page or google blocks with
                    //"Couldn't sign you in... This browser or app may not be secure."
                    if (savedUserAgent == null) savedUserAgent = wv2.CoreWebView2.Settings.UserAgent;
                    wv2.CoreWebView2.Settings.UserAgent = (new Uri(e.Uri)).Host.Contains("accounts.google.com") ? "could be anything" : savedUserAgent;

                    //MessageBox.Show("wv2.CoreWebView2.NavigationStarting\r\n\r\nurl: " + e.Uri + "\r\n\r\nuseragent: " + wv2.CoreWebView2.Settings.UserAgent);
                };

                wv2.CoreWebView2.NewWindowRequested += (object sender, CoreWebView2NewWindowRequestedEventArgs newWindowArgs) =>
                {
                    //if we're launching a new window for google login,
                    //we need to send it back to the existing webview so we can control the UserAgent
                    //which is the crucial piece of avoiding google's embedded-webview block
                    if (newWindowArgs.Uri.Contains("accounts.google.com"))
                    {
                        //newWindowArgs.NewWindow = wv2.CoreWebView2; //this worked for voice.google.com but crashed for mail.google.com
                        newWindowArgs.Handled = true;
                        wv2.CoreWebView2.Navigate(newWindowArgs.Uri);
                    }

                    //otherwise launch external links out to OS's default browser to get the best experience with cached logins and extensions, versus trapped in the self contained web view
                    else
                    {
                        newWindowArgs.Handled = true;

                        // some unexpected finesse required to get down to clean urls, especially when clicking links in gmail...
                        // gmail embeds what is probably a tracking wrapper around all urls so strip that off before launching because somehow it triggered launching android subsystem for windows on my machine?!?!
                        var url = newWindowArgs.Uri;
                        var match = System.Text.RegularExpressions.Regex.Match(url, @"https:\/\/www\.google\.com\/url\?(.*?)(q|url)=(.*?)&", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success) { url = match.Groups[3].Value; }

                        // if we see the long urlencoded value for "/" or short form of encoded ":/" then we know to urldecode... hopefully that's a reliable way to catch all cases???
                        if (url.ToUpper().Contains("%252F") || url.ToUpper().Contains("%3A%2F")) url = System.Web.HttpUtility.UrlDecode(url);

                        txtMessage.Text = "launching url: " + url;

                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo {
                                FileName = url,
                                Verb = "open",
                                UseShellExecute = true
                            }
                        );
                    }

                    //////////////////////////////////////////////////////////////////////////////
                    //i tried a bunch of stuff to instantiate a new webview
                    //but none of them would hit the CoreWebView2InitializationCompleted event
                    //i'm guessing we need to embed the webview in a wpf form for it to work

                    //Changes to settings should be made BEFORE setting NewWindow to ensure that those settings take effect for the newly setup WebView.

                    //msft webview2 team stack-o sample code: https://stackoverflow.com/questions/65087294/how-do-i-get-reference-to-the-new-window-in-webview2/65132490#65132490
                    //gotta use Deferalls pattern to handle async creation of new CoreWebView2: https://docs.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model#deferrals
                    //var deferall = newWindowArgs.GetDeferral();
                    //var newV = new Microsoft.Web.WebView2.Wpf.WebView2();
                    //newV.Initialized += (object sender, EventArgs e) => {
                    //newV.CoreWebView2InitializationCompleted += (object sender, CoreWebView2InitializationCompletedEventArgs newVargs) =>
                    //{
                    //    newV.CoreWebView2.Settings.UserAgent = "chrome";
                    //    newWindowArgs.NewWindow = newV.CoreWebView2;
                    //    newWindowArgs.Handled = true;
                    //    deferall.Complete();
                    //    //newV.CoreWebView2.Navigate(newWindowArgs.Uri);
                    //};
                    //newV.Source = new Uri(newWindowArgs.Uri);
                    //newV.EnsureCoreWebView2Async();
                    //};

                };

                //inject javascript function that scrapes the chat page for message count
                //here's a good thread: https://github.com/MicrosoftEdge/WebView2Feedback/issues/253#issuecomment-641577176
                //another good thread: https://blogs.msmvps.com/bsonnino/2021/02/27/using-the-new-webview2-in-a-wpf-app/
                //https://www.fatalerrors.org/a/excerpt-interaction-between-webview2-and-js.html

                //AddScriptToExecuteOnDocumentCreatedAsync fires on frames as well which gives us full power to override spammity spam vs just the main page
                wv2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), config["AppName"].Replace(" ", "") + ".js")));
            };

            //wow deploying webView2 is quite complicated since it's a native dll requiring multiple platform versions of the same file
            //adding the await here means we'll at least see the exception when it's not being resolved properly!!!
            //https://github.com/MicrosoftEdge/WebView2Feedback/issues/730
            //try
            //{
            //    await wv2.EnsureCoreWebView2Async(); //this initializes wv2.CoreWebView2 (i.e. makes it not null)
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message, "Error initializing embedded web browser", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            //    return;
            //}

            //good thread on init: https://github.com/MicrosoftEdge/WebView2Feedback/issues/1577#issuecomment-930639679
            //most of the time you'll want all instance to share things like google authentication,
            //but i had a scenario where i needed to run voice under a separate account...
            //of course google allows having multiple accounts available, but there must always be a default from starting up cold
            //and this way you can control that more directly by having a dedicated UserData folder
            if (config["SeparateUserData"] != null) wv2.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties { UserDataFolder = config["AppName"] };

            wv2.Source = new Uri(config["Url"]);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                //jiggling the window size upon re-dispaly avoids annoying little blank page bug
                Width += 1;
                Width -= 1;
            }
        }
    }
}
