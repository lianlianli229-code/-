# 🍔 汉堡店主题视觉验收清单与素材许可证记录

参考基准图：`docs/references/burger_theme_reference.jpg`

## 一、 视觉 7 项逐项核对清单

| 编号 | 视觉要素 | 规范要求 | 对应代码实现位置 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| 1 | **遮阳篷斜纹顶檐** | 红橙白相间斜纹篷布顶檐，左右两侧带金色小圆铆钉 | `StickyTicketWindow.cs` (`BuildAwningHeader`) | ✅ 已实现 |
| 2 | **铆钉铭牌夹** | 顶部居中芝士金黄圆角铭牌夹，左右对称铆钉 + 汉堡图形 + 深棕粗体字 | `StickyTicketWindow.cs` (`BuildTopClipPlaque`) | ✅ 已实现 |
| 3 | **芝麻番茄红横幅** | 圆角番茄红 (`#D6372B`) 横幅，上半部散布白色椭圆芝麻粒矢量点缀，左侧纯白粗体单号与标题 | `StickyTicketWindow.cs` (`BuildMainRedBanner`) | ✅ 已实现 |
| 4 | **汉堡剪影暗纹** | 表头中右侧嵌入半透明暖橙色 (`#F08A24`) 汉堡矢量剪影，不遮挡左侧文字 | `StickyTicketWindow.cs` (`CreateBurgerSilhouetteCanvas`) | ✅ 已实现 |
| 5 | **深暗红内凹铆钉倒计时牌** | 表头右侧深暗红 (`#7A1F1A`) 内凹圆角牌，四角金属微铆钉 + 无障碍形状符号 + 剩余时间 | `StickyTicketWindow.cs` (`BuildCountdownPlaque`) | ✅ 已实现 |
| 6 | **芝士金黄外框与奶白进度子卡** | 整张小票采用芝士金黄 (`#F5B731`) 立体外包边；工序进度条置于独立奶白 (`#FFF6E5`) 圆角子卡片内 | `StickyTicketWindow.cs` (`BuildProgressCard`) | ✅ 已实现 |
| 7 | **交单印章与出餐铃声** | 点击交单盖上倾斜红色 `PERFECT! 100%` 印章并播放出餐铃声 | `StickyTicketWindow.cs` & `AudioAndUpdateService.cs` | ✅ 已实现 |

## 二、 素材与音效许可证记录（Asset Provenance & License）

1. **主题插画与工序图标（肉饼/生菜/芝士/辣椒、遮阳篷、芝麻、汉堡剪影）**：
   - **来源**：100% 项目内原生 C# / WPF `PathGeometry` 与 `Canvas` 矢量自绘（见 `src/Themes/SkinThemeManager.cs` 与 `src/UI/StickyTicketWindow.cs`）。
   - **许可证**：项目自有原创代码绘制，零外部版权依赖。
2. **音效（出餐叮铃声、勾选声、催单提示音）**：
   - **来源**：100% 项目内通过 PCM 正弦/三角波谐波包络实时在内存中合成 RIFF WAVE 音频流（见 `src/Services/AudioAndUpdateService.cs`）。
   - **许可证**：CC0 / 自生成算法合成音，零外部音频文件侵权风险。

## 三、 本地存储安全声明（Plaintext Storage Disclosure）

- 本软件将便签内容、工序步骤及粘贴的 JPEG/PNG 图片保存在当前 Windows 登录用户专属目录 `%LocalAppData%\StickyTickets\sticky_tickets.db` 中。
- V1 版本采用明文 SQLite 数据库存储（依赖 Windows 操作系统用户账户目录 ACL 权限隔离），请勿在公共无密码共享账户下存放机密凭证。
- 应用日志与错误提示已严格脱敏，绝不记录任何便签正文或图片内容。
