# Ravenfield AI Enhanced Edition (V4)

## 游戏概述

Ravenfield 是一款由 SteelRaven7 开发的第一人称射击游戏，采用 Unity 5.4.0f3 引擎构建。游戏以红蓝两军大规模 AI 对战为核心玩法，玩家可以选择加入任意一方，与 AI 队友协同作战，争夺地图上的各个据点。

### 原版游戏核心机制

- **据点系统 (SpawnPoint)**：地图上分布着多个可占领的出生点，每个据点有归属阵营（红队/蓝队/中立）。控制据点可以获得该区域的出生权。
- **小队系统 (Squad)**：AI 以 4-6 人小队为单位行动，每队有队长，队员会跟随队长移动和攻击。
- **载具系统**：包含吉普车、坦克、直升机、炮艇等多种载具，AI 可以驾驶或乘坐载具参与战斗。
- **武器系统**：涵盖步枪、冲锋枪、狙击枪、火箭筒、手雷等多种武器类型。
- **路径寻找**：使用 A* Pathfinding Project 插件进行网格导航。

### 游戏文件结构

```
Ravenfield/
├── Ravenfield.exe                  # 游戏主程序
├── Ravenfield_Data/
│   ├── Managed/                    # .NET 程序集目录
│   │   ├── Assembly-CSharp.dll     # 游戏核心逻辑（已注入 MOD）
│   │   ├── Assembly-CSharp-firstpass.dll  # 优先编译的游戏代码
│   │   ├── UnityEngine.dll         # Unity 引擎 API
│   │   ├── UnityEngine.UI.dll      # Unity UI 系统
│   │   ├── UnityEngine.Networking.dll     # 网络模块
│   │   ├── AIEnhancement.dll       # AI 增强 MOD
│   │   ├── Pathfinding.*.dll       # A* 寻路插件
│   │   └── ...                     # 其他依赖库
│   ├── Mono/                       # Mono 运行时
│   ├── Resources/                  # 内置资源
│   └── level0~3                    # 场景文件
├── AIEnhancement/                  # MOD 源代码目录
│   ├── Part1_Core.cs               # 核心系统
│   ├── Part2_Spawn.cs              # 重生系统
│   ├── Part3_Vehicle.cs            # 载具系统
│   ├── Part4_Path.cs               # 路径系统
│   ├── Part5_Style.cs              # 战斗风格系统
│   ├── Injector.cs                 # 注入器源码
│   ├── Injector.exe                # 注入器程序
│   └── cecil/                      # Mono.Cecil 库
└── 原版/                           # 原版游戏备份
```

---

## AI 增强改进详解（V4 版本）

本次 AI 增强版本通过分析原版 Assembly-CSharp.dll 的 IL 代码，深入理解了原始 AI 逻辑后，进行了精准且克制的改进。所有修改都遵循"保留原版体验，适度增强智能"的原则。

### 一、威胁评估与目标优先级系统 (Part1_Core.cs)

#### 改进内容
AI 的目标选择更加智能化，不再单纯攻击最近的敌人，而是综合考虑多个因素。

#### 具体实现
```csharp
// 威胁度评分算法
private float CalculatePriorityScore(Actor self, Actor enemy)
{
    float score = 0f;
    float distance = Vector3.Distance(self.Position(), enemy.Position());

    // 基础分数：距离越近分数越高
    score += 1000f - distance;

    // 如果敌人正在瞄准，增加威胁度
    if (enemy.IsAiming())
    {
        Vector3 toMe = (self.Position() - enemy.Position()).normalized;
        Vector3 enemyFacing = enemy.Velocity().normalized;
        float alignment = Vector3.Dot(toMe, enemyFacing);
        if (enemy.Velocity().magnitude < 0.1f || alignment > 0.2f)
        {
            score += 500f;  // 敌人正在瞄准我，优先消灭
        }
    }

    // 载具威胁评估
    if (enemy.IsSeated())
    {
        score += 300f;
        Actor.TargetType targetType = enemy.GetTargetType();
        if (targetType == Actor.TargetType.Armored)
            score += 200f;  // 装甲载具威胁更大
        else if (targetType == Actor.TargetType.Air)
            score += 150f;  // 空中载具
    }

    // 武器有效性评估
    Weapon myWeapon = self.activeWeapon;
    if (myWeapon != null)
    {
        Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);
        switch (effectiveness)
        {
            case Weapon.Effectiveness.Preferred: score += 100f; break;
            case Weapon.Effectiveness.Yes: score += 50f; break;
            case Weapon.Effectiveness.No: score -= 200f; break;  // 武器无效，降低优先级
        }
    }

    return score;
}
```

