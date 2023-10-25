window.onload = () => {
    // var previousMessageCount = 0;
    setInterval(() => {
        // let messageCountNode = document.querySelector("div[role=navigation] div[aria-label*=Mail] > span > span");
        // const messageCount = messageCountNode ? Number.parseInt(messageCountNode.textContent) : 0;
        // if (previousMessageCount === messageCount) return;
        // previousMessageCount = messageCount;
        // window.chrome.webview.postMessage(messageCount.toString());

        Array.from(document.querySelectorAll("div")).findLast(el => el.textContent.includes("banner will go away"))?.querySelector("a")?.click();
        //    while (bannerParent && bannerParent.parentNode && getComputedStyle(bannerParent.parentNode) === "absolute") {
        //        bannerParent = bannerParent.parentNode;
        //    }
        //    bannerParent?.classList.add("d-none");

        // kill all iframes!! except the main video player
        Array.from(document.querySelectorAll("iframe")).forEach(iframe => iframe.title !== "embed player" && iframe.remove());

        //didn't work as an auto click on the video... need to keep fiddling to see if possible at all, maybe not
        // document.querySelector(".jwplayer")?.click();

        //remove the entire left and right side bar areas where the ads are
        document.querySelector(".col-12.col-xl-3")?.remove();
        document.querySelector(".col-12.col-lg-3")?.remove();
        
    }, 500);
}