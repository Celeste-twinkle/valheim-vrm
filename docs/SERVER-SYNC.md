# Optional server avatar synchronization / 可选服务器外观同步

## 中文

客户端和服务器推荐使用 1.8.7；同步协议仍兼容 1.8.0 客户端。推荐为参与外观同步的玩家统一分发
`ValheimVRM` 文件夹，但文件夹不一致不是入服限制：缺失、多出或不同的文件都不影响连接和正常游戏。
只有显示某位玩家选择的模型时，接收方才需要该文件的相同名称（含大小写）和 VRM 内容。
客户端模型目录只需 `.vrm`，不需要任何 TXT 或 JSON；服务端也不需要这些文件。
个人选项由客户端按需保存在 `BepInEx/config/ValheimVRM`，各玩家可保留自己的选择、渲染开关和物理权重。

1. 服务器先安装兼容的 BepInEx 5，再将 `ValheimVRM-Server-1.8.7.zip`
   解压到服务器程序所在目录。目标为
   `BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`。
   服务器只需该同步插件，不需要客户端的着色器、UniVRM DLL 或角色模型。
2. 参与外观同步的玩家安装完整客户端 `ValheimVRM-1.8.7.zip` 和相同的模型文件夹。
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
原版客户端忽略未知 RPC，最多三轮探测后服务端停止探测；每轮包含新能力和旧格式各一条消息，
不发送模型快照；
这类玩家看到原版角色，自己的外观也保持原版。此兼容路径有受控引擎测试覆盖。

服务器只中转模型名称与 SHA-256 指纹，不接收、导入或分发 VRM 文件。
服务器自己的模型文件夹可以不存在或与任一客户端不同，它不参与模型校验。

| 接收方文件情况 | 行为 |
| --- | --- |
| 所选模型同名且内容相同，其他文件不同 | 正常显示该玩家的模型；额外文件不影响同步。 |
| 文件或整个模型目录不存在、文件已删除或暂时无法读取 | 跳过该次外观更新；保留该玩家已有可用外观，首次加载显示原版角色。 |
| 同名 VRM 内容不同或本地旧缓存不同 | 跳过该次更新，不导入错误文件，不修改其他玩家的外观。 |
| 仅个人配置、画面和物理权重不同 | 继续同步；各客户端使用本机配置，外观效果可能不同。 |

未缓存模型先检查将要导入的字节指纹，再读取模型配置和导入。失败选择只提示一次，
不会反复导入，也不会阻塞其他玩家切换、影响登录或踢人。补齐或修正未导入文件后，
F8 的“刷新列表”会重试；已经导入缓存的同名文件被替换时需要重启客户端。
模型配置建议由服主统一分发以保持比例和装备偏移一致，但协议不强制统一本机设置。

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

Install the full 1.8.7 client on participating players. Their top-level
`ValheimVRM` folders may differ without affecting admission or normal play. To
display a selected remote avatar, its case-sensitive filename and SHA-256 must
match the sender's VRM. Extra files are ignored; folder equality is not enforced. Model settings should also be distributed
consistently. Personal avatar selections, lighting and physics-weight preferences
remain local.

The dedicated server needs BepInEx 5 and only the DLL from
`ValheimVRM-Server-1.8.7.zip`, under `BepInEx/plugins/ValheimVRM.Server/`.
It does not load avatar files, UniVRM or shaders. Restart, join, and enable
**Server avatar sync** in F8. A client-hosted server can install the same server
addon alongside its client plugin.

Unmodded clients may join normally and see vanilla characters. Discovery stops
after three unanswered rounds (one capability and one legacy message per round),
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
not machine clocks or game frames. Snapshot format remains protocol 1.

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
