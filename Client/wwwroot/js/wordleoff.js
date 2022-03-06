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
    DotNet.invokeMethodAsync("WordleOff.Client", "OnBrowserResize").then(data => data);
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