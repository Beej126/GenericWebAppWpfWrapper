let previousMessageCount = 0;
setInterval(() => {
    let messageCountBadgeNode = document.querySelector("a[gv-test-id='sidenav-messages'] .navItemBadge");
    let messageCount = messageCountBadgeNode ? Number.parseInt(messageCountBadgeNode.textContent) : 0;
    if (previousMessageCount === messageCount) return;
    previousMessageCount = messageCount;
    //console.log('new message count: ' + messageCount);
    window.chrome.webview.postMessage(messageCount.toString());
}, 750);
