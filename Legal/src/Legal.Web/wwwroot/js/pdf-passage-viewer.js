// ─────────────────────────────────────────────────────────────────────────────
//  Premium passage → PDF viewer with highlight (self-hosted module)
//
//  Renders an uploaded PDF into a host element using Mozilla pdf.js, jumps to the
//  passage's page, and highlights the passage inside the document. Two highlight
//  strategies:
//    1. Bounding-box geometry (when the extraction provider supplied polygons) —
//       precise rectangles drawn over the page canvas.
//    2. Text match (fallback, used for native-text PDFs where geometry is null) —
//       the passage text is normalized and located within the page text layer and
//       every matching run is tinted.
//
//  The module is self-contained and runs fully offline: it lazy-imports a vendored
//  pdf.js ESM build from wwwroot/lib/pdfjs and points its worker at the same local
//  copy. No external CDN is contacted at runtime.
// ─────────────────────────────────────────────────────────────────────────────

const PDFJS_VERSION = '4.6.82';
const PDFJS_URL = '/lib/pdfjs/pdf.min.mjs';
const PDFJS_WORKER_URL = '/lib/pdfjs/pdf.worker.min.mjs';

let pdfjsLibPromise = null;

// Active viewer instances keyed by host element id so dispose can tear them down.
const instances = new Map();

function loadPdfJs() {
    if (!pdfjsLibPromise) {
        pdfjsLibPromise = import(/* webpackIgnore: true */ PDFJS_URL).then(lib => {
            lib.GlobalWorkerOptions.workerSrc = PDFJS_WORKER_URL;
            return lib;
        });
    }
    return pdfjsLibPromise;
}

// Collapse whitespace, strip diacritics and lowercase so passage text extracted by
// the server matches the glyph order the PDF text layer reports.
function normalize(text) {
    return (text || '')
        .normalize('NFKD')
        .replace(/[\u0300-\u036f]/g, '')
        .replace(/\s+/g, ' ')
        .trim()
        .toLowerCase();
}

function parseBoundingRegions(json, targetPage) {
    if (!json) return [];
    try {
        const parsed = JSON.parse(json);
        const regions = Array.isArray(parsed) ? parsed : (parsed.regions || parsed.Regions || []);
        return regions
            .filter(r => {
                const page = r.pageNumber ?? r.PageNumber ?? r.page ?? r.Page;
                return page == null || page === targetPage;
            })
            .map(r => r.polygon ?? r.Polygon ?? r.boundingPolygon ?? r.BoundingPolygon ?? r.rect ?? r.Rect)
            .filter(Boolean);
    } catch {
        return [];
    }
}

