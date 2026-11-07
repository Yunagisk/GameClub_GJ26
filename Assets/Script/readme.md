# 玩家控制器 · 重力交换银河城

适用于 Unity 2022.3.62f3c1。固定键位保存在 `Input/PlayerControls.inputactions` 的 Gameplay 动作图。PlayerInput 管理设备和动作实例，PlayerInputReader 集中采集操作，PlayerController2D 在 FixedUpdate 中按固定顺序驱动各组件。

## 接入场景

1. 在玩家根物体添加非 Trigger 的 CapsuleCollider2D 或 BoxCollider2D，再添加 PlayerController2D。Unity 会补齐输入、刚体、探测、移动、跳跃、冲刺和重力依赖。Rigidbody2D 的 Mass 设为 1，Linear Drag 设为 0，取消 X/Y 位置冻结。运行时控制器设置 Dynamic、Freeze Rotation、Continuous 和 Interpolate。
2. 在同一根物体的 **Player Input** 组件上，把 PlayerControls 拖入 **Actions**，**Default Map** 选 Gameplay，**Default Control Scheme** 选 KeyboardMouse，**Behavior** 选 Invoke C Sharp Events。无需手接 UnityEvents。项目已启用 Input System Package (New)。若 Unity 提示输入后端变更，请重启编辑器。
3. 为地板、天花板和可站立箱子设置实体 Collider2D，在 PlayerGroundProbe 的 Ground Layers 中勾选 Ground 和 GravityBox。Player Collider 指定角色主碰撞体（留空取根节点上的第一个）。检查 Physics 2D 层碰撞矩阵。默认探测盒针对约 1 单位宽的角色，探测宽度应略小于碰撞体。建议玩家碰撞材质摩擦、弹性均为 0，以减少贴墙滞留和落地弹跳。
4. 在根节点添加 **PlayerVisuals**，指定面朝右、直立的 SpriteRenderer，可使用美术子节点。若使用 Animator，创建同名 Bool 参数 `isGravityFlipped`。脚本在 LateUpdate 同步此参数及 flipX / flipY。
5. 保持项目 Physics 2D 的 Gravity 为 (0, -9.81)。玩家和箱子使用各自的 GravityBody2D，按收到的方向给刚体设置带符号的 gravityScale。玩家已有的接地探测、下落倍率、跳跃方向和动画均读取玩家自身状态。

## 默认值

单位按世界单位、秒、质量 1 计算。在默认重力下，长按 W 起跳的理论高度约 2.1 单位；地面从静止加速到满速约 0.13 秒，松开后约 0.10 秒停住。固定物理步会使实测值略有偏差。

| Inspector 字段 | 默认值 | 用途 |
|---|---:|---|
| groundMaxSpeed / airMaxSpeed | 7 / 6.5 | 地面／空中水平速度上限（常规移动） |
| groundAccel / airAccel | 55 / 32 | 输入方向的水平加速度 |
| groundDecel / airDecel | 70 / 20 | 松键刹车及同向超速回落，空中刹车较弱 |
| jumpForce | 12 | 沿重力反方向的跳跃冲量，改变质量会改变跳高 |
| fallGravityMultiplier | 1.65 | 沿重力方向下落时的倍率 |
| lowJumpGravityMultiplier | 2 | 上升阶段松开 W 时的倍率 |
| dashSpeed | 16 | 有符号水平爆发速度的绝对值 |
| dashCooldown | 0.32 | 地面和空中共享的冲刺间隔，单位秒 |
| airDashCountMax | 1 | 每次离地后可用的空中冲刺次数 |
| dashMomentumRetention | 0.85 | 爆发后一物理步保留的超出常规速度上限部分的比例 |
| dashCanOverwriteSpeed | true | 开启时覆盖水平速度，关闭时沿冲刺方向叠加 |
| gravityScale | 3.5 | 参与者的基础重力倍率（正值） |
| gravityExchangeCD | 0.3 秒 | 每个交换参与者自己的冷却时间 |
| startsUpward | 玩家 false，示例箱 true | 初始方向分别为 (0, -1) 和 (0, 1) |
| interactableLocked | false | 锁定此物件的 F 交互 |
| interactionRadius | 2 世界单位 | 玩家主碰撞体接触或进入此圆时允许按 F；半径独立于物件缩放 |
| interactionCenterOffset | (0, 0) | 圆心相对物件原点的局部偏移，可将门的交互位置移到把手 |
| interactionCooldown | 0.1 秒 | 同一物件两次成功 F 交互的最短间隔，按游戏时间计算 |
| actorCollider | 玩家主碰撞体 | PlayerInteractionPicker 用其最近点判断距离；留空时取玩家根节点上的 Collider2D |
| pickLayers | GravityBox、Interactable | 鼠标射线的候选层，索引分别为 8、9 |
| pickCamera | 默认取 PlayerInput.camera，再取 Camera.main | 现有场景显式指定 Main Camera |
| markedColor | (1, 0.75, 0.2, 1) | 示例箱被 F 标记时的颜色 |
| groundCheckDistance | 0.06 | 足底外沿的探测余量 |
| groundCheckSize | (0.65, 0.06) | 沿重力方向投射的盒子尺寸 |

