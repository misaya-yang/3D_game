# G09-01 · 第三方 UI 资产登记

## 许可结论

本 Goal 只引入 Kenney 官方资产页中标注为 Creative Commons CC0 的 UI 面板和通用图标。Kenney 官方支持页说明资产页内容可用于个人及商业项目，且不强制署名；仓库仍保留原始许可文本、来源、选用文件和哈希。

## 来源

| 包 | 官方页面 | 下载包 SHA-256 | 仓库许可 |
|---|---|---|---|
| Kenney UI Pack (RPG Expansion) 1.0 | <https://kenney.nl/assets/ui-pack-rpg-expansion> | `c69c30c09d74df542842e4ec811735b6d260cd6c9e2ee261d7b894d259a6adb4` | `Assets/_Project/Art/ThirdParty/Licenses/Kenney_UI_Pack_RPG_Expansion_CC0.txt` |
| Kenney Game Icons 1.0 | <https://kenney.nl/assets/game-icons> | `7a86d8d58e0b851e22004b3c70bf90b003632bbf9ac633424daa3bb17d9e7e4e` | `Assets/_Project/Art/ThirdParty/Licenses/Kenney_Game_Icons_CC0.txt` |

官方支持说明：<https://kenney.nl/support>。

## 实际选用

- `Resources/UI/G09/Frames/`：11 个棕/米色九宫格面板、按钮、箭头和确认/关闭图形。
- `Resources/UI/G09/Icons/`：23 个白色 2× 通用 UI 图标；运行时统一染为米白、金色、玉色或状态色。
- 未把两个完整压缩包或未使用的数百个文件提交进 Unity 工程。

逐文件 SHA-256 已写入 `Assets/_Project/Art/ThirdParty/UI_SHA256SUMS`，由 `tools/validate_g09_01.sh` 校验。