// Converts a pdf.js text layer into highlight rectangles for every run that overlaps
// the normalized passage text. Returns an array of {left, top, width, height, evidence}
// in CSS px. Runs that also fall inside the specific evidence assertion (evidenceText)
// are flagged evidence:true so the caller can tint them red instead of yellow.
function computeTextHighlights(textContent, viewport, passageText, evidenceText, reasonText, propositionText) {
    const needle = normalize(passageText);
    if (needle.length < 4) return [];

    // Build a concatenated normalized string with a map back to each text item so we
    // can find which items fall inside a match span.
    const items = textContent.items.filter(i => typeof i.str === 'string' && i.str.length);
    let flat = '';
    const spans = [];
    for (const item of items) {
        const piece = normalize(item.str);
        if (!piece) continue;
        const start = flat.length;
        flat += piece + ' ';
        spans.push({ item, start, end: flat.length - 1 });
    }

    // Character ranges within `flat` that belong to the specific evidence assertion (red),
    // the "why linked" reason (green), and the proposition/claim (blue). These are matched
    // across the WHOLE page independently of the yellow source passage, so a semantic layer
    // still highlights even when its text lives in a different part of the document than the
    // source passage. Precedence when a span overlaps more than one: proposition > reason > evidence.
    const evidenceRanges = computeEvidenceRanges(flat, evidenceText);
    const reasonRanges = computeEvidenceRanges(flat, reasonText);
    const propositionRanges = computeEvidenceRanges(flat, propositionText);
    const overlaps = (span, ranges) => ranges.some(r => !(span.end <= r.start || span.start >= r.end));
    const tierFor = (span) =>
        overlaps(span, propositionRanges) ? 'proposition'
        : (overlaps(span, reasonRanges) ? 'reason'
        : (overlaps(span, evidenceRanges) ? 'evidence' : null));

    // Collect rectangles keyed by span so each text run is emitted once with its strongest tier.
    // Precedence: proposition/reason/evidence tint (semantic) beats a plain yellow passage run.
    const bySpan = new Map();
    const addSpan = (span, tier) => {
        const existing = bySpan.get(span);
        // null tier = plain yellow passage; any semantic tier should win over it.
        if (existing === undefined || (existing.tier === null && tier !== null)) {
            bySpan.set(span, { span, tier });
        }
    };

    let searchFrom = 0;
    // Highlight every occurrence of the passage (or its leading window for long passages).
    const window = needle.length > 400 ? needle.slice(0, 400) : needle;
    while (true) {
        const matchIndex = flat.indexOf(window, searchFrom);
        if (matchIndex === -1) break;
        const matchEnd = matchIndex + window.length;
        for (const span of spans) {
            if (span.end <= matchIndex || span.start >= matchEnd) continue;
            addSpan(span, tierFor(span));
        }
        searchFrom = matchEnd;
    }

    // Fallback: the full passage (or its leading window) may not appear verbatim in the page
    // text layer because the server concatenated or reflowed the extracted text (joined tokens,
    // missing spaces/newlines). Walk the passage word-by-word and highlight EVERY sufficiently
    // long contiguous run that exists in the page, so multi-line passages are highlighted across
    // all their lines on the document side — not just a single sentence.
    if (bySpan.size === 0) {
        const words = needle.split(' ').filter(w => w.length > 1);
        const matchedRanges = [];
        let i = 0;
        while (i < words.length) {
            let phrase = words[i];
            let idx = flat.indexOf(phrase);
            if (idx === -1) { i++; continue; }
            let j = i;
            while (j + 1 < words.length) {
                const next = phrase + ' ' + words[j + 1];
                const nextIdx = flat.indexOf(next);
                if (nextIdx === -1) break;
                phrase = next;
                idx = nextIdx;
                j++;
            }
            // Keep runs of at least 3 words or 12 chars to avoid noise from single stopwords.
            const wordCount = j - i + 1;
            if (phrase.length >= 12 || wordCount >= 3) {
                matchedRanges.push({ start: idx, end: idx + phrase.length });
            }
            i = j + 1;
        }
        for (const range of matchedRanges) {
            for (const span of spans) {
                if (span.end <= range.start || span.start >= range.end) continue;
                addSpan(span, tierFor(span));
            }
        }
    }

    // Independently highlight the semantic layers (blue/green/red) anywhere they appear on the
    // page, even outside the yellow source passage. This is what makes the "why linked" reason
    // show up on the document side when its text lives in a different section than the passage.
    const addRanges = (ranges, tier) => {
        for (const range of ranges) {
            for (const span of spans) {
                if (span.end <= range.start || span.start >= range.end) continue;
                addSpan(span, tier);
            }
        }
    };
    addRanges(evidenceRanges, 'evidence');
    addRanges(reasonRanges, 'reason');
    addRanges(propositionRanges, 'proposition');

    const rects = [];
    for (const { span, tier } of bySpan.values()) {
        rects.push({ ...itemToRect(span.item, viewport), tier });
    }
    return rects;
}

