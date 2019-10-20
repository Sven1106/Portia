// THIS IS WHERE THE EVENT HANDLERS ARE MAINTAINED

// Called when extension is Installed
chrome.runtime.onInstalled.addListener(function (d) {
  chrome.storage.sync.set({ color: '#3aa757' }, function () {
    console.log("The color is green.");
  });

});
chrome.tabs.onCreated.addListener(function (d) {
  // do something
  console.log("tabs.onCreated")
});
chrome.browserAction.onClicked.addListener(function (d) {
  console.log(d);
  console.log("browserAction.onClicked")
});