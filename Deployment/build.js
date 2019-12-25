const fs = require("fs");
const child_process = require("child_process");
RestoreNpmPackages();

const path = require("path");
const https = require("https");
const yauzl = require("yauzl");
const rimraf = require("rimraf");
const asar = require("asar");
const Inliner = require("inliner");
const babelMinifier = require("babel-minify");

const sqliteVersion = "3.11.2";
const electronVersion = "7.1.2";
const chromiumVersion = "75.0.3770.142-r652427";
const chromeDriverVersion = "75.0.3770.140";

(async () => {
    const commandLineArguments = process.argv.slice(2);
    const pathToHelixBinaryReleaseDirectory = `${commandLineArguments[commandLineArguments.indexOf("-o") + 1]}`.replace(/\\+/g, "/");

    await DeployElectronJs(`${pathToHelixBinaryReleaseDirectory}ui`, electronVersion);
    await DeploySqliteBrowser(`${pathToHelixBinaryReleaseDirectory}`, sqliteVersion);
    await DeployChromiumWebBrowser(`${pathToHelixBinaryReleaseDirectory}`, chromiumVersion);
    await DeployChromeWebDriver(pathToHelixBinaryReleaseDirectory, chromeDriverVersion);

    console.log("Build process completed!");
})();

function RestoreNpmPackages() {
    if (!fs.existsSync("node_modules")) {
        console.log("\nRestoring NPM packages ...");
        child_process.execSync("npm install --silent", {stdio: [0, 1, 2]});
    }
}

async function DeployElectronJs(pathToDestinationDirectory, version) {
    if (!fs.existsSync(pathToDestinationDirectory)) {
        console.log(`Downloading Electron v${version} from the Internet ...`);
        const pathToTemporaryDownloadedZipFile = "temp_electronjs.zip";
        const electronDownloadUrl = `https://github.com/electron/electron/releases/download/v${version}/electron-v${version}-win32-x64.zip`;
        await DownloadFileFromTheInternet(electronDownloadUrl, pathToTemporaryDownloadedZipFile);

        console.log("Unzipping downloaded ElectronJs binary ...");
        EnsureDirectoryRecreated(pathToDestinationDirectory);
        await Unzip(pathToTemporaryDownloadedZipFile, pathToDestinationDirectory);
        fs.unlinkSync(pathToTemporaryDownloadedZipFile);
    }

    console.log("Deploying GUI code ...");
    await CreateAndDeployAsarFile(`${pathToDestinationDirectory}/resources`);
}

async function DeploySqliteBrowser(pathToDestinationDirectory, version) {
    if (fs.existsSync(`${pathToDestinationDirectory}sqlite-browser`)) return;

    console.log(`Downloading Sqlite Browser v${version} from the Internet ...`);
    const pathToTemporaryDownloadedZipFile = "temp_sqlite_browser.zip";
    const sqliteBrowserDownloadUrl = `https://download.sqlitebrowser.org/DB.Browser.for.SQLite-${version}-win64.zip`;
    await DownloadFileFromTheInternet(sqliteBrowserDownloadUrl, pathToTemporaryDownloadedZipFile);

    console.log(`Unzipping downloaded Sqlite Browser v${version} ...`);
    await Unzip(pathToTemporaryDownloadedZipFile, pathToDestinationDirectory);
    await TryRename(`${pathToDestinationDirectory}DB Browser for SQLite`, `${pathToDestinationDirectory}sqlite-browser`);
    fs.unlinkSync(pathToTemporaryDownloadedZipFile);
}

async function DeployChromiumWebBrowser(pathToDestinationDirectory, version) {
    if (fs.existsSync(`${pathToDestinationDirectory}chromium`)) return;

    console.log(`Downloading Chromium Web Browser v${version} from the Internet ...`);
    const pathToTemporaryDownloadedZipFile = "temp_chromium.zip";
    const chromiumWebBrowserDownloadUrl = `https://github.com/henrypp/chromium/releases/download/v${version}-win64/chromium-sync.zip`;
    await DownloadFileFromTheInternet(chromiumWebBrowserDownloadUrl, pathToTemporaryDownloadedZipFile);

    console.log(`Unzipping downloaded Chromium Web Browser v${version} ...`);
    await Unzip(pathToTemporaryDownloadedZipFile, pathToDestinationDirectory);
    await TryRename(`${pathToDestinationDirectory}chrome-win32`, `${pathToDestinationDirectory}chromium`);
    fs.unlinkSync(pathToTemporaryDownloadedZipFile);
}

