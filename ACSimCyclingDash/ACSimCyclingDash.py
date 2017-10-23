import math
import mmap

import ac
import acsys

appWindow=0
gradeIndicator=0
speedIndicator=0
powerIndicator=0
hrIndicator=0
cadIndicator=0
kmIndicator=0
trackLenIndicator=0

mm=0
mmHr=0
mmCad=0

km=0.0

BASE_OUT_PATH = r"E:\Damien\Downloads\SimulANT+_2.1.0\out"


# This function gets called by AC when the Plugin is initialised
# The function has to return a string with the plugin name
def acMain(ac_version):
    global appWindow, gradeIndicator, speedIndicator, powerIndicator, hrIndicator, cadIndicator, kmIndicator, trackLenIndicator, mm, mmHr, mmCad
    appWindow=ac.newApp("BikeDash")
    ac.setSize(appWindow,333,343)
    ac.drawBorder(appWindow,0)
    ac.drawBackground(appWindow, 0)
    
    
    powerIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(powerIndicator, 96)
    ac.setFontAlignment(powerIndicator, "right")
    ac.setPosition(powerIndicator, 225, 20)   
    ac.setPosition(ac.addLabel(appWindow, "W"), 240, 80)
    
    speedIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(speedIndicator, 48)
    ac.setFontAlignment(speedIndicator, "right")
    ac.setPosition(speedIndicator, 115, 150)
    ac.setPosition(ac.addLabel(appWindow, "km/h"), 120, 180)
    
    cadIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(cadIndicator, 48)
    ac.setFontAlignment(cadIndicator, "right")
    ac.setPosition(cadIndicator, 265, 150)
    ac.setPosition(ac.addLabel(appWindow, "t/min"), 270, 180)
    
    gradeIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(gradeIndicator, 48)
    ac.setFontAlignment(gradeIndicator, "right")
    ac.setPosition(gradeIndicator, 115, 220)
    ac.setPosition(ac.addLabel(appWindow, "%"), 120, 250)
    
    hrIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(hrIndicator, 48)
    ac.setFontAlignment(hrIndicator, "right")
    ac.setPosition(hrIndicator, 265, 220)
    ac.setPosition(ac.addLabel(appWindow, "b/min"), 270, 250)
    
    kmIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(kmIndicator, 32)
    ac.setFontAlignment(kmIndicator, "right")
    ac.setPosition(kmIndicator, 115, 290)
    ac.setPosition(ac.addLabel(appWindow, "km"), 120, 310)
    
    trackLenIndicator = ac.addLabel(appWindow, "0")
    ac.setFontSize(trackLenIndicator, 32)
    ac.setFontAlignment(trackLenIndicator, "right")
    ac.setPosition(trackLenIndicator, 265, 290)
    ac.setPosition(ac.addLabel(appWindow, "km (lap)"), 270, 310)
    
    #ac.addRenderCallback(appWindow, onFormRender)
    
    mmFile = open(BASE_OUT_PATH + r"\mm.bin", "rb")
    mm = mmap.mmap(mmFile.fileno(), 32, access=mmap.ACCESS_READ)
    mmFile.close()
    mmFile = open(BASE_OUT_PATH + r"\hr.bin", "rb")
    mmHr = mmap.mmap(mmFile.fileno(), 32, access=mmap.ACCESS_READ)
    mmFile.close()
    mmFile = open(BASE_OUT_PATH + r"\cad.bin", "rb")
    mmCad = mmap.mmap(mmFile.fileno(), 32, access=mmap.ACCESS_READ)
    mmFile.close()
    
    return "BikeDash"
	
def acUpdate(deltaT):
    global gradeIndicator, speedIndicator, powerIndicator, kmIndicator, hrIndicator, cadIndicator, trackLenIndicator, km, mm, mmHr, mmCad

    trackLength = ac.getTrackLength(0)
    #ac.log("deltaT : " + str(deltaT))
    ac.setText(trackLenIndicator, "{0:.1f}".format(trackLength/1000.0))
    
    speedMs = ac.getCarState(0, acsys.CS.SpeedMS)
    #ac.log("speedMs " + str(speedMs))
    km += float(speedMs) * float(deltaT) / 1000.0
    ac.setText(kmIndicator, "{0:.1f}".format(km))
    
    #ac.log("BIKEDASH acUpdate")
    mm.seek(0)
    readVal = mm.read()
    #ac.log("BIKEDASH readVal " + str(readVal))
    readValString = readVal.decode('utf-8')
    #ac.log("BIKEDASH readValString " + readValString)
    
    mmHr.seek(0)
    readHr = mmHr.read()
    #ac.log("BIKEDASH readHr " + str(readHr))
    readHrString = readHr.decode('utf-8')
    #ac.log("BIKEDASH readHrString " + readHrString)
    
    mmCad.seek(0)
    readCad = mmCad.read()
    #ac.log("BIKEDASH readCad " + str(readCad))
    readCadString = readCad.decode('utf-8')
    #ac.log("BIKEDASH readCadString " + readCadString)
    
    data = readValString.split('|')
    power = str(int(float(data[0])))
    #ac.log("BIKEDASH power " + power)
    speed = "{0:.1f}".format(float(data[1]))
    #ac.log("BIKEDASH speed " + speed)
    grade = "{0:.1f}".format(float(data[2]))
    
    hr=str(int(readHrString.split("|")[0]))
    cad=str(int(readCadString.split("|")[0]))
    #ac.log("BIKEDASH hr " + hr)

    ac.setText(gradeIndicator, grade)
    ac.setText(speedIndicator, speed)
    ac.setText(powerIndicator, power)
    ac.setText(hrIndicator, hr)
    ac.setText(cadIndicator, cad)

def acShutdown():
    ac.log("BIKEDASH acShutdown")
    mm.close()

    #lateralGIndicator.setCurrentValue(x)
	