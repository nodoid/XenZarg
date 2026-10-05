#!/usr/bin/env python3
"""Packs a folder into an unsigned .msix for the Microsoft Store (the Store signs it).

    python3 tools/make_msix.py <folder containing AppxManifest.xml> <output.msix>

makeappx.exe only runs on Windows, so this writes the package format directly: a zip of the
files (stored, uncompressed) plus AppxBlockMap.xml (a SHA-256 hash of every 64 KB block of
every file) and [Content_Types].xml.
"""
import base64
import hashlib
import os
import sys
import zipfile
from urllib.parse import quote
from xml.sax.saxutils import escape

BLOCK = 65536
BLOCKMAP_NS = "http://schemas.microsoft.com/appx/2010/blockmap"
CONTENT_TYPES = {
    "dll": "application/x-msdownload",
    "exe": "application/x-msdownload",
    "png": "image/png",
    "xml": "application/vnd.ms-appx.manifest+xml",
    "json": "application/json",
    "ico": "image/vnd.microsoft.icon",
    "bmp": "image/bmp",
}


def zip_info(name):
    info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_STORED
    info.create_system = 0
    return info


def main(src, out):
    files = []
    for dirpath, _, names in os.walk(src):
        for n in sorted(names):
            full = os.path.join(dirpath, n)
            rel = os.path.relpath(full, src).replace(os.sep, "/")
            if n in (".DS_Store",) or rel in ("AppxBlockMap.xml", "[Content_Types].xml", "AppxSignature.p7x"):
                continue
            files.append(rel)
    # The manifest goes last among the payload, as makeappx does.
    files.sort(key=lambda r: (r == "AppxManifest.xml", r.lower()))
    if "AppxManifest.xml" not in files:
        sys.exit("AppxManifest.xml missing")

    blockmap = [f'<?xml version="1.0" encoding="UTF-8" standalone="no"?>\r\n'
                f'<BlockMap xmlns="{BLOCKMAP_NS}" HashMethod="http://www.w3.org/2001/04/xmlenc#sha256">']
    extensions = set()
    overrides = []

    with zipfile.ZipFile(out, "w", allowZip64=False) as z:
        for rel in files:
            data = open(os.path.join(src, rel), "rb").read()
            part = quote(rel, safe="/")
            info = zip_info(part)
            z.writestr(info, data)
            lfh = 30 + len(part.encode("utf-8")) + len(info.extra)
            win = escape(rel.replace("/", "\\"), {'"': "&quot;"})
            blockmap.append(f'<File Name="{win}" Size="{len(data)}" LfhSize="{lfh}">')
            for i in range(0, max(len(data), 1), BLOCK):
                if not data:
                    break
                digest = base64.b64encode(hashlib.sha256(data[i:i + BLOCK]).digest()).decode()
                blockmap.append(f'<Block Hash="{digest}"/>')
            blockmap.append("</File>")

            ext = os.path.splitext(rel)[1][1:].lower()
            if rel == "AppxManifest.xml":
                continue
            if ext:
                extensions.add(ext)
            else:
                overrides.append(part)

        blockmap.append("</BlockMap>")
        z.writestr(zip_info("AppxBlockMap.xml"), "".join(blockmap).encode("utf-8"))

        types = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\r\n'
                 '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">']
        for ext in sorted(extensions):
            types.append(f'<Default Extension="{ext}" ContentType="{CONTENT_TYPES.get(ext, "application/octet-stream")}"/>')
        types.append('<Override PartName="/AppxManifest.xml" ContentType="application/vnd.ms-appx.manifest+xml"/>')
        types.append('<Override PartName="/AppxBlockMap.xml" ContentType="application/vnd.ms-appx.blockmap+xml"/>')
        for part in overrides:
            types.append(f'<Override PartName="/{part}" ContentType="application/octet-stream"/>')
        types.append("</Types>")
        z.writestr(zip_info("[Content_Types].xml"), "".join(types).encode("utf-8"))
    print("wrote", out)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