## 文件职责（完整分类索引）

以下集中列出本目录全部 32 个 C# 文件与输入动作资产。路径相对 `Assets/Script`。普通组件挂载到对应 GameObject；接口和数据类不挂载。实现存档接口的组件，只有在同节点存在 SaveableEntity 时才会被收集。

### Audio：背景音乐

| 文件 | 职责 |
|---|---|
| Audio/MusicManager.cs | 自动创建并跨场景保留的单例，持有唯一音乐 AudioSource；读取场景配置，负责播放、停止、淡入淡出及死亡重载续播；不添加 AudioListener，不参与地图快照 |
| Audio/SceneMusic.cs | 每场景一个启用的音乐配置组件，提供曲目、音量、循环、淡入淡出时间与死亡续播开关；不创建音源、不保存播放进度 |

### SceneSwitch：开始界面入口

| 文件 | 职责 |
|---|---|
| SceneSwitch/StartGameController.cs | 挂在开始按钮上，自动订阅点击；提交 SceneA 加载请求，成功后锁定按钮并停止提示闪烁；只配置本次开始过渡，不自行加载场景或控制音乐 |

### Text：文字表现

| 文件 | 职责 |
|---|---|
| Text/BlinkText.cs | 对同节点 TextMeshProUGUI 的透明度进行正弦变化，实现提示文字呼吸闪烁；当前使用游戏时间，暂停时停止闪烁 |

### Input：输入配置

| 文件 | 职责 |
|---|---|
| Input/PlayerControls.inputactions | Gameplay 动作图，定义 A/D、W、Shift、F、左键选择、右键交换与 Point 鼠标位置的绑定；由 PlayerInput 引用 |

### Player：玩家控制与表现

| 路径（相对本目录） | 职责 |
|---|---|
| Player/PlayerInputReader.cs | 唯一输入采集入口。从所属 PlayerInput 读取轴、W 持有状态和鼠标位置；缓存跳跃／冲刺，分发 F／选择／交换事件；生命周期关闭操作时清理缓存并阻止输入 |
| Player/PlayerController2D.cs | 配置刚体，依次调度接地、冲刺次数重置、跳跃、水平移动和瞬时冲刺 |
| Player/PlayerHorizontalMotor.cs | 以 MoveTowards 插值 velocity.x，记录最后面朝方向 |
| Player/PlayerGroundProbe.cs | 沿 gravityDir 从当前支撑侧投射盒子，过滤自身、Trigger、墙面和离地上升状态 |
| Player/PlayerJump.cs | 接地时施加反重力冲量，根据运动方向和 W 状态选择重力倍率 |
| Player/PlayerDash.cs | 管理瞬时水平爆发、冷却、空中次数及一次性动量保留 |
| Player/PlayerVisuals.cs | 更新精灵朝向和 Animator 的重力状态 |
| Player/PlayerLifecycle.cs | 管理 Playing／Dying／Restoring／Reviving，控制输入、控制器、拾取和刚体模拟；请求死亡、放置复活玩家并重置重力／冲刺／朝向。文件同时定义 PlayerLifeState 和 PlayerMotionState 枚举 |
| Player/PlayerAnimationState.cs | 从生命周期、接地和速度提供只读 Life／Motion；区分 Idle／Running／Rising／Falling，死亡红色、死亡后的复活青色为占位表现；首次入场与正常换关显示角色原色；后续 Animator 可读取这些状态 |

### Gravity：独立重力能力

| 文件 | 职责 |
|---|---|
| Gravity/IGravityExchange.cs | 重力交换接口，定义方向、冷却状态、ExchangeGravity 和 ReceiveGravity；不负责计时或保存数据，不依赖 F 交互基类 |
| Gravity/GravityBody2D.cs | 在玩家或箱子的刚体节点实现重力交换，管理方向、倍率和自身冷却；保存／恢复方向、基础强度、当前倍率与剩余冷却；另提供玩家复活时重置向下的入口 |

### Interaction：通用交互、选择与箱子

| 文件 | 职责 |
|---|---|
| Interaction/InteractableBase.cs | F 交互抽象基类。登记启用物件，查找范围内最近目标；TryInteract 检查启用、锁定、圆形范围与冷却；绘制范围圆；提供锁定和剩余冷却的基础存档实现 |
| Interaction/EventInteractable.cs | F 交互时执行 Inspector 配置的 UnityEvent；继承基础交互存档，但不会自动保存事件接收者的业务状态 |
| Interaction/GravityBox.cs | 示例箱的 F 业务：切换颜色标记并触发事件；保存标记与基础交互状态，恢复后刷新颜色；重力能力由同节点 GravityBody2D 独立提供 |
| Interaction/GravitySelectionMarker.cs | 在已选物件上方显示独立的箭头，跟随移动并在取消选择时隐藏 |
| Interaction/GravityBoxPush.cs | 每个物理步检查玩家接触面与 A/D 方向，控制箱子的水平解锁和停止 |
| Interaction/PlayerInteractionPicker.cs | F 选择范围内最近的可交互物件；左键选中一个交换目标，右键对已选目标发起交换 |

