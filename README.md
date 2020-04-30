# SimCycling
Use your bike and your home trainer as a controller in racing simulation games.  

Short demo video  
[![Simcycling Video](https://img.youtube.com/vi/5hiM_qTmGkw/0.jpg)](https://www.youtube.com/watch?v=5hiM_qTmGkw)

## Prerequisites:
- Software
  - Assetto Corsa
  - .NET Framework 4.8
  - VJoy (used to create a virtual joystick for gas and steering control)
- Hardware
  - ANT+ USB stick
  - Bike home trainer with ANT+ FE-C or ANT+ Power capability
  - (optional) ANT+ heart rate sensor
  - (optional) ANT+ cadence sensor
  - (optional, if you want manual steering control) Android phone mounted on your handlebars with Monect PC Remote app

## Usage: 
1. Extract archive in assettocorsa install directory
1. Make sure settings in `apps/python/ACSimCyclingDash/bin/SimCycling.exe.config` are correct (cp = FTP)
1. Launch Assetto Corsa 
1. Configure controls to bind vJoy axis 3 to steering & axis 1 to throttle (example config supplied)
1. Enable ACSimCyclingDash python app in the game settings
1. Load up your favorite car & track combo
1. Enable ACSimCyclingDash (& WorkoutDash) apps on the HUD, click "Start"  
![ACSimCyclingDash](/ACSimCyclingDash/ACSimCyclingDash_ON.png) ![WorkoutDash](/ACSimCyclingDash/WorkoutDash_ON.png)
1. Start pedalling to make the car move 😎  
Load up a workout file if you want to follow a structured workout.


## Frequently Asked Questions (most common issues)
**Q**: *When I click start, nothing happens*  
**A**: Make sure you have all the prerequisites matched. Make sure your antivirus is not blocking `SimCycling.exe` in `apps/python/ACSimCyclingDash/bin`

**Q**: *When I pedal, I can see the power and speed update in the app but the car doesn't move*  
**A**: Check your control bindings. Use Vjoy feeder in the settings menu to see if the axis are properly mapped. Also make sure the driving aid "automatic gears" is enabled, otherwise the car will stay in neutral

**Q**: *What workout file formats are supported?*  
**A**: ZWO (Zwift Work Out), ERG/MRC

**Q**: *Are my virtual activities recorded and where?*  
**A**: Yes, output FIT files are recorded in `My Documents/Assetto Corsa/SimCyclingActivities`

**Q**: *Can I upload my activities to Strava?*  
**A**: Yes

**Q**: *After I upload my activity to Strava, the map isn't showing*  
**A**: This is normal. There is no conversion of ingame positions (X, Y, Z) to WGS coordinates (Longitude, Latitude, Altitude) yet.

## How it works
In-Game Python App launches a compiled executable whose job is
- to take care of the communication with Ant+ devices (i.e home trainer / power sensor, cadence sensor and heart rate sensor)
- to send throttle signal to the Game to replicate the home trainer speed onto the ingame car
- to send steering signal to keep the ingame car on track
- to record the activity into a FIT file

Python App UI : 
- displays read data from the executable (heart rate, speed, power...)
- displays workout data
- sends commands and game-specific data to the executable

### Communication protocols
- between Assetto Corsa and Executable (car position, incline, etc) : Assetto Corsa Shared Memory Library
- between Executable and Assetto Corsa (throttle) : VJOY virtual joystick
- between Executable and Python App (power, heart rate, speed, etc) : memory mapped JSON object (AntManagerState)
- between Python App and Executable (car position, user control...) :  memory mapped JSON object (RaceState) + 1 byte for UI control (start / stop / load workout)
