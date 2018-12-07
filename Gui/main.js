const { app, BrowserWindow } = require("electron");

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
    mainWindow.setMenuBarVisibility(false);
    mainWindow.loadURL(`file://${__dirname}/www/index.html`);
    mainWindow.webContents.on("did-finish-load", () => { mainWindow.show(); });
});