const { app, BrowserWindow, ipcMain } = require("electron");

app.on("ready", () => {
    const mainWindow = new BrowserWindow({
        width: 500,
        height: 695,
        show: false,
        center: true,
        fullscreenable: false,
        maximizable: false,
        resizable: false,
        frame: false
    });
    mainWindow.loadURL(`file://${__dirname}/index.html`);
    mainWindow.webContents.on("did-finish-load", () => { mainWindow.show(); });
});
ipcMain.on("btn-close-clicked", () => { app.quit(); });
