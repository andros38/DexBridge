from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
code = (root/'MainWindow.xaml.cs').read_text(encoding='utf-8')
project=(root/'DexBridge.csproj').read_text(encoding='utf-8')
build=(root/'tools/Build-Portable.ps1').read_text(encoding='utf-8')
ET.parse(root/'MainWindow.xaml')
ET.parse(root/'App.xaml')
assert '<Version>0.13.17</Version>' in project
assert 'DexBridge-v$AppVersion-win64.zip' in build
assert 'Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.MonitorIntervalSeconds, 4, 15))' in code
assert '_screenGuardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }' in code
assert 'DateTime.UtcNow - _lastRunningAppsPollUtc >= TimeSpan.FromSeconds(8)' in code
assert '_dashboardPollBusy' in code and 'finally { _dashboardPollBusy = false; }' in code
assert 'if (_session?.IsRunning == true) await EnforcePhoneScreenGuardAsync();' in code
assert 'while (_recentStreamFps.Count > 15) _recentStreamFps.Dequeue();' in code
assert '_recentStreamFps.Average()' in code
assert 'SelectedSessionMode() != "app"' in code
assert 'DateTime.UtcNow - _sustainedThermalSinceUtc.Value >= TimeSpan.FromSeconds(20)' in code
assert 'now - _lastThermalAdvisoryUtc < TimeSpan.FromMinutes(2)' in code
assert 'THERMAL ADVISORY' in code
assert 'if (_sessionLogLineCount >= 2000)' in code
assert 'if (!ReferenceEquals(session, _session)' in code
assert '_screenGuardTimer.Stop();' in code
assert (root/'runtime/scrcpy/scrcpy.exe').exists()
assert (root/'runtime/scrcpy/adb.exe').exists()
print('PASS: v0.13.5 static checks — scheduling, guard, FPS average, thermal, bounded logs, session query state and runtime files')
print('LIMITATION: .NET 10 WPF compilation and physical Galaxy testing require Windows.')
