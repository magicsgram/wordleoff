window.reanimateAll = {
  reanimateAll: function () {
    document.querySelectorAll('.colored').forEach(function (tile) {
      tile.style.animation = "none";
      setTimeout(function () {
        tile.style.animation = '';
      }, 1);
    });
  }
}