const {remote, ipcRenderer} = require("electron");
const socket = new require("net").Socket();

const endOfTransmissionCharacter = '\4';

const htmlElement = document.getElementById("html");
const titleBar = document.getElementById("title-bar");

const txtStartUri = document.getElementById("txt-start-uri");
const txtRemoteHost = document.getElementById("txt-remote-host");
const ckbVerifyExternalUrls = document.getElementById("ckb-verify-external-urls");
const ckbUseWebBrowsers = document.getElementById("ckb-use-web-browsers");

const configurationPanel = document.getElementById("configuration-panel");
const lblVerified = document.getElementById("lbl-verified");
const lblValid = document.getElementById("lbl-valid");
const lblBroken = document.getElementById("lbl-broken");
const lblRemaining = document.getElementById("lbl-remaining");
const lblAveragePageLoadTime = document.getElementById("lbl-average-page-load-time");
const lblAveragePageLoadTimeUnitOfMeasure = document.getElementById("lbl-average-page-load-time-unit-of-measure");
const lblElapsedTime = document.getElementById("lbl-elapsed-time");
const lblStatusText = document.getElementById("lbl-status-text");
const lblWaitingOverlayMessage = document.getElementById("waiting-overlay-message");

const waitingOverlay = document.getElementById("waiting-overlay");
const waitingOverlaySubtitle = document.getElementById("waiting-overlay-subtitle");
const dialogOverlay = document.getElementById("dialog-overlay");
const aboutMeOverlay = document.getElementById("about-me-overlay");

const btnShowAboutMeOverlay = document.getElementById("btn-show-about-me-overlay");
const btnMinimize = document.getElementById("btn-minimize");
const btnMain = document.getElementById("btn-main");
const btnStop = document.getElementById("btn-stop");
const btnClose = document.getElementById("btn-close");
const btnPreview = document.getElementById("btn-preview");
const btnCloseAboutMeOverlay = document.getElementById("btn-close-about-me-overlay");

let waitingCountdownTimer = null;

socket.connect(18880, "127.0.0.1", () => {

    btnMain.addEventListener("click", () => {
        if (mainButtonIsStartButton()) {
            lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "hidden";
            redraw({
                DisableMainButton: true,
                DisableStopButton: true,
                DisableCloseButton: true,
                DisableConfigurationPanel: true,
                VerifiedUrlCount: "-",
                ValidUrlCount: "-",
                BrokenUrlCount: "-",
                RemainingWorkload: "-",
                MillisecondsAveragePageLoadTime: "-",
                ElapsedTime: "-- : -- : --",
                StatusText: "Initializing start sequence ..."
            });
            socket.write(
                attachEndOfTransmissionCharacter(
                    JSON.stringify({
                        text: "Start",
                        payload: JSON.stringify({
                            StartUri: txtStartUri.value,
                            HostName: txtRemoteHost.value,
                            VerifyExternalUrls: ckbVerifyExternalUrls.checked,
                            UseWebBrowsers: ckbUseWebBrowsers.checked
                        })
                    })
                )
            );
        }
    });

    btnClose.addEventListener("click", () => {
        redraw({ShowWaitingOverlay: true});
        socket.end(JSON.stringify({text: "Close"}));
        socket.on("end", () => { ipcRenderer.send("btn-close-clicked"); });
    });

    btnStop.addEventListener("click", () => {
        redraw({ShowWaitingOverlay: true});
        socket.write(attachEndOfTransmissionCharacter(JSON.stringify({text: "Stop"})));
    });

    btnMinimize.addEventListener("click", () => { remote.BrowserWindow.getFocusedWindow().minimize(); });

    btnShowAboutMeOverlay.addEventListener("click", () => { aboutMeOverlay.style.display = "block"; });

    btnCloseAboutMeOverlay.addEventListener("click", () => { aboutMeOverlay.style.display = "none"; });

    btnPreview.addEventListener("click", () => {
        socket.write(attachEndOfTransmissionCharacter(JSON.stringify({text: "Preview"})));
    });

    socket.on("data", byteStream => {
        const frame = reconstructFrame(byteStream);
        redraw(frame);
    });

    function reconstructFrame(byteStream) {
        return new TextDecoder("utf-8").decode(byteStream)
            .split(endOfTransmissionCharacter)
            .filter(jsonMessage => jsonMessage)
            .map(jsonMessage => {
                let message = JSON.parse(jsonMessage);
                return JSON.parse(message.Payload);
            })
            .reduce((combinedFrame, frame) => Object.assign(combinedFrame, frame), {});
    }
});