#### 原版对比
原版 AI 主要基于距离选择目标，没有考虑敌人是否正在瞄准、载具类型、武器有效性等因素。改进后的 AI 能够：
- 优先消灭正在瞄准自己的敌人
- 根据所持武器选择合适的载具目标
- 对装甲载具和空中载具给予更高优先级

---

### 二、智能重生系统 (Part2_Spawn.cs)

#### 改进内容
1. **载具优先重生**：AI 优先选择有空闲己方载具的出生点
2. **战场附近出生**：提升在战场中心附近出生的概率

#### 具体实现

**载具优先逻辑**：
```csharp
private float CalculateSpawnPointScore(SpawnPoint spawnPoint, Vector3 battlefieldCenter)
{
    float score = 0f;
    Vector3 spawnPos = spawnPoint.GetSpawnPosition();

    // 距离战场中心越近，得分越高
    float distToBattlefield = Vector3.Distance(spawnPos, battlefieldCenter);
    score += Mathf.Max(0f, 500f - distToBattlefield);

    // 载具加成：附近有可用的己方载具，大幅加分
    List<Vehicle> nearbyVehicles = GetNearbyAvailableVehicles(spawnPos, spawnPoint.owner);
    if (nearbyVehicles.Count > 0)
    {
        score += 1000f;  // 高优先级使用载具
    }

    return score;
}
```

**战场中心计算**：
```csharp
private Vector3 CalculateBattlefieldCenter()
{
    Vector3 center = Vector3.zero;
    int count = 0;
    foreach (Actor actor in ActorManager.instance.actors)
    {
        if (actor == null || actor.dead) continue;
        center += actor.Position();
        count++;
    }
    return count > 0 ? center / count : Vector3.zero;
}
```

#### 触发概率
- 载具优先：70% 的 AI 会考虑载具因素
- 战场附近出生：80% 的 AI 会优先选择靠近战场的出生点

---

### 三、载具智能增强 (Part3_Vehicle.cs)

#### 改进内容
1. **障碍物检测与绕行**：AI 驾驶载具时能够检测前方障碍物并自动绕行
2. **速度提升**：部分 AI 驾驶载具时会提升速度

#### 具体实现

**障碍物检测**：
```csharp
private void HandleObstacleAvoidance(AiActorController ai, Vehicle vehicle)
{
    Vector3 vehiclePos = vehicle.transform.position;
    Vector3 vehicleForward = vehicle.transform.forward;

    // 前方射线检测（15米范围）
    if (Physics.Raycast(vehiclePos + Vector3.up, vehicleForward, out hit, 15f, obstacleMask))
    {
        // 计算绕行方向（垂直于前进方向的左右两侧）
        Vector3 avoidDirection = Vector3.Cross(vehicleForward, Vector3.up).normalized;

        // 选择更优的绕行方向
        if (Physics.Raycast(vehiclePos + Vector3.up, avoidDirection, 10f, obstacleMask))
        {
            avoidDirection = -avoidDirection;  // 左侧有障碍，走右侧
        }

        Vector3 avoidTarget = vehiclePos + avoidDirection * 20f + vehicleForward * 10f;
        ai.Goto(avoidTarget);
    }
}
```

**速度提升**：
```csharp
private void ApplySpeedBoost(AiActorController ai, Vehicle vehicle)
{
    // 30% 概率触发速度提升
    if (random.NextDouble() < 0.30)
    {
        Rigidbody rb = vehicle.rigidbody;
        if (rb != null)
        {
            Vector3 boostForce = vehicle.transform.forward * 15f;
            rb.AddForce(boostForce, ForceMode.Acceleration);
        }
    }
}
```

