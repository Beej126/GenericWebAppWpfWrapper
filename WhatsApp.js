let previousMessageCount = 0;
setInterval(() => {
    let messageCount = 0;
    let unreadMessageNodes = document.querySelector('#pane-side').querySelectorAll('[aria-label*=\'unread message\']');
    unreadMessageNodes.forEach(n => messageCount += Number.parseInt(n.innerText));
    if (previousMessageCount === messageCount) return;
    previousMessageCount = messageCount;
    //console.log('new message count: ' + messageCount);
    window.chrome.webview.postMessage(messageCount.toString());
}, 750);
