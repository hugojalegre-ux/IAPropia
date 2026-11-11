/**
 * Markdown ligero y seguro para burbujas del asistente (sin dependencias externas).
 */
(function (global) {
  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function sanitizeUrl(raw) {
    const u = (raw || "").trim();
    if (/^https?:\/\//i.test(u)) return u;
    return "#";
  }

  function renderInline(text) {
    let s = escapeHtml(text);
    s = s.replace(/`([^`]+)`/g, "<code>$1</code>");
    s = s.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
    s = s.replace(/\*([^*]+)\*/g, "<em>$1</em>");
    s = s.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, url) => {
      const href = sanitizeUrl(url);
      const safe = escapeHtml(label);
      return `<a href="${href}" target="_blank" rel="noopener noreferrer">${safe}</a>`;
    });
    return s;
  }

  function renderMarkdown(markdown) {
    if (!markdown) return "";
    const lines = String(markdown).split(/\r?\n/);
    const out = [];
    let inCode = false;
    let codeBuf = [];
    let listBuf = null;

    function flushList() {
      if (!listBuf?.length) return;
      out.push("<ul>" + listBuf.map((li) => `<li>${renderInline(li)}</li>`).join("") + "</ul>");
      listBuf = null;
    }

    for (const raw of lines) {
      const line = raw;

      if (line.trim().startsWith("```")) {
        flushList();
        if (inCode) {
          out.push("<pre><code>" + escapeHtml(codeBuf.join("\n")) + "</code></pre>");
          codeBuf = [];
          inCode = false;
        } else {
          inCode = true;
        }
        continue;
      }

      if (inCode) {
        codeBuf.push(line);
        continue;
      }

      const hm = line.match(/^(#{1,3})\s+(.+)$/);
      if (hm) {
        flushList();
        const level = hm[1].length;
        out.push(`<h${level}>${renderInline(hm[2])}</h${level}>`);
        continue;
      }

      const lm = line.match(/^\s*[-*]\s+(.+)$/);
      if (lm) {
        if (!listBuf) listBuf = [];
        listBuf.push(lm[1]);
        continue;
      }

      flushList();

      if (line.trim() === "") {
        out.push("");
        continue;
      }

      out.push(`<p>${renderInline(line)}</p>`);
    }

    if (inCode && codeBuf.length) {
      out.push("<pre><code>" + escapeHtml(codeBuf.join("\n")) + "</code></pre>");
    }
    flushList();

    return out.filter(Boolean).join("\n");
  }

  global.SiltexMarkdown = { render: renderMarkdown, escapeHtml };
})(window);
