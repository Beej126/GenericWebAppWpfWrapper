let previousMessageCount = 0;
setInterval(() => {
    const unreadMessageText = document.querySelector("[id^=NewMessagesBarJumpToNewMessages]")?.innerText?.split(" ")?.[0];
    const messageCount = unreadMessageText ? Number.parseInt(unreadMessageText) : 0;
    if (previousMessageCount === messageCount) return;
    previousMessageCount = messageCount;
    window.chrome.webview.postMessage(messageCount.toString());
}, 750);
