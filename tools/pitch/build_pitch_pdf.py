#!/usr/bin/env python3
"""Render a pitch document from Markdown to PDF.

No third-party dependencies: a small Markdown subset is converted to HTML and
printed with a headless Chromium-family browser (Chrome or Edge), which is what
this machine has. Swap the renderer only if you need a richer Markdown subset.

Usage:
    python tools/pitch/build_pitch_pdf.py
    python tools/pitch/build_pitch_pdf.py --input Docs/pitch/gdd-matchballance-v1.md \\
        --output Docs/pitch/gdd-matchballance-v1.pdf
"""

from __future__ import annotations

import argparse
import html
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

CSS = """
@page { size: A4; margin: 18mm 16mm 20mm 16mm; }
* { box-sizing: border-box; }
body {
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
  font-size: 10.2pt; line-height: 1.55; color: #1d1d1f;
  margin: 0; -webkit-print-color-adjust: exact; print-color-adjust: exact;
}
h1 { font-size: 23pt; line-height: 1.15; margin: 0 0 6mm 0; letter-spacing: -0.02em; color: #0d0d0d; }
h2 {
  font-size: 13pt; margin: 9mm 0 3mm; padding-bottom: 1.6mm;
  border-bottom: 1.5pt solid #22b14c; color: #0d0d0d;
  page-break-after: avoid; break-after: avoid;
}
h3 { font-size: 10.8pt; margin: 6mm 0 2mm; color: #14532d; page-break-after: avoid; break-after: avoid; }
p { margin: 0 0 2.6mm; orphans: 3; widows: 3; }
strong { font-weight: 650; }
em { color: #444; }
hr { border: 0; border-top: 0.6pt solid #d7d7db; margin: 6mm 0; }
ul, ol { margin: 0 0 3mm; padding-left: 5.5mm; }
li { margin-bottom: 1.4mm; }
code {
  font-family: "Cascadia Mono", Consolas, monospace; font-size: 8.8pt;
  background: #f2f2f4; padding: 0.3mm 1.1mm; border-radius: 1mm; color: #b02418;
}
table {
  width: 100%; border-collapse: collapse; margin: 2mm 0 4mm;
  font-size: 9.2pt; page-break-inside: avoid; break-inside: avoid;
}
th {
  text-align: left; font-weight: 650; background: #f4f6f4;
  border-bottom: 1pt solid #22b14c; padding: 1.8mm 2mm; color: #0d0d0d;
}
td { border-bottom: 0.5pt solid #e3e3e6; padding: 1.6mm 2mm; vertical-align: top; }
tr:last-child td { border-bottom: 1pt solid #c9c9ce; }
blockquote { margin: 3mm 0; padding: 2mm 0 2mm 4mm; border-left: 2pt solid #22b14c; color: #333; }
"""

INLINE_CODE = re.compile(r"`([^`]+)`")
BOLD = re.compile(r"\*\*(.+?)\*\*")
ITALIC = re.compile(r"(?<!\*)\*([^*\n]+)\*(?!\*)")


def inline(text: str) -> str:
    """Escape HTML, then apply the inline Markdown the document actually uses."""
    out = html.escape(text, quote=False)
    out = INLINE_CODE.sub(r"<code>\1</code>", out)
    out = BOLD.sub(r"<strong>\1</strong>", out)
    out = ITALIC.sub(r"<em>\1</em>", out)
    return out


def cells(row: str) -> list[str]:
    row = row.strip()
    if row.startswith("|"):
        row = row[1:]
    if row.endswith("|"):
        row = row[:-1]
    return [c.strip() for c in row.split("|")]


def is_separator(row: str) -> bool:
    return bool(re.fullmatch(r"\|?[\s:|-]+\|?", row.strip())) and "-" in row