### Saving：物件快照协议与数据

| 文件 | 职责 |
|---|---|
| Saving/ICheckpointState.cs | 状态组件接口：CaptureState 输出独立数据字符串，RestoreState 还原数据，RefreshState 在所有物件数据恢复后同步派生结果；接口不挂载 |
| Saving/LevelSnapshot.cs | 定义 LevelSnapshot 和 EntitySnapshot 数据类，记录关卡路径、当前复活点 ID、出生位置、物件基础状态及组件 JSON；本身不执行保存，也不挂载 |
| Saving/SaveableEntity.cs | 挂在参与存档的物件节点，提供固定唯一 ID；收集本节点 Transform、activeSelf、Rigidbody2D 和 ICheckpointState 组件；按组件完整类型名匹配恢复，提供统一刷新入口 |

### Checkpoints：复活点与恢复调度

| 文件 | 职责 |
|---|---|
| Checkpoints/LevelDefinition.cs | 每个关卡放置一个，指定默认出生 Transform 和淡出／淡入时长，并绘制出生位置；不保存当前进度 |
| Checkpoints/CheckpointInteractable.cs | F 范围交互复活点，只能成功激活一次；向 LevelSession 提交快照请求，保存已激活状态，区分当前／已使用／未使用的颜色；自动依赖 SaveableEntity，无需碰撞体 |
| Checkpoints/LevelSession.cs | 启动时自动创建并跨场景保留的总调度器。持有唯一最新内存快照，校验存档 ID／出生位置，处理保存、死亡重载、单向换关、分阶段恢复、相机对齐与过渡时序 |

### Mechanisms：测试机关

| 文件 | 职责 |
|---|---|
| Mechanisms/SwitchInteractable.cs | F 切换 isOn；保存 isOn、锁定和剩余冷却；RestoreState 只恢复这些值，RefreshState 再驱动所绑定的门并更新开关颜色；自动依赖 SaveableEntity |
| Mechanisms/DoorController.cs | SetOpen(bool) 明确设置门状态、BoxCollider2D 开关和颜色；当前不实现存档接口，门的开闭由 SwitchInteractable 的保存状态推导 |

### Hazards：死亡检测

| 文件 | 职责 |
|---|---|
| Hazards/DeathZone.cs | 挂在尖端／坠落区的 Trigger 上，通过进入和持续接触查找玩家刚体上的 PlayerLifecycle 并请求死亡；不执行地图恢复。地刺实体底座使用独立的非 Trigger 碰撞体 |

### Levels：关卡入口与过渡显示

| 文件 | 职责 |
|---|---|
| Levels/LevelExit.cs | 玩家进入出口 Trigger 时请求 LevelSession 加载 destinationScene；不携带旧关卡快照，也不自动生成返回出口 |
| Levels/TransitionOverlay.cs | 自动建立排序为 32767 的 Screen Space Overlay Canvas 和黑色 Image，Alpha 控制透明度、BlocksInput 控制点击拦截；随 LevelSession 跨场景保留，不执行加载业务 |

## 场景背景音乐

StartGame 的 ResourceSystem 已挂 SceneMusic，并指定 `Assets/Audio/StartGame.flac`。进入场景后自动从头播放并循环。MusicManager 在运行时自动创建并跨场景保留，无需手动挂载，不要在各场景重复添加音乐 AudioSource 或新的 AudioListener。

| SceneMusic 字段 | 默认值 | 说明 |
|---|---:|---|
| music | StartGame 已指定，其余按关卡配置 | 当前场景曲目；留空或场景无启用配置时停止旧音乐 |
| volume | 0.5 | 音源目标音量，范围 0～1 |
| loop | true | 循环播放；素材首尾是否无缝仍取决于音频本身 |
| fadeInDuration | 0.5 秒 | 新曲目从静音淡入的时间 |
| fadeOutDuration | 0.3 秒 | 通过 LevelSession 正常换关时旧曲目淡出时间 |
| continueOnDeathReload | true | 同一关卡死亡重载、曲目未改变时保留播放进度 |

为后续关卡配置音乐：创建普通空物体并挂 SceneMusic，将该关卡 AudioClip 拖入 Music 字段。每个场景只允许一个启用的 SceneMusic；发现重复时会报错并停止音乐，避免不确定地选中某首。目前只有 StartGame 音乐资源，其他关卡暂时保持安静。可将同一首音乐临时配置到测试关卡验证续播。

播放规则：首次进入场景或普通切换场景时从头播放，即使两个场景引用同一曲目也重启；返回 StartGame 从头播放菜单音乐。LevelSession 的死亡重载会明确传递死亡原因，只有同场景、相同曲目且启用 Continue On Death Reload 才续播，不重新调用 Play。关闭此选项后死亡重载也从头开始。非循环曲目播放完毕后，死亡续播不会让它重新响起。

