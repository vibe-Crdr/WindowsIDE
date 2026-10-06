# Convert the official Source Han Sans JP Regular CFF OTF into a static TrueType
# file that GDI+ DrawString can rasterize. Format conversion is an OFL Modified
# Version, so the user-facing family name must not use the Reserved Font Name
# "Source". The bundled name is 源ノ角ゴシック JP.

from __future__ import print_function

import sys

from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable

FAMILY = "源ノ角ゴシック JP"
POSTSCRIPT = "GennoKakuGothicJP-Regular"
MAX_ERR = 1.0


def glyphs_to_quadratic(glyphs):
    quad = {}
    names = list(glyphs.keys())
    total = len(names)
    for index, gname in enumerate(names):
        if index % 2000 == 0:
            print("glyphs %d/%d" % (index, total), flush=True)
        glyph = glyphs[gname]
        tt_pen = TTGlyphPen(glyphs)
        cu2qu_pen = Cu2QuPen(tt_pen, MAX_ERR, reverse_direction=True)
        glyph.draw(cu2qu_pen)
        quad[gname] = tt_pen.glyph()
    print("glyphs %d/%d" % (total, total), flush=True)
    return quad


def otf_to_ttf(tt_font):
    if tt_font.sfntVersion != "OTTO":
        raise SystemExit("input is not a CFF OTF")
    if "CFF " not in tt_font:
        raise SystemExit("input has no CFF table")

    glyph_order = tt_font.getGlyphOrder()
    tt_font["loca"] = newTable("loca")
    glyf = newTable("glyf")
    tt_font["glyf"] = glyf
    glyf.glyphOrder = glyph_order
    glyf.glyphs = glyphs_to_quadratic(tt_font.getGlyphSet())
    del tt_font["CFF "]
    glyf.compile(tt_font)

    hmtx = tt_font["hmtx"]
    for glyph_name, glyph in glyf.glyphs.items():
        if hasattr(glyph, "xMin"):
            hmtx[glyph_name] = (hmtx[glyph_name][0], glyph.xMin)

    maxp = newTable("maxp")
    tt_font["maxp"] = maxp
    maxp.tableVersion = 0x00010000
    maxp.maxZones = 1
    maxp.maxTwilightPoints = 0
    maxp.maxStorage = 0
    maxp.maxFunctionDefs = 0
    maxp.maxInstructionDefs = 0
    maxp.maxStackElements = 0
    maxp.maxSizeOfInstructions = 0
    max_components = 0
    for glyph in glyf.glyphs.values():
        components = glyph.components if hasattr(glyph, "components") else []
        if len(components) > max_components:
            max_components = len(components)
    maxp.maxComponentElements = max_components
    maxp.compile(tt_font)

    post = tt_font["post"]
    post.formatType = 3.0
    post.extraNames = []
    post.mapping = {}
    post.glyphOrder = glyph_order
    post.compile(tt_font)

    if "DSIG" in tt_font:
        del tt_font["DSIG"]

    tt_font.sfntVersion = "\000\001\000\000"
    rewrite_names(tt_font)


def rewrite_names(tt_font):
    name = tt_font["name"]
    kept = []
    for rec in name.names:
        if rec.platformID != 3 or rec.platEncID != 1:
            continue
        if rec.nameID in (1, 4, 16):
            rec.string = FAMILY
        elif rec.nameID == 6:
            rec.string = POSTSCRIPT
        kept.append(rec)
    if not kept:
        raise SystemExit("name table has no Windows Unicode records")
    name.names = kept


def main(argv):
    if len(argv) != 3:
        print("usage: convert_source_han_ttf.py INPUT.otf OUTPUT.ttf", file=sys.stderr)
        return 2
    font = TTFont(argv[1])
    otf_to_ttf(font)
    font.save(argv[2])
    print("saved " + argv[2], flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