def convert(markdown: str) -> str:
    lines = markdown.splitlines()
    out: list[str] = []
    paragraph: list[str] = []

    def flush() -> None:
        if paragraph:
            # Source lines inside one block are hard-wrapped metadata, so they keep
            # their line breaks instead of collapsing into a run-on sentence.
            out.append("<p>" + "<br>".join(inline(l) for l in paragraph) + "</p>")
            paragraph.clear()

    def is_block_start(line: str) -> bool:
        s = line.strip()
        return (
            s.startswith("#")
            or s.startswith("|")
            or s.startswith("- ")
            or s.startswith("* ")
            or re.match(r"^\d+\.\s", s) is not None
            or s in {"---", "***", "___"}
            or s.startswith(">")
        )

    def collect_list(lines: list[str], start: int, marker: re.Pattern[str]) -> tuple[list[str], int]:
        """Gather one list, folding wrapped continuation lines into the previous item."""
        items: list[str] = []
        i = start
        while i < len(lines):
            raw = lines[i].strip()
            if marker.match(raw):
                items.append(marker.sub("", raw, count=1).strip())
                i += 1
                continue
            if not raw:
                if i + 1 < len(lines) and marker.match(lines[i + 1].strip()):
                    i += 1
                    continue
                break
            if items and not is_block_start(lines[i]):
                items[-1] = f"{items[-1]} {raw}"
                i += 1
                continue
            break
        return items, i

    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if not stripped:
            flush()
            i += 1
            continue

        # Table: header row followed by a separator row.
        if (
            stripped.startswith("|")
            and i + 1 < len(lines)
            and is_separator(lines[i + 1])
        ):
            flush()
            header = cells(stripped)
            align = cells(lines[i + 1])
            rows: list[list[str]] = []
            j = i + 2
            while j < len(lines) and lines[j].strip().startswith("|"):
                rows.append(cells(lines[j]))
                j += 1
            out.append("<table><thead><tr>")
            for col, spec in zip(header, align):
                style = ""
                if spec.startswith(":") and spec.endswith(":"):
                    style = ' style="text-align:center"'
                elif spec.endswith(":"):
                    style = ' style="text-align:right"'
                out.append(f"<th{style}>{inline(col)}</th>")
            out.append("</tr></thead><tbody>")
            for row in rows:
                out.append("<tr>")
                for col in row:
                    out.append(f"<td>{inline(col)}</td>")
                out.append("</tr>")
            out.append("</tbody></table>")
            i = j
            continue

        if stripped.startswith("#"):
            flush()
            level = len(stripped) - len(stripped.lstrip("#"))
            text = stripped[level:].strip()
            tag = f"h{min(level, 6)}"
            out.append(f"<{tag}>{inline(text)}</{tag}>")
            i += 1
            continue

        if stripped in {"---", "***", "___"}:
            flush()
            out.append("<hr>")
            i += 1
            continue

        if stripped.startswith(("- ", "* ")):
            flush()
            items, i = collect_list(lines, i, re.compile(r"^[-*]\s"))
            out.append("<ul>")
            for item in items:
                out.append(f"<li>{inline(item)}</li>")
            out.append("</ul>")
            continue

        if re.match(r"^\d+\.\s", stripped):
            flush()
            items, i = collect_list(lines, i, re.compile(r"^\d+\.\s"))
            out.append("<ol>")
            for item in items:
                out.append(f"<li>{inline(item)}</li>")
            out.append("</ol>")
            continue

        if stripped.startswith(">"):
            flush()
            quoted = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quoted.append(lines[i].strip().lstrip(">").strip())
                i += 1
            out.append("<blockquote>" + inline(" ".join(quoted)) + "</blockquote>")
            continue

        paragraph.append(stripped)
        i += 1
        if i < len(lines) and not is_block_start(lines[i]):
            continue

    flush()
    return "\n".join(out)


def find_browser() -> str:
    override = shutil.which("chrome") or shutil.which("msedge")
    if override:
        return override
    for candidate in BROWSERS:
        if Path(candidate).exists():
            return candidate
    raise SystemExit(
        "No Chrome or Edge found. Install one, or render the HTML by hand."
    )


def render(input_path: Path, output_path: Path) -> None:
    markdown = input_path.read_text(encoding="utf-8-sig")
    body = convert(markdown)
    document = (
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">"
        f"<title>{html.escape(input_path.stem)}</title><style>{CSS}</style></head>"
        f"<body>{body}</body></html>"
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        html_path = Path(tmp) / "document.html"
        html_path.write_text(document, encoding="utf-8")
        browser = find_browser()
        result = subprocess.run(
            [
                browser,
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--no-pdf-header-footer",
                "--virtual-time-budget=10000",
                f"--print-to-pdf={output_path}",
                html_path.as_uri(),
            ],
            capture_output=True,
            text=True,
            timeout=180,
        )
        if result.returncode != 0 or not output_path.exists():
            print(result.stdout, file=sys.stderr)
            print(result.stderr, file=sys.stderr)
            raise SystemExit(f"PDF render failed for {input_path}")

    size_kb = output_path.stat().st_size / 1024
    print(f"PDF: {output_path} ({size_kb:.0f} KB)")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--input",
        type=Path,
        default=REPO_ROOT / "Docs/pitch/gdd-matchballance-v1.md",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=REPO_ROOT / "Docs/pitch/gdd-matchballance-v1.pdf",
    )
    args = parser.parse_args()
    render(args.input.resolve(), args.output.resolve())


if __name__ == "__main__":
    main()
