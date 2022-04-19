window.onload = () => {
    var previousMessageCount = 0;
    setInterval(() => {
        let messageCountNode = document.querySelector("div[role=navigation] div[aria-label*=Mail] > span > span");
        const messageCount = messageCountNode ? Number.parseInt(messageCountNode.textContent) : 0;
        if (previousMessageCount === messageCount) return;
        previousMessageCount = messageCount;
        window.chrome.webview.postMessage(messageCount.toString());
    }, 2000);
}