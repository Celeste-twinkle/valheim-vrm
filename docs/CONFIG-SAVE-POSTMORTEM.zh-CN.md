# 2.0.4 配置保存故障复盘

## 现象与已确认的原因

2.0.3 的现场日志显示：模型读取、导入和外观挂接已经完成，随后在 `AvatarCatalog.Select` 的 `File.Replace` 中抛出 `IOException: 无法删除要被替换的文件。`。调用链为 `File.Replace → AvatarCatalog.Select → OutfitSwitcher.Switch → RunSwitch`。

原实现首次保存时用 `File.Move` 创建 `BepInEx/config/ValheimVRM/avatar_selections.json`；文件存在后，每次保存改用 `File.Replace(temporary, target, null)`。这解释了“文件只生成一次，之后不再更新”，也解释了“外观已经切换，却显示 Avatar switch failed”：磁盘保存位于外观应用之后，保存异常被整个切换流程的通用错误处理接住。

该错误文本对应 Windows 的 `ERROR_UNABLE_TO_REMOVE_REPLACED`（1175／0x497），表示替换过程无法删除原文件。微软说明 `ReplaceFileW` 还会处理原文件属性、访问控制表及其他元数据。创建临时文件成功说明目录允许创建文件，但不能证明已有文件允许删除、重命名或合并权限信息。因此，旧日志不能确定具体是文件权限、占用、文件系统或文件过滤程序造成，也不能直接认定整个文件夹没有读写权限。参见 [ReplaceFileW 官方说明](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)。

## 修复如何改变保存流程

| 情况 | 2.0.3 | 2.0.4 |
| --- | --- | --- |
| 首次创建 | 固定 `.tmp` 写入后移动 | 同目录、每次独立的临时文件写入后移动 |
| 更新已有文件 | 不带备份的 `File.Replace`，失败立即退出 | 带恢复副本的替换；可恢复错误最多尝试三次，总等待 60 ms |
| Windows 替换失败或不支持 | 没有后备保存方式 | 对指定错误保留原文件副本，再调用 `MoveFileExW(REPLACE_EXISTING \| WRITE_THROUGH)` |
| 替换部分完成后失败 | 没有还原流程 | 目标缺失时尝试从副本还原；还原失败会保留副本并记录路径 |
| 保存后清理失败 | 清理异常可能覆盖原异常或误报操作失败 | 单独记录 `CLEANUP_FAILED`，保留真正的保存结果 |
| 外观成功但保存失败 | 显示模型切换失败；内存选择可能仍是旧值 | 明确提示选择保存失败，保留已应用的本次会话选择，包括切回原始角色 |

后备方式使用同目录的重命名替换，避免走 `ReplaceFileW` 的元数据合并流程；代码不先删除或截断旧配置。真实的只读、访问拒绝或持续占用仍会报错，代码不会修改文件属性或放宽权限。`MoveFileExW` 的重命名同样受操作系统访问权限约束。参见 [MoveFileExW 官方说明](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)。

这套实现同时用于模型选择、高度、站姿／坐姿偏移 TXT、动作／装备校准、部件、物理和渲染配置，避免各保存点重复保留同一种缺陷。配置格式和联机协议没有变化。

## 新日志确认了哪一步修复问题

用户反馈 2.0.3.1 测试 DLL 修复问题后，提供了该版的完整运行日志。2.0.4 保留相同的配置保存实现，并在公开版 2.0.3 基线上正式发布。

新日志记录 8 次 `SAVE_START` 和 8 次 `SAVE_OK`：

| 设置 | 首次创建（`move-new`） | 更新成功（`windows-move-fallback`） |
| --- | --- | --- |
| 渲染选项 | 1 | 1 |
| 模型部件 | 1 | 1 |
| 动作／装备校准 | 1 | 3 |
| 合计 | 3 | 5 |

5 次更新中，带备份的 `File.Replace` 都连续失败三次，累计 15 条 `REPLACE_FAILED`，均为 `hresult=0x80070497 win32=1175`；随后每次都有 `SAVE_FALLBACK ... method=MoveFileEx` 和 `SAVE_OK ... method=windows-move-fallback`。没有 `SAVE_FAILED`、`CLEANUP_FAILED` 或 `Avatar switch failed`。