// Finds the character ranges within the flattened page text that correspond to the specific
// evidence assertion. Tries a verbatim window first, then falls back to the longest contiguous
// word runs so partially-reflowed evidence text still resolves to the red run.
function computeEvidenceRanges(flat, evidenceText) {
    const ev = normalize(evidenceText);
    if (ev.length < 4) return [];

    const ranges = [];
    const window = ev.length > 400 ? ev.slice(0, 400) : ev;
    let from = 0;
    while (true) {
        const idx = flat.indexOf(window, from);
        if (idx === -1) break;
        ranges.push({ start: idx, end: idx + window.length });
        from = idx + window.length;
    }
    if (ranges.length) return ranges;

    const words = ev.split(' ').filter(w => w.length > 1);
    let i = 0;
    while (i < words.length) {
        let phrase = words[i];
        let idx = flat.indexOf(phrase);
        if (idx === -1) { i++; continue; }
        let j = i;
        while (j + 1 < words.length) {
            const next = phrase + ' ' + words[j + 1];
            const nextIdx = flat.indexOf(next);
            if (nextIdx === -1) break;
            phrase = next;
            idx = nextIdx;
            j++;
        }
        const wordCount = j - i + 1;
        // Accept long/multi-word runs, OR a single distinctive token that carries a digit
        // (dates, amounts, numbers like "2025-10-09") so short but unambiguous anchors match.
        const distinctive = wordCount === 1 && phrase.length >= 4 && /\d/.test(phrase);
        if (phrase.length >= 12 || wordCount >= 3 || distinctive) {
            ranges.push({ start: idx, end: idx + phrase.length });
        }
        i = j + 1;
    }
    return ranges;
}

function itemToRect(item, viewport) {
    // pdf.js transform: [scaleX, skewY, skewX, scaleY, x, y] in PDF space.
    const tx = window.pdfjsLib
        ? window.pdfjsLib.Util.transform(viewport.transform, item.transform)
        : multiply(viewport.transform, item.transform);
    const fontHeight = Math.hypot(tx[2], tx[3]);
    const width = item.width * viewport.scale;
    const left = tx[4];
    const top = tx[5] - fontHeight;
    return { left, top, width, height: fontHeight };
}

// Minimal 2x3 affine multiply fallback (pdf.js Util.transform shape).
function multiply(m1, m2) {
    return [
        m1[0] * m2[0] + m1[2] * m2[1],
        m1[1] * m2[0] + m1[3] * m2[1],
        m1[0] * m2[2] + m1[2] * m2[3],
        m1[1] * m2[2] + m1[3] * m2[3],
        m1[0] * m2[4] + m1[2] * m2[5] + m1[4],
        m1[1] * m2[4] + m1[3] * m2[5] + m1[5],
    ];
}

function polygonToRect(polygon, viewport) {
    // Azure polygons are [x1,y1,x2,y2,...] in inches; viewport works in PDF points.
    // When values look like inches (<= page size in inches) scale by 72. Otherwise
    // assume they are already in PDF user units.
    const xs = [], ys = [];
    for (let i = 0; i < polygon.length - 1; i += 2) {
        xs.push(polygon[i]);
        ys.push(polygon[i + 1]);
    }
    if (!xs.length) return null;
    const pageWidthInches = viewport.viewBox[2] / 72;
    const looksInches = Math.max(...xs) <= pageWidthInches + 1;
    const unit = looksInches ? 72 : 1;
    const pts = [];
    for (let i = 0; i < xs.length; i++) {
        pts.push(viewport.convertToViewportPoint(xs[i] * unit, ys[i] * unit));
    }
    const px = pts.map(p => p[0]);
    const py = pts.map(p => p[1]);
    const left = Math.min(...px);
    const top = Math.min(...py);
    return { left, top, width: Math.max(...px) - left, height: Math.max(...py) - top };
}