LevelSession.Travel 同时启动画面淡出与音乐淡出，两者完成后才加载；不把两个时长简单相加。Load 在加载前告知音乐系统是否为死亡重载，MusicManager 在 sceneLoaded 后读取新配置并播放。新音乐从场景加载完成开始淡入，可能早于玩家恢复操作。直接调用 SceneManager 切换也会应用新场景音乐，但没有旧音乐提前淡出的阶段，也不会被识别为死亡续播。

音乐音源固定为 2D，不随角色距离改变；使用不受 Time.timeScale 影响的淡入淡出。当前暂停游戏时间不会自动暂停背景音乐。音乐与地图快照独立，不回滚到激活复活点时的播放位置。配置在场景加载时读取，运行中修改配置或禁用配置组件不会自动切歌；StopMusic 可显式停止当前播放。此版本仅由 Single 场景加载驱动音乐，Additive 加载不会抢占曲目。

StartGame 音频使用 Streaming 与 Load In Background，保留立体声，以减少长音乐整段解压和同步加载的开销；音量需按实际设备试听调整。全局音量设置、AudioMixer 分组、音效和双音源交叉淡化尚未加入；将来可独立扩展，不需要更改机关存档协议。StartGame 已通过 StartGameController 接入 SceneA；尚未添加返回菜单按钮。

## 接入与试玩

打开 TestScene 并点击 Game 窗口。箱子初始向上，玩家初始向下。在远处瞄准青色箱子按 F，箱子保持原色。靠近后，让玩家碰撞体进入箱子的交互圆，把鼠标移到空白处再按 F，箱子就会切换颜色。左键点箱子后，它上方会出现黄色箭头。把鼠标移到空白处或另一只箱子上再点右键，玩家仍与箭头所指的箱子交换重力。

左键点击另一个可交换目标会切换选择；重复点击同一目标会保留选择。左键点击空白、玩家自身或仅支持 F 的物体会清除选择。右键成功交换后保留选择，即使目标完全离开镜头也可再次交换。目标禁用、销毁或随场景卸载后清除选择。

右键默认冷却为 **0.3 秒（游戏时间，沿用当前项目配置）**，由玩家和目标各自计时。冷却中的点击直接忽略，长按到期也不会触发，需再次点按。冷却期间可以更换选中目标，玩家自身剩余的冷却仍然生效。F 保留独立的近距离判断与 0.1 秒间隔。

多个交互圆重叠时，每次按 F 只触发一个物件：从当前可用的物件中，选择圆心离玩家主碰撞体最近的一个。距离相同时选先启用的物件。锁定、禁用、冷却中和超出各自半径的物件均会跳过。

在 Scene 视图打开 Gizmos 并选中物件，即可查看 F 的范围。进入范围只取得交互资格，仍需按下 F。距离取圆心到玩家主碰撞体的最近点，正立和倒挂使用同一判定。每次成功交互后该物件冷却 0.1 秒；超距、锁定和冷却中按 F 均不会启动或延长冷却，按住 F 走进范围也不会补触发。

| 物件能力 | 挂载与设置 |
|---|---|
| 仅 F | 在物体上挂 EventInteractable 并配置 On Interact；目标自身无需 Collider2D，F 范围判定使用玩家碰撞体，也不按目标 Layer 过滤。自定义物件继承 InteractableBase，将交互业务写在 protected OnInteract()；额外条件可重写 CanInteract() 并保留 base.CanInteract()。外部通过 TryInteract(actorCollider) 发起交互。若还要被鼠标射线识别，再配置目标碰撞体和拾取层。 |
| 仅交换 | 在有 Collider2D 的物体上挂 GravityBody2D，刚体设 Dynamic，设为 GravityBox 层，选择 Starts Upward；也可自行实现 IGravityExchange。 |
| 两者都有 | 在箱子上挂 GravityBox，自动补齐 BoxCollider2D 与 GravityBody2D。编辑器首次挂载默认选择 GravityBox 层，初始向上。F 事件可在 Inspector 中追加。 |

GravityBoxPush 默认锁定 X，只有玩家贴住箱子侧面并按住朝向箱子的 A/D 才解锁，由碰撞推动箱子。松键、反向或脱离侧面时，在下一次物理更新锁回当前 X。箱面和边角接触优先保持锁定，玩家正向或倒挂站立时都可以自行走下箱子。箱子保留 Z 旋转锁定，Y 随自身重力运动。

TestScene 已给箱子的 BoxCollider2D 指定 BoxContact 物理材质（摩擦和弹性均为 0）。新建箱子时，挂 GravityBox 会一并添加 GravityBoxPush；已有箱子应确认存在该组件，并将 BoxContact 指定给实体碰撞体。要在出生点直接试推，可在编辑模式暂时取消箱子的 Starts Upward，让玩家和箱子一起落到地板上。

已有玩家预制体需确认根节点存在 PlayerInteractionPicker；TestScene 的 Player 已接好。新添 PlayerController2D 时 Unity 会补齐拾取组件。玩家自身的 Collider2D 放在拾取层之外。若目标使用子节点碰撞体，请给**碰撞体所在节点**设置拾取层，能力组件放在该节点或父节点。

