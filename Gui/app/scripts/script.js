﻿const {remote, ipcRenderer} = require("electron");
const socket = new require("net").Socket();

socket.connect(18880, "127.0.0.1", () => {
    const btnMain = document.getElementById("btn-main");
    const txtStartUri = document.getElementById("txt-start-uri");
    const txtDomainName = document.getElementById("txt-domain-name");
    const txtHtmlRendererCount = document.getElementById("txt-html-renderer-count");
    const txtRequestTimeoutDuration = document.getElementById("txt-request-timeout-duration");
    const ckbReportBrokenLinksOnly = document.getElementById("ckb-report-broken-links-only");
    const ckbShowWebBrowsers = document.getElementById("ckb-show-web-browsers");
    btnMain.addEventListener("click", () => {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        socket.write(JSON.stringify({
            text: "btn-start-clicked",
            payload: JSON.stringify({
                StartUri: txtStartUri.value,
                DomainName: txtDomainName.value,
                HtmlRendererCount: txtHtmlRendererCount.value,
                RequestTimeoutDuration: txtRequestTimeoutDuration.value,
                ReportBrokenLinksOnly: ckbReportBrokenLinksOnly.checked,
                UseHeadlessWebBrowsers: !ckbShowWebBrowsers.checked,
                UserAgent: "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36"
            })
        }));
    });

    document.getElementById("btn-close").addEventListener("click", () => {
        socket.end(JSON.stringify({text: "btn-close-clicked"}));
        socket.on("end", () => { ipcRenderer.send("btn-close-clicked"); });
    });

    document.getElementById("btn-minimize").addEventListener("click", () => { remote.BrowserWindow.getFocusedWindow().minimize(); });

    const lblVerifiedUrls = document.getElementById("lbl-verified-urls");
    const lblValidUrls = document.getElementById("lbl-valid-urls");
    const lblBrokenUrls = document.getElementById("lbl-broken-urls");
    const lblRemainingUrls = document.getElementById("lbl-remaining-urls");
    const lblAveragePageLoadTime = document.getElementById("lbl-average-page-load-time");
    const lblAveragePageLoadTimeUnitOfMeasure = document.getElementById("lbl-average-page-load-time-unit-of-measure");
    const lblElapsedTime = document.getElementById("lbl-elapsed-time");
    const lblStatusText = document.getElementById("lbl-status-text");
    const btnStop = document.getElementById("btn-stop");
    const configurationPanel = document.getElementById("configuration-panel");
    socket.on("data", ipcMessageJson => {
        const ipcMessage = JSON.parse(ipcMessageJson);
        switch (ipcMessage.Text) {
            case "redraw":
                const viewModel = JSON.parse(ipcMessage.Payload);
                if (isNumeric(viewModel.VerifiedUrlCount)) lblVerifiedUrls.textContent = viewModel.VerifiedUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
                if (isNumeric(viewModel.ValidUrlCount)) lblValidUrls.textContent = viewModel.ValidUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
                if (isNumeric(viewModel.BrokenUrlCount)) lblBrokenUrls.textContent = viewModel.BrokenUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
                if (isNumeric(viewModel.RemainingUrlCount)) lblRemainingUrls.textContent = viewModel.RemainingUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
                if (isNumeric(viewModel.AveragePageLoadTime)) {
                    lblAveragePageLoadTime.textContent = viewModel.AveragePageLoadTime.toLocaleString("en-US", {maximumFractionDigits: 0});
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
                break;
        }
    });
});

function isNumeric(number) { return !isNaN(number) && typeof (number) === "number"; }
