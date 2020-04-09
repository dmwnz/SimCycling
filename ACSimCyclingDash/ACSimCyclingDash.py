import ac   
import json
import mmap
import subprocess
import sys
import acsys
import os

antManagerExecutable = None
antManagerState      = None
uiElements           = None
workoutUi            = None

SIGNAL_LOAD_WORKOUT  = b'\x01'
SIGNAL_STOP_WORKOUT  = b'\x02'
SIGNAL_EXIT          = b'\x03'


class RaceState:
    def _getMemoryMap(self):
        return mmap.mmap(0, 1024, "SimCyclingRaceState")

    def updateToMemory(self, stop=True):
        
        car_positions = []
        car_velocities = []
        car_npos = []

        for i in range(ac.getCarsCount()):
            x,y,z = ac.getCarState(i, acsys.CS.WorldPosition)
            pos = {
                "X" : x,
                "Y" : y,
                "Z" : z
            }
            car_positions.append(pos)
            x,y,z = ac.getCarState(i, acsys.CS.Velocity)
            vel = {
                "X" : x,
                "Y" : y,
                "Z" : z
            }
            car_velocities.append(vel)
            pos = ac.getCarState(i, acsys.CS.NormalizedSplinePosition)
            car_npos.append(pos)


        #heading = info.physics.heading
        #pitch = info.physics.pitch
        #roll = info.physics.roll
       
        dict = {
            "car_positions" : car_positions,
            "car_velocities" : car_velocities,
            "normalized_car_positions" : car_npos,
            "stop" : stop
            #"heading" : heading,
            #"pitch" : pitch,
            #"roll" : roll
        }
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        memoryMap.write(1024 * b"\0")
        memoryMap.seek(0)
        memoryMap.write(json.dumps(dict).encode())
        memoryMap.close()

class AntManagerState:
    def __init__(self):
        self.BikeCadence = 0
        self.BikeSpeedKmh = 0.0
        self.BikeIncline = 0.0
        self.CyclistHeartRate = 0
        self.CyclistPower = 0.0
        self.CriticalPower = 0.0
        self.TripTotalKm = 0.0
        self.TripTotalTime  = 0.0
        self.TargetPower = 0.0
        self.TrackLength = 0.0
        self.NextTargetPower = 0.0
        self.RemainingIntervalTime = 0.0
        self.RemainingTotalTime = 0.0

    def _instanciateFromDict(self, dictionary):
        for k, v in dictionary.items():
            setattr(self, k, v)

    def _getMemoryMap(self):
        return mmap.mmap(0, 1024, "SimCycling")

    def updateFromMemory(self):
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        flag = memoryMap.read(1)
        if flag == b'\xAA':
            ac.console("Concurrent write in progress. Skip reading")
            return False
        if flag != b'\x11':
            ac.console("Unknown signal flag. Skip reading")
            return False
        memoryMap.seek(1)
        readBytes  = memoryMap.read()
        memoryMap.close()
        readString = readBytes.decode("utf-8").rstrip("\0")
        #ac.console(readString)
        dictData   = json.loads(readString)
        self._instanciateFromDict(dictData)
        return True

    def eraseMemory(self):
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        memoryMap.write(bytes(1024))
        memoryMap.close()

def sendSignal(signal):
    memoryMap = mmap.mmap(0, 1, "SimCyclingSignal")
    memoryMap.seek(0)
    memoryMap.write(signal)
    memoryMap.close()

def btn1_clicked(*args):
    global antManagerExecutable
    if antManagerExecutable is None or antManagerExecutable.poll() is not None:
        startExecutable()
    elif not workoutInProgress():
        loadWorkout()
    else:
        stopWorkout()
 
def btn2_clicked(*args):
    global antManagerExecutable
    if antManagerExecutable is not None and antManagerExecutable.poll() is None:
        stopExecutable()

def workoutInProgress():
    global antManagerState
    return antManagerState is not None and antManagerState.RemainingTotalTime > 0

def startExecutable():
    global antManagerExecutable, uiElements
    ac.console("Starting executable")
    try:
        antManagerExecutable = subprocess.Popen(r".\apps\python\ACSimCyclingDash\bin\SimCycling.exe", cwd=os.path.join(os.path.dirname(os.path.realpath(__file__)), "bin"))
        ac.console("Executable launched : " + str(antManagerExecutable))
        ac.setText(uiElements.btn1, "starting...")
    except Exception as e:
        ac.log(repr(e))
        ac.console(str(antManagerExecutable))

def loadWorkout():
    ac.console("Sending signal to load workout")
    sendSignal(SIGNAL_LOAD_WORKOUT)

def stopWorkout():
    ac.console("Sending signal to stop workout")
    sendSignal(SIGNAL_STOP_WORKOUT)

def stopExecutable():
    global uiElements
    ac.console("Sending signal to stop")
    sendSignal(SIGNAL_EXIT)

