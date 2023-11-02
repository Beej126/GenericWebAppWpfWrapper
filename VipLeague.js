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

//override the eval function to remove the debugger statements that are in the code
//these forced breaks are intentional blocks put in to prevent inspecting the running elements and code
const origeval = window.eval;
window.eval = txt => origeval(txt?.replace(/debugger/gi, ""));

window.onload = () => {
    let mediaClicked;

    setInterval(() => {

        let deleteMe = Array.from(document.querySelectorAll("div")).findLast(el => el.textContent.includes("banner will go away"));
        while (deleteMe?.parentNode && getComputedStyle(deleteMe.parentNode).position === "absolute") deleteMe = deleteMe.parentNode;
        deleteMe?.remove();

        // kill all iframes!! except the main video player
        Array.from(document.querySelectorAll("iframe")).forEach(iframe => iframe.title !== "embed player" && iframe.remove());

        //remove the entire left and right side bar areas where the ads are
        document.querySelectorAll(".col-12.col-xl-3, .col-12.col-lg-3").forEach(n => n.remove());

        //remove buttons that link out to pointless garbage
        Array.from(document.querySelectorAll("button")).findLast(el => ["Watch Live Stream", "Stream Here Now", "HD Live Stream"].some(str => el.textContent.includes(str)))?.remove();

        //the media player responds to this programmatic clicks in the debugger console but for some reason the timing is off when it executes here
        document.querySelector(".jw-state-idle .jw-media")?.click();
        if (!mediaClicked) {
            mediaClicked = document.querySelector(".jw-state-paused .jw-media");
            mediaClicked?.click();
        }

    }, 500);
}