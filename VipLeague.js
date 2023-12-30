//notes for the shortcut:
//  home.bun - appears to make the search work but only on the main home page, also the clock
//  schedule.bun - this makes the search work on other pages and the main sports nav icons across the top
//  stream.bun - this gives the "more streams" links, but it brings in the annoying "banner will go away in x seconds" overlay that gets cleared out via script below
//  embed2.min - the main video player starting point which loads the rest below
//  jwplayer,provider.hls,hls.light - needed for player to show up

// atob is base64 DE-coding, which is often used to obfuscate code - not currently needed, but good to remember this is one of the common tricks
// const origatob = window.atob;
// window.atob = txt => "{}";
// {
//     const result = origatob(txt);
//     if (result.includes("eval")) {
//         debugger;
//     }
//     return result;
// } 

//override the native browser eval function to remove "debugger" statements
//these are intended to inhibit inspecting the running elements and code
const origEval = window.eval;
window.eval = txt => origEval(txt?.replace(/debugger/gi, ""));

const textMatch = text => node => [text].flat().some(str => node.textContent.includes(str));

const removeNodes = (selector, nodePredicate, parentPredicate) => {
    let nodes = Array.from(document.querySelectorAll(selector));
    if (parentPredicate) {
        // with walking up parents, the assumption is the selector is something generic like "div" and the nodePredicate is looking for text
        // inconveniently for us here, node.textContent matches the upper most parent where that text could be nested several layers down
        // so we need to use findLast to start with the lowest level node that matches and work up from there
        let found = nodes.findLast(node => nodePredicate(node));
        while (found?.parentNode && parentPredicate(found.parentNode)) found = found.parentNode;
        found?.remove();
    }
    else {
        nodes.forEach(node => (!nodePredicate || nodePredicate(node)) && node.remove());
    }

}

window.onload = () => {
    let onlyOnce = true;
    let onlyTwice = true;

    setInterval(() => {

        removeNodes("div", textMatch("banner will go away"), parentNode => getComputedStyle(parentNode).position === "absolute");

        // kill all iframes!! except the main video player
        removeNodes("iframe", node => node.title !== "embed player");

        //remove the entire left and right side bar areas where the ads are
        removeNodes(".col-12.col-xl-3, .col-12.col-lg-3");

        //remove buttons that link out to pointless garbage
        removeNodes("button", textMatch(["Live Stream", "Stream Here Now"]));

        //haven't been able to get media player to automatically start 
        //after trying everything, pretty sure this is intentionally disabled within the jwplayer api itself
        //https://github.com/jwplayer/jw-showcase/issues/179

        //this first click gets rid of a goofy overlay or something like that in the way 
        // document.querySelector(".jw-state-idle .jw-media")?.click();

        // if (typeof jwplayer !== "undefined") {
        //     const jwid = document.querySelector(".jwplayer")?.id;
        //     // if (jwplayer && ["idle", "paused"].includes(jwplayer().getState())) {

        //     if (!onlyOnce && !onlyTwice && jwid && ["idle", "paused"].includes(jwplayer(jwid).getState())) debugger;

        //     if (!onlyOnce && onlyTwice && jwid && ["idle", "paused"].includes(jwplayer(jwid).getState())) {
        //         onlyTwice = false;
        //         jwplayer(jwid).play();
        //     }

        //     const config = jwplayer(jwid)?.getConfig()?.setupConfig;
        //     if (onlyOnce && config) {
        //         onlyOnce = false;
        //         debugger;
        //         jwplayer(jwid).remove();
        //         jwplayer(jwid).setup({ ...config, autostart: true });
        //         jwplayer(jwid).on('ready', function(e) {
        //             this.play();
        //         });
        //     }

        // }
    }, 100);
};