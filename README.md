# Unturned AntiCheat

RocketMod anti-cheat plugin for Unturned focused on server-side behavioral scoring.

Chinese version: [README.zh-CN.md](README.zh-CN.md)

## What It Does

- Tracks player movement and raises evidence for speed, vertical spikes, and teleport-like jumps.
- Tracks gun damage telemetry for burst damage windows, suspicious headshot-hit ratios, and kill cadence spikes.
- Layers combat thresholds by inferred gun profile and engagement distance so shotguns, autos, and snipers are scored differently.
- Supports per-weapon GUID overrides for custom workshop guns and modded balance packs.
- Tracks chat spam as a lightweight abuse signal.
- Persists score and evidence to `Rocket/Plugins/Unturned_AntiCheat/anticheat-data.json`.
- Exposes `/ac` admin commands for status, evidence review, score reset, manual punish, and runtime reload.

## Commands

- `/ac status <player|steamId64>`
- `/ac recent`
- `/ac evidence <player|steamId64>`
- `/ac reset <player|steamId64>`
- `/ac punish <player|steamId64> <kick|ban>`
- `/ac reload`

## Build

1. Build the project targeting `.NET Framework 4.8`.
2. Copy the plugin output and its dependency DLLs from `bin/Debug/net48` into the server's `Rocket/Plugins` directory.
3. Start the server and confirm the load message appears in console.

## Notes

- v1 uses JSON persistence instead of SQLite to avoid extra runtime packaging and provider issues on dedicated servers.
- Automatic ban is disabled by default; automatic kick is enabled once score crosses the configured threshold.
- `ban` now calls `Provider.requestBanPlayer(...)` and includes the player's IPv4/HWID telemetry when available.
- `Penalties.BanDurationSeconds = 0` means permanent ban; set a positive number for temporary bans.
- `/ac reload` reloads the XML config in-place and updates active detector thresholds without resetting evidence or player sessions.
- Runtime reload only refreshes settings. If you change `StorageFileName`, do a full plugin reload to switch repository path.
- Combat evidence now includes `weapon_guid`, so you can copy a suspicious weapon's GUID directly into `Combat.WeaponOverrides`.

## Combat Tuning

You can tune three layers of combat thresholds in the plugin configuration XML:

- `Combat.WeaponProfiles`: broad defaults for inferred weapon classes like `Automatic`, `Sniper`, or `Shotgun`.
- `Combat.DistanceProfiles`: distance bands like close / mid / long.
- `Combat.WeaponOverrides`: exact overrides for specific weapon GUIDs.

Example override:

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

For very bursty custom weapons, increasing `MinimumHitsForBurstCheck` and `MinimumHitsForHeadshotCheck` is often safer than only raising multipliers.
If one weapon needs a hard baseline instead of class defaults, set `MaximumDamagePerWindow` and `MaximumHeadshotHitRatio` on that specific GUID.
If a weapon's combat rhythm should ignore close / mid / long distance scaling entirely, set `IgnoreDistanceProfile` to `true`.

After editing the config, run `/ac reload` to apply the new thresholds live.
