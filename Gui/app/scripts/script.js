const {remote, ipcRenderer} = require("electron");
const socket = new require("net").Socket();

const endOfTransmissionCharacter = '\4';

const txtStartUri = document.getElementById("txt-start-uri");
const txtDomainName = document.getElementById("txt-domain-name");
const ckbVerifyExternalUrls = document.getElementById("ckb-verify-external-urls");
const ckbShowWebBrowsers = document.getElementById("ckb-show-web-browsers");

const configurationPanel = document.getElementById("configuration-panel");
const lblVerified = document.getElementById("lbl-verified");
const lblValid = document.getElementById("lbl-valid");
const lblBroken = document.getElementById("lbl-broken");
const lblRemaining = document.getElementById("lbl-remaining");
const lblAveragePageLoadTime = document.getElementById("lbl-average-page-load-time");
const lblAveragePageLoadTimeUnitOfMeasure = document.getElementById("lbl-average-page-load-time-unit-of-measure");
const lblElapsedTime = document.getElementById("lbl-elapsed-time");
const lblStatusText = document.getElementById("lbl-status-text");
const lblShutdownOverlayMessage = document.getElementById("shutdown-overlay-message");

const shutdownOverlay = document.getElementById("shutdown-overlay");
const shutdownOverlaySubtitle = document.getElementById("shutdown-overlay-subtitle");
const shutdownFailureOverlay = document.getElementById("shutdown-failure-overlay");
const aboutMeOverlay = document.getElementById("about-me-overlay");

const btnShowAboutMeOverlay = document.getElementById("btn-show-about-me-overlay");
const btnMinimize = document.getElementById("btn-minimize");
const btnMain = document.getElementById("btn-main");
const btnStop = document.getElementById("btn-stop");
const btnClose = document.getElementById("btn-close");
const btnCloseAboutMeOverlay = document.getElementById("btn-close-about-me-overlay");

let shutdownCountdown = null;

socket.connect(18880, "127.0.0.1", () => {

    btnMain.addEventListener("click", () => {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
        lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "hidden";
        redraw({
            VerifiedUrlCount: "-",
            ValidUrlCount: "-",
            BrokenUrlCount: "-",
            RemainingWorkload: "-",
            MillisecondsAveragePageLoadTime: "-",
            ElapsedTime: "-- : -- : --",
            StatusText: "Initializing start sequence ..."
        });
        socket.write(JSON.stringify({
            text: "btn-start-clicked",
            payload: JSON.stringify({
                StartUri: txtStartUri.value,
                DomainName: txtDomainName.value,
                VerifyExternalUrls: ckbVerifyExternalUrls.checked,
                UseHeadlessWebBrowsers: !ckbShowWebBrowsers.checked
            })
        }));
    });

    btnClose.addEventListener("click", () => {
        showShutdownOverlay();
        socket.end(JSON.stringify({text: "btn-close-clicked"}));
        socket.on("end", () => { ipcRenderer.send("btn-close-clicked"); });
    });

    btnStop.addEventListener("click", () => {
        showShutdownOverlay();
        socket.write(JSON.stringify({text: "btn-stop-clicked"}));
    });

    btnMinimize.addEventListener("click", () => { remote.BrowserWindow.getFocusedWindow().minimize(); });

    btnShowAboutMeOverlay.addEventListener("click", () => { aboutMeOverlay.style.display = "block"; });

    btnCloseAboutMeOverlay.addEventListener("click", () => { aboutMeOverlay.style.display = "none"; });

    socket.on("data", byteStream => {
        const jsonMessages = new TextDecoder("utf-8").decode(byteStream).split(endOfTransmissionCharacter);
        const lastJsonMessage = JSON.parse(jsonMessages[jsonMessages.length - 2]);
        switch (lastJsonMessage.Text) {
            case "redraw":
                redraw(JSON.parse(lastJsonMessage.Payload));
                break;
        }
    });

});

function notNullAndUndefined(variable) { return variable !== null && variable !== undefined; }

function isNumeric(variable) { return !isNaN(variable) && typeof (variable) === "number"; }

function redraw(frame) {
    if (notNullAndUndefined(frame.VerifiedUrlCount)) lblVerified.textContent = frame.VerifiedUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.ValidUrlCount)) lblValid.textContent = frame.ValidUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.BrokenUrlCount)) lblBroken.textContent = frame.BrokenUrlCount.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.RemainingWorkload)) lblRemaining.textContent = frame.RemainingWorkload.toLocaleString("en-US", {maximumFractionDigits: 2});
    if (notNullAndUndefined(frame.MillisecondsAveragePageLoadTime)) lblAveragePageLoadTime.textContent = frame.MillisecondsAveragePageLoadTime.toLocaleString("en-US", {maximumFractionDigits: 0});
    if (isNumeric(frame.MillisecondsAveragePageLoadTime)) lblAveragePageLoadTimeUnitOfMeasure.style.visibility = "visible";
    if (notNullAndUndefined(frame.ElapsedTime)) lblElapsedTime.textContent = frame.ElapsedTime;
    if (notNullAndUndefined(frame.StatusText)) shutdownOverlay.style.display === "block"
        ? lblShutdownOverlayMessage.textContent = frame.StatusText
        : lblStatusText.textContent = frame.StatusText;
    if (frame.RestrictHumanInteraction === true) {
        if (!btnMain.hasAttribute("disabled")) btnMain.setAttribute("disabled", "");
        if (!configurationPanel.hasAttribute("disabled")) configurationPanel.setAttribute("disabled", "");
    } else if (frame.RestrictHumanInteraction === false) {
        if (btnMain.hasAttribute("disabled")) btnMain.removeAttribute("disabled");
        if (configurationPanel.hasAttribute("disabled")) configurationPanel.removeAttribute("disabled");
    }

    const btnMainIsStartButton = btnMain.firstElementChild.className === "controls__play-icon";
    const btnMainIsPauseButton = btnMain.firstElementChild.className === "controls__pause-icon";
    switch (frame.CrawlerState) {
        case "WaitingToRun":
        case "RanToCompletion":
        case "Cancelled":
        case "Faulted":
            if (btnMainIsStartButton) break;
            btnMain.firstElementChild.className = "controls__play-icon";
            if (btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.remove("controls__main-button--amber");
            if (!btnStop.hasAttribute("disabled")) btnStop.setAttribute("disabled", "");
            break;
        case "Running":
            if (btnMainIsPauseButton) break;
            btnMain.firstElementChild.className = "controls__pause-icon";
            if (!btnMain.classList.contains("controls__main-button--amber")) btnMain.classList.add("controls__main-button--amber");
            if (btnStop.hasAttribute("disabled")) btnStop.removeAttribute("disabled");
    }
}

function showShutdownOverlay() {
    if (shutdownCountdown) return;
    let waitingTime = 120;
    const getShutdownOverlaySubTitle = (remainingTime) => `(Please allow up to <div style='display:inline-block;color:#FF6347;'>${remainingTime}</div> seconds)`;
    shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
    lblShutdownOverlayMessage.textContent = "Initializing shutdown sequence ...";
    shutdownOverlay.style.display = "block";

    shutdownCountdown = setInterval(() => {
        waitingTime--;
        shutdownOverlaySubtitle.innerHTML = getShutdownOverlaySubTitle(waitingTime);
        if (waitingTime === 0) {
            shutdownFailureOverlay.style.display = "block";
            hideShutdownOverlay();
        }
    }, 1000);
}

function hideShutdownOverlay() {
    if (!shutdownCountdown) return;
    shutdownOverlay.style.display = "none";
    clearInterval(shutdownCountdown);
    shutdownCountdown = null;
}