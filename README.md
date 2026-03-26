# Controller Desktop

一个面向 Windows 10/11 的手柄桌面映射工具。运行时常驻托盘，默认支持 XInput；配置通过本地网页完成，规则保存到本地 JSON。

## 功能

- XInput 手柄输入轮询
- 键盘、组合键、鼠标移动、鼠标按键、滚轮、系统动作映射
- `单击 / 双击 / 长按 / 持续` 触发方式
- 全屏游戏场景自动停用映射
- 开机自启与托盘常驻
- 本地网页读取、编辑、导入、导出配置

## 运行

```powershell
dotnet build ControllerDesktop.sln -c Debug
.\src\ControllerDesktop\bin\x64\Debug\net7.0-windows\ControllerDesktop.exe
```

发布版入口：`dist/ControllerDesktop/ControllerDesktop.exe`

## 主要目录

- `src/ControllerDesktop/Models.cs`：配置模型与运行状态
- `src/ControllerDesktop/Interop.cs`：Win32 / XInput / SendInput 互操作
- `src/ControllerDesktop/Services.cs`：托盘、配置、输入注入、运行时协调
- `src/ControllerDesktop/WebEditorHost.cs`：本地网页站点与 API
- `src/ControllerDesktop/Web/`：配置网页资源

## 配置文件

默认保存在：`%AppData%\ControllerDesktop\settings.json`
