# fps_BackToBeAKING (RoguePulse Prototype)

A 3D third-person shooter roguelike prototype built with Unity.  
The project focuses on validating a playable loop: combat, loot, progression, and escalating enemy pressure.

## 1. Project Overview

Current gameplay focus:
- Third-person movement and combat
- Mixed enemy ecosystem (ground, ranged, elite, airborne)
- Kill-drop-progression systems
- Real-time HUD feedback (HP, gold, level, XP, threat)

Core loop:
`Fight enemies -> collect drops -> gain XP -> level up -> survive rising pressure`

## 2. Implemented Features

### 2.1 Combat
- Player supports movement, sprint, jump, aim, and attack
- Both ranged projectile combat and melee hit detection are implemented
- Animator parameters are integrated with JUTPS-style controllers
- Backward movement speed/animation mismatch has been tuned to reduce visual inconsistency

### 2.2 Enemy AI & Spawn
- Ground enemies support basic AI behaviors: chase, rotate, attack cooldown
- Elite enemies spawn with higher pressure and announcement feedback
- Third elite Slayer is integrated:
  - 1 spawned at run start
  - 1 spawned every 20 seconds afterward
- Air minion (`SciFi_Beast02`) is integrated as a flying ranged enemy
- Air-minion guarantee logic is enabled to keep airborne pressure persistent

### 2.3 Progression & Loot
- Enemy death drops blue XP orbs, gold, and other pickups
- Blue XP orbs support automatic pickup
- Level rule: fixed `100 XP` per level
- Level-up choices are connected to stat/reward growth

### 2.4 UI / Feedback
- HUD displays HP, Gold, Level, XP, Stage, Threat, and Objective
- HP bar shrinks visually when health is reduced
- Elite warnings and level-up choice prompts are shown at runtime

### 2.5 Grounding / Anti-Clipping
- Ground projection is applied during land-enemy spawn
- Visual feet are aligned to collider base
- Multi-frame post-spawn grounding is used to reduce floating/sinking/clipping

## 3. Controls

- `W / A / S / D`: Move
- `Shift`: Sprint
- `Space`: Jump
- `Mouse Left`: Attack / Shoot
- `Mouse Right`: Aim
- `E`: Interact
- `1 / 2 / 3`: Select level-up option

## 4. Quick Start

1. Open the project in Unity Hub (recommended Unity 6.3 LTS, e.g. 6000.3.x).
2. Open scene: `Assets/Scenes/Level01_Inferno.unity` (or `Assets/Scenes/Main.unity`).
3. Press Play.

## 5. External Asset Packages

Large third-party assets are handled through editor import flows (not all stored directly in Git):
- `RoguePulse/Import/Import POLYGON Fantasy Rivals 1.3.1`
- `RoguePulse/Import/Import SciFi Beasts Pack 1.0`
- `RoguePulse/Setup Characters/1. Import JU TPS 3 Package`
- `RoguePulse/Setup Characters/2. Apply JU TPS Animations To Player`

If your local package paths differ, update path constants in scripts under `Assets/Scripts/Editor`.

## 6. Project Structure

- `Assets/Scripts/Runtime/Combat` - player/enemy combat logic
- `Assets/Scripts/Runtime/Directors` - spawn and stage flow
- `Assets/Scripts/Runtime/Progression` - XP, level-up, build systems
- `Assets/Scripts/Runtime/UI` - HUD and runtime feedback
- `Assets/Scripts/Runtime/Core` - game managers and shared systems
- `Assets/Scripts/Editor` - tooling for scene setup and asset import

## 7. Notes

- This repository is an academic gameplay prototype with iterative development focus.
- If `dotnet build` fails from CLI on some machines, compile and run through Unity Editor directly.


# fps_BackToBeAKING (RoguePulse Prototype)

一个基于 Unity 的 3D 第三人称射击 Roguelike 原型项目。  
核心目标是验证“战斗 -> 掉落 -> 成长 -> 更高压力战斗”的可玩闭环。

## 1. Project Overview

本项目当前聚焦以下体验：
- 第三人称移动与战斗
- 地面怪 + 远程怪 + 精英怪 + 空中怪的混合敌人生态
- 击杀掉落、经验成长、升级选择
- HUD 实时反馈（血量、金币、等级、经验、威胁等）

核心循环：
`Fight enemies -> collect drops -> gain XP -> level up -> survive rising pressure`

## 2. Implemented Features

### 2.1 Combat
- 玩家支持移动、冲刺、跳跃、瞄准、攻击
- 支持远程投射物攻击与近战范围命中
- 动画参数已适配 JUTPS 控制器
- 后退移动速度与动画匹配已优化（减少“前跑后退”违和）

### 2.2 Enemy AI & Spawn
- 地面敌人具备追击、转向、攻击冷却等基础 AI
- 精英怪具备更高强度并触发来袭提示
- 第三个精英 Slayer 已接入：
  - 开局 1 只
  - 后续每 20 秒刷新 1 只
- 空中小怪（SciFi_Beast02）已接入为飞行远程单位
- 空中怪保底常驻机制已启用（避免空中怪断档）

### 2.3 Progression & Loot
- 敌人死亡掉落经验球（蓝色）、金币及其他拾取物
- 蓝色经验球支持自动拾取
- 升级规则：每满 `100 XP` 升 1 级
- 升级后可触发强化选择（属性/收益提升）

### 2.4 UI / Feedback
- HUD 显示：HP、Gold、Lv、XP、Stage、Threat、Objective
- 血量下降时血条长度同步缩短
- 精英来袭、升级选择等信息有实时提示

### 2.5 Grounding / Anti-Clipping
- 地面敌人生成时进行地面对齐
- 角色可视模型脚底对齐胶囊体底部
- 生成后多帧强制贴地，降低悬空/陷地/穿模概率

## 3. Controls

- `W / A / S / D`: 移动
- `Shift`: 冲刺
- `Space`: 跳跃
- `Mouse Left`: 攻击/射击
- `Mouse Right`: 瞄准
- `E`: 交互
- `1 / 2 / 3`: 升级选项选择

## 4. Quick Start

1. 使用 Unity Hub 打开本项目（建议 Unity 6.3 LTS，6000.3.x）。
2. 打开场景：`Assets/Scenes/Level01_Inferno.unity`（或 `Assets/Scenes/Main.unity`）。
3. 点击 Play 运行。

## 5. External Asset Packages

项目包含第三方资源接入流程（大资源不建议直接入 Git）：
- `RoguePulse/Import/Import POLYGON Fantasy Rivals 1.3.1`
- `RoguePulse/Import/Import SciFi Beasts Pack 1.0`
- `RoguePulse/Setup Characters/1. Import JU TPS 3 Package`
- `RoguePulse/Setup Characters/2. Apply JU TPS Animations To Player`

如本地路径不同，请修改 `Assets/Scripts/Editor` 下对应导入脚本中的路径常量。

## 6. Project Structure

- `Assets/Scripts/Runtime/Combat`：玩家/敌人战斗逻辑
- `Assets/Scripts/Runtime/Directors`：刷怪与关卡流程
- `Assets/Scripts/Runtime/Progression`：经验、升级、构筑
- `Assets/Scripts/Runtime/UI`：HUD 与提示
- `Assets/Scripts/Runtime/Core`：核心管理器与通用系统
- `Assets/Scripts/Editor`：场景搭建、资源导入、自动化工具

## 7. Known Notes

- 本项目为课程原型，重点在系统可玩性与迭代过程。
- 若命令行 `dotnet build` 失败，可直接通过 Unity Editor 编译运行。
