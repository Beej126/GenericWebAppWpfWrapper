let previousMessageCount = 0;
setInterval(() => {
    let messageCount = 0;
    let messageCountBadgeNodes = document.querySelectorAll(".activity-badge:not(.activity-badge-container)");
    messageCountBadgeNodes.forEach(n => messageCount += Number.parseInt(n.textContent));
    if (previousMessageCount === messageCount) return;
    previousMessageCount = messageCount;
    window.chrome.webview.postMessage(messageCount.toString());
}, 1000);
