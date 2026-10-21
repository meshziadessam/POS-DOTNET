"""Allowlisted commercial delivery packages. Never include issuer secrets or licenses."""
import hashlib
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts'
out.mkdir(exist_ok=True)
version = '1.2.3'

def archive(name, files):
    with zipfile.ZipFile(out / name, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path, relative in sorted(files, key=lambda item: str(item[1])):
            assert path.suffix.lower() not in {'.lic', '.db', '.pfx', '.key'}
            assert 'private.xml' not in path.name.lower()
            z.write(path, str(relative).replace('\\', '/'))
    with zipfile.ZipFile(out / name) as z:
        assert z.testzip() is None

source = []
for folder in ('src', 'tests', 'seller', 'installer', 'tools'):
    for path in (root / folder).rglob('*'):
        relative = path.relative_to(root)
        if not path.is_file() or any(part in {'bin', 'obj', '__pycache__'} for part in relative.parts):
            continue
        if path.suffix.lower() in {'.cs', '.csproj', '.sql', '.manifest', '.iss', '.py', '.json'} or path.name == 'LicensingPublicKey.xml':
            source.append((path, relative))
for name in ('RestaurantPos.sln', 'build.ps1', 'README.md', 'SELLER-GUIDE.md', 'USER-GUIDE.txt', 'THIRD-PARTY-NOTICES.txt', 'VALIDATION.md', '.gitignore', '.gitattributes'):
    source.append((root / name, Path(name)))
archive(f'PushPOS-Source-{version}-PRIVATE.zip', source)

seller = root / 'seller/PushPOS.LicenseIssuer/bin/Release-v1.2.1/net48'
names = ('PushPOS.LicenseIssuer.exe', 'PushPOS.LicenseIssuer.exe.config', 'RestaurantPos.exe', 'RestaurantPos.exe.config', 'System.Data.SQLite.dll', 'x86/SQLite.Interop.dll', 'x64/SQLite.Interop.dll')
files = [(seller / name, Path(name)) for name in names]
files += [(root / name, Path(name)) for name in ('SELLER-GUIDE.md', 'THIRD-PARTY-NOTICES.txt')]
archive(f'PushPOS-SellerTools-{version}-OWNER-ONLY.zip', files)

names = ['PushPOS-Setup.exe', 'PushPOS-Setup-Small.exe', f'PushPOS-Source-{version}-PRIVATE.zip', f'PushPOS-SellerTools-{version}-OWNER-ONLY.zip']
manifest = {'product': 'Push', 'version': version, 'licensing': 'RSA-4096/SHA-256; permanent device-bound; offline', 'files': []}
for name in names:
    data = (out / name).read_bytes()
    manifest['files'].append({'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
(out / 'release-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(json.dumps(manifest, indent=2))
