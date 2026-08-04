var dbusService = '__TRACKER_SERVICE_NAME__';
var dbusPath = '__TRACKER_OBJECT_PATH__';
var dbusInterface = '__TRACKER_INTERFACE__';

console.error('[CrossMacro] Script started, attempting DBus connection...');

var lastX = -1;
var lastY = -1;
var errorCount = 0;

function publishPosition() {
    try {
        var pos = workspace.cursorPos;
        if (!pos) return;

        var x = Math.floor(pos.x);
        var y = Math.floor(pos.y);
        if (x === lastX && y === lastY) return;

        callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition', x, y);
        lastX = x;
        lastY = y;
        errorCount = 0;
    } catch (e) {
        errorCount++;
        if (errorCount <= 3) {
            console.error('[CrossMacro] DBus Error (Pos #' + errorCount + '): ' + e);
        }
    }
}

// Send initial cursor position before any other startup calls so short-lived
// CLI commands such as `pixelcolor rel 0 0` have a cached position immediately.
try {
    var initialPos = workspace.cursorPos;
    if (initialPos) {
        var initX = Math.floor(initialPos.x);
        var initY = Math.floor(initialPos.y);
        callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition', initX, initY);
        lastX = initX;
        lastY = initY;
    }
} catch (e) {
    console.error('[CrossMacro] DBus Error (Initial Pos): ' + e);
}

var lastResX = null;
var lastResY = null;
var lastResW = null;
var lastResH = null;

function sendResolution() {
    try {
        if (!workspace.virtualScreenGeometry) return;
        var resX = Math.floor(workspace.virtualScreenGeometry.x);
        var resY = Math.floor(workspace.virtualScreenGeometry.y);
        var resW = Math.floor(workspace.virtualScreenGeometry.width);
        var resH = Math.floor(workspace.virtualScreenGeometry.height);
        if (resW > 0 && resH > 0) {
            if (resX === lastResX && resY === lastResY && resW === lastResW && resH === lastResH) return;

            console.error('[CrossMacro] Sending desktop bounds: (' + resX + ',' + resY + ') ' + resW + 'x' + resH);
            callDBus(dbusService, dbusPath, dbusInterface, 'UpdateResolution', resW, resH);
            callDBus(dbusService, dbusPath, dbusInterface, 'UpdateDesktopBounds', resX, resY, resW, resH);
            lastResX = resX;
            lastResY = resY;
            lastResW = resW;
            lastResH = resH;
            console.error('[CrossMacro] Resolution sent successfully');
        }
    } catch (e) {
        console.error('[CrossMacro] DBus Error (Res): ' + e);
    }
}

// Initial Resolution Attempt
sendResolution();

if (workspace.cursorPosChanged && workspace.cursorPosChanged.connect) {
    workspace.cursorPosChanged.connect(publishPosition);
} else {
    var positionTimer = new QTimer();
    positionTimer.interval = 1;
    positionTimer.timeout.connect(publishPosition);
    positionTimer.start();
}

if (workspace.virtualScreenGeometryChanged && workspace.virtualScreenGeometryChanged.connect) {
    workspace.virtualScreenGeometryChanged.connect(sendResolution);
} else {
    var resolutionTimer = new QTimer();
    resolutionTimer.interval = 1000;
    resolutionTimer.timeout.connect(sendResolution);
    resolutionTimer.start();
}
console.error('[CrossMacro] Position tracking started');
