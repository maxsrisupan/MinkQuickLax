// MinkQuickLax manual: search, contents highlight, language switch and theme (SPEC 4.9).
// Search is plain substring matching so Thai (written without spaces) works; Intl.Segmenter only ranks and trims snippets.
(() => {
  "use strict";

  const index = MANUAL_INDEX;
  const text = MANUAL_TEXT;
  const root = document.documentElement;
  const params = new URLSearchParams(location.search);
  const input = document.getElementById("search");
  const results = document.getElementById("results");
  const toc = document.getElementById("toc");
  const menu = document.getElementById("menu");
  const language = document.getElementById("language");
  const maxResults = 12;

  // The app passes ?theme=light|dark to match its own theme, and ?embedded=1 when it draws its own print button.
  const theme = params.get("theme");
  if (theme === "light" || theme === "dark") {
    root.dataset.theme = theme;
  }
  if (params.get("embedded") === "1") {
    document.body.classList.add("embedded");
  }

  const normalize = (value) => value.normalize("NFKC").toLocaleLowerCase();
  const escapeHtml = (value) =>
    value.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

  const segmenter = typeof Intl !== "undefined" && Intl.Segmenter
    ? new Intl.Segmenter(root.lang || "th", { granularity: "word" })
    : null;

  const entries = index.map((entry) => ({
    ...entry,
    titleKey: normalize(entry.title),
    textKey: normalize(entry.text),
    keywordKeys: entry.keywords.map(normalize),
    bounds: null,
  }));

  // Word starts of a string, found lazily and kept (Thai has no spaces between words).
  function wordStarts(entry, field) {
    entry.bounds ??= {};
    if (!entry.bounds[field]) {
      const starts = new Set([0]);
      if (segmenter) {
        for (const part of segmenter.segment(entry[field])) {
          starts.add(part.index);
        }
      }
      entry.bounds[field] = starts;
    }
    return entry.bounds[field];
  }

  function scoreTerm(entry, term) {
    let score = 0;
    const inTitle = entry.titleKey.indexOf(term);
    if (inTitle >= 0) {
      score += 100 + (entry.titleKey === term ? 60 : 0) + (wordStarts(entry, "titleKey").has(inTitle) ? 30 : 0);
    }
    for (const keyword of entry.keywordKeys) {
      const at = keyword.indexOf(term);
      if (at >= 0) {
        score += keyword === term ? 80 : at === 0 ? 60 : 40;
        break;
      }
    }
    const inText = entry.textKey.indexOf(term);
    if (inText >= 0) {
      let count = 0;
      for (let at = inText; at >= 0 && count < 5; at = entry.textKey.indexOf(term, at + term.length)) {
        count++;
      }
      score += 15 + count * 3 + (wordStarts(entry, "textKey").has(inText) ? 10 : 0);
    }
    return score;
  }

  function search(query) {
    const terms = normalize(query).split(/\s+/).filter(Boolean);
    if (terms.length === 0) {
      return [];
    }
    const found = [];
    for (const entry of entries) {
      let total = 0;
      for (const term of terms) {
        const score = scoreTerm(entry, term);
        if (score === 0) {
          total = 0;
          break;
        }
        total += score;
      }
      if (total > 0) {
        found.push({ entry, score: total });
      }
    }
    found.sort((a, b) => b.score - a.score);
    return found.slice(0, maxResults).map((f) => f.entry);
  }

  // A short piece of text around the first match, cut at word starts, with the matches marked.
  function snippet(entry, terms) {
    const source = entry.text;
    let at = -1;
    let length = 0;
    for (const term of terms) {
      at = entry.textKey.indexOf(term);
      if (at >= 0) {
        length = term.length;
        break;
      }
    }
    if (at < 0) {
      return escapeHtml(source.slice(0, 110)) + (source.length > 110 ? "…" : "");
    }
    const starts = [...wordStarts(entry, "textKey")].sort((a, b) => a - b);
    const from = Math.max(0, ...starts.filter((s) => s <= Math.max(0, at - 40)));
    let to = Math.min(source.length, at + length + 80);
    const after = starts.find((s) => s >= to);
    to = after ?? source.length;
    return (from > 0 ? "…" : "") + mark(source.slice(from, to), terms) + (to < source.length ? "…" : "");
  }

  function mark(value, terms) {
    const key = normalize(value);
    const ranges = [];
    for (const term of terms) {
      for (let at = key.indexOf(term); at >= 0; at = key.indexOf(term, at + term.length)) {
        ranges.push([at, at + term.length]);
      }
    }
    // NFKC can change lengths for some characters; fall back to no marks rather than marking the wrong text.
    if (key.length !== value.length || ranges.length === 0) {
      return escapeHtml(value);
    }
    ranges.sort((a, b) => a[0] - b[0]);
    let html = "";
    let last = 0;
    for (const [start, end] of ranges) {
      if (start < last) {
        continue;
      }
      html += escapeHtml(value.slice(last, start)) + "<mark>" + escapeHtml(value.slice(start, end)) + "</mark>";
      last = end;
    }
    return html + escapeHtml(value.slice(last));
  }

  let shown = [];
  let selected = -1;

  function render() {
    const query = input.value;
    const terms = normalize(query).split(/\s+/).filter(Boolean);
    shown = search(query);
    selected = shown.length > 0 ? 0 : -1;
    if (terms.length === 0) {
      results.hidden = true;
      results.innerHTML = "";
      input.setAttribute("aria-expanded", "false");
      return;
    }
    results.innerHTML = shown.length === 0
      ? `<div class="no-results">${escapeHtml(text.noResults)}</div>`
      : shown.map((entry, i) =>
          `<a class="result" role="option" id="result-${i}" href="#${entry.id}" aria-selected="${i === selected}">` +
          `<span class="result-title">${mark(entry.title, terms)}</span>` +
          (entry.chapter && entry.chapter !== entry.title ? `<span class="result-chapter">${escapeHtml(entry.chapter)}</span>` : "") +
          `<span class="result-snippet">${snippet(entry, terms)}</span></a>`).join("");
    results.hidden = false;
    input.setAttribute("aria-expanded", "true");
    updateSelection();
  }

  function updateSelection() {
    results.querySelectorAll(".result").forEach((item, i) => {
      item.setAttribute("aria-selected", String(i === selected));
      if (i === selected) {
        item.scrollIntoView({ block: "nearest" });
        input.setAttribute("aria-activedescendant", item.id);
      }
    });
  }

  function closeResults() {
    results.hidden = true;
    input.setAttribute("aria-expanded", "false");
  }

  function go(entry) {
    const terms = normalize(input.value).split(/\s+/).filter(Boolean);
    closeResults();
    input.blur();
    document.body.classList.remove("toc-open");
    if (location.hash === "#" + entry.id) {
      document.getElementById(entry.id)?.scrollIntoView();
    } else {
      location.hash = entry.id;
    }
    highlight(entry.id, terms);
  }

  // Marks the search words inside the chosen section, removing the previous marks.
  function highlight(id, terms) {
    document.querySelectorAll("mark.hit").forEach((m) => m.replaceWith(document.createTextNode(m.textContent)));
    document.getElementById("content").normalize();
    const heading = document.getElementById(id);
    if (!heading || terms.length === 0) {
      return;
    }
    const level = Number(heading.tagName.slice(1)) || 6;
    const nodes = [];
    for (let node = heading; node; node = node.nextElementSibling) {
      if (node !== heading && /^H[1-6]$/.test(node.tagName) && Number(node.tagName.slice(1)) <= level) {
        break;
      }
      const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT);
      while (walker.nextNode()) {
        nodes.push(walker.currentNode);
      }
    }
    for (const node of nodes) {
      const value = node.nodeValue;
      const key = value.toLocaleLowerCase();
      const ranges = [];
      for (const term of terms) {
        for (let at = key.indexOf(term); at >= 0; at = key.indexOf(term, at + term.length)) {
          ranges.push([at, at + term.length]);
        }
      }
      if (ranges.length === 0 || key.length !== value.length) {
        continue;
      }
      ranges.sort((a, b) => a[0] - b[0]);
      const fragment = document.createDocumentFragment();
      let last = 0;
      for (const [start, end] of ranges) {
        if (start < last) {
          continue;
        }
        fragment.append(value.slice(last, start));
        const hit = document.createElement("mark");
        hit.className = "hit";
        hit.textContent = value.slice(start, end);
        fragment.append(hit);
        last = end;
      }
      fragment.append(value.slice(last));
      node.replaceWith(fragment);
    }
  }

  input.addEventListener("input", render);
  input.addEventListener("focus", () => {
    if (input.value.trim()) {
      render();
    }
  });
  input.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown" && shown.length > 0) {
      selected = (selected + 1) % shown.length;
      updateSelection();
      event.preventDefault();
    } else if (event.key === "ArrowUp" && shown.length > 0) {
      selected = (selected - 1 + shown.length) % shown.length;
      updateSelection();
      event.preventDefault();
    } else if (event.key === "Enter" && selected >= 0) {
      go(shown[selected]);
      event.preventDefault();
    } else if (event.key === "Escape") {
      if (input.value) {
        input.value = "";
        render();
      } else {
        input.blur();
      }
      event.preventDefault();
    }
  });
  results.addEventListener("mousedown", (event) => event.preventDefault());
  results.addEventListener("click", (event) => {
    const item = event.target.closest(".result");
    if (item) {
      event.preventDefault();
      go(shown[Number(item.id.slice("result-".length))]);
    }
  });
  input.addEventListener("blur", () => setTimeout(closeResults, 100));

  document.addEventListener("keydown", (event) => {
    const typing = event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement;
    if ((event.key === "/" && !typing) || (event.key.toLowerCase() === "k" && (event.ctrlKey || event.metaKey))) {
      input.focus();
      input.select();
      event.preventDefault();
    } else if (event.key === "Escape" && document.body.classList.contains("toc-open")) {
      document.body.classList.remove("toc-open");
      menu.setAttribute("aria-expanded", "false");
    }
  });

  document.getElementById("print").addEventListener("click", () => window.print());

  menu.addEventListener("click", () => {
    const open = document.body.classList.toggle("toc-open");
    menu.setAttribute("aria-expanded", String(open));
  });
  toc.addEventListener("click", (event) => {
    if (event.target.closest("a")) {
      document.body.classList.remove("toc-open");
      menu.setAttribute("aria-expanded", "false");
    }
  });

  // Contents: highlight the section being read, and keep the language link on the same section.
  const tocLinks = new Map([...toc.querySelectorAll("a")].map((a) => [a.getAttribute("href").slice(1), a]));
  const headings = [...document.querySelectorAll(".chapter h1[id], .chapter h2[id], .chapter h3[id]")];
  let current = null;

  function sectionInView() {
    const top = document.querySelector(".topbar").offsetHeight + 24;
    let found = headings[0];
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top - top > 1) {
        break;
      }
      found = heading;
    }
    return found;
  }

  function updateCurrent() {
    const heading = sectionInView();
    if (!heading || heading === current) {
      return;
    }
    current = heading;
    // h3 sections are not in the contents; light up the h2 above them.
    let tocId = heading.id;
    if (!tocLinks.has(tocId)) {
      const i = headings.indexOf(heading);
      for (let j = i; j >= 0; j--) {
        if (tocLinks.has(headings[j].id)) {
          tocId = headings[j].id;
          break;
        }
      }
    }
    tocLinks.forEach((link, id) => link.classList.toggle("active", id === tocId));
    tocLinks.get(tocId)?.scrollIntoView({ block: "nearest" });
    const url = new URL(language.getAttribute("href"), location.href);
    url.search = location.search;
    url.hash = heading.id;
    language.href = url.pathname.split("/").pop() + url.search + url.hash;
  }

  let pending = false;
  addEventListener("scroll", () => {
    if (!pending) {
      pending = true;
      requestAnimationFrame(() => {
        pending = false;
        updateCurrent();
      });
    }
  }, { passive: true });
  addEventListener("hashchange", updateCurrent);
  addEventListener("load", updateCurrent);
  updateCurrent();

  // ?q=words opens the manual with those words already searched.
  const initialQuery = params.get("q");
  if (initialQuery) {
    input.value = initialQuery;
    render();
  }
})();
