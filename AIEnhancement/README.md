# Ravenfield AI Enhancement V6

## 概述

V6 是一个极简化的 AI 增强 MOD，只做一件事：**让 AI 更智能地选择攻击目标**。

### 核心设计原则

**不干预移动，只优化射击**

V6 放弃了所有干预 AI 移动决策的做法（如调用 `Goto()`），只通过反射调用游戏内部的 `SetTarget()` 方法来优化目标选择。

### 为什么这样设计

之前的版本试图通过调用 `Goto()` 来控制 AI 的移动路径，但这会覆盖游戏自己的路径逻辑，导致：
- AI 不知道该往哪走
- 载具 AI 完全失效
- AI 聚集在一起乱走

V6 认识到：**Ravenfield 原版的移动逻辑已经足够好**，我们只需要改进它的"射击决策"即可。

---

## 工作原理

```
原版 AI 决策流程：
1. 移动决策（游戏自己管）← 我们不干预
2. 目标选择（游戏自己选）← 我们用更智能的算法优化
```

### 目标评分算法

每 0.2 秒，V6 扫描所有 AI，计算最佳目标：

| 因素 | 分数调整 | 说明 |
|------|----------|------|
| 距离近 | +分 | 优先打近的敌人 |
| 敌人正在瞄准我 | +500 | 优先消灭威胁 |
| 敌人在载具里 | +300 | 优先打载具有效 |
| 装甲载具 | +200 | 优先消灭重威胁 |
| 飞行载具 | +150 | 优先消灭空中威胁 |
| 武器克制 | ±100 | 用对的武器打对的目标 |
| 敌人血量低 | +50 | 优先击杀残血敌人 |
| 目标是玩家 | +30 | 更关注玩家 |

---

## 文件结构

```
AIEnhancement/
├── AIEnhancement.cs    # 源代码（唯一的源文件）
├── AIEnhancement.dll   # 编译产物
├── Injector.cs         # 注入器源码
├── Injector.exe        # 注入器程序
├── Mono.Cecil.dll      # 注入器依赖
└── README.md           # 本文档
```

---

## 安装方法

1. 确保游戏已关闭
2. 运行 `Injector.exe` 注入（如果尚未注入）
3. 直接双击 `Ravenfield.exe` 启动游戏
4. MOD 会自动加载

### 卸载方法

复制 `Assembly-CSharp.dll.backup` 到 `Ravenfield_Data/Managed/Assembly-CSharp.dll`

---

## 技术细节

### 反射调用

V6 通过反射访问 `AiActorController.SetTarget()` 私有方法：

```csharp
Type aiType = typeof(AiActorController);
MethodInfo setTargetMethod = aiType.GetMethod("SetTarget",
    BindingFlags.Instance | BindingFlags.NonPublic);
setTargetMethod.Invoke(ai, new object[] { target });
```

### 更新间隔

为了性能考虑，目标评分每 0.2 秒执行一次，而不是每帧执行。

### 线程安全

所有代码在 Unity 主线程运行，无需额外同步。

---

## 版本历史

- **V6**：极简化版本，只保留目标增强，移除所有移动干预
- **V5.x**：尝试各种移动干预方案（失败）
- **V4**：精确还原原版路径算法
- **V3-V1**：早期版本

---

## 编译方法

```bash
cd AIEnhancement
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /target:library \
  /out:AIEnhancement.dll \
  /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll" \
  /reference:"Ravenfield_Data\Managed\UnityEngine.dll" \
  /reference:"Ravenfield_Data\Managed\Assembly-CSharp.dll" \
  AIEnhancement.cs
```

---

## 游戏信息

- **引擎**：Unity 5.4.0f3
- **运行时**：Mono .NET Framework 4.0
- **MOD 注入**：Mono.Cecil

---

## 免责声明

本 MOD 仅供学习研究使用。使用前请确保拥有正版游戏。
