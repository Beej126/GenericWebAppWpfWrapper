using Microsoft.Extensions.Configuration;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
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

            InitializeComponent();

            this.wv2.Source = new Uri(config["Url"]);
        }

        public override void OnApplyTemplate()
        {
            var oDep = GetTemplateChild("btnQuit");
            if (oDep != null)
            {
                ((Button)oDep).Click += this.btnHelp_Click;
            }

            base.OnApplyTemplate();
        }

        private void btnHelp_Click(System.Object sender, System.Windows.RoutedEventArgs e)
        {
            //MessageBox.Show("Help", "", MessageBoxButton.OK, MessageBoxImage.Information);
            //Environment.Exit(0);
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
            //else if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt) {

            //}
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

        private void webView_Initialized(object sender, System.EventArgs e)
        {
            wv2.CoreWebView2InitializationCompleted += (object sender, CoreWebView2InitializationCompletedEventArgs e) =>
            {
                //inject javascript function that scrapes the chat page for message count
                //here's a good thread: https://github.com/MicrosoftEdge/WebView2Feedback/issues/253#issuecomment-641577176
                //another good thread: https://blogs.msmvps.com/bsonnino/2021/02/27/using-the-new-webview2-in-a-wpf-app/
                //https://www.fatalerrors.org/a/excerpt-interaction-between-webview2-and-js.html

                //AddScriptToExecuteOnDocumentCreatedAsync fires on frames as well which gives us full power to override spammity spam vs just the main page
                wv2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), config["AppName"].Replace(" ", "") + ".js")));
                /*$@"
                let previousMessageCount = 0;
                setInterval(()=>{{
                    let messageCount = 0;
                    let unreadMessageNodes = document.querySelector('#pane-side').querySelectorAll('[aria-label*=\'unread message\']');
                    unreadMessageNodes.forEach(n=>messageCount += Number.parseInt(n.innerText));
                    if (previousMessageCount === messageCount) return;
                    previousMessageCount = messageCount;
                    //console.log('new message count: ' + messageCount);
                    window.chrome.webview.postMessage(messageCount.toString());
                }}, 750);*/
            };

            wv2.EnsureCoreWebView2Async(); //this initializes wv2.CoreWebView2 (i.e. makes it not null)
        }
    }
}
