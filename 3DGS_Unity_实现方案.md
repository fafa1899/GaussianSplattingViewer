# Unity 中可视化 3DGS 的实现方案

本文档用于明确本项目的学习目标、技术路线与阶段性实现步骤，作为后续开发时的统一参考。

## 1. 项目目标

本项目的目标不是一开始就做完整的高性能产品级 3DGS 渲染器，而是：

1. 在 Unity 中读取官方 3DGS 训练结果 `.ply`
2. 尽快得到可见的可视化结果
3. 在实现过程中逐步理解 3DGS 的核心原理
4. 从简到繁，逐步接近论文和官方实现中的渲染效果

当前使用的数据来源：

- 3DGS 原始项目：`D:\3DGS\Unity3DGS\gaussian-splatting`
- Unity URP 项目：`D:\3DGS\Unity3DGS\GaussianSplattingViewer`
- 测试模型：`D:\3DGS\Unity3DGS\models\bicycle\point_cloud\iteration_30000\point_cloud.ply`

该 `point_cloud.ply` 文件大小约为 1.5GB，包含约 613 万个高斯点。

## 2. 对 3DGS 的基本理解

### 2.1 3DGS 不是什么

3DGS 不是传统三角网格，也不是普通 RGB 点云。

它也不适合直接理解为：

- 每个点一个球体 Mesh
- 每个点一个 Unity GameObject
- 直接用 Unity 粒子系统代替完整渲染逻辑

这些方式可以临时做实验，但都偏离了 3DGS 的核心表达方式。

### 2.2 3DGS 是什么

3DGS 将场景表示为大量 3D Gaussian。

每个 Gaussian 至少包含：

- 空间位置 `x, y, z`
- 尺度 `scale_0, scale_1, scale_2`
- 旋转 `rot_0, rot_1, rot_2, rot_3`
- 透明度 `opacity`
- 颜色系数 `f_dc_*` 和 `f_rest_*`

渲染时并不是把它们当成实体球体去画，而是：

1. 把 3D Gaussian 看成空间中的各向异性体分布
2. 投影到屏幕后，得到 2D 椭圆形 splat
3. 再根据透明度和可见性进行混合，形成最终图像

## 3. 当前 PLY 数据的含义

当前模型文件头已经验证为标准 3DGS 输出格式，字段包括：

- `x, y, z`
- `nx, ny, nz`
- `f_dc_0, f_dc_1, f_dc_2`
- `f_rest_0 ...`
- `opacity`
- `scale_0, scale_1, scale_2`
- `rot_0, rot_1, rot_2, rot_3`

其中需要特别注意：

### 3.1 颜色不是直接 RGB

`f_dc_*` 不是普通 RGB，而是球谐系数中的 DC 项。

根据官方实现：

- `RGB2SH(rgb) = (rgb - 0.5) / C0`
- `SH2RGB(sh) = sh * C0 + 0.5`
- `C0 = 0.28209479177387814`

因此第一版可视化可以只使用 `f_dc` 恢复基础颜色。

### 3.2 scale 不是直接尺寸

`scale_0/1/2` 存储的通常是对数域缩放值，使用时需要：

`scale = exp(scaleLog)`

如果不做这个转换，高斯尺寸会完全不对。

### 3.3 opacity 不是直接透明度

`opacity` 通常是未激活值，使用时需要经过 sigmoid：

`alpha = sigmoid(opacity)`

### 3.4 rot 是四元数

`rot_0..3` 表示高斯的旋转，不应按欧拉角理解。

## 4. 学习型实现路线

本项目采用“先有结果，再逐步增强”的策略。

核心原则：

1. 先跑通数据读取与基础显示
2. 再逐步接近 3DGS 的真实渲染模型
3. 每一步都要能解释自己离论文效果还差什么

## 5. 技术路线总览

推荐路线如下：

1. 解析二进制 PLY
2. 将高斯数据加载到 CPU 内存
3. 上传到 GPU Buffer
4. 用自定义 Shader 绘制
5. 从点渲染逐步升级到 Gaussian splat 渲染