左键选择时，射线从相机穿过鼠标位置，只取过滤层中的首个 Collider2D。可点范围为当前相机画面；重叠目标请错开碰撞体或 Z 深度。可按关卡需求把单纯用于遮挡的层加入 Pick Layers。Trigger 拾取遵循项目 Physics 2D 的 Queries Hit Triggers 设置。菜单使用 EventSystem 和对应的 UI Raycaster，并为需要拦截点击的 UI 元素开启射线命中。点击这些元素会保留选择，也不会交换。

IGravityExchange 的 ReceiveGravity 用于提交一次交换的结果。发起方先检查双方冷却并保存两个方向，再让双方各自接收方向、启动本地冷却；自定义实现应遵循相同约定。仅交换方向，双方保留各自的重力强度、倍率和速度。同向交换也消耗各自冷却。

## 物理约定与扩展

- A+D 同按时轴为零。按 Shift 时优先采用当时的轴，无方向时沿面朝方向。跳跃、冲刺和交换每次按下只消费一次；若在冷却中按下，该次操作直接失效。空中按 W 不会预约落地起跳。
- gravityDir 指向吸引方向，故跳跃使用 `-gravityBody.gravityDir * jumpForce`。交换后接地探测转向新支撑侧，已有竖直惯性保留。
- 冲刺在一个物理步内注入水平速度。下一步对剩余超速量应用一次保留系数，之后按常规加减速回落。按反方向可以立即制动；撞墙后不会重新注入上一步的速度。同一步跳跃和冲刺按空中冲刺计数，右键交换和冲刺均可生效。
- 动态接地探测只接受朝向玩家的支撑面，探测距离会影响边缘容差。狭平台、斜坡和更窄的角色需要匹配检测盒宽度。Continuous 能改善高速碰撞，关卡仍需使用有厚度的实体边界。
- 单向平台可读取接触方 IGravityExchange.gravityDir。角色和箱子可能分别站在同一平台的两侧，后续单向平台应按每一对碰撞体判断放行方向。

## Play Mode 验收

| 操作 | 预期 |
|---|---|
| 地面按住 D 后松开，再在空中重复 | 地面快速停稳，空中减速较缓；反向按 A 始终可修正 |
| 短按、长按 W，各自保持至落地 | 短按跳得较低，长按只起跳一次 |
| 空中 Shift，等冷却结束再按 Shift | 第一次水平爆发并自然下坠；第二次需等落地重置 |
| 左键选中异向箱，冲刺时点按右键并按住 | 双方只交换一次，保留各自竖直惯性 |
| 选中同向箱后按右键，或在任一方冷却时再点 | 同向时方向不变；冷却期间拒绝下一次交换 |
| 在圆外按住 F，再走入圆内，松开并重新按 F | 入圈时保持原色；重新按下立即变色一次 |
| 对同一箱子在 0.1 秒内再次点按 F；间隔满后重新点按 | 快速连点只执行第一次，间隔满后可再次变色；其他物件独立计时 |
| 在圆内启用 Interactable Locked，或退到圆外，再分别试 F 和右键 | F 无效，右键仍遵循交换双方的冷却 |
| 在交互圆内，鼠标指向空白或仅交换物件时按 F | 最近的可用 F 物件触发一次 |
| 选中 A，将鼠标移到 B 或空白处，右键 | 只与 A 交换；A 离开镜头后依然有效 |
| 交换后在 0.3 秒内再次点按或一直按住 | 不排队；冷却结束后重新点按，才可再次交换 |
| 左键点空白后再按右键；选中 A 后点击 UI | 前者清除选择并无交换；后者保留选择，点击 UI 时不会交换 |
| 在天花板行走后按 W、Shift | 从天花板向下起跳，重新获得一次空中冲刺 |
| 从箱子左侧按 D、从右侧按 A，随后松键或反向 | 箱子朝按键方向移动，停止推动后停在当前 X |
| 在箱面上走下边缘；倒挂时从下表面重复 | 玩家走下箱子，箱子 X 坐标保持不变 |
| 贴墙下落，或经过 Trigger | 墙和 Trigger 不提供可跳跃的支撑面 |

## 复活与地图快照

新测试关卡位于 Assets/Scenes/SceneA.unity 和 SceneB.unity，已加入 Build Settings。打开 SceneA 后运行。原 TestScene 可继续用于已有移动与重力交换测试。

相关脚本已全部合并到前面的「文件职责（完整分类索引）」。以下说明配置与实际恢复过程。

### 推荐默认值与场景配置

