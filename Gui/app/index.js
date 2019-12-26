const {app, BrowserWindow, ipcMain, shell} = require("electron");

app.on("ready", () => {
    const mainWindow = new BrowserWindow({
        width: 450,
        height: 555,
        show: false,
        center: true,
        fullscreenable: false,
        maximizable: false,
        resizable: false,
        frame: false,
        webPreferences: {nodeIntegration: true}
    });
    mainWindow.loadURL(`file://${__dirname}/index.html`);
    mainWindow.webContents.on("did-finish-load", () => { mainWindow.show(); });
    mainWindow.webContents.on("new-window", (event, url) => {
        event.preventDefault();
        shell.openExternal(url);
    });
    mainWindow.on("close", event => {
        event.preventDefault();
        mainWindow.webContents.executeJavaScript("document.getElementById('btn-close').click()");
    });
});
ipcMain.on("btn-close-clicked", () => { app.exit(0); });
