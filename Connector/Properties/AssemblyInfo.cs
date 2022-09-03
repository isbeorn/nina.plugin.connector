using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("52c17ee7-6d6c-4ee1-8fa0-85bcf6677bef")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Connector")]
[assembly: AssemblyDescription("Equipment connection manager")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Stefan Berg")]
[assembly: AssemblyProduct("NINA.Plugins")]
[assembly: AssemblyCopyright("Copyright ©  2021-2022")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: InternalsVisibleTo("NINA.Plugins.Test")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]


//The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

//The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "2.0.0.9001")]

//Your plugin homepage - omit if not applicaple
[assembly: AssemblyMetadata("Homepage", "https://www.patreon.com/stefanberg/")]
//The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
//The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
//The repository where your pluggin is hosted
[assembly: AssemblyMetadata("Repository", "https://bitbucket.org/Isbeorn/nina.plugin.connector/")]

[assembly: AssemblyMetadata("ChangelogURL", "https://bitbucket.org/Isbeorn/nina.plugin.connector/src/master/Connector/Changelog.md")]

//Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "Connect, Equipment, Disconnect")]

//The featured logo that will be displayed in the plugin list next to the name
[assembly: AssemblyMetadata("FeaturedImageURL", "https://bitbucket.org/Isbeorn/nina.plugin.connector/downloads/connect.jpg")]
//An example screenshot of your plugin in action
[assembly: AssemblyMetadata("ScreenshotURL", "")]
//An additional example screenshot of your plugin in action
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"A plugin that adds a connect and disconnect instructions to the advanced sequencer  
  
Make sure your equipment is properly setup, so that it is automatically selected when scanning for it. The instructions will scan for the selected equipment and try to connect to the device that was connected successfully the last time.   
To use the instructions, just drag them in and select the type of device you want to connect to or disconnect from.

  
**Be careful when using these instructions**! Failing to connect to equipment will make your sequence impossible to run properly. It is recommended to have some sort of notification mechanism like GNS or Ground Station available to be notified of a failed connection! ")]


