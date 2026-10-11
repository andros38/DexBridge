#!/usr/bin/env python3
"""Static source-only checks for v0.13.6 localization. Windows WPF runtime not included."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json, re
root=Path(__file__).resolve().parents[1]
translations=json.loads((root/'Languages/id-ID.json').read_text(encoding='utf8'))
main=(root/'MainWindow.xaml.cs').read_text()
files=(root/'PhoneFilesWindow.xaml.cs').read_text()
service=(root/'Services/LocalizationService.cs').read_text()
settings=(root/'Models/AppSettings.cs').read_text()
csproj=(root/'DexBridge.csproj').read_text()
xaml=(root/'MainWindow.xaml').read_text()
assert 'public string Language { get; set; } = "id";' in settings
assert '<EmbeddedResource Include="Languages\\id-ID.json"' in csproj
assert 'LanguageCombo_SelectionChanged' in main and 'SelectionChanged="LanguageCombo_SelectionChanged"' in xaml
assert 'Tag="id"' in xaml and 'Tag="en"' in xaml
assert '_settings.Language = (LanguageCombo.SelectedItem' in main
assert 'Loc.SetLanguage(_settings.Language)' in main
assert 'Loc.Apply(this)' in main and 'Loc.Apply(this)' in files
assert 'Loc.LanguageChanged +=' in files and 'Loc.LanguageChanged -=' in files
assert 'if (!IsLoaded || _loadingUi' in main
assert 'Originals.GetOrCreateValue(item)' in service
for f in root.glob('*.xaml'): ET.parse(f)
# Verify every user-facing declarative label; allow technical IDs, numeric preset tooltips, and language names.
intentional={'DexBridge','CPU','RAM','Bahasa Indonesia','English','scrcpy.exe','sdk','uhid','h264','h265'}
missing=[]
for filename in ('MainWindow.xaml','PhoneFilesWindow.xaml'):
 for e in ET.parse(root/filename).iter():
  for k,v in e.attrib.items():
   if k in {'Text','Content','Header','Title','ToolTip'} and any(ch.isalpha() for ch in v) and v not in translations and v not in intentional and not v.startswith(('{','/')) and not re.match(r'^\d+(?:[×x]|\s+DPI|$)',v):
    missing.append(f'{filename}:{k}:{v}')
assert not missing, 'Missing static translations: '+str(missing)
for filename,code in [('MainWindow',main),('PhoneFiles',files),('RunningTask',(root/'Models/RunningTaskInfo.cs').read_text()),('PhoneFileEntry',(root/'Models/PhoneFileEntry.cs').read_text())]:
 for key in re.findall(r'(?:Loc\.|LocalizationService\.)T\("((?:[^"\\]|\\.)*)"\)',code):
  assert key in translations or key in intentional or key=='App',f'Missing runtime translation key in {filename}: {key!r}'
for english,translated in translations.items():
 a=sorted(re.findall(r'(?<!\{)\{(\d+)(?:\:[^}]*)?\}',english))
 b=sorted(re.findall(r'(?<!\{)\{(\d+)(?:\:[^}]*)?\}',translated))
 assert a==b, f'Format-placeholder mismatch: {english}'
# Important preset/data semantics remain stable; localization only alters UI strings.
assert 'SelectedSessionMode() == "app"' in main
assert 'item.Tag?.ToString()' in main
assert '--new-display={resolution}/{dpi}' in main
assert 'LogReceived' in main
assert 'private void LanguageCombo_SelectionChanged' in main
print('PASS: Both XAML files and other WPF markup are valid XML.')
print('PASS: language choice Indonesian/English, id default, persistence and live update are wired.')
print(f'PASS: {len(translations)} translation keys; all static UI labels covered (technical values excluded).')
print('PASS: runtime translation keys and format placeholders have been checked.')
print('PASS: streaming command, preset Tag IDs, and raw scrcpy logging are unaffected by localization.')
print('NOTE: Windows/.NET WPF compile and Galaxy interaction test still required.')