不建议作为主路线的方案：

- 为每个点创建 GameObject
- 为每个点实例化球体 Mesh
- 用 Unity 原生粒子系统直接代替 3DGS 渲染

原因是这些方案不利于理解 3DGS 的实际渲染逻辑，也很难自然过渡到后续的 `scale + rot + SH + sorting`。

## 6. 分阶段实现步骤

## 阶段 0：只做 PLY 解析

目标：

- 正确读取官方 3DGS 的二进制 PLY
- 明确每个字段对应什么意义
- 形成 Unity 内部的数据结构

建议建立最小数据结构：

```csharp
struct GaussianData
{
    public Vector3 position;
    public Vector3 scaleLog;
    public Quaternion rotation;
    public float opacityRaw;
    public Vector3 fdc;
}
```

后续再追加：

- `f_rest`
- 预处理后的真实 `scale`
- 预处理后的 `alpha`

这一阶段的输出：

1. 成功读取文件头
2. 成功读取全部顶点记录
3. 在 Unity 中打印顶点数与若干条样本数据用于校验

## 阶段 1：先画成彩色点云

目标：

- 尽快在 Unity 中看见 bicycle 的整体形状
- 验证坐标、颜色和相机关系是否正确

做法：

1. 只用 `position`
2. 颜色先只用 `f_dc`
3. 通过 `SH2RGB` 把 `f_dc` 转成基础颜色
4. 先忽略 `f_rest / scale / rot / opacity`
5. 使用点绘制方式进行第一版可视化

实现方式可以有两种：

1. `MeshTopology.Points`
2. `Graphics.DrawProcedural` 或类似的 GPU 点绘制

这个阶段不是为了接近论文画质，而是为了确认：

- 读取正确
- 数据上传正确
- 坐标系正确
- 基础颜色正确

## 阶段 2：从点升级到屏幕对齐的 billboard

目标：

- 从“点云”进入“splat”阶段
- 让画面开始呈现柔和的覆盖效果

做法：

1. 每个高斯不再只画 1 个点
2. 而是在屏幕上扩展成一个朝向相机的小 quad
3. 在片元着色器中根据中心距离计算高斯衰减
4. 透明度先使用 `sigmoid(opacity)`
5. 颜色仍然先只用 `f_dc`

这一阶段的意义：

- 开始理解为什么 3DGS 不是普通点云
- 开始理解 splat 是什么
- 画面会比单纯点渲染更接近最终效果

这一阶段中的 billboard 不是最终方案，但它是非常合理的中间层。

## 阶段 3：从圆形 splat 升级到椭圆 splat

目标：

- 引入 3DGS 中最关键的各向异性信息

做法：

1. 对 `scaleLog` 执行 `exp`
2. 对四元数做归一化
3. 四元数转换为旋转矩阵
4. 结合尺度构造 3D Gaussian 的协方差表达
5. 投影到屏幕后，得到 2D 椭圆分布
6. 用包围 quad 承载屏幕空间高斯核

这一阶段是从“像点精灵”迈向“像真实 3DGS”的关键一步。

如果没有 `scale + rot`：

- 所有 splat 的形状都过于简单
- 物体会发糊但缺少空间结构
- 很难接近论文效果

## 阶段 4：加入视角相关颜色 SH

目标：

- 理解 `f_rest_*` 的意义
- 让颜色随视角变化

做法：

1. 取观察方向 `viewDir`
2. 按球谐基函数评估 SH
3. 用 `f_dc + f_rest` 重建颜色

这一阶段之后可以观察到：

- 颜色并非固定不变
- 一些反光、金属感、方向相关效果会更自然

这也是 3DGS 区别于普通静态点云颜色的重要特征之一。

## 阶段 5：处理透明排序与可见性

目标：

- 解决半透明叠加错误
- 让前后遮挡关系更合理

原因：

3DGS 的 splat 本质上带透明度，大量高斯叠加时，如果没有排序和可见性处理，常见问题包括：

- 前后顺序错误
- 发黑
- 穿插混乱
- 局部糊成一团

