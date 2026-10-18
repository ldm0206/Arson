# Arson（纵火）

《城市：天际线》（Cities: Skylines）模组：按指令点燃建筑物和树木，支持**框选批量点火**。

基于 [algernon-A/Arson](https://github.com/algernon-A/Arson) 修改，新增框选点火与简体中文翻译。

## 功能

- 左键单击，点燃悬停的建筑物或树木
- 按住左键拖出方框，一次性点燃框内所有建筑物和树木
- 支持简体中文、英文、日文、俄文
- 快捷键可自定义

## 安装

1. **先关闭游戏**
2. 打开 PowerShell，粘贴运行以下命令（自动下载最新版并解压到 Mods 目录）：

```powershell
$dest = "$env:LOCALAPPDATA\Colossal Order\Cities_Skylines\Addons\Mods"
New-Item -ItemType Directory -Force $dest | Out-Null
Invoke-WebRequest "https://github.com/ldm0206/Arson/releases/latest/download/Arson.zip" -OutFile "$env:TEMP\Arson.zip"
Expand-Archive "$env:TEMP\Arson.zip" -DestinationPath $dest -Force
```

3. 启动游戏 → **内容管理（Content Manager）→ 模组（Mods）** → 找到 **Arson** 并启用

<details>
<summary>手动安装</summary>

从 [Releases](https://github.com/ldm0206/Arson/releases/latest) 下载 `Arson.zip`，解压后得到一个 `Arson` 文件夹，将其整个放到：

```
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\
```

最终路径形如 `...\Addons\Mods\Arson\Arson.dll`。

</details>

### 注意事项

- **不要与创意工坊中的原版 Arson 同时启用**——两者使用相同的 Harmony ID，会冲突
- Harmony 依赖由 `CitiesHarmony.API` 在启动时自动安装，无需手动订阅
- **点燃树木需要 Natural Disasters（自然灾害）DLC**：游戏底层的 `TreeManager.BurnTree` 要求该 DLC，未安装时树木点火会静默失败，建筑物不受影响
- 订阅了 UnifiedUI 的话，工具按钮会出现在 UUI 面板中；否则使用快捷键即可

## 使用

1. 按 `Alt+I`（默认快捷键，可在模组设置中修改）激活纵火工具
2. 左键单击点燃单个目标；或按住左键拖出方框，松开后点燃框内所有目标
3. 按 `Esc` 退出工具

语言在 **内容管理 → Arson 设置** 中切换（含简体中文）。

## 更新

重新运行上面的安装命令即可覆盖更新。

## 构建

项目通过 GitHub Actions 自动编译：每次推送到 `master` 都会编译、打包并发布新的 Release（标签格式 `v<版本>-<构建号>`），PR 只做编译验证。

本地构建需要：

- Visual Studio 2017+（MSBuild）与 NuGet
- 游戏程序集放在仓库 `Managed/` 目录（`Assembly-CSharp.dll`、`ColossalManaged.dll`、`ICities.dll`、`UnityEngine.dll`）

```powershell
nuget restore Arson.sln
msbuild Arson.sln /p:Configuration=Release
```

编译后会自动复制到游戏 Mods 目录；加 `/p:SkipDeploy=true` 可跳过。

## 许可

见 [License.txt](License.txt)。原模组作者：algernon。
