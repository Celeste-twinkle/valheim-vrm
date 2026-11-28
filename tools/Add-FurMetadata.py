"""Add optional ValheimVRM fur data to a standard VRM 0.x/1.0 GLB.

The original meshes and MToon materials remain the fallback. No avatar assets
or custom executable shader code are bundled with this tool.
"""
import argparse
import json
from pathlib import Path
import struct


def embed(source, destination, material, parameters, images):
    raw = Path(source).read_bytes()
    magic, version, size = struct.unpack_from('<III', raw)
    if (magic, version, size) != (0x46546C67, 2, len(raw)):
        raise ValueError('Expected a complete VRM / GLB 2.0 file')
    length, kind = struct.unpack_from('<II', raw, 12)
    if kind != 0x4E4F534A:
        raise ValueError('Missing GLB JSON chunk')
    document = json.loads(raw[20:20 + length])
    binary_size, kind = struct.unpack_from('<II', raw, 20 + length)
    if kind != 0x004E4942 or 28 + length + binary_size != len(raw):
        raise ValueError('Expected one embedded binary chunk')
    binary = bytearray(raw[28 + length:])
    if not 0 <= material < len(document.get('materials', [])):
        raise ValueError('Material index does not exist')
    metadata = dict(version=1, **parameters)
    for key, path in images.items():
        if path is None:
            continue
        png = Path(path).read_bytes()
        if not png.startswith(b'\x89PNG\r\n\x1a\n') or len(png) > 8 * 1024 * 1024:
            raise ValueError('Fur masks must be PNG files no larger than 8 MiB')
        width, height = struct.unpack_from('>II', png, 16)
        if not (0 < width <= 4096 and 0 < height <= 4096):
            raise ValueError('Fur masks must be at most 4096 x 4096')
        binary.extend(b'\0' * (-len(binary) % 4))
        views = document.setdefault('bufferViews', [])
        views.append(dict(buffer=0, byteOffset=len(binary), byteLength=len(png)))
        binary.extend(png)
        image_array = document.setdefault('images', [])
        metadata[key] = len(image_array)
        image_array.append(dict(bufferView=len(views) - 1, mimeType='image/png', name='ValheimVRM fur ' + key))
    document['materials'][material].setdefault('extras', {})['ValheimVRM_fur'] = metadata
    document['buffers'][0]['byteLength'] = len(binary)
    binary.extend(b'\0' * (-len(binary) % 4))
    encoded = json.dumps(document, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8')
    encoded += b' ' * (-len(encoded) % 4)
    output = (struct.pack('<III', magic, version, 28 + len(encoded) + len(binary)) +
              struct.pack('<II', len(encoded), 0x4E4F534A) + encoded +
              struct.pack('<II', len(binary), 0x004E4942) + binary)
    # Write beside the destination first, so a failed write cannot truncate it.
    target = Path(destination)
    temporary = target.with_name(target.name + '.tmp')
    temporary.write_bytes(output)
    temporary.replace(target)
    return {'material': material, 'bytes': len(output), 'metadata': metadata}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input')
    parser.add_argument('output')
    parser.add_argument('--material', type=int, required=True, help='glTF material index, not renderer slot or material name')
    parser.add_argument('--length', type=float, default=.005, help='Meters at the original model scale (0..0.03)')
    parser.add_argument('--density', type=int, default=2, choices=(1, 2, 3))
    parser.add_argument('--randomness', type=float, default=.5)
    parser.add_argument('--direction', type=float, nargs=3, default=(0, 0, 1), metavar=('X', 'Y', 'Z'))
    parser.add_argument('--root-offset', type=float, default=-1)
    parser.add_argument('--noise-scale', type=float, nargs=2, default=(1, 1))
    parser.add_argument('--length-mask')
    parser.add_argument('--noise-mask')
    parser.add_argument('--coverage-mask')
    args = parser.parse_args()
    if not 0 <= args.length <= .03 or not 0 <= args.randomness <= 1 or not -1 <= args.root_offset <= 0:
        parser.error('Invalid length, randomness or root offset')
    print(json.dumps(embed(args.input, args.output, args.material,
          dict(length=args.length, density=args.density, randomness=args.randomness,
               directionX=args.direction[0], directionY=args.direction[1], directionZ=args.direction[2],
               rootOffset=args.root_offset, noiseScaleX=args.noise_scale[0], noiseScaleY=args.noise_scale[1]),
          dict(lengthImage=args.length_mask, noiseImage=args.noise_mask, maskImage=args.coverage_mask))))