| 字段 | 默认值 | 所在组件 |
|---|---:|---|
| deathDuration | 0.3 秒 | PlayerLifecycle，死亡红色占位时间 |
| reviveDuration | 0.6 秒 | PlayerLifecycle，入场／复活占位时间，死亡复活显示青色 |
| fadeOutDuration / fadeInDuration | 0.15 / 0.15 秒 | LevelDefinition，关卡过渡时长 |
| interactionRadius | 2 世界单位 | CheckpointInteractable、SwitchInteractable，继承基类字段 |
| interactionCooldown | 0.1 秒 | 同一物件成功 F 交互的最短间隔 |
| interactableLocked | false | 通用交互锁定 |
| defaultSpawn | 必填 | LevelDefinition，玩家根节点的世界出生位置 |
| respawnPoint | 必填 | CheckpointInteractable，玩家根节点的世界复活位置 |
| destinationScene | SceneB | LevelExit，目标必须启用在 Build Settings 中 |
| persistentId | 编辑器生成唯一值 | SaveableEntity，同关卡内必须唯一 |

新关卡放一个 LevelDefinition 并指定 DefaultSpawn。玩家挂 PlayerLifecycle 和 PlayerAnimationState；现有移动、输入、交互组件继续放在玩家根节点。LevelSession 在启动时自动创建，读取当前 LevelDefinition 的过渡参数。

测试复活点为黄色柱体，激活后当前点为青色，已经使用的旧点为灰色。复活点无 Collider2D。独立的 RespawnPoint 位于装置右侧 1.2 世界单位，测试玩家根节点高度为 -3.24，确保碰撞体底部在地板上方。

地刺分成灰色实体底座和红色尖端。底座是普通 BoxCollider2D，尖端是 Trigger 并挂 DeathZone。玩家触碰尖端后停止运动，经过死亡占位时间和淡出后重载；复活位置准备完成即淡入，并播放复活占位表现，结束后接受操作。地图物件在淡入时继续运行，玩家在占位阶段固定于出生位置。

### 存档范围与扩展约定

SaveableEntity 决定存档范围，Layer 继续用于原有碰撞和鼠标选择。同节点上的 ICheckpointState 组件全部参与保存，子节点有独立逻辑时需明确安排对应的状态组件。复制存档物件后若 ID 重复，在 SaveableEntity 组件菜单执行 Generate new save ID for duplicate；加载时会检查缺失和重复 ID。

当前覆盖预放置物件。快照保存 Transform、启用状态、刚体线速度／角速度／Simulated，以及各状态组件提供的数据。重力箱保存方向、倍率、冷却剩余时间和 F 标记；交互组件保存锁定和冷却剩余时间。复活点保存是否使用。箱子的水平运动仍遵循侧推规则，恢复后离开玩家侧推接触时会重新锁定 X。

测试机关由一个开关完全决定一扇门，因此开关保存逻辑状态，门在 RefreshState 阶段直接应用对应外观和碰撞。若门以后具有独立的钥匙或剧情状态，为门实现 ICheckpointState。多开关联动应在所有数据恢复后统一计算最终门状态，恢复过程中使用 SetOpen 一类明确设置入口。

保存的组件数据转成独立 JSON 字符串，后续游玩不会改变旧快照。保存时发现新生成的 SaveableEntity 会拒绝提交并保留旧快照；运行时生成物件需要后续增加生成类型、实例 ID 与生成适配器。预放置物件在快照后销毁，会通过重载原场景重新建立；快照前已经销毁的预放置物件，在恢复时禁用。禁用物件也参与收集，快照组件应允许在 Awake 尚未执行时安全读取和恢复状态。

地图恢复依次处理物件数据、启用状态和 RefreshState，最后同步物理、放置玩家和对齐 Cinemachine。新机关若在 Update 中自行推进，恢复阶段应检查 LevelSession.Instance.IsRestoring 并暂停推进。不要在 Start 中无条件覆盖 RestoreState 已写入的值。粒子、声音和奖励只在正常交互时播放，RefreshState 直接同步结果。

### 数据恢复的实际调用流程

当前采用「重载原始场景，再应用最新数据快照」。LevelSession 的 snapshot 字段持有这一份内存数据，并通过 DontDestroyOnLoad 跨场景重载保留。没有在运行时把地图生成预制体，也没有写入硬盘存档文件。JSON 字符串只是各组件的数据载体。

保存入口：

1. PlayerInteractionPicker 收到 F 事件，找到范围内可用的复活点，调用继承的 TryInteract。
2. CheckpointInteractable.OnInteract 先把 activated 设为 true，使本次快照包含「该点已使用」，再传入自己的 ID 和 respawnPoint.position 调用 LevelSession.SaveCheckpoint。提交失败时恢复未激活状态。
3. LevelSession 校验出生空间，调用 Capture 收集所有仍存在的已登记 SaveableEntity，包括禁用物件；完整构建新快照后替换旧 snapshot。
4. 每个 SaveableEntity.Capture 记录自身局部位置／旋转／缩放、activeSelf、刚体速度等，再调用同节点每个 ICheckpointState.CaptureState，把返回字符串与组件类型名对应保存。后续修改运行对象不会改写这些字符串。

死亡恢复入口：

