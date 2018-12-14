const child_process = require('child_process');
child_process.execSync("npm install yauzl@latest rimraf@latest asar@latest --silent", {stdio: [0, 1, 2]});

const path = require("path");
const fs = require("fs");
const https = require("https");
const url = require("url");
const yauzl = require("yauzl");
const rimraf = require("rimraf");
const asar = require("asar");

(async () => {
    const commandLineArguments = process.argv.slice(2);
    const pathToUnzippedElectronJsBinaryDirectory = `${commandLineArguments[commandLineArguments.indexOf("-o") + 1]}/ui`;
    if (!fs.existsSync(pathToUnzippedElectronJsBinaryDirectory)) {
        console.log("Fetching latest ElectronJs release metadata ...");
        const latestElectronJsReleaseJson = await SendGETRequestOverHttps("https://api.github.com/repos/electron/electron/releases/latest");
        const latestElectronJsBinaryDownloadUrl = ExtractLatestElectronJsBinaryDownloadUrl(latestElectronJsReleaseJson);

        console.log("Downloading latest ElectronJs binary from the Internet ...");
        const pathToTemporaryDownloadedZipFile = "temp.zip";
        await DownloadFileFromTheInternet(latestElectronJsBinaryDownloadUrl, pathToTemporaryDownloadedZipFile);

        console.log("Unzipping downloaded ElectronJs binary ...");
        await Unzip(pathToTemporaryDownloadedZipFile, pathToUnzippedElectronJsBinaryDirectory);
        fs.unlinkSync(pathToTemporaryDownloadedZipFile);
    }

    console.log("Deploying GUI code ...");
    const pathToGuiSourceCode = "../Gui/app";
    const pathToDeploymentArchiveFile = `${pathToUnzippedElectronJsBinaryDirectory}/resources/app.asar`;
    if (fs.existsSync(pathToDeploymentArchiveFile)) fs.unlinkSync(pathToDeploymentArchiveFile);
    await new Promise(resolve => asar.createPackage(pathToGuiSourceCode, pathToDeploymentArchiveFile, resolve));

    console.log("Done!");
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

function ExtractLatestElectronJsBinaryDownloadUrl(latestElectronJsReleaseJson) {
    const electronJsWindowsBinarySelector = /electron-.+-win32-x64.zip/g;
    const electronJsWindowsBinaryMetadata = latestElectronJsReleaseJson.assets
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
                    destinationFileOnDisk.on("finish", () => destinationFileOnDisk.close(resolve));
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

function Unzip(pathToLatestElectronJsBinaryZipFile, pathToDestinationDirectory) {
    return new Promise((resolve, reject) => {
        EnsureDirectoryRecreated(pathToDestinationDirectory);
        yauzl.open(pathToLatestElectronJsBinaryZipFile, {lazyEntries: true}, (error, latestElectronJsBinaryZipFile) => {
            if (error) reject(error.message);
            latestElectronJsBinaryZipFile.readEntry();
            latestElectronJsBinaryZipFile.on("entry", entry => {
                latestElectronJsBinaryZipFile.openReadStream(entry, (error, readStream) => {
                    if (error) throw error;
                    const pathToUnzippedDestinationFile = `${pathToDestinationDirectory.replace(/\/+$/, "/")}/${entry.fileName}`;
                    EnsureParentDirectoryExistence(pathToUnzippedDestinationFile);
                    readStream.pipe(fs.createWriteStream(pathToUnzippedDestinationFile));
                    readStream.on("end", () => latestElectronJsBinaryZipFile.readEntry());
                });
            });
            latestElectronJsBinaryZipFile.on("end", resolve);
        });
    });
}

function EnsureDirectoryRecreated(pathToDirectory) {
    if (fs.existsSync(pathToDirectory)) rimraf.sync(pathToDirectory);
    fs.mkdirSync(pathToDirectory);
}

function EnsureParentDirectoryExistence(generalPath) {
    const parentDirectory = path.dirname(generalPath);
    if (!fs.existsSync(parentDirectory)) fs.mkdirSync(parentDirectory);
}
