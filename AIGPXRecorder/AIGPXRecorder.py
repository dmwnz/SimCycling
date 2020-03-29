import ac
import acsys
k=0
f_loc=""
started=False
passed_half=False
finished=False
prev_p = 0
import os

class UIElements:
    def __init__(self, appWindow):
        self.appWindow = appWindow
        self.posLabel = ac.addLabel(self.appWindow, "0")
        ac.setFontSize(self.posLabel, 12)
        ac.setFontAlignment(self.posLabel, "right")
        ac.setPosition(self.posLabel, 225, 50)   

    def setup(self):
        ac.addRenderCallback(self.appWindow, onRender)
        ac.setSize(self.appWindow,333,70)
        ac.drawBorder(self.appWindow,0)

    def setPos(self, x, y, z):
        ac.setText(self.posLabel, "{0:.2f}, {1:.2f}, {2:.2f}".format(x,y,z))
        
    def update(self, x, y, z):
        self.setPos(x,y,z)


def acMain(ac_version):
    global uiElements, f_loc
    appWindow=ac.newApp("AIGPXRecorder")

    uiElements = UIElements(appWindow)
    uiElements.setup()
    uiElements.update(0,0,0)
    track_name = ac.getTrackName(0)
    f_loc = os.path.join(os.path.dirname(os.path.realpath(__file__)), track_name, ".csv")
    f = open(f_loc, "w")
    f.close()
    
    return "AIGPXRecorder"

def onRender(*args):
    global prev_p, k, f_loc, started, passed_half, finished
    if (k == 20):
        x,y,z = ac.getCarState(ac.getCarsCount() - 1,acsys.CS.WorldPosition)
        p = ac.getCarState(ac.getCarsCount() - 1,acsys.CS.NormalizedSplinePosition)
        if not started:
            if prev_p > 0.5 and p < 0.5:
                started = True
            prev_p = p
        else:
            if prev_p > 0.5 and p < 0.5:
                finished = True
                return
            prev_p = p
            if not finished:
                uiElements.update(x,y,z)
                f = open(f_loc, "a")
                f.write("{0:.5f}, {1:.5f}, {2:.5f}, {3: .5f}\n".format(x,y,z,p))
                f.close()
        k=0
    k +=1
