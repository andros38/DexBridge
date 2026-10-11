#!/usr/bin/env python3
"""Version label is derived from project assembly metadata, not a frozen UI string."""
from pathlib import Path
from xml.etree import ElementTree as ET
import re
root=Path(__file__).resolve().parents[1]
project=ET.parse(root/'DexBridge.csproj').getroot()
version=project.find('PropertyGroup/Version').text
assert re.fullmatch(r'\d+\.\d+\.\d+(?:-[\w.-]+)?', version), version
assert project.find('PropertyGroup/AssemblyVersion').text.startswith(version+'.')
view=ET.parse(root/'MainWindow.xaml').getroot()
ns={'w':'http://schemas.microsoft.com/winfx/2006/xaml/presentation','x':'http://schemas.microsoft.com/winfx/2006/xaml'}
credit=view.find('.//w:TextBlock[@x:Name="VersionCreditText"]',ns)
assert credit is not None
assert credit.get('Text') == '© Ahmad Asyhari'
code=(root/'MainWindow.xaml.cs').read_text(encoding='utf-8')
for token in ('GetCustomAttribute<AssemblyInformationalVersionAttribute>()', "InformationalVersion.Split('+')[0]", 'VersionCreditText.Text = $"v{currentVersion} © Ahmad Asyhari"'):
    assert token in code, token
assert f'v{version} © Ahmad Asyhari' not in code, 'Avoid a hardcoded version in code-behind'
print(f'PASS: build version {version} is automatically included in visible credit.')
print(f'EXPECTED CURRENT LABEL: v{version} © Ahmad Asyhari')
print('NOTE: WPF UI execution still requires Windows/.NET 10.')
