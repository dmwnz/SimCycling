# SimCycling
Use your bike and your home trainer as a controller in racing simulation games.

## Prerequisites:
- Software
-- Assetto Corsa
-- .NET Framework 4.8
-- VJoy (used to create a virtual joystick for gas and steering control)
- Hardware
-- ANT+ USB stick
-- Bike home trainer with ANT+ FE-C or ANT+ Power capability
-- (optional) Heart rate sensor
-- (optional) Cadence sensor
-- (optional, if you want manual steering control) Android phone mounted on your handlebars with Monect PC Remote app

## Usage: 
1. Extract archive in assettocorsa install directory
2. Make sure settings in apps/python/ACSimCyclingDash/bin/SimCycling.exe.config are correct (cp = FTP)
3. Launch Assetto Corsa 
4. Configure controls so that vJoy controls throttle, brakes & steering
5. Enable ACSimCyclingDash python app in the game settings
6. Load up your favorite car & track combo
7. Enable ACSimCyclingDash (& WorkoutDash) app on the HUD
8. Start pedalling to make the car move. Load up a workout file if you want to follow a structured workout.


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
