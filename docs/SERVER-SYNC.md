# Optional server avatar synchronization / 可选服务器外观同步

## 中文

### 原始角色与背负装备（1.8.15+）

F8 顶部的“原始角色模型”会撤回自己的公开 VRM 选择，并恢复原版身体和装备；不会关闭接收其他玩家外观的同步开关。空选择沿用递增请求序号，迟到的旧 VRM 请求不能恢复已撤回的外观；重生／重新加入时保持原始模型偏好。

背负装备的等比缩放、X／Y／Z 偏移与其他校准一起同步，按玩家独立绑定。发送端和观察端需升级到 1.8.15+；服务端 1.8.14 也能中转这些附加数据。旧观察端继续显示其支持的模型／校准功能，无法应用新增背负控件。建议统一升级完整客户端和服务端包到 1.8.16。

### 全部校准滑块同步（1.8.14+）

服务器、发送方和观察方客户端都升级到 **1.8.14 或更新版本**。F8 显示“姿态、道具与物理设置已按玩家同步”时，以下设置随各自玩家同步：

- 模型身高，站姿／坐姿高度偏移。
- 所有动画状态及混合片段的 X／Y／Z 偏移。
- 左手、右手、双手道具各自的等比缩放与 X／Y／Z 偏移。
- 物理摆动权重，以及已有模型设置中的整体位置和握持／姿态偏移。

拖动期间本机预览，松开后发送完整设置；不变时不重复发送。模型、身高和完整校准共用一条递增请求序号，后发先到时旧请求不能覆盖其中任一部分。下行快照用 revision 比较；较大配置分片后完整接收才原子应用，残缺数据不替换有效状态。重生、重新显示和后来加入的观察者都读取该玩家的最新完整设置。

例如 A、B 使用同一 VRM，A 身高 1.4 米、右手缩放 80%，B 身高 2 米、右手缩放 120%，所有参与完整同步的观察者会分别使用这些值。接收配置绑定到经过身份与角色网络 ID 校验的独立显示实例，不修改本机自己的 TXT／JSON，不共享其他玩家的设置对象。仅改变微调时直接更新原实例，不重载模型；身高／模型变化时按原始骨架参考重新进行初始标定，绝不累计上一次修正。

自动标定由各端依据相同 VRM、身高和算法计算；同步的是设置，不是逐帧物理骨骼结果。动画播放和弹簧模拟保留游戏自身的网络／本地时序。渲染开关仍是观察者的画面偏好。旧客户端／旧服务器保留原有模型或身高能力，F8 提示完整校准不可用；缺少该能力不会阻止入服。

### 玩家身高同步（1.8.12+）

服务端、发送方和观察方客户端均升级到 1.8.12 后，F8 显示“玩家身高同步已启用”。模型身高范围 1.4–2.2 米，默认 2 米；A 的身高只作用于 A，B 的身高只作用于 B，即使用同一模型也不共享实例缩放。身高与模型选择一起使用递增请求序号，防止乱序覆盖；服务器快照保留每位玩家的身高，支持重生和后来加入。

发送端松开滑块后重新标定该玩家显示实例，观察方收到新身高后按相同加载流程标定脚底和坐姿。身高按本机游戏角色保存到 `BepInEx/config/ValheimVRM/avatar_heights.json`；不会写入其他玩家的配置。1.8.14 起站姿／坐姿微调也随该玩家同步。

无服务端插件、关闭同步或旧服务器时仍可本地调整。旧版本继续使用原模型同步格式；新版观察者对旧发送方采用默认 2 米，旧观察者无法显示自定义身高。只有协商成功的客户端接收带身高的新快照；不要求未装 Mod 的玩家安装插件。

### 安装

