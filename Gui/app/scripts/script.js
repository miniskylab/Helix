const { remote, ipcRenderer } = require("electron");
const socket = new require("net").Socket();

const btnMain = document.getElementById("btn-main");
const txtStartUri = document.getElementById("txt-start-uri");
const txtDomainName = document.getElementById("txt-domain-name");
const txtHtmlRendererCount = document.getElementById("txt-html-renderer-count");
const ckbVerifyExternalUrls = document.getElementById("ckb-verify-external-urls");
const ckbShowWebBrowsers = document.getElementById("ckb-show-web-browsers");
const configurationPanel = document.getElementById("configuration-panel");
const shutdownOverlay = document.getElementById("shutdown-overlay");
const shutdownOverlaySubtitle = document.getElementById("shutdown-overlay-subtitle");
const shutdownFailureOverlay = document.getElementById("shutdown-failure-overlay");

const lblVerified = document.getElementById("lbl-verified");
const lblValid = document.getElementById("lbl-valid");
const lblBroken = document.getElementById("lbl-broken");
const lblRemaining = document.getElementById("lbl-remaining");
const lblAveragePageLoadTime = document.getElementById("lbl-average-page-load-time");
const lblAveragePageLoadTimeUnitOfMeasure = document.getElementById("lbl-average-page-load-time-unit-of-measure");
const lblElapsedTime = document.getElementById("lbl-elapsed-time");
const lblStatusText = document.getElementById("lbl-status-text");
const btnStop = document.getElementById("btn-stop");

socket.connect(18880, "127.0.0.1", () => {

    btnMain.addEventListener("click", () => {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
        socket.write(JSON.stringify({
            text: "btn-start-clicked",
            payload: JSON.stringify({
                StartUri: txtStartUri.value,
                DomainName: txtDomainName.value,
                HtmlRendererCount: txtHtmlRendererCount.value,
                VerifyExternalUrls: ckbVerifyExternalUrls.checked,
                UseHeadlessWebBrowsers: !ckbShowWebBrowsers.checked,
                UserAgent: "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36"
            })
        }));
    });

    document.getElementById("btn-close").addEventListener("click", () => {
        let waitingTime = 120;
        const getShutdownOverlaySubTitle = (remainingTime) => `(Please allow up to <div style='display:inline-block;color:#FF6347;'>${remainingTime}</div> seconds)`;
        shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
        shutdownOverlay.style.display = "block";

        const shutdownCountdown = setInterval(() => {
            waitingTime--;
            shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
            if (waitingTime === 0) {
                shutdownFailureOverlay.style.display = "block";
                shutdownOverlay.style.display = "none";
                clearInterval(shutdownCountdown);
            }
        }, 1000);

        socket.end(JSON.stringify({ text: "btn-close-clicked" }));
        socket.on("end", () => { ipcRenderer.send("btn-close-clicked"); });
    });

    document.getElementById("btn-minimize").addEventListener("click", () => { remote.BrowserWindow.getFocusedWindow().minimize(); });

    socket.on("data", ipcMessageJson => {
        const ipcMessage = JSON.parse(ipcMessageJson);
        switch (ipcMessage.Text) {
            case "redraw":
                redraw(JSON.parse(ipcMessage.Payload))
                break;
        }
    });

});

function isNumeric(number) { return !isNaN(number) && typeof (number) === "number"; }

function redraw(viewModel) {
    if (isNumeric(viewModel.VerifiedUrlCount)) lblVerified.textContent = viewModel.VerifiedUrlCount.toLocaleString("en-US", { maximumFractionDigits: 2 });
    if (isNumeric(viewModel.ValidUrlCount)) lblValid.textContent = viewModel.ValidUrlCount.toLocaleString("en-US", { maximumFractionDigits: 2 });
    if (isNumeric(viewModel.BrokenUrlCount)) lblBroken.textContent = viewModel.BrokenUrlCount.toLocaleString("en-US", { maximumFractionDigits: 2 });
    if (isNumeric(viewModel.RemainingWorkload)) lblRemaining.textContent = viewModel.RemainingWorkload.toLocaleString("en-US", { maximumFractionDigits: 2 });
    if (isNumeric(viewModel.AveragePageLoadTime)) {
        lblAveragePageLoadTime.textContent = viewModel.AveragePageLoadTime.toLocaleString("en-US", { maximumFractionDigits: 0 });
        lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "visible";
    }
    if (viewModel.ElapsedTime) lblElapsedTime.textContent = viewModel.ElapsedTime;
    if (viewModel.StatusText) lblStatusText.textContent = viewModel.StatusText;

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
}