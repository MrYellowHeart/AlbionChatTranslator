#!/usr/bin/env python3
"""
Regenerates src/AlbionChatTranslator.Core/Translation/model-catalog.json.

The catalog pins every offline language pack to an exact Hugging Face commit and
records the SHA-256 of every file, so the app refuses anything that was changed
or corrupted after this script ran.

Usage:  python tools/update_model_catalog.py
Needs:  Python 3.10+, internet access. No third-party packages.
"""
import hashlib
import json
import pathlib
import sys
import urllib.request

HF = "https://huggingface.co"

# Files each pack needs. Quantized (int8) weights keep a pack around 115 MB.
FILES = [
    "config.json",
    "vocab.json",
    "source.spm",
    "target.spm",
    "onnx/encoder_model_quantized.onnx",
    "onnx/decoder_model_quantized.onnx",
]

# Language (ISO 639-1) -> Xenova repo that translates it INTO English.
TO_ENGLISH = {
    "ar": "opus-mt-ar-en", "cs": "opus-mt-cs-en", "da": "opus-mt-da-en",
    "de": "opus-mt-de-en", "es": "opus-mt-es-en", "et": "opus-mt-et-en",
    "fi": "opus-mt-fi-en", "fr": "opus-mt-fr-en", "hi": "opus-mt-hi-en",
    "hu": "opus-mt-hu-en", "id": "opus-mt-id-en", "it": "opus-mt-it-en",
    "ja": "opus-mt-ja-en", "ko": "opus-mt-ko-en", "nl": "opus-mt-nl-en",
    "pl": "opus-mt-pl-en", "pt": "opus-mt-ROMANCE-en", "ro": "opus-mt-ROMANCE-en",
    "ru": "opus-mt-ru-en", "sv": "opus-mt-sv-en", "th": "opus-mt-th-en",
    "tr": "opus-mt-tc-big-tr-en", "uk": "opus-mt-uk-en", "vi": "opus-mt-vi-en",
    "zh": "opus-mt-zh-en",
}

# Language -> (repo that translates English INTO it, target token or None).
FROM_ENGLISH = {
    "ar": ("opus-mt-en-ar", ">>ara<<"), "cs": ("opus-mt-en-cs", None),
    "da": ("opus-mt-en-da", None), "de": ("opus-mt-en-de", None),
    "es": ("opus-mt-en-es", None), "fi": ("opus-mt-en-fi", None),
    "fr": ("opus-mt-en-fr", None), "hi": ("opus-mt-en-hi", None),
    "hu": ("opus-mt-en-hu", None), "id": ("opus-mt-en-id", None),
    "it": ("opus-mt-en-it", None), "nl": ("opus-mt-en-nl", None),
    "pt": ("opus-mt-en-ROMANCE", ">>pt_br<<"), "ro": ("opus-mt-en-ro", None),
    "ru": ("opus-mt-en-ru", None), "sv": ("opus-mt-en-sv", None),
    "uk": ("opus-mt-en-uk", None), "vi": ("opus-mt-en-vi", ">>vie<<"),
    "zh": ("opus-mt-en-zh", ">>cmn_Hans<<"),
}


def get_json(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.load(r)


def sha256_of_url(url):
    h = hashlib.sha256()
    with urllib.request.urlopen(url, timeout=120) as r:
        while chunk := r.read(1 << 16):
            h.update(chunk)
    return h.hexdigest()


def describe_pack(name):
    repo = f"Xenova/{name}"
    revision = get_json(f"{HF}/api/models/{repo}")["sha"]
    tree = {f["path"]: f for f in get_json(f"{HF}/api/models/{repo}/tree/{revision}?recursive=1")}
    files = []
    for path in FILES:
        entry = tree.get(path)
        if entry is None:
            raise SystemExit(f"{repo}@{revision} is missing {path}")
        lfs = entry.get("lfs")
        sha = lfs["oid"] if lfs else sha256_of_url(f"{HF}/{repo}/resolve/{revision}/{path}")
        files.append({"path": path, "size": entry["size"], "sha256": sha})
    vocab = get_json(f"{HF}/{repo}/resolve/{revision}/vocab.json")
    tokens = sorted(k for k in vocab if k.startswith(">>") and k.endswith("<<"))
    print(f"  {name}: {sum(f['size'] for f in files) / 1e6:.0f} MB, target tokens: {tokens[:8]}", file=sys.stderr)
    return {"id": name.removeprefix("opus-mt-"), "repo": repo, "revision": revision, "files": files}


def main():
    names = sorted(set(TO_ENGLISH.values()) | {repo for repo, _ in FROM_ENGLISH.values()})
    packs = []
    for name in names:
        print(f"Pinning {name}", file=sys.stderr)
        packs.append(describe_pack(name))

    catalog = {
        "schema": 1,
        "packs": packs,
        "toEnglish": {lang: repo.removeprefix("opus-mt-") for lang, repo in sorted(TO_ENGLISH.items())},
        "fromEnglish": {
            lang: {"pack": repo.removeprefix("opus-mt-"), "prefix": prefix}
            for lang, (repo, prefix) in sorted(FROM_ENGLISH.items())
        },
    }
    out = pathlib.Path(__file__).resolve().parent.parent / "src/AlbionChatTranslator.Core/Translation/model-catalog.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {out}", file=sys.stderr)


if __name__ == "__main__":
    main()
