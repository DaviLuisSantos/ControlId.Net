"""Dump completo da documentação oficial da Access API da Control iD.

Fontes:
  1. Site MkDocs https://www.controlid.com.br/docs/access-api-pt/ (todas as páginas do sitemap.xml)
  2. Coleção Postman oficial (https://documenter.getpostman.com/view/10800185/SztHW4xo)

Saída (padrão: docs/controlid-api/):
  README.md              índice geral e estatísticas
  paginas/**.md          cada página do site convertida para Markdown (+ imagens em paginas/img/)
  postman/colecao.json   coleção Postman bruta
  postman/*.md           cada pasta de topo da coleção em Markdown (método, URL, corpo, descrição, respostas)
  endpoints.md/.json     catálogo de todos os endpoints *.fcgi e onde aparecem

Uso:
  python -m venv .venv && .venv/Scripts/pip install beautifulsoup4 markdownify
  .venv/Scripts/python tools/controlid-docs-dump/dump_controlid_docs.py [--out docs/controlid-api]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import time
import unicodedata
import urllib.parse
import urllib.request
from collections import defaultdict
from datetime import date
from pathlib import Path

from bs4 import BeautifulSoup
from markdownify import MarkdownConverter

BASE = "https://www.controlid.com.br/docs/access-api-pt/"
POSTMAN_VIEW = "https://documenter.getpostman.com/view/10800185/SztHW4xo"
POSTMAN_API = (
    "https://documenter.gw.postman.com/api/collections/10800185/SztHW4xo"
    "?segregateAuth=true&versionTag=latest"
)
UA = "Mozilla/5.0 (docs-dump)"
ENDPOINT_RE = re.compile(r"\b([A-Za-z0-9_]+\.fcgi)\b")
BINARY_RE = re.compile(r"[\x00-\x08\x0e-\x1f]")


def fetch(url: str, retries: int = 3) -> bytes:
    for attempt in range(retries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.read()
        except Exception as exc:  # noqa: BLE001
            if attempt == retries - 1:
                raise RuntimeError(f"falha ao baixar {url}: {exc}") from exc
            time.sleep(2 * (attempt + 1))
    raise AssertionError


class Converter(MarkdownConverter):
    """markdownify com linguagem nos blocos de código."""

    def convert_pre(self, el, text, parent_tags=None, **kw):
        code = el.find("code")
        lang = ""
        if code is not None:
            for cls in code.get("class", []) or []:
                if cls.startswith("language-"):
                    lang = cls[len("language-"):]
                elif cls not in ("hljs",):
                    lang = lang or cls
        body = el.get_text()
        body = body.rstrip("\n")
        return f"\n\n```{lang}\n{body}\n```\n\n"


def html_to_md(html: str) -> str:
    md = Converter(heading_style="ATX", bullets="-", escape_underscores=False).convert(html)
    md = re.sub(r"\n{3,}", "\n\n", md)
    return md.strip() + "\n"


# --------------------------------------------------------------------------- site

def page_urls() -> list[str]:
    xml = fetch(BASE + "sitemap.xml").decode("utf-8")
    urls = re.findall(r"<loc>\s*([^<]+?)\s*</loc>", xml)
    seen, out = set(), []
    for u in urls:
        if u.startswith(BASE) and u not in seen:
            seen.add(u)
            out.append(u)
    return out


def rel_path(url: str) -> str:
    rel = url[len(BASE):].strip("/")
    return rel or "index"


def dump_pages(out: Path, delay: float) -> list[dict]:
    pages_dir = out / "paginas"
    img_dir = pages_dir / "img"
    img_dir.mkdir(parents=True, exist_ok=True)
    pages = []
    images: dict[str, str] = {}
    urls = page_urls()
    for i, url in enumerate(urls, 1):
        rel = rel_path(url)
        print(f"[pagina {i}/{len(urls)}] {rel}", file=sys.stderr)
        soup = BeautifulSoup(fetch(url).decode("utf-8"), "html.parser")
        main = soup.select_one('div[role="main"]') or soup.body
        for a in main.select("a.headerlink"):
            a.decompose()
        md_path = pages_dir / f"{rel}.md"
        md_path.parent.mkdir(parents=True, exist_ok=True)
        depth = len(Path(rel).parts) - 1
        up = "../" * depth
        # imagens: baixa e reescreve o src para caminho local
        for img in main.find_all("img"):
            src = img.get("src")
            if not src:
                continue
            abs_src = urllib.parse.urljoin(url, src)
            if abs_src not in images:
                ext = Path(urllib.parse.urlparse(abs_src).path).suffix or ".bin"
                name = hashlib.sha1(abs_src.encode()).hexdigest()[:12] + ext
                try:
                    (img_dir / name).write_bytes(fetch(abs_src))
                    images[abs_src] = name
                except RuntimeError as exc:
                    print(f"  aviso: {exc}", file=sys.stderr)
                    images[abs_src] = ""
            if images[abs_src]:
                img["src"] = f"{up}img/{images[abs_src]}"
        # links internos: aponta para o .md local quando possível
        for a in main.find_all("a", href=True):
            abs_href = urllib.parse.urljoin(url, a["href"])
            path, _, frag = abs_href.partition("#")
            if path.startswith(BASE):
                target = rel_path(path)
                a["href"] = f"{up}{target}.md" + (f"#{frag}" if frag else "")
        title_el = main.find("h1")
        title = title_el.get_text(strip=True) if title_el else rel
        body = html_to_md(str(main))
        md_path.write_text(
            f"<!-- fonte: {url} -->\n\n{body}", encoding="utf-8", newline="\n"
        )
        pages.append({"url": url, "rel": rel, "title": title, "md": body})
        time.sleep(delay)
    return pages


# ------------------------------------------------------------------------ postman

def url_of(req: dict) -> str:
    u = req.get("url", "")
    if isinstance(u, dict):
        return u.get("raw", "")
    return u or ""


def desc_md(d) -> str:
    if not d:
        return ""
    if isinstance(d, dict):
        d = d.get("content", "")
    return html_to_md(d) if "<" in d else d.strip() + "\n"


def slug(s: str) -> str:
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode()
    s = re.sub(r"[^\w\- ]+", "", s).strip().lower()
    return re.sub(r"\s+", "-", s) or "sem-nome"


def fence(text: str, lang: str = "") -> str:
    ticks = "````" if "```" in text else "```"
    return f"{ticks}{lang}\n{text.rstrip()}\n{ticks}\n"


def render_request(item: dict, level: int, lines: list[str], reqs: list[dict], trail: list[str]):
    req = item.get("request", {}) or {}
    method = req.get("method", "GET")
    url = url_of(req)
    lines.append(f"{'#' * min(level, 6)} {item.get('name', '')}\n")
    lines.append(f"`{method} {url}`\n")
    d = desc_md(req.get("description"))
    if d:
        lines.append(d)
    headers = [h for h in req.get("header", []) if not h.get("disabled")]
    if headers:
        lines.append("**Headers:** " + ", ".join(f"`{h['key']}: {h.get('value', '')}`" for h in headers) + "\n")
    body = req.get("body") or {}
    mode = body.get("mode")
    if mode == "raw" and body.get("raw", "").strip():
        lines.append("**Corpo:**\n")
        lines.append(fence(body["raw"], "json" if body["raw"].lstrip().startswith(("{", "[")) else ""))
    elif mode in ("formdata", "urlencoded"):
        rows = body.get(mode, [])
        lines.append(f"**Corpo ({mode}):**\n")
        for r in rows:
            lines.append(f"- `{r.get('key')}` ({r.get('type', 'text')}): {r.get('value', r.get('src', ''))}")
        lines.append("")
    elif mode == "file":
        lines.append(f"**Corpo:** arquivo binário `{body.get('file', {}).get('src', '')}`\n")
    for resp in item.get("response", []) or []:
        code = resp.get("code", "")
        lines.append(f"**Exemplo de resposta — {resp.get('name', '')}** ({code} {resp.get('status', '')}):\n")
        rb = resp.get("body") or ""
        if BINARY_RE.search(rb):
            lines.append(f"*(corpo binário omitido, {len(rb)} caracteres; veja `colecao.json`)*\n")
        elif rb.strip():
            lines.append(fence(rb, "json" if rb.lstrip().startswith(("{", "[")) else ""))
    reqs.append({"pasta": " / ".join(trail), "nome": item.get("name", ""), "metodo": method, "url": url})


def render_folder(items: list, level: int, lines: list[str], reqs: list[dict], trail: list[str]):
    for it in items:
        if "item" in it:
            lines.append(f"{'#' * min(level, 6)} {it.get('name', '')}\n")
            d = desc_md(it.get("description"))
            if d:
                lines.append(d)
            render_folder(it["item"], level + 1, lines, reqs, trail + [it.get("name", "")])
        else:
            render_request(it, level, lines, reqs, trail)


def dump_postman(out: Path) -> tuple[list[dict], list[tuple[str, str, int]]]:
    pdir = out / "postman"
    pdir.mkdir(parents=True, exist_ok=True)
    print("[postman] baixando coleção", file=sys.stderr)
    raw = fetch(POSTMAN_API)
    col = json.loads(raw.decode("utf-8"))
    (pdir / "colecao.json").write_text(json.dumps(col, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")
    reqs: list[dict] = []
    files = []
    intro = ["# Coleção Postman — introdução\n", f"<!-- fonte: {POSTMAN_VIEW} -->\n", desc_md(col["info"].get("description"))]
    (pdir / "00-introducao.md").write_text("\n".join(intro), encoding="utf-8", newline="\n")
    for n, top in enumerate(col["item"], 1):
        lines = [f"# {top.get('name', '')}\n", f"<!-- fonte: {POSTMAN_VIEW} -->\n"]
        before = len(reqs)
        if "item" in top:
            d = desc_md(top.get("description"))
            if d:
                lines.append(d)
            render_folder(top["item"], 2, lines, reqs, [top.get("name", "")])
        else:
            render_request(top, 2, lines, reqs, [])
        fname = f"{n:02d}-{slug(top.get('name', ''))}.md"
        (pdir / fname).write_text("\n".join(lines), encoding="utf-8", newline="\n")
        files.append((fname, top.get("name", ""), len(reqs) - before))
    return reqs, files


# ---------------------------------------------------------------------- catálogo

def build_catalog(out: Path, pages: list[dict], reqs: list[dict]) -> dict:
    cat: dict[str, dict] = defaultdict(lambda: {"metodos": set(), "postman": [], "paginas": set()})
    for r in reqs:
        for ep in ENDPOINT_RE.findall(r["url"]):
            cat[ep]["metodos"].add(r["metodo"])
            cat[ep]["postman"].append(f"{r['pasta']} / {r['nome']}".strip(" /"))
    for p in pages:
        for ep in set(ENDPOINT_RE.findall(p["md"])):
            cat[ep]["paginas"].add(p["rel"])
    data = {
        ep: {
            "metodos": sorted(v["metodos"]),
            "postman": v["postman"],
            "paginas": sorted(v["paginas"]),
        }
        for ep, v in sorted(cat.items())
    }
    (out / "endpoints.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")
    lines = [
        "# Catálogo de endpoints da Access API\n",
        f"{len(data)} endpoints `*.fcgi` distintos, extraídos das páginas do site e da coleção Postman.\n",
        "| Endpoint | Métodos (Postman) | Requisições no Postman | Páginas do site |",
        "|---|---|---|---|",
    ]
    for ep, v in data.items():
        pm = "<br>".join(v["postman"][:6]) + (f"<br>… +{len(v['postman']) - 6}" if len(v["postman"]) > 6 else "")
        pg = "<br>".join(f"[{p}](paginas/{p}.md)" for p in v["paginas"])
        lines.append(f"| `{ep}` | {', '.join(v['metodos']) or '—'} | {pm or '—'} | {pg or '—'} |")
    (out / "endpoints.md").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return data


def write_readme(out: Path, pages: list[dict], files, reqs, catalog):
    only_site = [e for e, v in catalog.items() if not v["postman"]]
    only_pm = [e for e, v in catalog.items() if not v["paginas"]]
    lines = [
        "# Dump da documentação da Access API (Control iD)\n",
        f"Gerado em {date.today().isoformat()} por `tools/controlid-docs-dump/dump_controlid_docs.py`. "
        "Não edite à mão: rode o script de novo para atualizar.\n",
        f"- Site: {BASE} — {len(pages)} páginas em [`paginas/`](paginas/)",
        f"- Coleção Postman: {POSTMAN_VIEW} — {len(reqs)} requisições em [`postman/`](postman/) (bruto em `postman/colecao.json`)",
        f"- Catálogo: [`endpoints.md`](endpoints.md) / `endpoints.json` — {len(catalog)} endpoints distintos",
        f"- Só no site (sem requisição no Postman): {len(only_site)}; só no Postman (sem página): {len(only_pm)}\n",
        "## Páginas do site\n",
    ]
    by_section: dict[str, list[dict]] = defaultdict(list)
    for p in pages:
        by_section[p["rel"].split("/")[0] if "/" in p["rel"] else "(raiz)"].append(p)
    for sec, ps in by_section.items():
        lines.append(f"### {sec}\n")
        lines += [f"- [{p['title']}](paginas/{p['rel']}.md)" for p in ps]
        lines.append("")
    lines.append("## Coleção Postman\n")
    lines += [f"- [{name}](postman/{f}) — {n} requisições" for f, name, n in files]
    lines.append("")
    if only_site:
        lines.append("## Endpoints citados no site sem requisição no Postman\n")
        lines.append(", ".join(f"`{e}`" for e in only_site) + "\n")
    if only_pm:
        lines.append("## Endpoints no Postman sem página no site\n")
        lines.append(", ".join(f"`{e}`" for e in only_pm) + "\n")
    (out / "README.md").write_text("\n".join(lines), encoding="utf-8", newline="\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="docs/controlid-api")
    ap.add_argument("--delay", type=float, default=0.3, help="pausa entre páginas (s)")
    args = ap.parse_args()
    out = Path(args.out)
    for sub in ("paginas", "postman"):
        if (out / sub).exists():
            shutil.rmtree(out / sub)
    out.mkdir(parents=True, exist_ok=True)
    pages = dump_pages(out, args.delay)
    reqs, files = dump_postman(out)
    catalog = build_catalog(out, pages, reqs)
    write_readme(out, pages, files, reqs, catalog)
    print(f"ok: {len(pages)} páginas, {len(reqs)} requisições, {len(catalog)} endpoints -> {out}", file=sys.stderr)


if __name__ == "__main__":
    main()
