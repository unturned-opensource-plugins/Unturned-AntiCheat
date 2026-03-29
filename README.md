# Unturned AntiCheat

RocketMod anti-cheat plugin for Unturned focused on server-side behavioral scoring.

Chinese version: [README.zh-CN.md](README.zh-CN.md)

## What It Does

- Tracks player movement and raises evidence for speed, vertical spikes, and teleport-like jumps.
- Tracks driver-only vehicle movement and raises evidence for impossible acceleration, burst speed, sustained overspeed, and teleport-like jumps.
- Layers vehicle thresholds by class so ground vehicles, boats, helicopters, planes, bicycles, and tracked armor can use different models.
- Tracks gun damage telemetry for burst damage windows, suspicious headshot-hit ratios, and kill cadence spikes.
- Records combat telemetry only after the hit is actually allowed, avoiding safezone/friendly-fire/godmode false positives.
- Layers combat thresholds by inferred gun profile and engagement distance so shotguns, autos, and snipers are scored differently.
- Supports per-weapon GUID overrides for custom workshop guns and modded balance packs.
- Tracks chat spam as a lightweight abuse signal.
- Persists score and evidence to `Rocket/Plugins/Unturned_AntiCheat/anticheat-data.json`.
- Exposes `/ac` admin commands for status, evidence review, score reset, manual punish, and runtime reload.
- Ships automated detector tests under `Tests/` so vehicle and movement threshold changes can be validated without a live Unturned runtime.

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
- Penalty throttling now reads only `Penalties.AlertCooldownMinutes`, `Penalties.KickCooldownMinutes`, and `Penalties.BanCooldownMinutes`. Older configs must be updated to these three explicit fields.
- `/ac reload` reloads the XML config in-place and updates active detector thresholds without resetting evidence or player sessions.
- Runtime reload only refreshes settings. If you change `StorageFileName`, do a full plugin reload to switch repository path.
- Combat evidence now includes `weapon_guid` and `damage_allowed_source=post_event`, so you can copy a suspicious weapon's GUID directly into `Combat.WeaponOverrides` and confirm the hit was scored post-approval.
- Player sessions are cleared on disconnect so reconnects do not inherit stale movement, chat, or combat windows.
- On load and `/ac reload`, the plugin now writes normalized penalty cooldown fields back to the XML so older configs are materialized into the explicit three-field form.
- Vehicle anti-cheat only scores the driver seat and derives thresholds from each vehicle asset's target speed, so passengers are not blamed for someone else's hacked car.
- Vehicle class handling now distinguishes `Helicopter`, `Plane`, `Boat`, `Tracked`, `Bicycle`, and `Ground`. Legacy `Air` profiles still act as a fallback for older configs.

Penalty cooldown example:

```xml
<Penalties>
  <AlertCooldownMinutes>2</AlertCooldownMinutes>
  <KickCooldownMinutes>10</KickCooldownMinutes>
  <BanCooldownMinutes>10</BanCooldownMinutes>
</Penalties>
```

Set any of the three cooldowns to `0` to disable that specific throttle entirely.

## Vehicle Tuning

Vehicle anti-cheat thresholds live under `Vehicle` in the plugin XML. The detector compares actual movement against each vehicle asset's `TargetForwardSpeed` / `TargetReverseSpeed`, then applies your configured grace and multipliers.
Like combat tuning, vehicle tuning now also supports exact per-vehicle GUID overrides.
You can additionally tune `Vehicle.VehicleClassProfiles` so helicopters, planes, boats, tracked vehicles, bicycles, and normal ground vehicles each get their own threshold model.

```xml
<Vehicle>
  <Enabled>true</Enabled>
  <MinimumReferenceSpeedMetersPerSecond>14</MinimumReferenceSpeedMetersPerSecond>
  <InstantaneousSpeedMultiplier>1.45</InstantaneousSpeedMultiplier>
  <SustainedSpeedMultiplier>1.20</SustainedSpeedMultiplier>
  <TeleportDistanceMultiplier>2.25</TeleportDistanceMultiplier>
  <FlatSpeedGraceMetersPerSecond>6</FlatSpeedGraceMetersPerSecond>
  <FlatTeleportGraceMeters>18</FlatTeleportGraceMeters>
  <AbsoluteTeleportDistanceMeters>140</AbsoluteTeleportDistanceMeters>
  <MaximumAccelerationMetersPerSecondSquared>35</MaximumAccelerationMetersPerSecondSquared>
</Vehicle>
```

Raise `FlatSpeedGraceMetersPerSecond` or `MaximumAccelerationMetersPerSecondSquared` first if high-latency players or boost-heavy modded vehicles false positive.

Per-vehicle GUID override example:

```xml
<VehicleOverrides>
  <VehicleOverrideSettings>
    <VehicleGuid>01234567-89ab-cdef-0123-456789abcdef</VehicleGuid>
    <VehicleName>Workshop Nitro Car</VehicleName>
    <MinimumReferenceSpeedMetersPerSecond>22</MinimumReferenceSpeedMetersPerSecond>
    <InstantaneousSpeedMultiplier>1.70</InstantaneousSpeedMultiplier>
    <SustainedSpeedMultiplier>1.35</SustainedSpeedMultiplier>
    <TeleportDistanceMultiplier>2.80</TeleportDistanceMultiplier>
    <FlatSpeedGraceMetersPerSecond>9</FlatSpeedGraceMetersPerSecond>
    <FlatTeleportGraceMeters>28</FlatTeleportGraceMeters>
    <AbsoluteTeleportDistanceMeters>180</AbsoluteTeleportDistanceMeters>
    <MaximumAccelerationMetersPerSecondSquared>52</MaximumAccelerationMetersPerSecondSquared>
  </VehicleOverrideSettings>
</VehicleOverrides>
```

If a modded vehicle is intentionally boost-heavy or its asset target speed is lower than real gameplay, override `MinimumReferenceSpeedMetersPerSecond`, `InstantaneousSpeedMultiplier`, and `MaximumAccelerationMetersPerSecondSquared` for that GUID first.
If you already shipped an older config using `Air`, it will keep working as a fallback profile for `Helicopter` and `Plane` until you split them explicitly.

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