实现可以由简到繁：

1. 先做按相机距离的全局排序
2. 再做块级排序
3. 再做基于 tile 的可见性与局部排序
4. 最终逐渐靠近论文中的 visibility-aware splatting

这一阶段是“从能看”走向“看起来对”的关键部分。

## 阶段 6：性能优化

目标：

- 在大规模高斯场景下保持可接受性能

主要优化点包括：

- frustum culling
- 距离剔除
- 尺寸剔除
- tile/binning
- GPU 排序
- LOD
- 预过滤漂浮高斯

但这些内容不应放在最开始，它们应建立在基础渲染已经正确的前提上。

## 7. 推荐的工程实现方向

对于 Unity 学习型实现，推荐主路线为：

1. C# 解析 PLY
2. 构造结构化数据数组
3. 上传到 `ComputeBuffer` 或 `GraphicsBuffer`
4. 用自定义 Shader 绘制
5. 逐步补齐：
   - 颜色解码
   - opacity 解码
   - scale 解码
   - rotation 解码
   - Gaussian 衰减
   - SH 颜色
   - 排序与可见性

相比之下：

- `Particle System` 不利于承载完整 3DGS 参数模型
- 球体实例化会造成极高的对象管理开销
- 每点一个 GameObject 基本不可行

## 8. 当前确定的目标版本

为了保证节奏稳定，当前计划先锁定以下里程碑：

### 里程碑 A：看到基础点云

要求：

- 成功读取 `.ply`
- 成功解析 `position + f_dc`
- 在 Unity 中显示基础彩色点云

### 里程碑 B：看到基础 splat 效果

要求：

- 引入 `opacity`
- 点扩展为小 quad
- 使用简单高斯衰减

### 里程碑 C：接近真正的 3DGS 形状

要求：

- 加入 `scale + rot`
- 从圆形 splat 升级到椭圆 splat

### 里程碑 D：理解视角相关外观

要求：

- 加入 `f_rest`
- 实现 SH 视角相关颜色

### 里程碑 E：提升正确性与性能

要求：

- 加入排序与可见性
- 做基本优化

## 9. 第一阶段的实际开发任务

接下来的第一批任务应集中在以下内容：

1. 新建 PLY 解析器
2. 定义 Gaussian 数据结构
3. 读取全部高斯到内存
4. 先基于 `position + f_dc` 做第一版渲染

建议最先在 Unity 项目中建立如下模块：

- `PlyHeader`：解析 header
- `GaussianPlyReader`：读取二进制数据
- `GaussianData`：高斯结构定义
- `GaussianPointRenderer`：第一版点渲染器

## 10. 当前项目的策略结论

本项目采用的策略是：

**先顺向理解渲染，再逐步反推 3DGS 的核心原理。**

具体落地为：

1. 不先碰训练
2. 不先做复杂优化
3. 先把官方 PLY 数据读进 Unity
4. 先画出来
5. 再一层层加上 3DGS 的关键机制

这是当前最适合学习和入门的路线。

## 11. 后续开发时的判断标准

每做完一个阶段，都应回答这三个问题：

1. 我现在画出来的东西，和“真正的 3DGS”相比还缺什么？
2. 这些缺失对应的是哪一类参数或哪一步渲染逻辑？
3. 下一步最值得补的是正确性，还是性能？

这样推进，项目会始终保持清晰，不容易在中途跑偏。

## 12. 参考依据

本方案建立在以下依据之上：

1. 3D Gaussian Splatting 论文  
   [https://arxiv.org/abs/2308.04079](https://arxiv.org/abs/2308.04079)

2. 官方实现仓库  
   [https://github.com/graphdeco-inria/gaussian-splatting](https://github.com/graphdeco-inria/gaussian-splatting)

3. 本地官方实现中的属性定义与 SH 转换逻辑  
   - [gaussian_model.py](/D:/3DGS/Unity3DGS/gaussian-splatting/scene/gaussian_model.py:225)
   - [sh_utils.py](/D:/3DGS/Unity3DGS/gaussian-splatting/utils/sh_utils.py:114)

