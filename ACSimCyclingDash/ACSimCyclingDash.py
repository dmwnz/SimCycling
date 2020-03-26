import ac   
import json
import mmap
import subprocess
import sys

antManagerExecutable = None
antManagerState      = None
uiElements           = None


def startClick(*args):
    ac.console("Hello start")
    ac.console(str(antManagerExecutable))

def stopClick(*args):
    ac.console("Hello stop")

class AntManagerState:
    def __init__(self):
        self.BikeCadence = 0
        self.BikeSpeedKmh = 0.0
        self.BikeIncline = 0.0
        self.CyclistHeartRate = 0
        self.CyclistPower = 0.0
        self.TripTotalKm = 0.0

    def _instanciateFromDict(self, dictionary):
        for k, v in dictionary.items():
            setattr(self, k, v)

    def _getMemoryMap(self):
        return mmap.mmap(0, 256, "SimCycling")

    def updateFromMemory(self):
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        readBytes  = memoryMap.read()
        memoryMap.close()
        readString = readBytes.decode("utf-8").rstrip("\0")
        ac.console(readString)
        dictData   = json.loads(readString)
        self._instanciateFromDict(dictData)

    def eraseMemory(self):
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        memoryMap.write(bytes(256))
        memoryMap.close()

class UIElements:
    def __init__(self, appWindow):
        self.appWindow = appWindow
        self.powerLabel    = ac.addLabel(self.appWindow, "0")
        self.speedLabel    = ac.addLabel(self.appWindow, "0")
        self.cadLabel      = ac.addLabel(self.appWindow, "0")
        self.gradeLabel    = ac.addLabel(self.appWindow, "0")
        self.hrLabel       = ac.addLabel(self.appWindow, "0")    
        self.kmLabel       = ac.addLabel(self.appWindow, "0")
        self.trackLenLabel = ac.addLabel(self.appWindow, "0")
        self.startButton   = ac.addButton(self.appWindow, "start")
        self.stopButton    = ac.addButton(self.appWindow, "stop")

    def setup(self):
        ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,333,343)
        ac.drawBorder(self.appWindow,0)
        ac.drawBackground(self.appWindow, 0)
        self.setupPowerLabel()
        self.setupSpeedLabel()
        self.setupCadLabel()
        self.setupGradeLabel()
        self.setupHrLabel()
        self.setupKmLabel()
        self.setupTrackLenLabel()
        self.setupButtons()

    def setupPowerLabel(self):
        ac.setFontSize(self.powerLabel, 96)
        ac.setFontAlignment(self.powerLabel, "right")
        ac.setPosition(self.powerLabel, 225, 10)   
        ac.setPosition(ac.addLabel(self.appWindow, "W"), 240, 80)

    def setupSpeedLabel(self):
        ac.setFontSize(self.speedLabel, 48)
        ac.setFontAlignment(self.speedLabel, "right")
        ac.setPosition(self.speedLabel, 115, 130)
        ac.setPosition(ac.addLabel(self.appWindow, "km/h"), 120, 160)

    def setupCadLabel(self):
        ac.setFontSize(self.cadLabel, 48)
        ac.setFontAlignment(self.cadLabel, "right")
        ac.setPosition(self.cadLabel, 265, 130)
        ac.setPosition(ac.addLabel(self.appWindow, "t/min"), 270, 160)

    def setupGradeLabel(self):
        ac.setFontSize(self.gradeLabel, 48)
        ac.setFontAlignment(self.gradeLabel, "right")
        ac.setPosition(self.gradeLabel, 115, 200)
        ac.setPosition(ac.addLabel(self.appWindow, "%"), 120, 230)

    def setupHrLabel(self):
        ac.setFontSize(self.hrLabel, 48)
        ac.setFontAlignment(self.hrLabel, "right")
        ac.setPosition(self.hrLabel, 265, 200)
        ac.setPosition(ac.addLabel(self.appWindow, "b/min"), 270, 230)
    
    def setupKmLabel(self):
        ac.setFontSize(self.kmLabel, 32)
        ac.setFontAlignment(self.kmLabel, "right")
        ac.setPosition(self.kmLabel, 115, 270)
        ac.setPosition(ac.addLabel(self.appWindow, "km"), 120, 290)
    
    def setupTrackLenLabel(self):
        ac.setFontSize(self.trackLenLabel, 32)
        ac.setFontAlignment(self.trackLenLabel, "right")
        ac.setPosition(self.trackLenLabel, 265, 270)
        ac.setPosition(ac.addLabel(self.appWindow, "km (lap)"), 270, 290)

    def setupButtons(self):
        ac.setSize(self.startButton, 165, 26)
        ac.setSize(self.stopButton , 165, 26)
        ac.setFontSize(self.startButton, 18)
        ac.setFontSize(self.stopButton , 18)
        ac.setFontAlignment(self.startButton, "center")
        ac.setFontAlignment(self.stopButton, "center")
        ac.setPosition(self.startButton, 1  , 318)
        ac.setPosition(self.stopButton , 168, 318)
        ac.addOnClickedListener(self.startButton, startClick)
        ac.addOnClickedListener(self.stopButton , stopClick)

    def setTrackLen(self, len: float):
        ac.setText(self.trackLenLabel, "{0:.1f}".format(len/1000.0))

    def setKm(self, km: float):
        ac.setText(      self.kmLabel, "{0:.1f}".format(km))
        
    def setPower(self, power: float):
        ac.setText(   self.powerLabel, "{0:.1f}".format(power))

    def setGrade(self, grade: float):
        ac.setText(   self.gradeLabel, "{0:.1f}".format(grade))

    def setSpeed(self, speed: float):
        ac.setText(   self.speedLabel, "{0:.1f}".format(speed))

    def setHr(self, hr: int):
        ac.setText(      self.hrLabel,     "{0}".format(hr))

    def setCad(self, cad: int):
        ac.setText(     self.cadLabel,     "{0}".format(cad))

    def update(self, antManagerState: AntManagerState):
        self.setCad(antManagerState.BikeCadence)
        self.setGrade(antManagerState.BikeIncline)
        self.setHr(antManagerState.CyclistHeartRate)
        self.setKm(antManagerState.TripTotalKm)
        self.setPower(antManagerState.CyclistPower)
        self.setSpeed(antManagerState.BikeSpeedKmh)

def acMain(ac_version):
    global uiElements, antManagerState
    appWindow=ac.newApp("ACSimCyclingDash")

    antManagerState = AntManagerState()
    antManagerState.eraseMemory()

    uiElements = UIElements(appWindow)
    uiElements.setup()

    return "ACSimCyclingDash"

def onRender(*args):
    global antManagerExecutable, antManagerState
    if antManagerExecutable is None:
        ac.console("Starting exeinstance")
        try:
            antManagerExecutable = subprocess.Popen(r".\apps\python\ACSimCyclingDash\bin\SimCycling.exe")
            ac.console("Executable launched : " + str(antManagerExecutable))
        except Exception as e:
            ac.log(repr(e))
    trackLength = ac.getTrackLength(0)
    uiElements.setTrackLen(trackLength)
    try:
        antManagerState.updateFromMemory()
    except Exception as e:
        ac.console(repr(e))
    
    uiElements.update(antManagerState)

def acShutdown():
    ac.log("BIKEDASH acShutdown")
    antManagerExecutable.terminate()