async function DeployChromeWebDriver(pathToDestinationDirectory, version) {
    if (fs.existsSync(`${pathToDestinationDirectory}/chromedriver.exe`)) return;

    console.log(`Downloading Chrome Web Driver v${version} from the Internet ...`);
    const pathToTemporaryDownloadedZipFile = "temp_chromedriver.zip";
    const chromeWebDriverDownloadUrl = `https://chromedriver.storage.googleapis.com/${version}/chromedriver_win32.zip`;
    await DownloadFileFromTheInternet(chromeWebDriverDownloadUrl, pathToTemporaryDownloadedZipFile);

    console.log(`Unzipping downloaded Chrome Web Driver v${version} ...`);
    await Unzip(pathToTemporaryDownloadedZipFile, pathToDestinationDirectory);
    fs.unlinkSync(pathToTemporaryDownloadedZipFile);
}

async function CreateAndDeployAsarFile(pathToDestinationDirectory) {
    const pathToTemporaryAppDirectory = `${pathToDestinationDirectory}/app`;
    EnsureDirectoryRecreated(pathToTemporaryAppDirectory);

    const pathToGuiSourceCodeDirectory = "../Gui/app";
    fs.copyFileSync(`${pathToGuiSourceCodeDirectory}/index.js`, `${pathToTemporaryAppDirectory}/index.js`);
    fs.copyFileSync(`${pathToGuiSourceCodeDirectory}/package.json`, `${pathToTemporaryAppDirectory}/package.json`);

    const pathToJavaScriptFile = `${pathToGuiSourceCodeDirectory}/scripts/script.js`;
    const originalJavaScriptCode = fs.readFileSync(pathToJavaScriptFile, "utf8");
    try {
        const minifiedJavaScriptCode = MinifyJavaScriptCode(originalJavaScriptCode);
        fs.writeFileSync(pathToJavaScriptFile, minifiedJavaScriptCode, "utf8");
        await InlineAssetsIntoHtml(`${pathToGuiSourceCodeDirectory}/index.html`, `${pathToTemporaryAppDirectory}/index.html`);
    } finally { fs.writeFileSync(pathToJavaScriptFile, originalJavaScriptCode, "utf8"); }

    const pathToAsarFile = `${pathToDestinationDirectory}/app.asar`;
    if (fs.existsSync(pathToAsarFile)) fs.unlinkSync(pathToAsarFile);
    await asar.createPackage(pathToTemporaryAppDirectory, pathToAsarFile);
    rimraf.sync(pathToTemporaryAppDirectory);
}

async function InlineAssetsIntoHtml(pathToOriginalHtmlFile, pathToDestinationHtmlFile) {
    return new Promise(resolve => {
        new Inliner(pathToOriginalHtmlFile, (error, minifiedAndInlinedHtmlContent) => {
            fs.writeFileSync(pathToDestinationHtmlFile, minifiedAndInlinedHtmlContent, "utf8");
            resolve();
        });
    })
}

function MinifyJavaScriptCode(javaScriptCode) {
    const {code: minifiedJavaScriptCode} = babelMinifier(javaScriptCode);
    return minifiedJavaScriptCode;
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
                    const pathToUnzippedDestinationFile = `${pathToDestinationDirectory}/${entry.fileName}`;
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
    fs.mkdirSync(pathToDirectory, {recursive: true});
}

function EnsureParentDirectoryExistence(generalPath) {
    const parentDirectory = path.dirname(generalPath);
    if (!fs.existsSync(parentDirectory)) fs.mkdirSync(parentDirectory, {recursive: true});
}

async function TryRename(oldPath, newPath, attemptCount = 10) {
    while (attemptCount > 0) {
        try {
            fs.renameSync(oldPath, newPath);
            return Promise.resolve();
        } catch (_) {
            await new Promise(resolve => { setTimeout(resolve, 500); });
            attemptCount--;
        }
    }

    if (attemptCount <= 0)
        return Promise.reject("Try rename failed!");
}
