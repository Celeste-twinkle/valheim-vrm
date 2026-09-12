# Optional server avatar synchronization / 可选服务器外观同步

## 中文

客户端和服务器推荐使用 1.8.1；同步协议仍兼容 1.8.0 客户端。参与外观同步的玩家在自己的游戏根目录安装同一套
`ValheimVRM` 文件夹，模型文件名（含大小写）及 VRM 内容必须一致。
各客户端仍可保留自己的角色选择、渲染开关和物理摆动权重。

1. 服务器先安装兼容的 BepInEx 5，再将 `ValheimVRM-Server-1.8.1.zip`
   解压到服务器程序所在目录。目标为
   `BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`。
   服务器只需该同步插件，不需要客户端的着色器、UniVRM DLL 或角色模型。
2. 参与外观同步的玩家安装完整客户端 `ValheimVRM-1.8.1.zip` 和相同的模型文件夹。
3. 重启服务器和客户端。进入世界后，F8 应显示“服务器同步已连接”。
4. 保持“服务器外观同步（服务器支持时）”勾选，点击模型。只有进行切换的
   玩家改变外观；其他玩家各自的选择不变。

玩家 A 选模型 1、玩家 B 选模型 2 时，所有参与同步的客户端都显示
“A = 模型 1，B = 模型 2”。即使 A、B 的昵称相同，或都选择模型 1，
它们也对应不同的角色实例。后来加入的玩家会收到当前选择；离开视野后
重新加载、死亡重生和重连时，按新的角色网络 ID 恢复对应选择。

服务器未安装同步插件时，F8 自动显示本地模式，切换仍可正常使用。
取消勾选同步也会进入本地模式：本机保留自己的模型，停止接收其他玩家的
选择，并通知服务器清除自己的公开选择。其他客户端眼中的该玩家恢复为原版外观。
服务器未安装此插件时，客户端不会发送本协议的模型选择请求。

未安装客户端 Mod 的玩家也能正常加入：服务端不修改登录、版本校验或踢人逻辑。
原版客户端忽略未知 RPC，最多三次探测后服务端停止探测，不发送模型快照；
这类玩家看到原版角色，自己的外观也保持原版。此兼容路径有受控引擎测试覆盖。

服务器只中转模型名称与 SHA-256 指纹，不接收、导入或分发 VRM 文件。
接收方缺少模型或文件指纹不一致时，显示提示并保留已有可用外观（首次
加载时为原版角色）。请统一文件夹并重启客户端，防止旧模型缓存继续生效。
F8 的“刷新列表”可重新尝试之前缺失的模型。设置文件也应由服主统一分发，
以保持模型比例和装备偏移一致；协议校验 VRM 内容，不强制统一本机画面设置。

服务器配置：`BepInEx/config/com.celestetwinkle.valheimvrm.server.cfg`，
`[Sync] Enabled = true`。客户端配置：
`BepInEx/config/com.yoship1639.plugins.valheimvrm.cfg`，
`[AvatarSync] Enabled = true`。客户端 F8 勾选框会保存该配置。

通过游戏客户端“启动服务器”的房主，也可在自己的客户端同时安装服务器
插件；仅房主需要这个附加 DLL。未安装服务器插件的普通本地游戏照常运行。
Linux 服务器使用同一个托管 DLL，但本次引擎验证环境为 Windows/D3D11。

旧版上传整份 VRM 的共享流程默认停用。`global_settings.txt` 中的
`EnableLegacyVrmSharing=false` 应保持默认；它与这里的同文件夹同步是不同协议。
协议最多记录 128 个同时在线的同步玩家，并限制消息大小和应用频率。

## English

Install the full 1.8.1 client on every participating player and distribute an identical top-level
`ValheimVRM` model folder. File names are case-sensitive protocol identifiers;
SHA-256 must match the sender's VRM. Model settings should also be distributed
consistently. Personal avatar selections, lighting and physics-weight preferences
remain local.

The dedicated server needs BepInEx 5 and only the DLL from
`ValheimVRM-Server-1.8.1.zip`, under `BepInEx/plugins/ValheimVRM.Server/`.
It does not load avatar files, UniVRM or shaders. Restart, join, and enable
**Server avatar sync** in F8. A client-hosted server can install the same server
addon alongside its client plugin.

Unmodded clients may join normally and see vanilla characters. Discovery stops
after three unanswered messages, and no avatar snapshot is sent without the mod
handshake. Admission and game version checks are unchanged; missing this optional
addon never triggers a disconnect. Protocol version 1 remains compatible with
1.8.0 clients.

The server authenticates the sender by its connection, validates ownership of its
character ZDO, and broadcasts a per-player snapshot. A selecting model 1 never
changes B. Equal names and equal model choices still use independent character
IDs and independent avatar instances. Snapshots support late joins, respawns,
reconnections and reappearing remote players.

Without the addon, local selection works without sending selection requests.
Opting out preserves your local appearance, withdraws your shared choice and
restores remote players to their original visuals. Missing or different files
retain the last usable appearance; update the shared folder and restart to clear
cached imports. VRM bytes are never transferred by this protocol.

Server: `[Sync] Enabled` in `com.celestetwinkle.valheimvrm.server.cfg`.
Client: `[AvatarSync] Enabled` in `com.yoship1639.plugins.valheimvrm.cfg`.
Keep the old `EnableLegacyVrmSharing` option disabled. The protocol is bounded to
128 active synchronized players. See the release validation document for the
tested scope; real dedicated-server login and Linux are separate deployment checks.
