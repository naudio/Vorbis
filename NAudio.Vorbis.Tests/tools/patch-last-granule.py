"""Rewrites the granule position on the final Ogg page of a file, fixing up the page CRC.

Used to build TestFiles/short-granule.ogg, whose stated length understates its
contents; see ../TestFiles/README.md.

    python3 patch-last-granule.py <source.ogg> <destination.ogg> <delta>
"""
import struct, sys

def crc32_ogg(data):
    poly = 0x04c11db7
    crc = 0
    for b in data:
        crc ^= b << 24
        for _ in range(8):
            crc = ((crc << 1) ^ poly) & 0xFFFFFFFF if crc & 0x80000000 else (crc << 1) & 0xFFFFFFFF
    return crc

def pages(d):
    out, i = [], 0
    while True:
        j = d.find(b'OggS', i)
        if j < 0: break
        nseg = d[j+26]
        body = sum(d[j+27:j+27+nseg])
        out.append((j, 27 + nseg + body))
        i = j + 27 + nseg + body
    return out

def patch_last_granule(src, dst, delta):
    d = bytearray(open(src,'rb').read())
    off, size = pages(d)[-1]
    gran = struct.unpack_from('<q', d, off+6)[0]
    struct.pack_into('<q', d, off+6, gran + delta)
    struct.pack_into('<I', d, off+22, 0)                     # zero the CRC field
    crc = crc32_ogg(bytes(d[off:off+size]))
    struct.pack_into('<I', d, off+22, crc)
    open(dst,'wb').write(bytes(d))
    print(f"{dst}: final granule {gran} -> {gran + delta}")

patch_last_granule(sys.argv[1], sys.argv[2], int(sys.argv[3]))