async function renderInternal(hostId, url, options) {
    const host = document.getElementById(hostId);
    if (!host) return { ok: false, error: 'host-missing' };

    await disposeInternal(hostId);
    host.classList.add('ppv-loading');
    host.innerHTML = '';

    const lib = await loadPdfJs();
    window.pdfjsLib = lib;

    const loadingTask = lib.getDocument({ url, withCredentials: true });
    const instance = { doc: null, canceled: false };
    instances.set(hostId, instance);

    let doc;
    try {
        doc = await loadingTask.promise;
    } catch (e) {
        host.classList.remove('ppv-loading');
        instances.delete(hostId);
        return { ok: false, error: 'load-failed', detail: String(e && e.message || e) };
    }
    if (instance.canceled) { doc.destroy(); return { ok: false, error: 'canceled' }; }
    instance.doc = doc;
    host.classList.remove('ppv-loading');

    const total = doc.numPages;
    const targetPage = Math.min(Math.max(options.pageNumber || 1, 1), total);
    const scale = options.scale || 1.35;
    let highlightedPageEl = null;

    for (let pageNo = 1; pageNo <= total; pageNo++) {
        if (instance.canceled) break;
        const page = await doc.getPage(pageNo);
        const viewport = page.getViewport({ scale });

        const pageEl = document.createElement('div');
        pageEl.className = 'ppv-page';
        pageEl.dataset.page = String(pageNo);
        pageEl.style.width = `${Math.floor(viewport.width)}px`;
        pageEl.style.height = `${Math.floor(viewport.height)}px`;

        const canvas = document.createElement('canvas');
        canvas.className = 'ppv-canvas';
        const ratio = window.devicePixelRatio || 1;
        canvas.width = Math.floor(viewport.width * ratio);
        canvas.height = Math.floor(viewport.height * ratio);
        canvas.style.width = `${Math.floor(viewport.width)}px`;
        canvas.style.height = `${Math.floor(viewport.height)}px`;
        const ctx = canvas.getContext('2d');
        ctx.scale(ratio, ratio);
        pageEl.appendChild(canvas);

        const highlightLayer = document.createElement('div');
        highlightLayer.className = 'ppv-highlight-layer';
        pageEl.appendChild(highlightLayer);

        host.appendChild(pageEl);

        await page.render({ canvasContext: ctx, viewport }).promise;

        if (pageNo === targetPage) {
            const rects = await buildHighlights(page, viewport, options);
            for (const r of rects) {
                if (!r) continue;
                const mark = document.createElement('div');
                mark.className = r.tier === 'proposition'
                    ? 'ppv-highlight ppv-highlight--proposition'
                    : (r.tier === 'reason'
                        ? 'ppv-highlight ppv-highlight--reason'
                        : (r.tier === 'evidence' ? 'ppv-highlight ppv-highlight--evidence' : 'ppv-highlight'));
                mark.style.left = `${r.left}px`;
                mark.style.top = `${r.top}px`;
                mark.style.width = `${Math.max(r.width, 4)}px`;
                mark.style.height = `${Math.max(r.height, 8)}px`;
                highlightLayer.appendChild(mark);
            }
            highlightedPageEl = pageEl;
        }
    }

    if (highlightedPageEl) {
        highlightedPageEl.scrollIntoView({ behavior: 'instant', block: 'start' });
        const firstMark = highlightedPageEl.querySelector('.ppv-highlight--proposition') || highlightedPageEl.querySelector('.ppv-highlight--reason') || highlightedPageEl.querySelector('.ppv-highlight--evidence') || highlightedPageEl.querySelector('.ppv-highlight');
        if (firstMark) {
            firstMark.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    return { ok: true, pages: total, page: targetPage };
}

async function buildHighlights(page, viewport, options) {
    const regions = parseBoundingRegions(options.boundingRegionJson, page.pageNumber);
    if (regions.length) {
        return regions.map(polygon => polygonToRect(polygon, viewport)).filter(Boolean);
    }
    if (options.passageText) {
        const textContent = await page.getTextContent();
        return computeTextHighlights(textContent, viewport, options.passageText, options.evidenceText, options.reasonText, options.propositionText);
    }
    return [];
}

async function disposeInternal(hostId) {
    const instance = instances.get(hostId);
    if (!instance) return;
    instance.canceled = true;
    try { if (instance.doc) await instance.doc.destroy(); } catch { /* no-op */ }
    instances.delete(hostId);
    const host = document.getElementById(hostId);
    if (host) { host.innerHTML = ''; host.classList.remove('ppv-loading'); }
}

export async function render(hostId, url, options) {
    try {
        return await renderInternal(hostId, url, options || {});
    } catch (e) {
        return { ok: false, error: 'render-exception', detail: String(e && e.message || e) };
    }
}

export async function dispose(hostId) {
    await disposeInternal(hostId);
}