#### 触发概率
- 障碍物检测：60% 的 AI 载具会启用
- 速度提升：30% 的 AI 载具会获得 15% 速度加成

---

### 四、路径系统重构 (Part4_Path.cs) - V4 核心改进

#### 改进内容
**这是 V4 版本最重要的改进**。通过分析原版 IL 代码，精确还原了原版路径算法，并实现了 65/35 的分层策略。

#### 原版路径算法分析

通过 Mono.Cecil 反编译原版 `Assembly-CSharp.dll`，我们还原了原版 AI 的路径逻辑：

**原版 `Squad.NewAttackOrder()` 逻辑**：
1. 获取队长位置
2. 调用 `ClosestSpawnPoint()` 找到最近的出生点
3. 如果最近点是敌方所有 → 直接攻击该点
4. 如果最近点是己方所有 → 遍历相邻出生点
5. 收集所有敌方或中立的相邻点
6. 如果有敌方/中立相邻点 → 随机选择一个攻击
7. 如果没有 → 随机选择任意敌方出生点攻击

**原版 `Squad.MoveTo()` 逻辑**：
1. 设置 `hasAssignedOrder = true`
2. 设置状态为 `Moving`
3. 对每个队员调用 `Goto(point + randomOffset)`
4. 随机偏移范围：±3 米

#### V4 实现

**65% AI 严格遵循原版**：
```csharp
// 精确还原原版 NewAttackOrder 逻辑
private void ExecuteOriginalNewAttackOrder(Squad squad)
{
    AiActorController leader = squad.members[0];
    int team = leader.actor.team;

    // Step 1: 找到最近的出生点（原版 ClosestSpawnPoint）
    SpawnPoint closestPoint = FindClosestSpawnPoint(squad, team);

    // Step 2: 最近点是敌方？
    if (closestPoint.owner != team)
    {
        AttackSpawnPoint(squad, closestPoint);
        return;
    }

    // Step 3: 寻找相邻的敌方/中立点
    List<SpawnPoint> validTargets = new List<SpawnPoint>();
    foreach (SpawnPoint adjacent in closestPoint.adjacentSpawnPoints)
    {
        if (adjacent.owner != team) validTargets.Add(adjacent);
        if (adjacent.owner < 0) validTargets.Add(adjacent);  // 中立
    }

    // Step 4: 随机选择或回退
    if (validTargets.Count > 0)
    {
        int randomIndex = random.Next(0, validTargets.Count);
        AttackSpawnPoint(squad, validTargets[randomIndex]);
    }
    else
    {
        SpawnPoint randomEnemy = GetRandomEnemySpawnPoint(team);
        AttackSpawnPoint(squad, randomEnemy);
    }
}
```

**35% AI 使用增强策略**：
```csharp
private void ExecuteEnhancedStrategy(Squad squad)
{
    // 计算战场中心（所有存活 AI 的平均位置）
    Vector3 battlefieldCenter = CalculateBattlefieldCenter();

    // 优先选择靠近战场中心的目标
    SpawnPoint bestCandidate = null;
    float bestScore = float.MaxValue;

    foreach (SpawnPoint candidate in candidates)
    {
        float distToBattlefield = Vector3.Distance(candidate.transform.position, battlefieldCenter);
        float distToSquad = Vector3.Distance(candidate.transform.position, leader.actor.Position());
        float score = distToBattlefield * 0.6f + distToSquad * 0.4f;

        if (score < bestScore)
        {
            bestScore = score;
            bestCandidate = candidate;
        }
    }

    AttackSpawnPoint(squad, bestCandidate);
}
```

**策略动态切换**：
- 每个小队初始有 65% 概率使用原版策略
- 每 60-120 秒重新评估一次策略
- 确保游戏不会完全偏离原版体验

---

### 五、战斗风格与动态组队系统 (Part5_Style.cs)

#### 改进内容
1. **四种战斗风格**：普通、防御、激进、偷袭
2. **动态 33 制组队**：自动形成 3 人小组和 9 人大组
3. **守家 AI 进攻化**：防御型 AI 有概率转为进攻

