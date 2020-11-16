import ac   
import json
import mmap
import subprocess
import sys
import acsys
import os
import threading
import time
import random

antManagerExecutable = None
antManagerState      = None
uiElements           = None
workoutUi            = None
otherCyclists        = dict()
otherCyclistsMaxCount= 4

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
        self.WorkoutElapsedTime = 0.0
        self.WorkoutMessage = ""
        self.WorkoutName = ""
        self.LapPosition = 0.0
        self.AirDensity = 0.0
        self.DraftingCoefficient = 1.0
        self.DraftPercentage = 0

        self._onUpdated = []


    def _instanciateFromDict(self, dictionary):
        for k, v in dictionary.items():
            setattr(self, k, v)

    def _isSameAsDict(self, dictionary):
        for k, v in dictionary.items():
            if getattr(self, k) != v:
                return False
        return True

    def _getMemoryMap(self):
        return mmap.mmap(0, 1024, "SimCycling")

    def addUpdateCallback(self, callback):
        self._onUpdated.append(callback)

    def updateFromMemory(self):
        memoryMap = self._getMemoryMap()
        memoryMap.seek(0)
        flag = memoryMap.read(1)
        if flag == b'\xAA':
            ac.console("Concurrent write in progress. Skip reading")
            return False
        if flag != b'\x11':
            return False
        memoryMap.seek(1)
        readBytes  = memoryMap.read()
        memoryMap.close()
        readString = readBytes.decode("utf-8").rstrip("\0")
        #ac.console(readString)
        dictData   = json.loads(readString)
        if not self._isSameAsDict(dictData):
            self._instanciateFromDict(dictData)
            for callback in self._onUpdated:
                callback(self)
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
    if ac.getText(uiElements.btn1) != "starting..." and ( antManagerExecutable is None or antManagerExecutable.poll() is not None ):
        startExecutable()
    elif not workoutInProgress():
        loadWorkout()
    else:
        stopWorkout()
 
def btn2_clicked(*args):
    if ac.getText(uiElements.btn1) == "starting...":
        antManagerExecutable.kill()
        ac.setText(uiElements.btn1, "start")
    if antManagerExecutable is not None and antManagerExecutable.poll() is None:
        stopExecutable()

def workoutInProgress():
    return antManagerState is not None and antManagerState.RemainingTotalTime > 0

class ExecutableStarter(threading.Thread):
    def run(self):
        global antManagerExecutable, uiElements
        ac.console("Starting executable")
        ac.setText(uiElements.btn1, "starting...")
        time.sleep(0.5)
        try:
            antManagerExecutable = subprocess.Popen(r".\apps\python\ACSimCyclingDash\bin\SimCycling.exe", cwd=os.path.join(os.path.dirname(os.path.realpath(__file__)), "bin"))
            ac.console("Executable launched : " + str(antManagerExecutable))
        except Exception as e:
            ac.log(repr(e))
            ac.console(str(antManagerExecutable))
            ac.setText(uiElements.btn1, "start")

class WorkoutLoader(threading.Thread):
    def run(self):
        time.sleep(0.5)
        ac.console("Sending signal to load workout")
        sendSignal(SIGNAL_LOAD_WORKOUT)

def startExecutable():
    ExecutableStarter().start()

def loadWorkout():
    WorkoutLoader().start()

def stopWorkout():
    ac.console("Sending signal to stop workout")
    sendSignal(SIGNAL_STOP_WORKOUT)

def stopExecutable():
    ac.console("Sending signal to stop")
    sendSignal(SIGNAL_EXIT)