class UIElement():
    def __init__(self, valueName, appWindow, x, y, fontSize=48, unit="", format=None):
        self.valueName = valueName
        self.unit = unit
        self.label = ac.addLabel(appWindow, "0")
        self.unitLabel = ac.addLabel(appWindow, self.unit)
        self.format = format
        self.x = x
        self.y = y
        self.fontSize = fontSize


    def setup(self):
        ac.setFontSize(self.label, self.fontSize)
        ac.setFontAlignment(self.label, "right")
        ac.setFontSize(self.unitLabel, self.fontSize//2)
        ac.setPosition(self.label, self.x, self.y)  
        ac.setPosition(self.unitLabel, self.x+self.fontSize//10, self.y+self.fontSize//2)

    def update(self, state):
        value = getattr(state, self.valueName)
        if self.format is not None:
            ac.setText(self.label, ("{0:" + self.format + "}").format(value))
        else:
            ac.setText(self.label, "{0}".format(getattr(state, self.valueName)))

class TimerUIElement(UIElement):
    def setup(self):
        UIElement.setup(self)
        ac.setFontAlignment(self.label, "center")

    def update(self, state):
        value = int(getattr(state, self.valueName))
       
        h = value // 3600
        m = (value - 3600*h) // 60
        s = (value - 3600*h - 60*m)
        disp = (("{0}:{1:02d}:{2:02d}".format(h,m,s)))
        ac.setText(self.label, disp)


class UIElements:
    def __init__(self, appWindow):
        self.appWindow = appWindow
        self.uiElements = []
        self.uiElements.append(UIElement("CyclistPower", appWindow, 225, 10, 96, 'W'))
        self.uiElements.append(UIElement("BikeSpeedKmh", appWindow, 115, 130, 48, 'km/h', ".1f"))
        self.uiElements.append(UIElement("BikeCadence", appWindow, 265, 130, 48, 'rpm', ".1f"))
        self.uiElements.append(UIElement("BikeIncline", appWindow, 115, 200, 48, '%', ".1f"))
        self.uiElements.append(UIElement("CyclistHeartRate", appWindow, 265, 200, 48, 'bpm', ".1f"))
        self.uiElements.append(UIElement("TripTotalKm", appWindow, 115, 270, 32, 'km', ".1f"))
        self.uiElements.append(UIElement("TrackLength", appWindow, 265, 270, 32, 'km', ".1f"))

        self.uiElements.append(TimerUIElement("TripTotalTime", appWindow, 166, 320, 32))

        self.btn1   = ac.addButton(self.appWindow, "start")
        self.btn2   = ac.addButton(self.appWindow, "stop")

    def setup(self):
        ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,333,413)
        ac.drawBorder(self.appWindow,0)
        ac.drawBackground(self.appWindow, 0)
        for el in self.uiElements:
            el.setup()
        self.setupButtons()

    
    def setupButtons(self):
        ac.setSize(self.btn1, 165, 26)
        ac.setSize(self.btn2 , 165, 26)
        ac.setFontSize(self.btn1, 18)
        ac.setFontSize(self.btn2 , 18)
        ac.setFontAlignment(self.btn1, "center")
        ac.setFontAlignment(self.btn2, "center")
        ac.setPosition(self.btn1, 1  , 378)
        ac.setPosition(self.btn2 , 168, 378)
        ac.addOnClickedListener(self.btn1, btn1_clicked)
        ac.addOnClickedListener(self.btn2 , btn2_clicked)

    def update(self, antManagerState: AntManagerState):
        for el in self.uiElements:
            el.update(antManagerState)

class WorkoutUI:
    def __init__(self, appWindow):
        self.appWindow = appWindow
        self.uiElements = []
        self.uiElements.append(UIElement("TargetPower", appWindow, 225, 10, 96, 'W', ".0f"))
        self.uiElements.append(UIElement("NextTargetPower", appWindow, 225, 130, 48, 'W', ".0f"))
        self.uiElements.append(TimerUIElement("RemainingIntervalTime", appWindow, 166, 200, 48))
        self.uiElements.append(TimerUIElement("RemainingTotalTime", appWindow, 166, 270, 48))

        self.graph = ac.addGraph(appWindow, "0")
        ac.addSerieToGraph(self.graph, 1.0, 0.0, 0.0)

    def setup(self):
        ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,333,393)
        ac.drawBorder(self.appWindow,0)
        ac.drawBackground(self.appWindow, 0)
        for el in self.uiElements:
            el.setup()

        ac.setPosition(self.graph, 10, 330)
        ac.setSize(self.graph,313,50)
        ac.setRange(self.graph, 0.0, 500.0, 1000)
   
    def update(self, antManagerState: AntManagerState):
        for el in self.uiElements:
            el.update(antManagerState)
        
        x = ac.addValueToGraph(self.graph, 0, antManagerState.CyclistPower)


def acMain(ac_version):
    global uiElements, antManagerState, workoutUi
    appWindow=ac.newApp("ACSimCyclingDash")
   

    antManagerState = AntManagerState()
    antManagerState.eraseMemory()

    uiElements = UIElements(appWindow)
    uiElements.setup()

    workoutAppWindow=ac.newApp("WorkoutDash")
    workoutUi = WorkoutUI(workoutAppWindow)
    workoutUi.setup()
    
    return "ACSimCyclingDash"

def onRender(*args):
    global antManagerExecutable, antManagerState, uiElements
    try:
        stateFound = antManagerState.updateFromMemory()
        if stateFound:
            if not workoutInProgress():
                ac.setText(uiElements.btn1, "load workout")
            else:
                ac.setText(uiElements.btn1, "stop workout")
        elif not stateFound and ac.getText(uiElements.btn1) != "starting...":
            ac.setText(uiElements.btn1, "start")
    except Exception as e:
        ac.console(repr(e))
    
    antManagerState.TrackLength = ac.getTrackLength(0) / 1000
    uiElements.update(antManagerState)
    workoutUi.update(antManagerState)
    RaceState().updateToMemory()

def acShutdown():
    ac.log("BIKEDASH acShutdown")
    sendSignal(SIGNAL_EXIT)
