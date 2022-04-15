# WhatsAppWebWpf
.Net 5 WPF app using msft webView2 to host web.whatsapp.com so i can do handy UX stuff like ESC to minimize

## Notes
- since this does a window minimize on close button, there's an additional button in the title bar to truly quit the app
  ![image](https://user-images.githubusercontent.com/6301228/137362283-e9df8bf1-38df-40f5-8f42-efcdce31a9fa.png)
- it does a simple document.querySelector style scrape of the page to gather an unread message count and display that as a Windows taskbar icon badge
- i'm not at the point of releasing precompiled binaries for this yet... mostly just putting out there to gauge interest and ridicule ;)  i have just achieved ClickOnce deploy [on another project](https://github.com/Beej126/VipLeagueWpf) so it's possible given encouragement.
