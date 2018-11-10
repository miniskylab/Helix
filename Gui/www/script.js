const {ipcRenderer} = require("electron");
const port = ipcRenderer.sendSync("get-web-port", "");

const btnMain = document.getElementById("btn-main");
btnMain.addEventListener("click", () => {
    if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
    const http = new XMLHttpRequest();
    http.open("POST", `http://localhost:${port}/btn-start-clicked`);
    http.setRequestHeader("Content-Type", "application/json");
    http.send(JSON.stringify(JSON.stringify({
        StartUrl: document.getElementById("inp-start-url").value,
        DomainName: document.getElementById("ipn-domain-name").value,
        WebBrowserCount: document.getElementById("ipn-web-browser-count").value,
        RequestTimeoutDuration: document.getElementById("ipn-request-timeout-duration").value,
        ReportBrokenLinksOnly: document.getElementById("ipn-report-broken-links-only").checked,
        ShowWebBrowsers: document.getElementById("ipn-show-web-browsers").checked,
        UserAgent: "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36"
    })));
});

document.getElementById("btn-close").addEventListener("click", () => {
    const http = new XMLHttpRequest();
    http.open("POST", `http://localhost:${port}/btn-close-clicked`);
    http.send();
});

ipcRenderer.on("redraw", (_, viewModelJson) => {
    const lblVerifiedUrls = document.getElementById("lbl-verified-urls");
    const lblValidUrls = document.getElementById("lbl-valid-urls");
    const lblBrokenUrls = document.getElementById("lbl-broken-urls");
    const lblRemainingUrls = document.getElementById("lbl-remaining-urls");
    const lblIdleWebBrowsers = document.getElementById("lbl-idle-web-browsers");
    const lblElapsedTime = document.getElementById("lbl-elapsed-time");
    const lblStatusText = document.getElementById("lbl-status-text");

    const viewModel = JSON.parse(viewModelJson);
    if (isNumeric(viewModel.VerifiedUrlCount)) lblVerifiedUrls.textContent = viewModel.VerifiedUrlCount;
    if (isNumeric(viewModel.ValidUrlCount)) lblValidUrls.textContent = viewModel.ValidUrlCount;
    if (isNumeric(viewModel.BrokenUrlCount)) lblBrokenUrls.textContent = viewModel.BrokenUrlCount;
    if (isNumeric(viewModel.RemainingUrlCount)) lblRemainingUrls.textContent = viewModel.RemainingUrlCount;
    if (isNumeric(viewModel.IdleWebBrowserCount)) lblIdleWebBrowsers.textContent = viewModel.IdleWebBrowserCount;
    if (viewModel.ElapsedTime) lblElapsedTime.textContent = viewModel.ElapsedTime;
    if (viewModel.StatusText) lblStatusText.textContent = viewModel.StatusText;

    const btnStop = document.getElementById("btn-stop");
    const configurationPanel = document.getElementById("configuration-panel");
    const btnMainIsStartButton = btnMain.firstElementChild.className === "controls__play-icon";
    const btnMainIsPauseButton = btnMain.firstElementChild.className === "controls__pause-icon";
    switch (viewModel.CrawlerState) {
        case "Ready":
            if (btnMainIsStartButton) break;
            btnMain.firstElementChild.className = "controls__play-icon";
            if (btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.remove("controls__main-button--amber");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (configurationPanel.hasAttribute("disabled")) configurationPanel.removeAttribute("disabled");
            // if (!btnStop.hasAttribute("disabled")) btnStop.setAttribute("disabled", "");
            break;
        case "Working":
            if (btnMainIsPauseButton) break;
            btnMain.firstElementChild.className = "controls__pause-icon";
            if (!btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.add("controls__main-button--amber");
            if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
            if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
            // if (btnStop.hasAttribute("disabled")) btnStop.removeAttribute("disabled");
    }
});

function isNumeric(number) { return !isNaN(number) && typeof(number) === "number"; }