因此，现场起作用的修复是 Windows 后备提交方式。增加备份和短暂重试没有使该用户的 `File.Replace` 成功；旧接口的错误仍存在，但现在已被捕获并恢复，最终文件实际写入成功。日志改进本身不参与提交，会话选择和提示改进也不能替代写盘成功。

环境日志显示 Windows 10 x64、NTFS、约 144 GB 可用空间，目标文件属性为 `Archive`，没有记录 `ReadOnly`。首次创建、备份复制和后备覆盖均成功，可以排除“整个配置目录没有读写权限”作为这些操作失败的解释。能够确认的故障边界是该环境中的 `File.Replace` 替换流程；是否进一步涉及其元数据／权限合并、运行时实现或文件过滤程序，新日志仍不足以确定。

这次新日志没有 `avatar selections` 或 `avatar height offsets` 保存操作，因此直接证明的是渲染、部件和校准文件恢复更新。模型选择和 TXT 使用相同的统一保存实现，并由回归测试覆盖，但不能把本次日志当作这些操作已经执行的现场记录。确认选择跨重启保留时，应检查最新 JSON 和重启后的恢复结果。

日志中的 `REPLACE_FAILED` 是被恢复的内部步骤警告；判断最终结果应看 `SAVE_OK` 或 `SAVE_FAILED`。回归和现场日志摘要随验证记录说明，公开文档不包含原始用户日志、账号或模型内容。

不能把 Windows 10 或 NTFS 本身视为“必定失败”的环境。必要权限不足、只读属性、持续持有且不允许删除／重命名共享的句柄、跨卷替换或必要路径缺失，都会使对应调用持续失败，直到条件改变。该用户没有记录只读属性，且后备覆盖成功；这不等于 `File.Replace` 的全部权限／元数据处理前提都已满足，也不足以指定某个占用进程。共享规则参见 [CreateFileW 官方说明](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)。

## TXT 为什么可能没有生成

普通换模只更新 `avatar_selections.json`，模型身高滑块更新 `avatar_heights.json`。没有可选 `settings_模型名.txt` 时，插件使用内存默认参数；在 F8 保存“站姿高度偏移”或“坐姿高度偏移”才创建／更新该 TXT。`global_settings.txt` 由用户按需手动创建。旧日志没有 TXT 写入失败记录，因此不能把 TXT 缺失一并认定为文件权限故障。

## 一个 JSON 是否让所有模型共用同一套设置

JSON 按设置类型合并为文件，文件内部继续按模型或角色名区分记录。`avatar_calibration.json` 和 `avatar_parts.json` 以模型名为键；更新模型 A 的校准或部件不会改成模型 B 的设置。`avatar_selections.json` 和 `avatar_heights.json` 以游戏角色名为键。站姿／坐姿和其他可选 TXT 参数仍位于每个模型的 `settings_模型名.txt`。

`rendering_options.json` 和 `physics_options.json` 本来就是当前客户端的通用偏好，不按模型拆分。此次修复只改变保存方法，没有把原来独立的模型设置合并为一套参数。完整存储对应表见 [诊断指南](CONFIG-SAVE-DIAGNOSTICS.zh-CN.md)。

## 验证与后续改进

原有保存验证主要覆盖正常创建和更新，没有覆盖 Windows 替换错误及外观应用后的保存失败。此次增加可注入系统错误的回归，覆盖 1175、短暂占用、替换不支持、部分替换后的恢复、实际只读与文件占用、清理失败、角色选择隔离和会话选择保留。

正式版保留 `[ValheimVRM Config]` 诊断日志，记录实际路径、失败阶段、异常码与成功方法。独立回归在 .NET 7 和 Valheim 自带的 Unity Mono 中运行；游戏场景探针已加入，详细范围见 [2.0.4 验证记录](release-2.0.4-validation.md)。后续保存改动应继续复用统一实现并运行这些回归。
