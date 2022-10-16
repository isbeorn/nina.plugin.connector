# Changelog

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