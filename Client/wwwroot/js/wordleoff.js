window.browserResize = {
  getInnerHeight: function () {
    return window.innerHeight;
  },
  getInnerWidth: function () {
    return window.innerWidth;
  },
  registerResizeCallback: function () {
    window.addEventListener("resize", browserResize.resized);
  },
  resized: function () {
    DotNet.invokeMethodAsync("WordleOff.Client", "OnBrowserResizeAsync").then(data => data);
  }
};

window.setFocusToElement = (element) => {
  element.focus();
};

window.reanimateAll = () => {
  document.querySelectorAll('.colored').forEach(function (tile) {
    tile.style.animation = "none";
    setTimeout(function () {
      tile.style.animation = '';
    }, 1);
  });
};

window.setTitle = (title) => {
  document.title = title;
};

// From https://github.com/danroth27/BlazorExcelSpreadsheet
window.saveAsFile = (filename, bytesBase64) => {
  var link = document.createElement('a');
  link.download = filename;
  link.href = "data:application/octet-stream;base64," + bytesBase64;
  document.body.appendChild(link); // Needed for Firefox
  link.click();
  document.body.removeChild(link);
}