#### 战斗风格概率（V4 调整）
```csharp
private CombatStyle AssignRandomCombatStyle()
{
    double roll = random.NextDouble();
    if (roll < 0.50) return CombatStyle.Normal;      // 50%
    else if (roll < 0.70) return CombatStyle.Defensive;   // 20%
    else if (roll < 0.95) return CombatStyle.Aggressive;  // 25%（大幅提升）
    else return CombatStyle.Stealth;     // 5%
}
```

#### 防御型 AI 的动态行为
```csharp
case CombatStyle.Defensive:
    // V4: 40% 概率防御型 AI 转为进攻
    if (random.NextDouble() < 0.40)
    {
        // 主动寻找最近敌人并进攻
        Actor nearestEnemy = FindNearestEnemy(ai.actor, enemies);
        if (nearestEnemy != null)
            ai.Goto(nearestEnemy.Position());
    }
    else
    {
        // 60% 概率保持防御，寻找掩体
        ai.FindCover();
    }
    break;
```

#### 动态 33 制组队
```csharp
// 自动寻找附近 2 个同阵营 AI 组成 3 人小组
private void FormTrioGroups(List<Actor> livingAI)
{
    while (ungroupedAI.Count >= 3)
    {
        Actor seed = ungroupedAI[0];
        List<Actor> nearestNeighbors = FindNearestNeighbors(seed, ungroupedAI, 2);

        TrioGroup newTrio = new TrioGroup();
        newTrio.members.Add(seed);
        newTrio.members.Add(nearestNeighbors[0]);
        newTrio.members.Add(nearestNeighbors[1]);
        newTrio.purpose = (TacticalPurpose)random.Next(3);
        activeTrioGroups.Add(newTrio);
    }
}

// 3 个 3 人小组距离较近时自动组成 9 人大组
private void FormNineGroups()
{
    while (ungroupedTrios.Count >= 3)
    {
        NineGroup newNine = new NineGroup();
        newNine.trios.Add(seed);
        newNine.trios.Add(nearestTrios[0]);
        newNine.trios.Add(nearestTrios[1]);
        newNine.objective = (StrategicObjective)random.Next(3);
        activeNineGroups.Add(newNine);
    }
}
```

#### 支援小组进攻化
```csharp
private void ExecuteSupportTactic(TrioGroup trio)
{
    // V4: 50% 概率支援小组主动推进
    if (random.NextDouble() < 0.50)
    {
        Actor nearestEnemy = FindNearestEnemy(leader, enemies);
        if (nearestEnemy != null)
        {
            foreach (var member in trio.members)
                ai.Goto(nearestEnemy.Position());
            return;
        }
    }

    // 标准支援行为：寻找掩体
    foreach (var member in trio.members)
        ai.FindCover();
}
```

---

## 可改动接口与开发指南

### 一、核心游戏类接口

#### 1. ActorManager（全局管理器）
```csharp
public class ActorManager : MonoBehaviour
{
    public static ActorManager instance;           // 单例实例
    public List<Actor> actors;                     // 所有角色列表
    public List<Vehicle> vehicles;                 // 所有载具列表
    public List<SpawnPoint> spawnPoints;           // 所有出生点列表

    public static SpawnPoint RandomEnemySpawnPoint(int team);  // 获取随机敌方出生点
}
```

#### 2. Actor（角色基类）
```csharp
public class Actor : Hurtable
{
    public int team;                               // 阵营（0=红队，1=蓝队）
    public bool dead;                              // 是否死亡
    public bool aiControlled;                      // 是否 AI 控制
    public float health;                           // 生命值
    public Seat seat;                              // 当前座位
    public ActorController controller;             // 控制器
    public Weapon activeWeapon;                    // 当前武器

    public Vector3 Position();                     // 获取位置
    public bool IsSeated();                        // 是否在座位上
    public bool IsDriver();                        // 是否是驾驶员
    public bool IsAiming();                        // 是否正在瞄准
    public Actor.TargetType GetTargetType();       // 获取目标类型
}
```

