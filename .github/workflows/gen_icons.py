import struct, zlib, os

def make_png(path):
    def chunk(name, data):
        c = name + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png  = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 2, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(b'\x00\xff\x00\xff'))
    png += chunk(b'IEND', b'')
    with open(path, 'wb') as f:
        f.write(png)

base = os.environ.get('ICONS_DIR', '.')
for dpi in ['mdpi', 'hdpi', 'xhdpi', 'xxhdpi', 'xxxhdpi']:
    d = base + '/Platforms/Android/Resources/mipmap-' + dpi
    os.makedirs(d, exist_ok=True)
    make_png(d + '/appicon.png')
    make_png(d + '/appicon_round.png')
    print(f'Created icons in {d}')