function redraw(frame) {
    if (notNullAndUndefined(frame.VerifiedUrlCount)) lblVerified.textContent = frame.VerifiedUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.ValidUrlCount)) lblValid.textContent = frame.ValidUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.BrokenUrlCount)) lblBroken.textContent = frame.BrokenUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.RemainingWorkload)) lblRemaining.textContent = frame.RemainingWorkload.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.MillisecondsAveragePageLoadTime)) lblAveragePageLoadTime.textContent = frame.MillisecondsAveragePageLoadTime.toLocaleString("en-US", {maximumFractionDigits: 0});
    if (isNumeric(frame.MillisecondsAveragePageLoadTime)) lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "visible";
    if (notNullAndUndefined(frame.ElapsedTime)) lblElapsedTime.textContent = frame.ElapsedTime;

    if (frame.ShowWaitingOverlay === true) showWaitingOverlay(120, () => { dialogOverlay.style.display = "block"; });
    else if (frame.ShowWaitingOverlay === false) {
        hideWaitingOverlay();
        dialogOverlay.style.display = "none";
    }

    if (notNullAndUndefined(frame.StatusText)) waitingOverlay.style.display === "block"
        ? lblWaitingOverlayMessage.textContent = frame.StatusText
        : lblStatusText.textContent = frame.StatusText;

    if (frame.DisableStopButton === true && !btnStop.hasAttribute("disabled")) btnStop.setAttribute("disabled", "");
    else if (frame.DisableStopButton === false && btnStop.hasAttribute("disabled")) btnStop.removeAttribute("disabled");

    if (frame.DisableCloseButton === true && !btnClose.hasAttribute("disabled")) btnClose.setAttribute("disabled", "");
    else if (frame.DisableCloseButton === false && btnClose.hasAttribute("disabled")) btnClose.removeAttribute("disabled");

    if (frame.DisablePreviewButton === true && !btnPreview.hasAttribute("disabled")) btnPreview.setAttribute("disabled", "");
    else if (frame.DisablePreviewButton === false && btnPreview.hasAttribute("disabled")) btnPreview.removeAttribute("disabled");

    if (frame.DisableConfigurationPanel === true && !configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
    else if (frame.DisableConfigurationPanel === false && configurationPanel.hasAttribute("disabled")) configurationPanel.removeAttribute("disabled");

    if (frame.DisableMainButton === true && !btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
    else if (frame.DisableMainButton === false && btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");

    switch (frame.MainButtonFunctionality) {
        case "Start":
            if (mainButtonIsStartButton()) break;
            btnMain.firstElementChild.className = "controls__play-icon";
            if (btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.remove("controls__main-button--amber");
            break;
        case "Pause":
            if (mainButtonIsPauseButton()) break;
            btnMain.firstElementChild.className = "controls__pause-icon";
            if (!btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.add("controls__main-button--amber");
    }

    switch (frame.BorderColor) {
        case "Normal":
            htmlElement.className = "border";
            titleBar.className = "title-bar";
            break;
        case "Error":
            htmlElement.className = "border border--error";
            titleBar.className = "title-bar title-bar--error";
    }

    function notNullAndUndefined(variable) { return variable !== null && variable !== undefined; }

    function isNumeric(variable) { return !isNaN(variable) && typeof (variable) === "number"; }
}

function mainButtonIsStartButton() { return btnMain.firstElementChild.className === "controls__play-icon"; }

function mainButtonIsPauseButton() { return btnMain.firstElementChild.className === "controls__pause-icon"; }

function showWaitingOverlay(waitingTimeInSecond = 0, onTimeup = () => {}) {
    if (waitingCountdownTimer) return;
    const getWaitingOverlaySubTitle = (remainingTime) => `(Please allow up to <div style='display:inline-block;color:#FF6347;'>${remainingTime}</div> seconds)`;
    waitingOverlaySubtitle.innerHTML = getWaitingOverlaySubTitle(waitingTimeInSecond);
    lblWaitingOverlayMessage.textContent = "Initializing stop sequence ...";
    waitingOverlay.style.display = "block";

    waitingCountdownTimer = setInterval(() => {
        waitingTimeInSecond--;
        waitingOverlaySubtitle.innerHTML = getWaitingOverlaySubTitle(waitingTimeInSecond);
        if (waitingTimeInSecond === 0) {
            onTimeup();
            hideWaitingOverlay();
        }
    }, 1000);
}

function hideWaitingOverlay() {
    if (!waitingCountdownTimer) return;
    waitingOverlay.style.display = "none";
    clearInterval(waitingCountdownTimer);
    waitingCountdownTimer = null;
}

function attachEndOfTransmissionCharacter(message) { return `${message}${endOfTransmissionCharacter}`; }