1. DeathZone → PlayerLifecycle.RequestDeath → LevelSession.RequestDeath。接受死亡后玩家进入 Dying，关闭输入和刚体模拟。
2. LevelSession.Reload 等待死亡占位时间、淡出，再由 Load 使用 LoadSceneAsync(..., Single) 重载 snapshot.scenePath。重载重建场景中的原始对象及 Inspector 引用。
3. 暂停物理模拟，等待场景 Start 完成；TryInitialize 重新收集全部 SaveableEntity，以固定 ID 匹配快照记录，不使用重载前的对象引用或临时 InstanceID。
4. 对所有记录执行 SaveableEntity.Restore：恢复 Transform、刚体数据，并调用各组件 RestoreState。随后统一恢复 activeSelf；原场景有但快照没有的存档对象会被禁用，表示它在保存前已经销毁。
5. 全部物件数据恢复后，RefreshEntities 统一调用各组件 RefreshState，同步机关联动、门碰撞和视觉表现。业务组件不应在此重放 F 操作、奖励或声音。部分组件会在 RestoreState 内直接应用自身物理参数，例如 GravityBody2D 的 gravityScale。
6. 同步物理 Transform、检查出生空间，PlayerLifecycle.PlaceForRevival 放置玩家并重置玩家默认状态；相机对齐后恢复地图物理、淡入和复活占位表现，最后开放操作。

若进入新关卡，LevelSession.Travel 调用 Load(destination, null)，不会应用旧快照。新关卡初始化成功后，从其默认地图和 DefaultSpawn 创建新的默认快照，覆盖上一关记录。

### 开关和门如何保持恢复一致

当前测试机关是一只开关控制一扇门。**开关的 isOn 是开闭状态的唯一来源，门根据它设置结果。**

| 阶段 | SwitchInteractable | DoorController |
|---|---|---|
| 正常按 F | OnInteract 翻转 isOn，再调用 RefreshState | 接收 SetOpen(isOn) |
| 激活复活点 | CaptureState 把 isOn 保存为 JSON 中的 on，并保存继承的锁定与冷却 | 不单独保存开闭值 |
| 重载并恢复数据 | RestoreState 将保存的 on 写回 isOn，此时不调用 F 业务 | 等待统一刷新阶段应用最终结果 |
| 统一刷新 | RefreshState 调用 door.SetOpen(isOn)，更新开关红／绿色 | 同步 IsOpen、BoxCollider2D.enabled = !open 和门的颜色 |

例如存档时开关为关闭，之后玩家开门再死亡：重载后把开关还原成关闭，统一刷新调用 SetOpen(false)，门碰撞体重新启用、外观恢复关闭色。存档时为开启，则调用 SetOpen(true)，门恢复可通过状态。这里用明确的布尔值设置，重复刷新也不会反向切换。

配置时在开关节点保留 SaveableEntity，并将目标门拖入 SwitchInteractable 的 Door 引用。门的开闭派生自开关，因此当前门无需额外存一份开闭状态。如果门的位置也会改变，需要为门挂 SaveableEntity 保存 Transform；如果门有独立的钥匙解锁、永久打开或剧情状态，则还需实现自己的 ICheckpointState 并明确它与开关的优先关系。

当前一对一实现不能直接作为多开关控制同一扇门的方案：多个开关的 RefreshState 会相互覆盖，结果取决于最后一次设置。扩展时应增加负责门条件的统一组件，在相关开关数据全部恢复后汇总 AND／OR 等规则，再调用一次 SetOpen。该协调组件尚未实现。

### 新机关接入存档时需要明确的边界

- 只挂 SaveableEntity 不会自动保存任意脚本字段，也不会记录 UnityEvent 的所有副作用。它只保存已列出的基础数据，以及同节点 ICheckpointState 明确提供的数据。
- 自定义 F 机关继承 InteractableBase 时已有基础接口实现，但仍需覆盖 CaptureState／RestoreState 保存自身业务值，并用 CaptureInteraction／RestoreInteraction 保留基类锁定与冷却；RefreshState 直接应用结果，不调用 OnInteract。
- EventInteractable 若通过事件打开一扇门，仅能自动保存交互基类状态。应改用当前 SwitchInteractable，或为事件影响到的对象实现独立状态保存，否则仍可能出现开关与门不一致。
- 同节点不能挂两个相同完整类型名的状态组件；子节点状态不会被父节点 SaveableEntity 自动收集。当前基础记录也不保存运行时换父节点、任意组件 enabled、材质或全部刚体设置，需要时由业务状态组件补充。
- 快照恢复会还原物件的线速度和角速度；玩家复活则明确清零速度并恢复向下重力。这与日常重力交换不同：交换过程仍不修改双方速度。
- 保存前已经销毁的预放置对象，恢复时会被禁用；保存后才销毁的对象，由原始场景重载重新创建并应用记录。运行时新生成的存档对象暂不支持。

### 玩家与关卡生命周期

玩家的生命周期与移动状态独立。占位时长使用真实经过时间；交互、冲刺、重力交换冷却继续采用游戏时间。复活重置重力向下、速度为零、冲刺次数和冷却、朝向向右，并清除选择和输入缓存。A/D 在开放操作后读取当前持有状态，W、Shift、F 和鼠标动作需要新的点按。

