"""Static checks for DexBridge v0.13.17 setting wiring and contextual language.
Requires only Python 3, not Windows. Runtime validation still needs a Windows WPF build.
"""
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[1]
view = ET.parse(root / 'MainWindow.xaml').getroot()
ET.parse(root / 'App.xaml')
words = json.loads((root / 'Languages/id-ID.json').read_text(encoding='utf-8'))
main = (root / 'MainWindow.xaml.cs').read_text(encoding='utf-8')
settings = (root / 'Models/AppSettings.cs').read_text(encoding='utf-8')
service = (root / 'Services/SettingsService.cs').read_text(encoding='utf-8')
project = (root / 'DexBridge.csproj').read_text(encoding='utf-8')
ns = {'x': 'http://schemas.microsoft.com/winfx/2006/xaml'}

assert '<Version>0.13.17</Version>' in project
assert words['Light'] == 'Terang', 'Theme Light must translate to Terang'
assert words['Dark'] == 'Gelap'
assert 'if (status == 1) return Loc.Language == "id" ? "Ringan" : "Light";' in main
assert 'public string PreferredDeviceSerial ' in settings
assert 'public string ExternalScrcpyPath ' in settings
assert 'public string DesktopWindowMode ' in settings
assert 'public string UiLayout ' in settings
assert 'public string Language ' in settings
assert 'File.Move(temporaryPath, SettingsPath, overwrite: true)' in service
assert 'BackupPath' in service and 'new[] { SettingsPath, BackupPath }' in service
assert '_restoringDevices = true' in main
assert '_settings.PreferredDeviceSerial' in main
assert '_settings.ExternalScrcpyPath' in main
assert 'NoDownsizeCheck.IsChecked = _settings.NoDownsizeOnError' in main
assert '_settings.DesktopWindowMode = SelectedGameWindowMode()' in main
assert 'SelectTaggedItem(GameWindowModeCombo, _settings.DesktopWindowMode);' in main

for control, evt, handler in [
    ('KeyboardCombo','SelectionChanged','SettingsChoice_SelectionChanged'),
    ('MouseCombo','SelectionChanged','SettingsChoice_SelectionChanged'),
    ('NoDownsizeCheck','Checked','PersistentOption_Changed'),
    ('AudioCheck','Checked','PersistentOption_Changed'),
    ('NoHoverCheck','Checked','PersistentOption_Changed'),
    ('TurnPhoneScreenOffCheck','Checked','PersistentOption_Changed'),
    ('StayAwakeCheck','Checked','PersistentOption_Changed'),
    ('ForceDpiCheck','Checked','PersistentOption_Changed'),
    ('HandoffDexAppsOnStopCheck','Checked','PersistentOption_Changed'),
    ('CloseFailedHandoffTasksCheck','Checked','PersistentOption_Changed'),
    ('ScrcpyPathBox','TextChanged','ScrcpyPathBox_TextChanged'),
]:
    element = view.find(f'.//*[@{{{ns["x"]}}}Name="{control}"]')
    assert element is not None, f'Missing control {control}'
    assert element.get(evt) == handler, f'{control}.{evt} not wired to {handler}'
    assert f'void {handler}(' in main, f'Missing method {handler}'
    if evt == 'Checked':
        assert element.get('Unchecked') == handler, f'{control}.Unchecked missing'

# Numeric and protocol identifiers must not be translated.
for s in ('h264', 'h265', 'sdk', 'uhid', '1920x1080'):
    assert s not in words or words[s] == s
print('PASS: XAML XML and Indonesian JSON parsed')
print('PASS: 11 persistent-control event handlers verified')
print('PASS: startup restoration, mode isolation, atomic saving and backup hooks verified')
print('PASS: Light=Terang theme, Light=Ringan thermal contextual translation')
