# Description
.Net 6 WPF app using msft webView2 to host web apps like WhatsApp, Gmail, Google Voice, etc so I can fine tune my preferred desktop UX.

![image](https://user-images.githubusercontent.com/6301228/164066607-777a8b53-3c32-4214-b294-fb62047a2195.png)


## Motivations
there's a few "good citizen" user experiences i'm looking for that don't come with the corresponding apps or web pages out of the box:
- ESC to minimize
- Standard window close button overridden to minimize
- no tray icon - shifting away from Notification Tray icons to having everything on Taskbar (Windows 11 influence)
- message count indicator on Taskbar icon

## Usage
- there are no binary releases published here yet... you'll have to build the exe yourself... let me know if there's interest and i'll take the time to create the CI/CD github action script
- once you have the exe, the idea is to create shortcuts to it with following parameters in sequential order (see .lnk examples in this repo):
  <div>---------- first two parms are required ------</div>

  1. the starting url (e.g. https://gmail.com)
  1. appname (e.g. Gmail) - this correlates to:
     - the main window title
     - pulling appname.ico for the running taskbar icon
     - loading appname.js as custom script that executes inside of every web page the designated url winds up loading... this faciliates all kinds of customization... i'm using it to scrape pages for message counts and display that on the windows taskbar... i've also leveraged it for customizing and elimating undersirable page content (see vipleague.js), ala a mini "[Greasemonkey](https://en.wikipedia.org/wiki/Greasemonkey)"
      <div>---------- the rest are optional ------</div>
  1. True/False - [Default: False] True = separate folder for application storage, cookies, etc.
  1. True/False - [Default: False] True = block all external link nav (good for disabling sites that are riddled with click jacking)
  1. "filename1.js,filename2.js" - comma delimited list of strings which are the only script resources allowed to be loaded (wildcard match via "Contains")
  1. x:y - aspect ratio, decimals allowed (e.g. 16:9.5 somehow worked best for a video site)
 
  example command line:<br/>
  `path\GenericWebAppWpfWrapper.exe https://vipleague.im/american-football-schedule-streaming-links "VipLeague" False True "embed2.min.js" 16:9.5`
 
  lastly, set the "Start in:" of the shortcut to the directory those ico and js files are in

## Notes
- since this does a window minimize on close button, there's an additional button in the title bar to truly quit the app<br/>
  ![image](https://user-images.githubusercontent.com/6301228/137362283-e9df8bf1-38df-40f5-8f42-efcdce31a9fa.png)
- in the js scripts, i'm using straightforward document.querySelector patterns to scrape the unread message count for each site and send that out through the embedded browser's messaging api into a wpf event handler that renders the count as a standard "badge" on the Windows Taskbar icon ... pretty neat integration!