前置下载：[Valheim 专用 BepInEx 整合包（推荐）](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) · [BepInEx 5.4.23.3 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) · [全部 Release](https://github.com/BepInEx/BepInEx/releases)。验证版本为整合包 5.4.2333／核心 5.4.23.3。

**本地 `10_ValheimVRM_Server_1.8.16.zip` 已附带 Windows x64 BepInEx 5.4.23.3。** 首次安装时，停止服务器，将包内 `BepInEx`、`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version` 放到 `valheim_server.exe` 同级目录，再按原启动方式启动。已有兼容 BepInEx 时，只更新 `BepInEx/plugins/ValheimVRM.Server`，保留原加载器、配置和其他插件。GitHub 公开包 `ValheimVRM-Server-1.8.16.zip` 不附带前置；Linux 需要按专用整合包说明安装对应平台加载器。

客户端和服务器推荐使用 1.8.15；同步协议仍兼容 1.8.0 客户端。推荐为参与外观同步的玩家统一分发
`ValheimVRM` 文件夹，但文件夹不一致不是入服限制：缺失、多出或不同的文件都不影响连接和正常游戏。
只有显示某位玩家选择的模型时，接收方才需要该文件的相同名称（含大小写）和 VRM 内容。
客户端模型目录只需 `.vrm`，不需要任何 TXT 或 JSON；服务端也不需要这些文件。
个人选项由客户端按需保存在 `BepInEx/config/ValheimVRM`，各玩家保留自己的选择、渲染开关和校准偏好；收到的校准／物理权重只应用到发送者的显示实例。

1. 使用 GitHub 公开包时，服务器先安装兼容的 BepInEx 5，再将 `ValheimVRM-Server-1.8.16.zip`
   解压到服务器程序所在目录。目标为
   `BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`。
   服务器只需该同步插件，不需要客户端的着色器、UniVRM DLL 或角色模型。
2. 参与外观同步的玩家安装完整客户端 `ValheimVRM-1.8.16.zip` 和相同的模型文件夹。
3. 重启服务器和客户端。进入世界后，F8 应显示“服务器同步已连接”。
4. 保持“服务器外观同步（服务器支持时）”勾选，点击模型。只有进行切换的
   玩家改变外观；其他玩家各自的选择不变。

玩家 A 选模型 1、玩家 B 选模型 2 时，所有参与同步的客户端都显示
“A = 模型 1，B = 模型 2”。即使 A、B 的昵称相同，或都选择模型 1，
它们也对应不同的角色实例。后来加入的玩家会收到当前选择；离开视野后
重新加载、死亡重生和重连时，按新的角色网络 ID 恢复对应选择。

1.8.3 起，发送端与服务器都更新后会自动启用递增请求序号。F8 显示
“请求顺序保护已启用”。每条实际发送的选择请求使用一个更大的正整数，服务器按
当前网络连接独立记录最大序号，只接受比它更大的请求。先发 A（序号 1）、后发 B
（序号 2），即使 B 先到，迟到的 A 也不会覆盖 B；重复序号同样丢弃。
序号在死亡重生、刷新列表和开关同步时保留，仅新连接重置。旧连接消息不能影响
新连接。关闭同步也使用序号，避免迟到请求恢复已撤回的选择。下行快照继续按
服务端 revision 比较，新客户端同时忽略更旧和相同版本的快照。

1.8.0–1.8.2 仍可联机，未安装 Mod 的玩家也可正常加入。但发送端或服务器为旧版时，
该连接的上行请求仍按到达顺序处理，不具备新增的请求顺序保护。新版客户端连接
旧服务器时，F8 会提示兼容模式；建议同时更新客户端完整包和服务器包。
这里按实际网络连接绑定序号，不使用各电脑的时间戳或游戏帧号。

服务器未安装同步插件时，F8 自动显示本地模式，切换仍可正常使用。
取消勾选同步也会进入本地模式：本机保留自己的模型，停止接收其他玩家的
选择，并通知服务器清除自己的公开选择。其他客户端眼中的该玩家恢复为原版外观。
服务器未安装此插件时，客户端不会发送本协议的模型选择请求。

未安装客户端 Mod 的玩家也能正常加入：服务端不修改登录、版本校验或踢人逻辑。
原版客户端忽略未知 RPC，最多三轮探测后服务端停止探测；每轮包含完整校准、身高、请求序号和旧格式四条消息，
不发送模型快照；
这类玩家看到原版角色，自己的外观也保持原版。此兼容路径有受控引擎测试覆盖。

服务器只中转模型名称、SHA-256 指纹、身高与校准数值，不接收、导入或分发 VRM 文件。
服务器自己的模型文件夹可以不存在或与任一客户端不同，它不参与模型校验。

| 接收方文件情况 | 行为 |
| --- | --- |
| 所选模型同名且内容相同，其他文件不同 | 正常显示该玩家的模型；额外文件不影响同步。 |
| 文件或整个模型目录不存在、文件已删除或暂时无法读取 | 跳过该次外观更新；保留该玩家已有可用外观，首次加载显示原版角色。 |
| 同名 VRM 内容不同或本地旧缓存不同 | 跳过该次更新，不导入错误文件，不修改其他玩家的外观。 |
| 仅个人校准配置不同 | 1.8.14+ 使用该玩家发送的校准和物理权重；不覆盖观察者自己的配置文件。 |

未缓存模型先检查将要导入的字节指纹，再读取模型配置和导入。失败选择只提示一次，
不会反复导入，也不会阻塞其他玩家切换、影响登录或踢人。补齐或修正未导入文件后，
F8 的“刷新列表”会重试；已经导入缓存的同名文件被替换时需要重启客户端。
无需统一分发校准配置：完整校准同步为每位玩家独立传递数值。

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

1.8.15 adds a persistent **Original character model** list entry. It sends an
empty sequenced selection while keeping synchronization enabled, so observers
restore the native character and the sender can still see other avatars.
Back-equipment scale/XYZ are additional per-player calibration controls, requiring
sender/viewer 1.8.15+. Existing 1.8.14 relays preserve the additive reserved
settings entries; older viewers ignore the new back controls. Updating all
participating packages to 1.8.15 is recommended.

Full calibration synchronization requires **server, sender and viewer 1.8.14+**.
It carries standing/sitting offsets, every animation state/blend-clip XYZ offset,
left/right/two-handed uniform scales and XYZ offsets, physics sway weight and
legacy model/pose/grip offsets, together with model and height. F8 indicates
whether calibration synchronization is available.

Changes preview locally while dragging and are sent on release; unchanged state
is not resent. Each complete visual selection uses one increasing sequence.
Malformed, stale or downgraded requests cannot replace newer settings. Snapshot
format 3 carries the complete per-player values, with bounded 48 KiB parts for
large snapshots and atomic revision-checked reassembly. Maximum calibration is
768 entries / 128 KiB decoded per player; invalid/nonfinite/out-of-range values
are rejected before advancing request order.

Same-model players have separate received settings and profile objects. Controls
update their existing instances in place, preserving held-item group references;
model/height changes reattach and calibrate from the original reference, never
from previously corrected transforms. Received settings never write personal
configuration. Late join, respawn and external reattachment preserve the latest
values. Automatic calibration uses the same VRM/height on each viewer; animation
and spring simulation retain normal network/local timing. Rendering switches
remain viewer preferences. Legacy peers retain their negotiated model/height
behavior and unmodded admission is unaffected.

Height sync requires server, sender and viewer 1.8.12+. F8 accepts 1.4–2.2 m,
default 2 m, applied on release and saved per local character in
`BepInEx/config/ValheimVRM/avatar_heights.json`. Each player's model and height
share one authenticated selection and increasing request sequence. Same-model
players retain independent scale; respawn/late-join snapshots carry height.
Each receiver reattaches a cached clone and recalibrates foot/seat placement.
Manual standing/sitting offsets are also synchronized with 1.8.14+.

HeightHello negotiates the extension: selection packets append a float after
the request sequence, and capable receivers use snapshot format 2. Other peers
retain format 1 and default height 2 m. Invalid/nonfinite/out-of-range heights
are rejected before advancing the sequence. Capability upgrades cannot be
downgraded by delayed legacy messages. Without the addon, height works locally.
Unmodded clients retain admission and receive no avatar snapshots.

Install the full 1.8.15 client on participating players. Their top-level
`ValheimVRM` folders may differ without affecting admission or normal play. To
display a selected remote avatar, its case-sensitive filename and SHA-256 must
match the sender's VRM. Extra files are ignored; folder equality is not enforced. Calibration settings
are shared per player in 1.8.14+, so personal config files need not be distributed.
Received controls never overwrite the observer's own preferences.

Prerequisite downloads: [Valheim-specific BepInEx pack (recommended)](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) · [BepInEx 5.4.23.3 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) · [All releases](https://github.com/BepInEx/BepInEx/releases). Tested pack/core: 5.4.2333 / 5.4.23.3.

The local `10_ValheimVRM_Server_1.8.16.zip` includes Windows x64 BepInEx 5.4.23.3. For a fresh installation, stop the server and extract its `BepInEx`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` beside `valheim_server.exe`, then start normally. If compatible BepInEx is already installed, update only `BepInEx/plugins/ValheimVRM.Server`, preserving the loader, configuration and other plugins. The public GitHub ZIP excludes the loader; Linux needs the platform-specific setup documented by the Valheim pack.

For the public package, the dedicated server needs BepInEx 5 and only the DLL from
`ValheimVRM-Server-1.8.16.zip`, under `BepInEx/plugins/ValheimVRM.Server/`.
It does not load avatar files, UniVRM or shaders. Restart, join, and enable
**Server avatar sync** in F8. A client-hosted server can install the same server
addon alongside its client plugin.

Unmodded clients may join normally and see vanilla characters. Discovery stops
after three unanswered rounds (four capability/legacy messages per round, twelve total),
and no avatar snapshot is sent without the mod
handshake. Admission and game version checks are unchanged; missing this optional
addon never triggers a disconnect. Protocol version 1 remains compatible with
1.8.0 clients.

The server authenticates the sender by its connection, validates ownership of its
character ZDO, and broadcasts a per-player snapshot. A selecting model 1 never
changes B. Equal names and equal model choices still use independent character
IDs and independent avatar instances. Snapshots support late joins, respawns,
reconnections and reappearing remote players.

When both the sender and server run 1.8.3+, capability negotiation enables
positive 64-bit request sequences; F8 shows **Request order protection active**.
The server keeps a separate maximum per authenticated RPC connection and accepts
only greater sequences. If request 2 arrives before request 1, request 1 is ignored.
Duplicates and legacy packets received after upgrading that connection are also
ignored. Opt-out participates in the same ordering. Respawn, refreshing and toggling
sync keep the counter; only a new connection resets it. Old-RPC packets cannot
change a new connection. New clients reject older and equal snapshot revisions.

Legacy 1.8.0–1.8.2 peers still work, but their upstream requests retain arrival-order
behavior. Both sender and server must support the extension for the guarantee;
new clients show a compatibility notice for an old server. Update the complete
client and separate server packages. Ordering uses connection-scoped counters,
not machine clocks or game frames. Negotiated snapshots use format 1 (model),
2 (model/height) or 3 (model/height/calibration).

Without the addon, local selection works without sending selection requests.
Opting out preserves your local appearance, withdraws your shared choice and
restores remote players to their original visuals. Missing or different files
retain that player's last usable appearance (vanilla on first load). Missing
folders, removed files and unreadable files behave the same way. Uncached bytes
are verified before settings/import/cache changes; failures are not repeatedly
imported and do not block healthy players. Correct unimported files and refresh
the F8 list to retry; restart after replacing a cached model. Local settings may
differ. The server ignores its own avatar folder, including an absent folder,
and VRM bytes are never transferred by this protocol.

Server: `[Sync] Enabled` in `com.celestetwinkle.valheimvrm.server.cfg`.
Client: `[AvatarSync] Enabled` in `com.yoship1639.plugins.valheimvrm.cfg`.
Keep the old `EnableLegacyVrmSharing` option disabled. The protocol is bounded to
128 active synchronized players. See the release validation document for the
tested scope; real dedicated-server login and Linux are separate deployment checks.
