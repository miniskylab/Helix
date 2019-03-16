const fs = require("fs");
const child_process = require("child_process");
RestoreNpmPackages();

const path = require("path");
const https = require("https");
const url = require("url");
const yauzl = require("yauzl");
const rimraf = require("rimraf");
const asar = require("asar");

const latestStableChromiumVersion = "73.0.3683.75-r625896";
const latestChromeDriverVersion = "73.0.3683.68";

(async () => {
    const commandLineArguments = process.argv.slice(2);
    const pathToHelixBinaryReleaseDirectory = `${commandLineArguments[commandLineArguments.indexOf("-o") + 1]}`;

    await DeployElectronJs(`${pathToHelixBinaryReleaseDirectory}ui`);
    await DeployChromiumWebBrowser(`${pathToHelixBinaryReleaseDirectory}`, latestStableChromiumVersion);
    await DeployChromeWebDriver(pathToHelixBinaryReleaseDirectory, latestChromeDriverVersion);

    console.log("Build process completed!");
})();

function RestoreNpmPackages() {
    if (!fs.existsSync("node_modules")) {
        console.log("\nRestoring NPM packages ...");
        child_process.execSync("npm install yauzl@latest rimraf@latest asar@latest --silent", {stdio: [0, 1, 2]});
    }
}

async function DeployChromiumWebBrowser(pathToUnzippedChromiumWebBrowserDirectory, version) {
    if (fs.existsSync(`${pathToUnzippedChromiumWebBrowserDirectory}chromium`)) return;

    console.log(`Downloading Chromium Web Browser v${version} from the Internet ...`);
    const pathToTemporaryDownloadedZipFile = "temp_chromium.zip";
    const chromiumWebBrowserDownloadUrl = `https://github.com/henrypp/chromium/releases/download/v${version}-win64/chromium-sync.zip`;
    await DownloadFileFromTheInternet(chromiumWebBrowserDownloadUrl, pathToTemporaryDownloadedZipFile);

    console.log(`Unzipping downloaded Chromium Web Browser v${version} ...`);
    await Unzip(pathToTemporaryDownloadedZipFile, pathToUnzippedChromiumWebBrowserDirectory);
    TryRename(`${pathToUnzippedChromiumWebBrowserDirectory}chrome-win32`, `${pathToUnzippedChromiumWebBrowserDirectory}chromium`);
    fs.unlinkSync(pathToTemporaryDownloadedZipFile);
}

async function DeployChromeWebDriver(pathToUnzippedChromeWebDriverDirectory, version) {
    if (fs.existsSync(`${pathToUnzippedChromeWebDriverDirectory}/chromedriver.exe`)) return;

    console.log(`Downloading Chrome Web Driver v${version} from the Internet ...`);
    const pathToTemporaryDownloadedZipFile = "temp_chromedriver.zip";
    const chromeWebDriverDownloadUrl = `https://chromedriver.storage.googleapis.com/${version}/chromedriver_win32.zip`;
    await DownloadFileFromTheInternet(chromeWebDriverDownloadUrl, pathToTemporaryDownloadedZipFile);

    console.log(`Unzipping downloaded Chrome Web Driver v${version} ...`);
    await Unzip(pathToTemporaryDownloadedZipFile, pathToUnzippedChromeWebDriverDirectory);
    fs.unlinkSync(pathToTemporaryDownloadedZipFile);
}

async function DeployElectronJs(pathToUnzippedElectronJsBinaryDirectory) {
    if (!fs.existsSync(pathToUnzippedElectronJsBinaryDirectory)) {
        console.log("Fetching latest ElectronJs release metadata ...");
        const latestElectronJsReleaseJson = await SendGETRequestOverHttps("https://api.github.com/repos/electron/electron/releases/latest");
        const latestElectronJsBinaryDownloadUrl = ExtractLatestElectronJsBinaryDownloadUrl(latestElectronJsReleaseJson);

        console.log("Downloading latest ElectronJs binary from the Internet ...");
        const pathToTemporaryDownloadedZipFile = "temp_electronjs.zip";
        await DownloadFileFromTheInternet(latestElectronJsBinaryDownloadUrl, pathToTemporaryDownloadedZipFile);

        console.log("Unzipping downloaded ElectronJs binary ...");
        EnsureDirectoryRecreated(pathToUnzippedElectronJsBinaryDirectory);
        await Unzip(pathToTemporaryDownloadedZipFile, pathToUnzippedElectronJsBinaryDirectory);
        fs.unlinkSync(pathToTemporaryDownloadedZipFile);
    }

    console.log("Deploying GUI code ...");
    const pathToGuiSourceCode = "../Gui/app";
    const pathToDeploymentArchiveFile = `${pathToUnzippedElectronJsBinaryDirectory}/resources/app.asar`;
    if (fs.existsSync(pathToDeploymentArchiveFile)) fs.unlinkSync(pathToDeploymentArchiveFile);
    await asar.createPackage(pathToGuiSourceCode, pathToDeploymentArchiveFile);
}

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
    const electronJsWindowsBinarySelector = /electron-.+-win32-x64\.zip/g;
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
            reject(`Download from Url: "${downloadUrl}" timed out.`);
        });
    });
}

function Unzip(pathToZipFile, pathToDestinationDirectory) {
    const isDirectory = entry => /\/$/.test(entry.fileName);
    return new Promise((resolve, reject) => {
        yauzl.open(pathToZipFile, {lazyEntries: true}, (error, zipFile) => {
            if (error) reject(error.message);
            zipFile.readEntry();
            zipFile.on("entry", entry => {
                if (isDirectory(entry)) zipFile.readEntry();
                else zipFile.openReadStream(entry, (error, readStream) => {
                    if (error) throw error;
                    const pathToUnzippedDestinationFile = `${pathToDestinationDirectory.replace(/\/+$/, "/")}/${entry.fileName}`;
                    EnsureParentDirectoryExistence(pathToUnzippedDestinationFile);
                    readStream.pipe(fs.createWriteStream(pathToUnzippedDestinationFile));
                    readStream.on("end", () => zipFile.readEntry());
                });
            });
            zipFile.on("close", resolve);
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

async function TryRename(oldPath, newPath, attemptCount = 10) {
    while (attemptCount > 0) {
        try {
            fs.renameSync(oldPath, newPath);
            break;
        } catch (_) {
            await new Promise(resolve => { setTimeout(resolve, 500); });
            attemptCount--;
        }
    }
    if (attemptCount <= 0) process.exit(-1);
}
