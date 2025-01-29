# Changelog

## Version 2.1
- All instructions and triggers are now part of the core application. Existing templates and saved sequences will be migrated automatically.
- The connect on startup options remain in the plugin and are now its main purpose

## Version 2.0.3.0
- Add an option to "Switch Profile" to not automatically reconnect all devices

## Version 2.0.2.2
- Fix notification for filter move to correctly report filter position

## Version 2.0.2.1
- Utilize new cameramediator event hook for download timeouts

## Version 2.0.2.0
- Added a new trigger "Reconnect Camera on Download Failure" that will try to reconnect the camera in case it encounters a camera exposure download timeout
  - Additionally it will enable cooling and dew heater if they have been active before reconnecting

## Version 2.0.1.0
- Switch Profile Instruction now remembers the selection when being loaded from files, templates or being copied

## Version 2.0.0.2
- Updated to latest nightly changes

## Version 2.0.0.1
- Added "Switch Profile" instruction to switch between equipment profiles during a sequence. This will disconnect equipment, switch the profile and reconnect equipment.

## Version 2.0.0.0
- Upgrade to .NET7

## Version 1.3.0.0
- Option page now has options to auto connect all devices and do some fixed actions on application start. These will be executed once all plugins have been loaded.

## Version 1.2.0.0

- Added "Connect All" as well as "Disconnect All" instructions. 
- "Connect All" will try to connect all devices that have a stored id in the profile

## Version 1.1.3.0

- Fixed issue where the equipment selection was not saved and was lost on reload of the trigger

## Version 1.1.1.0

- Allow reconnect trigger to be placed into a set multiple times

## Version 1.1.0.0

- Fixed issue when connection could not be established that the stored id of the device was overwritten with no device by accident
- Added a trigger to connect to equipment when not connected
- Layout improvement for mini sequencer
- Added a FeaturedImageURL

## Version 1.0.2.0

- Added a validation to connect instruction, when no device of the selected type was ever connected before and no id is available in the profile

## Version 1.0.1.0

- Fixed issue where the equipment selection was not saved and was lost on reload of the instruction

## Version 1.0.0.0

- Initial release with a set of two instructions for connection and disconnection.