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
| gravityExchangeCD | 0.7 秒 | 每个交换参与者自己的冷却时间 |
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

## 文件职责

| 路径（相对本目录） | 职责 |
|---|---|
| Input/PlayerControls.inputactions | 移动、跳跃、冲刺、F、左键选中、右键交换以及鼠标屏幕坐标 Point 的绑定 |
| Player/PlayerInputReader.cs | 从所属 PlayerInput 的动作实例读取轴和 W 持有状态，缓存跳跃和冲刺按下边沿供物理步消费，F、左键和右键通过独立 performed 事件分发 |
| Player/PlayerController2D.cs | 配置刚体，依次调度接地、冲刺次数重置、跳跃、水平移动和瞬时冲刺 |
| Player/PlayerHorizontalMotor.cs | 以 MoveTowards 插值 velocity.x，记录最后面朝方向 |
| Player/PlayerGroundProbe.cs | 沿 gravityDir 从当前支撑侧投射盒子，过滤自身、Trigger、墙面和离地上升状态 |
| Player/PlayerJump.cs | 接地时施加反重力冲量，根据运动方向和 W 状态选择重力倍率 |
| Player/PlayerDash.cs | 管理瞬时水平爆发、冷却、空中次数及一次性动量保留 |
| Gravity/IGravityExchange.cs | 定义重力方向、冷却状态、发起交换以及接收对方方向的契约 |
| Gravity/GravityBody2D.cs | 实现 IGravityExchange，持有单个刚体的方向、倍率和冷却，执行双方交换 |
| Player/PlayerVisuals.cs | 更新精灵朝向和 Animator 的重力状态 |
| Interaction/InteractableBase.cs | F 交互入口 TryInteract，检查启用状态、锁、距离和物件自己的冷却；选中物件时绘制范围圆 |
| Interaction/EventInteractable.cs | 仅 F 的通用物件，在 Inspector 的 On Interact 挂接开关、门或机关事件 |
| Interaction/GravityBox.cs | 示例箱的 F 业务：切换颜色标记并触发 On Interact；同物体上的 GravityBody2D 提供交换能力 |
| Interaction/GravitySelectionMarker.cs | 在已选物件上方显示独立的箭头，跟随移动并在取消选择时隐藏 |
| Interaction/GravityBoxPush.cs | 每个物理步检查玩家接触面与 A/D 方向，控制箱子的水平解锁和停止 |
| Interaction/PlayerInteractionPicker.cs | F 选择范围内最近的可交互物件；左键选中一个交换目标，右键对已选目标发起交换 |

## 接入与试玩

打开 TestScene 并点击 Game 窗口。箱子初始向上，玩家初始向下。在远处瞄准青色箱子按 F，箱子保持原色。靠近后，让玩家碰撞体进入箱子的交互圆，把鼠标移到空白处再按 F，箱子就会切换颜色。左键点箱子后，它上方会出现黄色箭头。把鼠标移到空白处或另一只箱子上再点右键，玩家仍与箭头所指的箱子交换重力。

左键点击另一个可交换目标会切换选择；重复点击同一目标会保留选择。左键点击空白、玩家自身或仅支持 F 的物体会清除选择。右键成功交换后保留选择，即使目标完全离开镜头也可再次交换。目标禁用、销毁或随场景卸载后清除选择。

右键默认冷却为 **0.7 秒（游戏时间）**，由玩家和目标各自计时。冷却中的点击直接忽略，长按到期也不会触发，需再次点按。冷却期间可以更换选中目标，玩家自身剩余的冷却仍然生效。F 保留独立的近距离判断与 0.1 秒间隔。

多个交互圆重叠时，每次按 F 只触发一个物件：从当前可用的物件中，选择圆心离玩家主碰撞体最近的一个。距离相同时选先启用的物件。锁定、禁用、冷却中和超出各自半径的物件均会跳过。

在 Scene 视图打开 Gizmos 并选中物件，即可查看 F 的范围。进入范围只取得交互资格，仍需按下 F。距离取圆心到玩家主碰撞体的最近点，正立和倒挂使用同一判定。每次成功交互后该物件冷却 0.1 秒；超距、锁定和冷却中按 F 均不会启动或延长冷却，按住 F 走进范围也不会补触发。

| 物件能力 | 挂载与设置 |
|---|---|
| 仅 F | 在带 Collider2D 的物体上挂 EventInteractable，设为 Interactable 层，配置 On Interact。自定义物件继承 InteractableBase，将成功交互的业务写在 protected OnInteract()；额外条件可重写 CanInteract() 并保留 base.CanInteract()。外部通过 TryInteract(actorCollider) 发起交互。 |
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
| 交换后在 0.7 秒内再次点按或一直按住 | 不排队；冷却结束后重新点按，才可再次交换 |
| 左键点空白后再按右键；选中 A 后点击 UI | 前者清除选择并无交换；后者保留选择，点击 UI 时不会交换 |
| 在天花板行走后按 W、Shift | 从天花板向下起跳，重新获得一次空中冲刺 |
| 从箱子左侧按 D、从右侧按 A，随后松键或反向 | 箱子朝按键方向移动，停止推动后停在当前 X |
| 在箱面上走下边缘；倒挂时从下表面重复 | 玩家走下箱子，箱子 X 坐标保持不变 |
| 贴墙下落，或经过 Trigger | 墙和 Trigger 不提供可跳跃的支撑面 |
