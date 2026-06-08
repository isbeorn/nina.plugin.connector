# PR: Connector Plugin Device Connection Order Support

## Summary

Updates the `nina.plugin.connector` plugin to honor the same custom device connection order defined in the main NINA profile settings.

The plugin currently uses its own hardcoded `ConnectAllEquipment` sequence order. This PR makes that connector-plugin path respect `UseCustomDeviceConnectionOrder` and the saved `DeviceConnectionOrder` list from the active profile when enabled.

---

## Branch

- `https://github.com/ikeysolomon/nina.plugin.connector/tree/ReorderingExperiment`

---

## Files Changed

### `Connector/Instructions/ConnectAllEquipment.cs`
- Adds support for reading `UseCustomDeviceConnectionOrder` and `DeviceConnectionOrder` from `profileService.ActiveProfile.ApplicationSettings`.
- When the option is enabled, the connector plugin connects devices in the user-defined order rather than the plugin's hardcoded order.

### `README` / release notes / docs (if applicable)
- No changes expected in the main repo documentation; this is a plugin-specific fix.

---

## Motivation

The main NINA repository now supports a user-configurable Connect All order. The connector plugin must follow the same ordering behavior to avoid inconsistent behavior between the toolbar and the plugin-driven connect flow.

This is especially important for setups where a switch (such as the SV241 SVBony power switch) must connect before the cameras and other powered devices.

---

## Relationship to Main Repo PR

This PR is a sibling to the main repo PR described in `PR-device-connection-order.md` in the `NINA` repository. The two PRs should be merged together to ensure both the built-in NINA connect-all path and the connector-plugin connect-all path honor the same custom ordering.

- Main repo PR: `PR-device-connection-order.md`
- Connector plugin PR: this branch `ReorderingExperiment`

---

## Testing Notes

- Enable `UseCustomDeviceConnectionOrder` in the NINA options UI and reorder the device list.
- Run the connector plugin's `ConnectAllEquipment` instruction or sequence step.
- Verify the plugin connects devices in the saved custom order.
- Confirm no regressions in default order when `UseCustomDeviceConnectionOrder` is disabled.
