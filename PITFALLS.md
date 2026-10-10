# 开发踩坑记录（PITFALLS）

> 每踩一个坑记一条，避免重复。按"现象 → 根因 → 正确做法"记录。

## 一、图标相关（本次重灾区）

### 1. 任务栏图标越改越小 / 显示成小圆环
- **现象**：改了好几轮 GDI+、多尺寸 ICO，任务栏图标还是小、Alt+Tab 显示空白文件图标。
- **根因**：改错了对象。**任务栏按钮图标、Alt+Tab 预览图标来自 WPF 窗口的 `Window.Icon` 属性**，不是托盘 `NotifyIcon`。我一直在改托盘，没动窗口图标。
- **正确做法**：
  - 窗口图标用 `.csproj` 的 `<ApplicationIcon>app.ico</ApplicationIcon>` 嵌入标准 .ico 资源，WPF 任务栏/Alt+Tab/标题栏**自动**用它。
  - 不要在运行时用 MemoryStream 画 BitmapFrame 赋给 `Window.Icon`——WPF 对运行时生成的图标有 DPI 缩放和缓存坑（高 DPI 下缩小、Alt+Tab 回退成默认空白文件图标）。

### 2. 图标毛边（小图标拉大）
- **现象**：图标清晰但边缘有毛边，像小图生生拉大。
- **根因**：从 16px 小图画起，Windows 拉大到 32/48 时放大模糊。
- **正确做法**：**从 256px 高清母版画起，高质量缩小到 16/24/32/48/64/128/256**。Pillow 生成 ICO 时 sizes 列全，但源图必须是高清母版缩小，不能从小图放大。

### 3. 托盘图标显示成白色方块
- **现象**：托盘图标是白色方块，圆在中间。
- **根因**：`Bitmap.GetHicon()` 生成的 HICON **丢失 alpha 透明通道**，透明区域显示成白色。
- **正确做法**：托盘用多尺寸 ICO（PNG 嵌入，Vista+ 原生支持 alpha），不要用 GetHicon。

### 4. 托盘图标尺寸
- **正确做法**：托盘 NotifyIcon 用多尺寸 ICO（16/24/32/48 PNG 嵌入），Windows 按系统 DPI 自动选最合适尺寸，不要只给单尺寸。

## 二、WPF 深色主题

### 5. ComboBox 下拉框白底白字
- **现象**：深色主题下，下拉框弹出是白色背景+白色文字，看不清。
- **根因**：WPF ComboBox 默认弹出面板（Popup/ComboBoxItem）用系统白色，主题 DynamicResource 覆盖不到。
- **正确做法**：**完整重写 ComboBox 模板**（ToggleButton + Popup + ComboBoxItem 触发器），背景/文字/选中态全部用 DynamicResource。

### 6. 设置对话框"应用"按钮
- **正确做法**：标准三按钮——**取消**（放弃关闭）/ **应用**（立即生效不关闭，边调边看）/ **保存**（生效并关闭）。ViewModel 拆 SaveCommand（保存+关闭）和 ApplyCommand（只保存）。

## 三、架构与流程

### 7. 改一处冒新 bug（用户最反感）
- **根因**：UI 层和业务逻辑耦合，改 UI 顺手动了业务。
- **正确做法**：严格分层（Domain 纯逻辑 / Core 状态机 / Infrastructure 持久化 / App 表现层），单向依赖。改 UI 只动 App 层；改完必须跑全部单元测试（当前 57 个）+ 启动冒烟。

### 8. "自我感动说解决了实际没解决"
- **教训**：每次改完必须**逐项实测**（启动日志、进程存活、真实功能操作），不能只看编译通过。用户截图是唯一验收标准。

## 四、环境与发布

### 9. GitHub 推送
- **现象**：`github.com` 主站 443 连接超时（被墙），但 `api.github.com` 通。
- **根因**：系统开了代理（127.0.0.1:7890）但 git 没配代理。
- **正确做法**：`git config --global http.proxy http://127.0.0.1:7890` 后可正常 push。
- 备选：走 api.github.com 的 Git Data API（blobs/trees/commits/refs），用 dulwich 读本地对象上传。

### 10. 便携包体积
- **正确做法**：C# WPF `.NET 8` `self-contained false`（依赖系统运行时），exe 仅 ~150KB，zip ~411KB。Python+PySide6 方案 ~100MB，不要用。

### 11. 提交方式
- **现象**：`git.exe` 写入被本机安全软件按进程拦截。
- **正确做法**：用 `python git_commit.py` 提交（dulwich 写）；只读 git 命令（log/remote）可用。

## 五、悬浮窗透明与圆角（浅色主题重灾区）

### 12. 卡片半透明导致背景纹理/发灰
- **现象**：浅色主题下主界面出现灰色棋盘格纹理。
- **根因**：窗口是 `AllowsTransparency=True` 的圆角悬浮窗，而卡片背景色用了半透明白（96%）。DWM 把半透明卡片和桌面做 per-pixel alpha 合成，浅色桌面上合成出异常纹理；深色下因深灰叠黑桌面看不出来。
- **正确做法**：**悬浮卡片在 AllowsTransparency 窗口里必须用完全不透明色**（Light 纯白 / Dark 深灰）。卡片是"盖在桌面上的实心层"，不该透出后面。

### 13. 浅色桌面上 DropShadowEffect 必然显脏
- **现象**：圆角卡片弧形外侧一圈淡灰晕，怎么调 Opacity 都不干净。
- **根因**：`DropShadowEffect` 从卡片边缘向外扩散阴影，浅色/白桌面上这团阴影就是灰晕；这是"分层投影"在浅色下的固有问题，不是 bug。
- **正确做法**：悬浮小卡片**不要用阴影**，改用 1px 极淡 `BorderBrush` 描边界定边界。需要悬浮感时也只给大卡片用很淡的阴影，小弹窗直接纯描边。