#### 3. AiActorController（AI 控制器）
```csharp
public class AiActorController : ActorController
{
    public Actor actor;                            // 控制的角色
    public Actor target;                           // 当前目标
    public Squad squad;                            // 所属小队
    public bool squadLeader;                       // 是否队长
    public bool hasPath;                           // 是否有路径
    public Pathfinding.Path path;                  // 当前路径

    public void Goto(Vector3 targetPoint);         // 移动到目标点
    public void FindCover();                       // 寻找掩体
    public void FindCoverAtPoint(Vector3 point);   // 在指定点寻找掩体
    public void FindCoverTowards(Vector3 direction); // 朝方向寻找掩体
    public bool HasTarget();                       // 是否有目标
    public bool InCover();                         // 是否在掩体中
    public bool IsSquadLeader();                   // 是否队长
    public bool IsTakingFire();                    // 是否正在受击
    public void MarkTakingFireFrom(Vector3 direction); // 标记受击方向
    public void EmoteMoveOrder(Vector3 point);     // 移动命令表情
    public void EmoteHailLeader();                 // 致敬队长表情
}
```

#### 4. Squad（小队系统）
```csharp
public class Squad
{
    public List<AiActorController> members;        // 队员列表
    public SpawnPoint targetSpawnPoint;            // 目标出生点
    public bool hasAssignedOrder;                  // 是否有分配命令
    public Squad.State state;                      // 当前状态
    public Vehicle squadVehicle;                   // 小队载具

    public void MoveTo(Vector3 point);             // 移动到点
    public void AttackSpawnPoint(SpawnPoint sp);   // 攻击出生点
    public void NewAttackOrder();                  // 新攻击命令
    public void DigIn();                           // 就地防守
    public void DigInTowards(Vector3 direction);   // 朝方向防守
    public void EnterVehicle(Vehicle vehicle);     // 进入载具
    public void ExitVehicle();                     // 离开载具
    public bool HasTargetSpawnPoint();             // 是否有目标出生点
    public bool IsTakingFire();                    // 是否正在受击
    public AiActorController Leader();             // 获取队长
}
```

#### 5. SpawnPoint（出生点）
```csharp
public class SpawnPoint : MonoBehaviour
{
    public int owner;                              // 拥有者（0=红，1=蓝，-1=中立）
    public List<SpawnPoint> adjacentSpawnPoints;   // 相邻出生点

    public Vector3 GetSpawnPosition();             // 获取出生位置
    public bool IsSafe(int team);                  // 对指定阵营是否安全
    public bool IsFrontLine();                     // 是否是前线
    public float GotoRadius();                     // 到达半径
}
```

#### 6. Vehicle（载具系统）
```csharp
public class Vehicle : Hurtable
{
    public bool dead;                              // 是否被摧毁
    public int ownerTeam;                          // 所属阵营
    public Seat[] seats;                           // 座位数组
    public bool stuck;                             // 是否卡住
    public Rigidbody rigidbody;                    // 物理刚体

    public bool HasDriver();                       // 是否有驾驶员
    public bool IsFull();                          // 是否满员
    public bool IsEmpty();                         // 是否空载
}
```

#### 7. Seat（座位）
```csharp
public class Seat : MonoBehaviour
{
    public Actor occupant;                         // 占据者
    public Vehicle vehicle;                        // 所属载具

    public bool IsOccupied();                      // 是否被占据
}
```

#### 8. Weapon（武器系统）
```csharp
public class Weapon : MonoBehaviour
{
    public enum Effectiveness { Preferred, Yes, No }  // 有效性等级
    public enum TargetType { Infantry, Armored, Air, Water }  // 目标类型

    public Effectiveness EffectivenessAgainst(TargetType targetType);
}
```

### 二、MOD 开发接口

#### AIEnhancement.dll 入口点
```csharp
// 游戏场景加载后自动调用
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
public static void OnGameStart()
{
    // 初始化所有增强系统
}
```

#### 可扩展的系统组件
1. **AIThreatUpdaterV3**：威胁评估系统，继承 MonoBehaviour
2. **AISpawnManager**：重生管理系统，继承 MonoBehaviour
3. **AIVehicleEnhancer**：载具增强系统，继承 MonoBehaviour
4. **AIPathOptimizerV4**：路径优化系统，继承 MonoBehaviour
5. **AICombatStyleSystem**：战斗风格系统，继承 MonoBehaviour

### 三、注入机制说明

