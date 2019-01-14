var tabs = {};
chrome.tabs.query({}, results => { results.forEach(tab => { tabs[tab.id] = tab; }); });
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => { tabs[tab.id] = tab; });
chrome.tabs.onRemoved.addListener(tabId => { delete tabs[tabId]; });

chrome.webRequest.onBeforeSendHeaders.addListener(
    details => {
        if (details.tabId !== -1) {
            const initiatorUrl = tabs[details.tabId].url;
            details.requestHeaders.push({"name": "initiator", "value": initiatorUrl});
        }
        return {requestHeaders: details.requestHeaders};
    },
    {urls: ["<all_urls>"]},
    ["blocking", "requestHeaders"]
);