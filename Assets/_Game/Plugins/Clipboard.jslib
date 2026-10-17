mergeInto(LibraryManager.library, {
  CopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    // itch's iframe may lack clipboard-write permission; execCommand still works there.
    function fallback() {
      var focused = document.activeElement;
      var area = document.createElement("textarea");
      area.value = text;
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      try { document.execCommand("copy"); } catch (e) {}
      area.remove();
      if (focused) focused.focus();
    }
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).catch(fallback);
    else fallback();
  },
});