新加载的玩家采用该关卡序列化的配置。未来的 Animator 桥可读取 PlayerAnimationState.Life 和 Motion，生命周期动画优先于移动动画。当前使用可调时长和颜色占位，尚未接入动画结束事件。

只保留当前关卡的一份内存快照。死亡重载当前关卡并读取这份记录；进入 B 会在 B 初始化成功后建立 B 的默认记录并替换 A。未激活复活点时，死亡恢复默认地图与默认出生点。初始快照在场景 Start 完成且物理开始推进前建立。A 中的紫色出口只通往 B，B 没有返回 A 的出口。

死亡和换关请求在下一次调度中统一处理，死亡优先。复活流程中重复接触地刺只恢复一次。出生点若被实体或死区占用、存档 ID 重复等，会输出具体错误并保持玩家操作锁定，请修正场景后重新运行。此版本的快照用于单场景关卡与本次运行，退出游戏后的读档、叠加场景、运行时生成机关尚需另行扩展。

### 测试路线

1. 打开 SceneA，玩家初始在最左侧。青色重力箱默认向下，便于先测试推动和 F 标记。
2. 走到红色开关附近按 F。门变成半透明绿色并可通过。
3. 在门后第一根黄色柱体附近按 F，柱体变青。再返回开关关闭门，推动箱子或交换箱子重力，改变地图。
4. 接触红色地刺尖端。观察玩家变红、短暂淡出、回到复活点右侧变青，然后恢复操作。门应恢复保存时的状态，箱子和重力也应恢复。
5. 跳过地刺，激活第二个复活点。第一个点变灰且无法再次激活，之后死亡出现在第二个点旁。
6. 进入最右侧紫色出口。B 的门为关闭状态、复活点未使用。直接触碰 B 的地刺，会回到 B 的默认出生位置。


## StartGame → SceneA → SceneB

StartGame 的 Button 已挂 StartGameController，Prompt Blink 已引用文字闪烁组件；启用时自动订阅 Button.onClick，因此 Inspector 的 On Click 列表可以为空，不要重复手动绑定。文字 Raycast Target 关闭、按钮 Image 开启。按钮使用全屏拉伸的透明 Image 接收点击，子节点 Background 显示原图并通过 AspectRatioFitter（Envelope Parent，16:9）保持比例铺满屏幕，非 16:9 会裁切边缘。背景子节点不接收射线，因此底部文字位置和屏幕边缘都能点击。SceneA 出口的旧名称引用已改为 SceneB，两个关卡均保持现有配置。

| StartGameController 字段 | 默认值 | 含义 |
|---|---|---|
| destinationScene | Assets/Scenes/SceneA.unity | 使用完整场景路径定位目标，必须在 Build Settings 中启用 |
| promptBlink | 提示文字上的 BlinkText | 请求成功后禁用闪烁，文字保留在当前亮度 |
| fadeOutDuration | 0.35 秒 | 开始界面逐渐变黑 |
| minimumBlackDuration | 0.15 秒 | 全黑最短时间，加载／初始化和等待音乐淡出所花时间计入其中 |
| fadeInDuration | 0.45 秒 | 关卡画面逐渐显露 |

点击时先请求 LevelSession.TryStartGame，目标无效或系统正在切换时返回 false，按钮和文字继续保持可用；接受请求后锁定按钮、停止闪烁并拦截其他 UI 点击。LevelSession 在下一次 Update 调度，画面与菜单音乐同时淡出，确保全黑画面有机会呈现后再异步加载。初始化默认地图快照、出生位置、相机后淡入，入场占位结束才开放控制。默认玩家占位为 0.6 秒，与 0.45 秒淡入重叠，淡入结束后剩余约 0.15 秒。

开始入口的时间仅用于本次请求，不覆盖 LevelDefinition 的关卡过渡配置。原 RequestTravel(string) 仍可供出口使用；新增 TryRequestTravel 返回请求是否被接受，CanTravel 为只读可请求状态。死亡仍优先于同一步的普通换关请求。PlayerLifecycle.IsRespawning 区分死亡复活与普通入场；两者使用相同 Reviving 生命周期锁定操作，但普通入场使用角色原色，死亡复活使用青色。

遮罩 Canvas 是 LevelSession 的运行时子物体，无需手动创建，不要把其他游戏 UI 排序设置到遮罩之上。透明度与点击拦截独立：过渡开始即拦截，完成入场才放行。淡出淡入使用真实时间。黑屏能遮住加载的画面跳变，但不能消除重型场景初始化的主线程耗时。

验证路线：运行 StartGame，点击背景或文字位置，应只进入一次 SceneA；观察音乐切换、默认出生点、角色原色和过渡期间输入锁定；死亡时确认青色复活表现和音乐续播；走到 SceneA 出口应进入 SceneB。若目标缺失，在请求阶段拒绝并记录错误；若关卡内部配置或出生点无效，沿用现有恢复失败保护，解除黑色遮罩但保持玩家操作锁定，请修正 Console 指出的配置。
