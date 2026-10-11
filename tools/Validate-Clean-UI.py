#!/usr/bin/env python3
"""Static checks: short bilingual UI, no broken WPF references, preserved mode IDs."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json,re
root=Path(__file__).resolve().parents[1]
main=root/'MainWindow.xaml'
text=main.read_text(encoding='utf-8')
code=(root/'MainWindow.xaml.cs').read_text(encoding='utf-8')
files=(root/'PhoneFilesWindow.xaml').read_text(encoding='utf-8')
trans=json.loads((root/'Languages/id-ID.json').read_text(encoding='utf-8'))
xml=ET.parse(main)
ET.parse(root/'PhoneFilesWindow.xaml')
ns={'w':'http://schemas.microsoft.com/winfx/2006/xaml/presentation'}
assert 'x:Name="VersionCreditText"' in text
assert 'Text="© Ahmad Asyhari"' in text
assert 'Versi aplikasi © Ahmad Asyhari' not in text
assert 'Versi aplikasi © Ahmad Asyhari' not in trans
assert 'Samsung DeX over USB — desktop, app streaming' not in text
mode=xml.find('.//w:ComboBox[@{http://schemas.microsoft.com/winfx/2006/xaml}Name="SessionModeCombo"]',ns)
assert [x.attrib['Tag'] for x in mode]==['desktop','app','mirror']
assert [x.attrib['Content'] for x in mode]==['DeX desktop','App / Game','Screen mirroring']
assert 'MirroringInfoText' not in text and 'MirroringInfoText' not in code
assert 'StreamDescriptionText' not in text and 'StreamDescriptionText' not in code
assert 'DisplayStreamGroup.ToolTip = mirrorMode' in code
assert 'SessionModeCombo.ToolTip = mirrorMode' in code
assert 'Text="Thermal status is the current' not in text
assert 'ToolTip="Thermal status is the current' in text
assert 'Text="Stream FPS history only;' not in text
assert 'ToolTip="Stream FPS history only;' in text
assert 'Text="Transfer files between' not in files
assert 'ToolTip="Transfer files between' in files
assert not any(len(v)>115 for node in xml.iter() for k,v in node.attrib.items() if k=='Text'), 'Long visible text remains in main XAML'
for key in ['App / Game','Screen mirroring','Forward PC gamepad (UHID)',
            'Prevent video downscaling','Performance telemetry','Save FPS log',
            'Night contrast','Keep phone screen off','Keep Android awake','Force DPI',
            'Return apps on Stop','Retry failed handoff','Disable mouse hover','Audio to PC',
            'Mirror the phone screen (display 0).','Open one app directly on a virtual display.',
            'Open the Samsung DeX desktop.',
            "Mirroring uses the phone's resolution and DPI. Encoding options still apply.",
            'Resolution and DPI change the virtual display. New settings apply on the next session.']:
 assert key in trans, f'Missing translation: {key}'
# All named XAML controls referenced by new code exist; avoid stale fields.
for name in ('DisplayStreamGroup','SessionModeCombo','PhoneScreenButton','ResolutionCombo','DpiCombo'):
 assert f'x:Name="{name}"' in text
assert '<Version>0.13.17</Version>' in (root/'DexBridge.csproj').read_text()
print('PASS: version-derived application credit in both languages, compact session modes, key translations.')
print('PASS: explanatory paragraphs hidden in tooltips; no obsolete controls or long XAML Text remain.')
print('PASS: preserved desktop/app/mirror IDs; WPF XML and control names validated.')
print('NOTE: WPF visual test and real-device behavior require Windows/Galaxy.')
