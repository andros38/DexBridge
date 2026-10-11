from pathlib import Path
import re, xml.etree.ElementTree as ET, zipfile
root=Path(__file__).resolve().parent.parent
xaml=(root/'MainWindow.xaml').read_text()
code=(root/'MainWindow.xaml.cs').read_text() + '\n' + (root/'MainWindow.Layout.cs').read_text()
catalog=(root/'Services/StreamPresetCatalog.cs').read_text()
settings=(root/'Models/AppSettings.cs').read_text()
xml=ET.fromstring(xaml)
ns={'w':'http://schemas.microsoft.com/winfx/2006/xaml/presentation','x':'http://schemas.microsoft.com/winfx/2006/xaml'}
entries=re.findall(r'\["([a-z0-9-]+)"\]\s*=\s*new\("([a-z0-9-]+)",\s*"(\d+x\d+)",\s*"(\d+)",\s*"(\d+)",\s*"(\d+)",\s*"([a-z0-9]+)"\)',catalog)
assert len(entries)==11, f'Expected 11 presets, got {len(entries)}'
allpresets={x[0]:x[2:] for x in entries}
assert all(k==v for k,v,*_ in entries)
selects={}
for combo in xml.findall('.//w:ComboBox',ns):
 name=combo.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name')
 if name in ('PresetCombo','GamingProfileCombo','ResolutionCombo','DpiCombo','FpsCombo','BitrateCombo','CodecCombo'):
  selects[name]=combo
assert len(selects)==7, f'Only found {selects.keys()}'
opts={name:{i.get('Content') for i in combo.findall('./w:ComboBoxItem',ns)} for name,combo in selects.items()}
for area in ('PresetCombo','GamingProfileCombo'):
 for it in selects[area].findall('./w:ComboBoxItem',ns):
  tag=it.get('Tag')
  assert tag=='custom' or tag in allpresets,(area,tag)
  if tag=='custom': continue
  res,dpi,fps,mbps,codec=allpresets[tag]
  for n,v in [('ResolutionCombo',res),('DpiCombo',dpi),('FpsCombo',fps),('BitrateCombo',mbps),('CodecCombo',codec)]:
   assert v in opts[n],(tag,n,v)
assert allpresets['youtube-quality']==('1920x1080','240','60','32','h265')
assert allpresets['performance']==('1600x900','240','60','16','h264')
assert allpresets['desktop-balanced']==('1920x1080','220','60','16','h264')
assert 'ApplyStreamPreset(tag)' in code
assert 'ApplyStreamPreset(preset)' in code
assert 'SelectPresetForSettings(ComboValue(ResolutionCombo), ComboValue(DpiCombo))' in code
assert 'SelectPresetForSettings(_settings.Resolution, _settings.Dpi);' in code
assert 'if (!StreamPresetCatalog.Validate(mirrorMode ? "1920x1080" : resolution,' in code
assert 'var firstUsePreset = package.Equals("com.google.android.youtube"' in code
assert '? "youtube-quality"' in code
assert 'var profile = FindAppProfile(package);' in code
assert 'if (profile is not null)' in code
assert '<Version>0.13.17</Version>' in (root/'DexBridge.csproj').read_text()
assert '$AppVersion' in (root/'tools/Build-Portable.ps1').read_text()
# Confirm XAML handler names resolve to functions in code (or opening On/Loaded defined in markup)?
events={'SelectionChanged','Click','Checked','Unchecked','TextChanged','Expanded','Collapsed','PreviewMouseDown','MouseDoubleClick','PreviewDragOver','Drop','SelectionChanged','GotFocus','LostFocus','Closing','Loaded'}
handlers=set()
for node in xml.iter():
 for name,value in node.attrib.items():
  if name in events:
   handlers.add(value)
for handler in handlers:
 if handler.endswith('}'): continue
 assert re.search(r'\b'+re.escape(handler)+r'\s*\(',code),f'No event handler: {handler}'
print(f'PASS: valid WPF XML, {len(allpresets)} coherent stream presets, 7 selectors, {len(handlers)} UI handlers, migration & default behavior static checks')
print('LIMITATION: WPF/.NET 10 compilation and real Android runtime test unavailable on Linux.')
