"""Static release packaging checks. Does not replace a Windows WPF build."""
from pathlib import Path
from xml.etree import ElementTree
import json
import sys

root = Path(__file__).resolve().parents[1]
project = ElementTree.parse(root / "DexBridge.csproj").getroot()
props = project.find("PropertyGroup")
assert props is not None
version = props.findtext("Version")
assert version == "0.13.17", version
assert props.findtext("UseWPF") == "true"
assert props.findtext("TargetFramework") == "net10.0-windows"
for f in ("MainWindow.xaml", "App.xaml", "PhoneFilesWindow.xaml", "TelemetryOverlayWindow.xaml"):
    ElementTree.parse(root / f)
json.loads((root / "Languages/id-ID.json").read_text(encoding="utf-8"))
for f in (
    ".github/workflows/build-windows.yml", ".gitignore", "README.md",
    "tools/Start-Development.ps1", "tools/Build-Portable.ps1",
    "docs/PANDUAN-PENGGUNA.md", "docs/BUILD-DAN-RILIS.md", "docs/LISENSI-DAN-ATRIBUSI.md",
    "runtime/scrcpy/LICENSE.txt", "runtime/scrcpy/scrcpy-server", "runtime/scrcpy/adb.exe", "runtime/scrcpy/scrcpy.exe"
):
    assert (root / f).is_file(), f"missing file: {f}"
for f in ("scrcpy.exe", "adb.exe"):
    assert (root / "runtime/scrcpy" / f).read_bytes()[:2] == b"MZ", f"not a Windows executable: {f}"
workflow = (root / ".github/workflows/build-windows.yml").read_text(encoding="utf-8")
assert "windows-latest" in workflow and "Build-Portable.ps1" in workflow
assert "actions/upload-artifact" in workflow and "dotnet-version: '10.0.x'" in workflow
assert "gh release create" in workflow
builder = (root / "tools/Build-Portable.ps1").read_text(encoding="utf-8")
assert "--self-contained true" in builder and "PublishSingleFile=true" in builder
assert "DexBridge.exe" in builder and r"runtime\scrcpy" in builder
print(f"PASS: repository v{version}; WPF sources and translations parse; runtime executables present")
print("PASS: Windows Actions build, portable EXE output and GitHub Release steps configured")
print("LIMITATION: actual WPF build and running the EXE require Windows/.NET 10")
