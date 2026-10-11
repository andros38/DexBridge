#!/usr/bin/env python3
"""Static regression checks for v0.13.9 theme palettes and WPF disabled control templates."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json,re,colorsys
root=Path(__file__).resolve().parents[1]
app=root/'App.xaml'
main=root/'MainWindow.xaml'
theme=root/'Services/ThemeService.cs'
content=theme.read_text(encoding='utf-8')
ns='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
x='{http://schemas.microsoft.com/winfx/2006/xaml}'
xml=ET.parse(app)
for name in ('MainWindow.xaml','PhoneFilesWindow.xaml','TelemetryOverlayWindow.xaml'):
 ET.parse(root/name)
styles={s.attrib['TargetType']:s for s in xml.iter(ns+'Style') if 'TargetType' in s.attrib}
assert all(c in styles for c in ('Button','ComboBox','CheckBox'))
for kind,expected in {
    'Button': [('ButtonChrome','Background','DisabledBackgroundBrush'),('ButtonChrome','BorderBrush','DisabledBorderBrush'),('ButtonContent','TextElement.Foreground','DisabledTextBrush')],
    'ComboBox': [('ComboChrome','Background','DisabledBackgroundBrush'),('ComboChrome','BorderBrush','DisabledBorderBrush'),('SelectedContent','TextElement.Foreground','DisabledTextBrush'),('ComboArrow','Stroke','DisabledTextBrush')],
    'CheckBox': [('CheckChrome','Background','DisabledBackgroundBrush'),('CheckChrome','BorderBrush','DisabledBorderBrush'),('CheckMark','Stroke','DisabledTextBrush'),('CheckContent','TextElement.Foreground','DisabledTextBrush')]
}.items():
    style=styles[kind]
    template=next(e for e in style.iter(ns+'ControlTemplate') if e.get('TargetType')==kind)
    named={e.get(x+'Name') for e in template.iter() if e.get(x+'Name')}
    inactive=[e for e in template.iter(ns+'Trigger') if e.get('Property')=='IsEnabled' and e.get('Value')=='False']
    assert len(inactive)==1, f'{kind} must have a disabled template trigger'
    setters={(e.get('TargetName'),e.get('Property'),e.get('Value')) for e in inactive[0].iter(ns+'Setter')}
    for target,prop,key in expected:
        assert target in named, (kind,target)
        assert (target,prop,f'{{DynamicResource {key}}}') in setters, (kind,target,prop,key)
    print(f'PASS: {kind} disabled templates force neutral chrome and muted text/indicators.')

button=styles['Button']
style_setters={s.get('Property'):s.get('Value') for s in button.findall(ns+'Setter')}
assert style_setters.get('Cursor')=='Hand'
assert any(setter.get('Property')=='Cursor' and setter.get('Value')=='Arrow' for trigger in button.iter(ns+'Trigger') if trigger.get('Property')=='IsEnabled' and trigger.get('Value')=='False' for setter in trigger.iter(ns+'Setter'))
template=next(e for e in button.iter(ns+'ControlTemplate'))
for trigger in template.findall('.//'+ns+'MultiTrigger'):
    conditions={(c.get('Property'),c.get('Value')) for c in trigger.iter(ns+'Condition')}
    if ('IsMouseOver','True') in conditions or ('IsPressed','True') in conditions:
        assert ('IsEnabled','True') in conditions, 'Disabled hover/pressed must not recolor controls'
assert 'x:Name="VersionCreditText"' in main.read_text(encoding='utf-8')
translations=json.loads((root/'Languages/id-ID.json').read_text(encoding='utf-8'))
assert 'Versi aplikasi © Ahmad Asyhari' not in translations
assert 'Created by © Ahmad Asyhari' not in translations

# Same theme palette resource names must be updated dynamically when switching theme.
for brush in ('ButtonBackgroundBrush','DisabledBackgroundBrush','DisabledTextBrush','DisabledBorderBrush','ButtonHoverBrush','ButtonPressedBrush'):
    assert f'SetBrush("{brush}",' in content
assert 'var button = oled ?' in content and 'var disabledBg = oled ?' in content
assignments={}
for key in ('button','disabledBg','disabledText','disabledBorder'):
 m=re.search(rf'var {key} = oled \? "(#[A-Fa-f0-9]{{6}})" : dark \? "(#[A-Fa-f0-9]{{6}})" : "(#[A-Fa-f0-9]{{6}})";', content)
 assert m,key
 assignments[key]=dict(oled=m.group(1),dark=m.group(2),light=m.group(3))
def sat(c):
 vals=[int(c[i:i+2],16)/255 for i in (1,3,5)]
 return colorsys.rgb_to_hsv(*vals)[1]
for name in ('oled','dark','light'):
 enabled=assignments['button'][name]
 disabled=assignments['disabledBg'][name]
 assert len({enabled,disabled})==2
 assert sat(enabled)>sat(disabled)+0.05, (name,enabled,disabled)
 assert sat(assignments['disabledBorder'][name])<0.02
 assert sat(assignments['disabledText'][name])<0.02
 print(f'PASS: {name} enabled buttons are accented; disabled chrome/text stay gray.')
print('PASS: version and copyright text are provided by runtime metadata, same for both language settings.')
print('PASS: disabled state has no hover/pressed visuals; cursor differs on enabled vs disabled buttons.')
print('NOTE: visual/keyboard runtime verification requires Windows WPF and real hardware.')
