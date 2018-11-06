const { ipcRenderer } = require("electron");

document.getElementById("btn-start").addEventListener("click", () => {
    ipcRenderer.send("btnStartClicked", JSON.stringify({
        StartUrl: document.getElementById("inp-start-url").value,
        DomainName: document.getElementById("ipn-domain-name").value,
        MaxThreadCount: document.getElementById("ipn-max-thread-count").value,
        RequestTimeoutDuration: document.getElementById("ipn-request-timeout-duration").value,
        ReportBrokenLinksOnly: document.getElementById("ipn-report-broken-links-only").checked,
        EnableDebugMode: document.getElementById("ipn-enable-debug-mode").checked,
        UserAgent: "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36"
    }));
});

document.getElementById("btn-close").addEventListener("click", () => { ipcRenderer.send("btnCloseClicked", ""); });

ipcRenderer.on("redraw", (event, viewModelJson) => {
    const viewModel = JSON.parse(viewModelJson);
    document.getElementById("elapsed-time").textContent = viewModel.ElapsedTime;
});
