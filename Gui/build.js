const child_process = require('child_process');
child_process.execSync("npm install yauzl@latest rimraf@latest --silent", {stdio: [0, 1, 2]});

const path = require("path");
const fs = require("fs");
const https = require("https");
const url = require("url");
const yauzl = require("yauzl");
const rimraf = require("rimraf");

(async () => {
    const latestElectronJsReleaseMetadata = await SendGETRequestOverHttps("https://api.github.com/repos/electron/electron/releases/latest");
    const latestElectronJsBinaryDownloadUrl = ExtractLatestElectronJsBinaryDownloadUrl(latestElectronJsReleaseMetadata);
    const pathToLatestElectronJsBinaryZipFile = await DownloadFileFromTheInternet(latestElectronJsBinaryDownloadUrl, "electron.zip");
    Unzip(pathToLatestElectronJsBinaryZipFile, "electron");
    fs.unlinkSync("electron.zip");
})();

function SendGETRequestOverHttps(destinationUrl) {
    return new Promise((resolve, reject) => {
        https.get({
            host: url.parse(destinationUrl).hostname,
            path: destinationUrl,
            headers: {"user-agent": "node.js"}
        }, httpResponse => {
            let responseBody = "";
            httpResponse.on("data", chunk => responseBody += chunk);
            httpResponse.on("end", () => resolve(JSON.parse(responseBody)));
        }).on("error", error => reject(error.message));
    });
}

function ExtractLatestElectronJsBinaryDownloadUrl(latestElectronJsReleaseMetadata) {
    const electronJsWindowsBinarySelector = /electron-.+-win32-x64.zip/g;
    const electronJsWindowsBinaryMetadata = latestElectronJsReleaseMetadata.assets
        .find(asset => electronJsWindowsBinarySelector.test(asset.browser_download_url));
    return electronJsWindowsBinaryMetadata.browser_download_url;
}

function DownloadFileFromTheInternet(downloadUrl, pathToDestinationFileOnDisk) {
    return new Promise((resolve, reject) => {
        const destinationFileOnDisk = fs.createWriteStream(pathToDestinationFileOnDisk);
        const request = https.get(downloadUrl, httpResponse => {
            switch (httpResponse.statusCode) {
                case 200:
                    httpResponse.pipe(destinationFileOnDisk);
                    destinationFileOnDisk.on("finish", () => destinationFileOnDisk.close(() => resolve(pathToDestinationFileOnDisk)));
                    destinationFileOnDisk.on("error", error => fs.unlink(destinationFileOnDisk, () => reject(error.message)));
                    break;
                case 302:
                    const redirectedDownloadUrl = httpResponse.headers.location;
                    DownloadFileFromTheInternet(redirectedDownloadUrl, pathToDestinationFileOnDisk).then(resolve).catch(reject);
                    break;
                default:
                    reject(`Status code was: ${httpResponse.statusCode}`);
            }
        }).on("error", error => {
            fs.unlink(destinationFileOnDisk, () => reject(error.message));
        }).setTimeout(60000, () => {
            request.abort();
            reject("ElectronJs pre-built binary download took too long!");
        });
    });
}

function Unzip(pathToLatestElectronJsBinaryZipFile, pathToDestinationFolder) {
    EnsureRecreated(pathToDestinationFolder);
    yauzl.open(pathToLatestElectronJsBinaryZipFile, {lazyEntries: true}, (error, latestElectronJsBinaryZipFile) => {
        if (error) throw error;
        latestElectronJsBinaryZipFile.readEntry();
        latestElectronJsBinaryZipFile.on("entry", entry => {
            latestElectronJsBinaryZipFile.openReadStream(entry, (error, readStream) => {
                if (error) throw error;
                const pathToUnzippedDestinationFile = `${pathToDestinationFolder.replace(/\/+$/, "/")}/${entry.fileName}`;
                EnsureParentDirectoryExistence(pathToUnzippedDestinationFile);
                readStream.pipe(fs.createWriteStream(pathToUnzippedDestinationFile));
                readStream.on("end", () => latestElectronJsBinaryZipFile.readEntry());
            });
        });
    });
}

function EnsureRecreated(generalPath) {
    if (fs.existsSync(generalPath)) rimraf.sync(generalPath);
    fs.mkdirSync(generalPath);
}

function EnsureParentDirectoryExistence(generalPath) {
    const parentDirectory = path.dirname(generalPath);
    if (!fs.existsSync(parentDirectory)) fs.mkdirSync(parentDirectory);
}
