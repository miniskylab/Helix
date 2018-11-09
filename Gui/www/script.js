const {ipcRenderer} = require("electron");

setInterval(() => { ipcRenderer.send("keep-alive"); }, 1000);

const btnMain = document.getElementById("btn-main");
btnMain.addEventListener("click", () => {
    if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
    ipcRenderer.send("btnStartClicked", JSON.stringify({
        StartUrl: document.getElementById("inp-start-url").value,
        DomainName: document.getElementById("ipn-domain-name").value,
        WebBrowserCount: document.getElementById("ipn-web-browser-count").value,
        RequestTimeoutDuration: document.getElementById("ipn-request-timeout-duration").value,
        ReportBrokenLinksOnly: document.getElementById("ipn-report-broken-links-only").checked,
        ShowWebBrowsers: document.getElementById("ipn-show-web-browsers").checked,
        UserAgent: "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36"
    }));
});

document.getElementById("btn-close").addEventListener("click", () => { ipcRenderer.send("btnCloseClicked", ""); });

ipcRenderer.on("redraw", (_, viewModelJson) => {
    const viewModel = JSON.parse(viewModelJson);
    if (viewModel.VerifiedUrlCount) document.getElementById("lbl-verified-urls").textContent = viewModel.VerifiedUrlCount;
    if (viewModel.ValidUrlCount) document.getElementById("lbl-valid-urls").textContent = viewModel.ValidUrlCount;
    if (viewModel.BrokenUrlCount) document.getElementById("lbl-broken-urls").textContent = viewModel.BrokenUrlCount;
    if (viewModel.RemainingUrlCount) document.getElementById("lbl-remaining-urls").textContent = viewModel.RemainingUrlCount;
    if (viewModel.IdleWebBrowserCount) document.getElementById("lbl-idle-web-browsers").textContent = viewModel.IdleWebBrowserCount;
    if (viewModel.ElapsedTime) document.getElementById("lbl-elapsed-time").textContent = viewModel.ElapsedTime;
    if (viewModel.StatusText) document.getElementById("lbl-status-text").textContent = viewModel.StatusText;

    const btnStop = document.getElementById("btn-stop");
    switch (viewModel.CrawlerState) {
        case "Ready":
            if (!btnStop.hasAttribute("disabled")) btnStop.setAttribute("disabled", "");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.remove("controls__main-button--amber");
            if (btnMain.firstChild.className !== "controls__play-icon") btnMain.firstChild.className = "controls__play-icon";
            break;
        case "Working":
            if (btnStop.hasAttribute("disabled")) btnStop.removeAttribute("disabled");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (!btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.add("controls__main-button--amber");
            if (btnMain.firstChild.className !== "controls__pause-icon") btnMain.firstElementChild.className = "controls__pause-icon";
    }
});
