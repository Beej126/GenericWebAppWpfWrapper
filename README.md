# Description
.Net 6 WPF app using msft webView2 to host web apps like Google Chat, Google Voice and WhatsApp so i can fine tune my preferred desktop UX.

![image](https://user-images.githubusercontent.com/6301228/164066607-777a8b53-3c32-4214-b294-fb62047a2195.png)


## Motivations
- shifting away from Notification Tray icons to having everything on Taskbar (Windows 11 influence)
- the windows apps provided by the respective vendors don't do the few "good citizen" UX i'm looking for, e.g. ESC to minimize, no tray icon, message count indicator on taskbar icon

## Usage
- there are no binary releases... you'll have to build the exe yourself... let me know if there's interest and i'll take the time to create the CI/CD github action script
- once you have the exe, the idea is to create a shortcut to it with two parameters (see blahLnk.lnk example in this repo):
  1. the url of the web app
  2. the filename prefix for (blah.ico and blah.js)
  - and set the "Start in:" of the shortcut to the directory those ico and js files are in

## Notes
- since this does a window minimize on close button, there's an additional button in the title bar to truly quit the app<br/>
  ![image](https://user-images.githubusercontent.com/6301228/137362283-e9df8bf1-38df-40f5-8f42-efcdce31a9fa.png)
- i'm using document.querySelector style scrape of the pages to gather an unread message count and display that as a Windows taskbar icon badge... works amazingly well!
