# Unturned AntiCheat

面向 Unturned 的 RocketMod 反作弊插件，核心思路是基于服务端可见行为做评分与处罚。

English version: [README.md](README.md)

## 功能概览

- 跟踪玩家移动行为，并为异常速度、异常垂直位移、疑似传送生成证据。
- 跟踪枪械伤害遥测，识别爆发伤害窗口、可疑爆头命中率与异常击杀节奏。
- 只有在伤害最终被允许生效后才记录战斗遥测，避免 safezone、友伤关闭、godmode 等场景产生误报。
- 按推断出的武器类型和交战距离分层调整战斗阈值，让 shotgun、auto、sniper 的判定更贴近实际。
- 支持按具体武器 GUID 单独覆写阈值，适配 workshop 枪包与自定义武器平衡。
- 跟踪聊天刷屏，作为轻量 abuse 信号。
- 将评分与证据持久化到 `Rocket/Plugins/Unturned_AntiCheat/anticheat-data.json`。
- 提供 `/ac` 管理命令，用于查看状态、证据、重置分数、手动处罚与运行时重载配置。

## 命令

- `/ac status <player|steamId64>`
- `/ac recent`
- `/ac evidence <player|steamId64>`
- `/ac reset <player|steamId64>`
- `/ac punish <player|steamId64> <kick|ban>`
- `/ac reload`

## 构建

1. 使用 `.NET Framework 4.8` 构建项目。
2. 将 `bin/Debug/net48` 下的插件输出与依赖 DLL 一并复制到服务器 `Rocket/Plugins` 目录。
3. 启动服务器，确认控制台出现插件加载日志。

## 说明

- 当前版本使用 JSON 持久化而不是 SQLite，目的是避免 Rocket 服务端部署额外 runtime/provider 的复杂性。
- 默认关闭自动 ban，默认开启自动 kick。
- `ban` 已接到 `Provider.requestBanPlayer(...)`，并在可用时附带玩家 IPv4/HWID 信息。
- `Penalties.BanDurationSeconds = 0` 表示永久封禁；如果要临时封禁，填正整数秒数。
- 处罚节流现在只读取 `Penalties.AlertCooldownMinutes`、`Penalties.KickCooldownMinutes` 与 `Penalties.BanCooldownMinutes` 三项显式配置；旧配置也必须改成这三项。
- `/ac reload` 会原地重载 XML 配置，并更新当前运行中的 detector 阈值，不会清空证据或会话。
- 运行时重载只更新设置；若修改了 `StorageFileName`，仍需完整重载插件才能切换持久化文件路径。
- 战斗证据里会写入 `weapon_guid` 与 `damage_allowed_source=post_event`，可以据此确认命中是在最终放行后才被计入，并直接回填到 `Combat.WeaponOverrides`。
- 玩家断线时会清掉会话状态，重连后不会继承旧的移动、聊天或战斗窗口。
- 插件启动与 `/ac reload` 时都会把处罚 cooldown 规范化写回 XML，让旧配置自动落成新的三字段形式。

处罚 cooldown 配置示例：

```xml
<Penalties>
  <AlertCooldownMinutes>2</AlertCooldownMinutes>
  <KickCooldownMinutes>10</KickCooldownMinutes>
  <BanCooldownMinutes>10</BanCooldownMinutes>
</Penalties>
```

三项中的任意一项设为 `0`，即可关闭该处罚类型自身的 cooldown。

## Combat 调参

插件配置 XML 中，战斗阈值可以分三层调整：

- `Combat.WeaponProfiles`: 针对推断武器类型的默认规则，例如 `Automatic`、`Sniper`、`Shotgun`。
- `Combat.DistanceProfiles`: 按近中远距离分层。
- `Combat.WeaponOverrides`: 针对具体武器 GUID 的精确覆写。

覆写示例：

```xml
<WeaponOverrides>
  <CombatWeaponOverrideSettings>
    <WeaponGuid>01234567-89ab-cdef-0123-456789abcdef</WeaponGuid>
    <WeaponName>Workshop Laser Rifle</WeaponName>
    <IgnoreDistanceProfile>true</IgnoreDistanceProfile>
    <MinimumHitsForBurstCheck>10</MinimumHitsForBurstCheck>
    <MinimumHitsForHeadshotCheck>12</MinimumHitsForHeadshotCheck>
    <MaximumDamagePerWindow>420</MaximumDamagePerWindow>
    <MaximumHeadshotHitRatio>0.92</MaximumHeadshotHitRatio>
    <DamageThresholdMultiplier>1.35</DamageThresholdMultiplier>
    <HeadshotRatioThresholdMultiplier>1.20</HeadshotRatioThresholdMultiplier>
  </CombatWeaponOverrideSettings>
</WeaponOverrides>
```

调参建议：

- 对高爆发的自定义武器，优先提高 `MinimumHitsForBurstCheck` 与 `MinimumHitsForHeadshotCheck`，通常比单纯拉高倍率更稳。
- 如果某把武器需要独立的绝对阈值，而不是沿用武器类型默认值，可单独设置 `MaximumDamagePerWindow` 与 `MaximumHeadshotHitRatio`。
- 如果某把武器的节奏与交战距离无明显相关性，可将 `IgnoreDistanceProfile` 设为 `true`，跳过近中远距离倍率层。

修改配置后，执行 `/ac reload` 即可在不停服的情况下应用新阈值。