class UIElement():
    def __init__(self, valueName, appWindow, x, y, fontSize=48, unit="", format=None, title=None, alignment="right"):
        self.valueName = valueName
        self.unit = unit
        self.label = ac.addLabel(appWindow, "0")
        self.unitLabel = ac.addLabel(appWindow, self.unit)
        self.format = format
        self.x = x
        self.y = y
        self.fontSize = fontSize
        self.title = title
        self.alignment = alignment
        if self.title is not None:
            self.titleLabel = ac.addLabel(appWindow, self.title)


    def setup(self):
        ac.setFontSize(self.label, self.fontSize)
        ac.setFontAlignment(self.label, self.alignment)
        ac.setFontSize(self.unitLabel, self.fontSize//2)
        ac.setPosition(self.label, self.x, self.y)  
        ac.setPosition(self.unitLabel, self.x+self.fontSize//10, self.y+self.fontSize//2)
        if self.title is not None:
            ac.setFontAlignment(self.titleLabel, "right")
            ac.setFontSize(self.titleLabel, self.fontSize//2)
            ac.setPosition(self.titleLabel, self.x, self.y + self.fontSize)  


    def update(self, state):
        if state is None:
            ac.setText(self.label, "-")
            return

        value = getattr(state, self.valueName)
        if self.format is not None:
            ac.setText(self.label, ("{0:" + self.format + "}").format(value))
        else:
            attrValue = getattr(state, self.valueName)
            if attrValue is not None:
                ac.setText(self.label, "{0}".format(attrValue))
            else:
                ac.setText(self.label, "")

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
        self.uiElements.append(UIElement("BikeCadence", appWindow, 265, 130, 48, 'rpm'))
        self.uiElements.append(UIElement("BikeIncline", appWindow, 115, 200, 48, '%', ".1f"))
        self.uiElements.append(UIElement("CyclistHeartRate", appWindow, 265, 200, 48, 'bpm'))
        self.uiElements.append(UIElement("LapPosition", appWindow, 115, 270, 32, 'km', ".1f"))
        self.uiElements.append(UIElement("TrackLength", appWindow, 265, 270, 32, 'km (lap)', ".1f"))

       
        self.uiElements.append(TimerUIElement("TripTotalTime", appWindow, 166, 310, 32))
        self.uiElements.append(UIElement("TripTotalKm", appWindow, 166, 350, 32, 'km (session)', ".1f"))

        self.btn1   = ac.addButton(self.appWindow, "start")
        self.btn2   = ac.addButton(self.appWindow, "stop")

    def setup(self):
        ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,333,424)
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
        ac.setPosition(self.btn1, 1  , 398)
        ac.setPosition(self.btn2 , 168, 398)
        ac.addOnClickedListener(self.btn1, btn1_clicked)
        ac.addOnClickedListener(self.btn2 , btn2_clicked)

    def update(self, antManagerState: AntManagerState):
        for el in self.uiElements:
            el.update(antManagerState)

class WorkoutUI:
    def __init__(self, appWindow):
        self.appWindow = appWindow
        self.uiElements = []
        self.uiElements.append(UIElement("TargetPower", appWindow, 100, 10, 48, 'W', ".0f", title="Target"))
        self.uiElements.append(UIElement("NextTargetPower", appWindow, 250, 10, 48, 'W', ".0f", title="Next"))
        self.uiElements.append(TimerUIElement("RemainingIntervalTime", appWindow, 400, 10, 48))
        self.uiElements.append(TimerUIElement("RemainingTotalTime", appWindow, 650 , 10, 48))
        self.uiElements.append(UIElement("WorkoutMessage", appWindow, 400, 215, 36, alignment="center"))

        self.graph = ac.addGraph(appWindow, "0")
        ac.addSerieToGraph(self.graph, 0.0, 1.0, 0.0) # current power : green
        ac.addSerieToGraph(self.graph, 1.0, 1.0, 1.0) # target power : white
        ac.addSerieToGraph(self.graph, 1.0, 0.0, 0.0) # current heart rate : red

    def setup(self):
        # ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,800,100)
        ac.drawBorder(self.appWindow,0)
        ac.drawBackground(self.appWindow, 0)
        for el in self.uiElements:
            el.setup()

        ac.setPosition(self.graph, 0, 100)
        ac.setSize(self.graph,800,100)
        ac.setRange(self.graph, 0.0, 600, 2000)
   
    def update(self, antManagerState: AntManagerState):
        for el in self.uiElements:
            el.update(antManagerState)
        
        x = ac.addValueToGraph(self.graph, 0, antManagerState.CyclistPower)
        x = ac.addValueToGraph(self.graph, 1, antManagerState.TargetPower)
        x = ac.addValueToGraph(self.graph, 2, (antManagerState.CyclistHeartRate - 10) * 3)

class OtherCyclistData:
    def __init__(self, name="Cyclist", distance=0, power=0):
        self.Name = name
        self.Distance = distance
        self.Power = power


class OtherCyclistsUI:
    def __init__(self, appWindow):
        self.appWindow = appWindow

        self.uiElements = []
        for cyclist in range(otherCyclistsMaxCount+1):
            uiElementsPerCyclist = []
            yPos = 25 + 30*cyclist
            # name and distance are only useful for other cyclists (not self which is at midpoint)
            if cyclist != int(otherCyclistsMaxCount/2): 
                uiElementsPerCyclist.append(UIElement("Name"    , appWindow,  10, yPos, 24, alignment="left"))
                uiElementsPerCyclist.append(UIElement("Distance", appWindow, 175, yPos, 24, 'm', '.0f'))
            uiElementsPerCyclist.append(UIElement("Power"   , appWindow, 250, yPos, 24, 'W', '.0f'))
            self.uiElements.append(uiElementsPerCyclist)
        self.draftPercentage = UIElement("DraftPercentage" , appWindow,  175, 25+30*int(otherCyclistsMaxCount/2), 24, unit="%", format=".1f")

       
    def setup(self):
        ac.setSize(self.appWindow,270, 30 + 30*(otherCyclistsMaxCount+1))
        ac.drawBorder(self.appWindow,0)
        ac.drawBackground(self.appWindow, 0)

        for uiElements in self.uiElements:
            for uiElementsPerCyclist in uiElements:
                uiElementsPerCyclist.setup()
        self.draftPercentage.setup()

    def update(self, otherCyclistsList: list):
        self.draftPercentage.update(antManagerState)
        for cyclist, uiElementsPerCyclist in enumerate(self.uiElements):
            for uiElement in uiElementsPerCyclist:
                uiElement.update(otherCyclistsList[cyclist])

def updateDrafting(state):
    state.DraftPercentage = 100 * (1-state.DraftingCoefficient)


def acMain(ac_version):
    global uiElements, antManagerState, workoutUi, otherCyclistsUI
    appWindow=ac.newApp("ACSimCyclingDash")


    antManagerState = AntManagerState()
    antManagerState.eraseMemory()
    antManagerState.addUpdateCallback(sendStateToChat)
    antManagerState.addUpdateCallback(updateDrafting)

    uiElements = UIElements(appWindow)
    uiElements.setup()

    workoutAppWindow=ac.newApp("WorkoutDash")
    workoutUi = WorkoutUI(workoutAppWindow)
    workoutUi.setup()
    
    otherCyclistsAppWindow=ac.newApp("Other SimCyclists")
    otherCyclistsUI = OtherCyclistsUI(otherCyclistsAppWindow)
    otherCyclistsUI.setup()

    ac.addOnChatMessageListener(appWindow, onChatMessage)

    return "ACSimCyclingDash"

def onRender(*args):
    try:
        stateFound = antManagerState.updateFromMemory()
        if stateFound:
            if not workoutInProgress():
                ac.setText(uiElements.btn1, "load workout")
            else:
                ac.setText(uiElements.btn1, "stop workout")
                ac.console("Workout message : " + repr(antManagerState.WorkoutMessage))
        elif not stateFound and ac.getText(uiElements.btn1) != "starting...":
            ac.setText(uiElements.btn1, "start")
    except Exception as e:
        ac.console(repr(e))
    
    antManagerState.TrackLength = ac.getTrackLength(0) / 1000
    antManagerState.LapPosition = ac.getCarState(0, acsys.CS.NormalizedSplinePosition) * ac.getTrackLength(0) / 1000
    uiElements.update(antManagerState)
    workoutUi.update(antManagerState)
    RaceState().updateToMemory()

def acShutdown():
    ac.log("BIKEDASH acShutdown")
    sendSignal(SIGNAL_EXIT)


def sendStateToChat(*args):
    # ac.console("Send chat message !")
    ac.sendChatMessage("SIMCYCLING {} {}".format(antManagerState.LapPosition, antManagerState.CyclistPower))

    # uncomment for offline testing purposes
    #friends = ['Damien', 'Paul', 'Samuel', 'Tim', 'Aline']
    #friend = friends[random.randint(0,len(friends)-1)]
    #onChatMessage("SIMCYCLING {} {}".format(random.random()*antManagerState.TrackLength, random.random()*350), friend)

def onChatMessage(message:str, author:str):
    # ac.console("onChatMessage !" + author + " " + message)
    if message.startswith("SIMCYCLING"):
        position, power = message.split(' ')[1:]
        # moi   :  5 | 5.5| 2 |  2
        # autre : 5.5| 0.5| 0 | 5.5
        # dist  :-0.5|-1.0| 2 | 2.5

        track_len = antManagerState.TrackLength
        friend_pos = float(position)
        my_pos = antManagerState.LapPosition

        dist = my_pos - friend_pos
        
        # assuming the track is circular 
        if dist > track_len/2: 
            dist -= antManagerState.TrackLength
        elif dist < -1 * track_len/2:
            dist += antManagerState.TrackLength

        data = OtherCyclistData(author, dist*1000, float(power))
        updateCyclists(data)

def updateCyclists(data: OtherCyclistData):
    otherCyclists[data.Name] = data
    otherCyclistsList = sorted(otherCyclists.values(), key=lambda c : c.Distance)

    # center the list around self
    targetSelfIndex = int(otherCyclistsMaxCount/2)
    currentSelfIndex = 0
    for i in range(len(otherCyclistsList)):
        if otherCyclistsList[i].Name == ac.getDriverName(0):
            currentSelfIndex = i
            break
    
    renderList = otherCyclistsList
    if targetSelfIndex - currentSelfIndex > 0: # need to move data to the right (pad with None)
        renderList = [None] * (targetSelfIndex - currentSelfIndex) + otherCyclistsList
    elif targetSelfIndex - currentSelfIndex < 0: # need to move data to the left (=truncate)
        renderList = otherCyclistsList[currentSelfIndex-targetSelfIndex:]

    if len(renderList) < otherCyclistsMaxCount+1:
        renderList = renderList + [None] * (otherCyclistsMaxCount+1 - len(renderList))

    otherCyclistsUI.update(renderList)
