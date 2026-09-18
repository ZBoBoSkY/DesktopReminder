# -*- coding: utf-8 -*-
import io

p = r'src\DesktopReminder.App\Views\MainWindow.xaml'
s = io.open(p, encoding='utf-8').read()
old = 'Converter={StaticResource InvertBoolToVis}"/>'
new = 'Converter={StaticResource InvertBoolToVis}}"/>'
count = s.count(old)
s = s.replace(old, new)
io.open(p, 'w', encoding='utf-8').write(s)
print(f'replaced {count} occurrences')
