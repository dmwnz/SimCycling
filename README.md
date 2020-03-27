# SimCycling
Use your bike and your home trainer as a controller in racing simulation games.

## Usage: 
1. Add python file to Assetto Corsa apps/python dir
2. Launch Assetto Corsa 
3. Configure controls so that vJoy controls throttle and brakes
4. Enable ACSimCycling python app in the game settings
5. Load up your favorite car & track combo
6. Enable ACSimCycling app on the HUD
8. Start pedalling to make the car move


## How it works
In-Game Python App launches a compiled executable whose job is
- to take care of the communication with Ant+ devices (i.e home trainer, cadence sensor and heart rate sensor)
- to send throttle command to the Game to replicate the home trainer speed onto the ingame car
- to convert in-game X,Y,Z position to WGS (GPS) positions - eventually create a GPX track

Python App UI : displays read data from the executable (heart rate, speed, power...)

Communication protocols
- between Assetto Corsa and Executable (car position, incline, etc) : Assetto Corsa Shared Memory Library
- between Executable and Assetto Corsa (throttle) : VJOY virtual joystick
- between Executable and Python App (power, heart rate, speed, etc) : memory mapped JSON object (AntManagerState)
- between Python App and Executable (user control...) : TODO
