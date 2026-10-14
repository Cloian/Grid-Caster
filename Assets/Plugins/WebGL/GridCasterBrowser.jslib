mergeInto(LibraryManager.library, {
  GridCaster_IsMobileBrowser: function () {
    // 화면 폭이나 터치 지원만으로 PC를 모바일로 바꾸지 않는다. iPad 데스크톱 UA도 포함한다.
    return /Android|iPhone|iPad|iPod/i.test(navigator.userAgent) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) ? 1 : 0;
  }
});
