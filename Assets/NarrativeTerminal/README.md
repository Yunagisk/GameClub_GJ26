# 剧情日志终端（只含日志模块）

这个模块只负责“剧情日志终端”，**不包含玩家角色、关卡、地面或相机控制**。

## 交付内容

- `Scripts/`：日志数据、终端交互、UI 显示。
- `Prefabs/NarrativeTerminalCanvas.prefab`：日志 UI，一个场景放一个。
- `Prefabs/NarrativeLogTerminal.prefab`：场景里的终端物体，可以放多个。
- `Content/ExampleLog.asset`：日志内容，策划/剧情可以直接在 Inspector 改。
- `Demo/NarrativeTerminalDemo.unity`：只有日志终端和 UI 的预览场景，没有可见角色。

## 给组里用

1. 把 `NarrativeTerminalCanvas.prefab` 拖进正式场景。
2. 把 `NarrativeLogTerminal.prefab` 拖到想要的位置，可以复制多个。
3. 给每个终端换 `Log Asset`，或直接编辑 `ExampleLog.asset`。
4. 如果打开终端时要冻结角色：把角色的移动脚本拖到终端的 `Player Control Scripts`。

## 预览

打开 `Demo/NarrativeTerminalDemo.unity`，按 Play。场景里有一个不可见的交互探针，所以会直接出现“按 F 读取终端”的提示。

- `F`：打开 / 关闭
- `Esc`：关闭
- `A / D`：翻页
- `W / S`：滚动正文

## 说明

- 玩家需要 `Player` Tag 和 `Collider2D`，终端才能检测到。
- 一个场景只放一个 `NarrativeTerminalCanvas`，多个终端共用它。
- 如果角色是直接改 Transform 的移动方式，必须把移动脚本拖到 `Player Control Scripts`；否则冻结不住。
