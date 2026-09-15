# ValheimVRM 毛绒扩展接入规范

面向模型作者、导出工具作者及 Mod 开发者。本文定义的数据版本为 **1**。这是 ValheimVRM 的可选应用数据，不是 VRM Consortium 或 Khronos 的官方扩展。

客户端最低版本：**ValheimVRM 1.8.17**。服务器不需要新增协议；要让别人看到毛绒，需要观察者也更新客户端。

## 1. 容器与基础模型

基础文件必须是有效的 VRM 0.x 或 VRM 1.0 GLB。保留原始网格及标准 MToon 衣料，在 **glTF 材质对象**的 `extras.ValheimVRM_fur` 中增加参数。依据 [glTF 2.0 extras 规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#reference-extras)，读取器可以忽略不认识的应用数据。

不支持本功能的读取器显示标准衣料；支持的 ValheimVRM 客户端额外绘制毛绒。模型只携带参数和图片，不携带任意 Shader 源码、DLL 或 AssetBundle；Shader 由 Mod 自己提供。

**不要**把 `ValheimVRM_fur` 加入 `extensionsRequired` 或 `extensionsUsed`，本规范使用的是 `extras`。不要覆盖 `extensions.VRM`、`extensions.VRMC_vrm`、`extensions.VRMC_materials_mtoon` 或其他原有数据。

| 情况 | 行为 |
| --- | --- |
| 新版客户端、有效扩展、支持几何着色器 | 标准衣料 + GPU 毛绒 |
| 普通查看器、旧 Mod、不支持几何着色器 | 标准衣料 |
| 没有扩展、`enabled: false`、`length: 0` | 无毛绒组件、纹理、材质或绘制 |
| 未知版本、非法参数、损坏或缺失的遮罩 | 忽略该材质毛绒，保留可正常导入的基础模型 |

## 2. JSON 定义

在目标 `materials[index]` 对象上保留所有原有字段，并添加：

```json
{
  "name": "Sweater",
  "extras": {
    "ValheimVRM_fur": {
      "version": 1,
      "enabled": true,
      "length": 0.005,
      "density": 2,
      "directionX": 0,
      "directionY": 0,
      "directionZ": 1,
      "randomness": 0.5,
      "rootOffset": -1,
      "lengthImage": 14,
      "noiseImage": 15,
      "maskImage": 16,
      "noiseScaleX": 1,
      "noiseScaleY": 1
    }
  }
}
```

图片编号必须替换为该文件 `images` 数组的实际索引，**不是** `textures`、`bufferViews` 或 Unity 材质槽索引。导入器按材质索引取得实际材质对象，不根据衣服、模型或材质名称猜测。同名的不同材质应分别设置正确索引。

## 3. 字段表

可供工具校验的 [JSON Schema](ValheimVRM_fur.schema.json) 随本规范提供。Schema 校验字段与推荐边界；图片索引是否存在、PNG 格式和 GLB 字节范围仍需文件级校验。

所有数字必须有限，不能使用 NaN、无穷或数字字符串。省略字段使用默认值；合法数值越界时钳制，非法类型使整项毛绒被忽略。未知字段由当前读取器忽略。

| 字段 | 类型 | 默认值 | 范围及含义 |
| --- | --- | --- | --- |
| `version` | integer，必填 | 无 | 必须为 `1`；未知版本不启用 |
| `enabled` | boolean | `true` | `false` 时不读取图片或加载毛绒资源 |
| `length` | number | `0.005` | `0..0.03`；原始模型尺度下的米数，0 为禁用，随整体模型缩放变化 |
| `density` | integer | `2` | `1..3`；近距离每个基础三角形生成的毛片数 |
| `directionX` | number | `0` | `-1..1`；切线方向分量 |
| `directionY` | number | `0` | `-1..1`；副切线方向分量 |
| `directionZ` | number | `1` | `0..1`；法线方向分量 |
| `randomness` | number | `0.5` | `0..1`；稳定的毛流随机偏移强度 |
| `rootOffset` | number | `-1` | `-1..0`；毛根覆盖曲线，-1 时毛根较密 |
| `lengthImage` | integer | 不使用 | `images` 索引；红通道乘毛长，黑色为零长度 |
| `noiseImage` | integer | 不使用 | `images` 索引；红通道控制毛片内纤维分布 |
| `maskImage` | integer | 不使用 | `images` 索引；红通道乘覆盖，黑色不显示毛绒 |
| `noiseScaleX` | number | `1` | `0.01..100`；噪声横向平铺 |
| `noiseScaleY` | number | `1` | `0.01..100`；噪声纵向平铺 |

方向向量使用网格切线空间并归一化。应保留法线、切线和 `TEXCOORD_0`。未提供噪声图时使用白色，能加载但毛片较宽；细腻纤维需要实际噪声纹理。

当前没有毛流贴图、独立 UV 集、KHR_texture_transform、毛绒风力、重力或碰撞扩展。模型原有骨骼物理由 SpringBone 处理。

## 4. 图片与 GLB 二进制布局

三类遮罩均为**同一个 GLB buffer 0 内嵌的 PNG**，单张尺寸不超过 4096×4096，压缩数据不超过 8 MiB。当前不读取文件路径、HTTP、data URI、第二个外部 buffer 或 JPEG 遮罩。

在 `images` 中追加图片，令它引用新的 `bufferViews` 项：

```json
{
  "name": "FurLength",
  "mimeType": "image/png",
  "bufferView": 23
}
```

相应 buffer view 结构为：

```json
{
  "buffer": 0,
  "byteOffset": 1024,
  "byteLength": 4096
}
```

以上数字仅说明字段，不能直接复制。`byteOffset` 必须是 PNG 在 BIN 数据中的实际起点，`byteLength` 是 PNG 原始字节数。追加后更新 `buffers[0].byteLength`、BIN chunk 长度和 GLB 总长度。chunk 按 4 字节补齐，JSON 用空格，BIN 用零；PNG 的 buffer view 长度不包括补齐字节。推荐使用下一节工具完成，避免手算损坏 GLB。

遮罩以**线性数据**读取红通道，启用 mipmap 和 Repeat 平铺，不应做 sRGB 颜色转换。噪声及覆盖跟随基础材质 UV 变换／动画；毛长在几何阶段采样基础 UV，不随 UV 动画改变几何长度。

毛绒底色来自原衣料主纹理、主颜色与暗部颜色，服从 Mod 的亮度上限。不会复制额外自发光、MatCap 或边缘光去提亮毛绒。

## 5. 导出工具用法

先正常导出 VRM，确认基础衣料、蒙皮、形态键和透明模式正确。不要在同件衣服上同时保留烘焙毛片并启用 GPU 毛绒，避免重复绘制。

仓库脚本只依赖 Python 3 标准库：

```powershell
python tools/Add-FurMetadata.py avatar.vrm avatar-fur.vrm `
  --material 7 --length 0.005 --density 2 --randomness 0.5 `
  --direction 0 0 1 --root-offset -1 `
  --length-mask FurLength.png --noise-mask FurNoise.png `
  --coverage-mask FurCoverage.png --noise-scale 1 1
```

`--material 7` 必须换成自己的 glTF 材质索引，可用以下脚本查询：

```python
import json, struct
from pathlib import Path
data = Path("avatar.vrm").read_bytes()
length = struct.unpack_from("<I", data, 12)[0]
document = json.loads(data[20:20 + length])
for index, material in enumerate(document["materials"]):
    print(index, material.get("name", ""))
```

工具保留原网格、骨骼、动画、材质、图片及其他 extras，只追加遮罩，并替换指定材质的 `ValheimVRM_fur`。多件衣服需分别调用，下一次以前一次输出为输入。重复调用不自动删除原有但不再引用的图片，正式导出应从干净的基础 VRM 开始。

停用方式：删除该对象、设 `enabled: false` 或 `length: 0`，然后重新加载模型。本版本没有 F8 毛绒开关。玩家只需要复制生成的 `.vrm`，不需要旁边的 PNG、TXT 或 JSON。

## 6. 宏定义与按需资源

专用 Shader `ValheimVRM/Fur` 使用本地宏 **`AVATAR_FUR_ON`**。开启变体才包含毛绒采样、几何生成、光照及覆盖计算；关闭变体不生成几何，不含毛绒采样与光照代码。`AVATAR_FUR_SHADOWS` 独立选择阴影接收变体。

仅有 Shader 分支不能消除蒙皮和 Draw Call，所以运行时同时采取以下措施：

1. 没有有效扩展时沿用原材质导入器，不创建毛绒组件或每帧任务。
2. 禁用／零长度扩展不读取 PNG，不创建毛绒 GPU 纹理、材质或 Renderer。
3. 独立的 `avatar_fur` 包只在有效毛绒模型使用时加载，不混入常规渲染包。
4. 覆盖层复用原 `sharedMesh`、骨骼及绑定姿态，不复制、细分或烘焙常驻网格。
5. 材质和遮罩归导入模板持有，玩家克隆通过既有模型驻留机制保护模板寿命；骨骼引用属于各自实例。
6. 最后一个毛绒资源持有者释放后卸载该着色器包。

这里的“未启用无占用”指没有毛绒运行时图形资源、覆盖层蒙皮、每帧回调和绘制；Mod 安装文件仍含支持代码和独立压缩 Shader 资源。其他可见毛绒角色或尚未释放的模型缓存仍需要共享资源，不能因为角色暂时离开相机视野就提前销毁。

## 7. 渲染与性能边界

基础衣料继续使用原有 SSAO 深度／法线修复。毛绒是透明覆盖层，关闭深度写入，在游戏 AO 之后合成，不将背景 AO 作为衣料遮蔽乘入。它仍进行场景深度测试，避免出现在遮挡物前面。

颜色绘制与“关闭模型泛光”的覆盖绘制共用几何生成、UV、透明度、密度及距离衰减逻辑；TAA 沿用透明／不透明投影一致性修复。毛流随机种子由固定三角形编号产生，不随相机抖动或时间重新随机。

毛绒接受客户端场景光照与阴影选项，使用已限制的衣料底色，不额外发光。不投射每条纤维的细阴影，基础衣服仍投影。

每个基础三角形最多生成 3 张毛片，每张 4 个几何阶段顶点、2 个三角形。运行时仍有几何着色器、透明过绘、额外蒙皮与绘制成本。减少的是文件体积和常驻毛绒网格，并不表示绘制免费。

相机距离 4–12 米逐渐减少额外毛片，12–20 米使剩余毛绒平滑淡出，基础衣料保留。密度依赖原网格三角形数量；低面数衣服可能较稀疏。单网格大于 200,000 顶点，或材质槽数量与 submesh 数不匹配时不创建覆盖层。

本功能不是 lilToon Fur 的全部移植，不是标准 VRM 置换贴图支持，也不提供顺序无关透明。多层真实透明仍受 Unity 排序限制。

## 8. 联机与版本

服务端不渲染毛绒，不需要 Shader 或图片。它继续同步每位玩家的模型、身高和既有校准参数。各客户端需使用支持本扩展的 Mod 与**内容一致的 VRM 文件**才能看到相同毛绒。

参数随模型携带，不发送每帧毛绒消息。玩家 A 和 B 的位置、骨骼、形态键及身高由各自实例驱动；模型身高变化也缩放毛长。客户端画质和光照／泛光选项仍可能导致最终画面差异。

## 9. 接入验收与故障排查

- 普通 VRM 读取器打开后基础模型应正常，不能因 extras 未知而无法加载。
- 核对材质索引和图片索引，防止赋给身体或同名的另一份材质。
- 检查禁用、零长度、未知版本、缺失图片与损坏数据的回退行为。
- 无毛绒模型不应出现 `ValheimVRM GPU fur` 子对象或 `avatar_fur` 已加载包。
- 切换模型和退回菜单后检查纹理、材质及包释放；另一个角色仍使用时不能提前释放。
- 用两个不同身高、位置的角色检查骨骼引用与形态键是否独立。
- 用连续帧检查 TAA、模型泛光开关、远近衰减与遮挡，不能用单帧判定闪烁。
- 基础衣料正常但毛绒缺失时，检查设备几何着色器支持及日志中的 `GPU fur is unavailable`、`Ignoring invalid fur material`、`Keeping standard material`。

实现入口：`src/AvatarFurDefinition.cs`（协议校验）、`AvatarFurImporter.cs`（绑定与图片读取）、`AvatarFurResources.cs`（资源寿命）、`AvatarFurSurface.cs`（克隆与形态键），以及 `shaders/AvatarRendering/AvatarFur.shader`／`.cginc`（宏和绘制）。
