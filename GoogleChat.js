window.onload = () => {
    var previousMessageCount = '';
    setInterval(() => {
        let messageCount = document.querySelector('.XU').textContent;
        if (previousMessageCount === messageCount) return;
        previousMessageCount = messageCount;
        //console.log('new message count: ' + messageCount);
        window.chrome.webview.postMessage(messageCount);
    }, 750);
}