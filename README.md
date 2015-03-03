
SimpleActivity
==========

SimpleActivity is a sample application which demonstrates the usage of the 
Activity Monitor API in Windows Phone 8.1. This application shows different 
activity types the user has performed during the current day. The user is 
able to see the activities from the last seven days as well zoom the activity 
graph by using the application bar buttons.

1. Instructions
--------------------------------------------------------------------------------

Learn about the Lumia SensorCore SDK from the Lumia Developer's Library. The
example requires the Lumia SensorCore SDK's NuGet package but will retrieve it
automatically (if missing) on first build.

To build the application you need to have Windows 8.1 and Windows Phone SDK 8.1
installed.

Using the Windows Phone 8.1 SDK:

1. Open the SLN file: File > Open Project, select the file `SimpleActivity.sln`
2. Remove the "AnyCPU" configuration (not supported by the Lumia SensorCore SDK)
or simply select ARM
3. Select the target 'Device'.
4. Press F5 to build the project and run it on the device.

Alternatively you can also build the example for the emulator (x86) in which case
the Activity Monitor will use simulated data (and no history is available with
the default constructor used).

Please see the official documentation for
deploying and testing applications on Windows Phone devices:
http://msdn.microsoft.com/en-us/library/gg588378%28v=vs.92%29.aspx


2. Implementation
--------------------------------------------------------------------------------

The main functionality is in the MainPage.xaml.cs file, which demonstrates the usage of
Activity Monitor API that is initialized (if supported).

The API is called through the CallSenseApiAsync() helper function, which helps handling
the typical errors, like required features being disalbed in the system settings.
In the Initialize() function is added compatibility check for devices that have different 
sensorCore SDK service.

UpdateScreenAsync() returns activity types that the user performed during the current day 
or the last seven days. Calling the GetActivityHistoryAsync() method will return a list of
activities occured during each hour in the current day.
The user is able to zoom the activity graph by using the application bar button.

3. Version history
--------------------------------------------------------------------------------
* Version 1.1.0.0: The first release.

4. Downloads
---------

| Project | Release | Download |
| ------- | --------| -------- |
| SimpleActivities | v1.1.0.0 | [simpleactivity-1.1.0.0.zip](https://github.com/Microsoft/SimpleActivity/archive/v1.1.0.0.zip) |

5. See also
--------------------------------------------------------------------------------

The projects listed below are exemplifying the usage of the SensorCore APIs

* SimpleSteps -  https://github.com/Microsoft/SimpleSteps
* SimplePlaces - https://github.com/Microsoft/SimplePlaces
* SimpleTracks - https://github.com/Microsoft/SimpleTracks