MOD 使用 Mono.Cecil 库修改 `Assembly-CSharp.dll`，在 `GameManager.Awake()` 方法开头插入对 `AIEnhancementAutoStart.OnGameStart()` 的调用。

**注入流程**：
1. 备份原版 `Assembly-CSharp.dll` → `Assembly-CSharp.dll.backup`
2. 使用 Mono.Cecil 读取原版 DLL
3. 添加对 `AIEnhancement.dll` 的 Assembly 引用
4. 在 `GameManager.Awake()` 方法开头插入 `call AIEnhancementAutoStart.OnGameStart()`
5. 保存修改后的 DLL

**恢复原版**：
```bash
copy "Ravenfield_Data\Managed\Assembly-CSharp.dll.backup" "Ravenfield_Data\Managed\Assembly-CSharp.dll"
```

### 四、开发注意事项

#### 1. 编译环境要求
- **C# 版本**：C# 5.0（.NET Framework 4.0 编译器）
- **不支持语法**：字符串插值（$""）、表达式主体成员、async/await 等 C# 6+ 特性
- **字符串拼接**：使用 `+` 而非 `$""`

#### 2. Unity 版本限制
- **Unity 5.4.0f3**：部分新 API 不可用
- **反射访问**：大量私有方法需要通过 Reflection 调用
- **类型查找**：使用 `Type.GetType("ClassName, AssemblyName")`

#### 3. 常见陷阱
- **空引用检查**：所有 Unity 对象都需要显式检查 null
- **线程安全**：MOD 代码在主线程运行，但避免耗时操作
- **异常处理**：所有反射调用和外部方法调用需要 try-catch
- **DLL 锁定**：编译前确保游戏进程已关闭

#### 4. 性能优化建议
- **更新间隔**：使用 `timer += Time.deltaTime` 控制更新频率，避免每帧执行
- **缓存引用**：缓存 `ActorManager.instance` 等频繁访问的对象
- **列表复用**：避免在 Update 中频繁创建 List 对象
- **距离计算**：使用 `Vector3.SqrMagnitude` 替代 `Vector3.Distance` 进行距离比较

#### 5. 调试方法
- **日志输出**：使用 `Debug.Log("[Tag] Message")`
- **错误日志**：使用 `Debug.LogError("[Tag] Error")`
- **日志位置**：`%USERPROFILE%\AppData\LocalLow\SteelRaven7\Ravenfield\output_log.txt`
- **启动参数**：`ravenfield.exe -logFile "game_log.txt"`

### 五、可扩展方向

#### 1. 新增武器平衡
```csharp
// 修改 Weapon.EffectivenessAgainst 的行为
// 可以添加新的武器类型或调整现有武器对载具的有效性
```

#### 2. 自定义载具 AI
```csharp
// 扩展 AIVehicleEnhancer，添加新的载具行为
// 例如：直升机低空飞行、坦克迂回战术等
```

#### 3. 天气/时间系统影响
```csharp
// 根据游戏时间和天气调整 AI 行为
// 夜晚增加偷袭概率，雨天降低载具速度等
```

#### 4. 玩家行为学习
```csharp
// 记录玩家常用路线和战术
// AI 针对性地进行反制或配合
```

#### 5. 语音/指令系统
```csharp
// 扩展 AiActorController 的 Emote 方法
// 添加更多战术指令和反馈
```

---

## 版本历史

- **V1**：基础威胁评估系统
- **V2**：目标优先级、载具评估、小队重武器管理、33 制组队
- **V3**：载具优先重生、战场附近出生、载具障碍物判断、70/30 路线分配、战斗风格系统
- **V4**：精确还原原版路径算法（65/35 分配）、增强战斗参与度、守家 AI 进攻化

---

## 技术栈

- **游戏引擎**：Unity 5.4.0f3
- **运行时**：Mono .NET Framework 4.0
- **寻路系统**：A* Pathfinding Project
- **MOD 注入**：Mono.Cecil 0.11.5
- **编译器**：csc.exe (C# 5.0)

---

## 免责声明

本 MOD 仅供学习和研究使用。所有修改基于对原版游戏的逆向分析，不涉及任何商业用途。使用本 MOD 前请确保拥有正版游戏。
