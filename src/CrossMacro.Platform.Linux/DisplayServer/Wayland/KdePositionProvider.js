var dbusService = '__TRACKER_SERVICE_NAME__';
var dbusPath = '__TRACKER_OBJECT_PATH__';
var dbusInterface = '__TRACKER_INTERFACE__';

console.error('[CrossMacro] Script started, attempting DBus connection...');

var lastX = -1;
var lastY = -1;
var errorCount = 0;

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

var resSent = false;

function sendResolution() {
    try {
        if (!workspace.virtualScreenGeometry) return;
        var resW = Math.floor(workspace.virtualScreenGeometry.width);
        var resH = Math.floor(workspace.virtualScreenGeometry.height);
        if (resW > 0 && resH > 0) {
            console.error('[CrossMacro] Sending resolution: ' + resW + 'x' + resH);
            callDBus(dbusService, dbusPath, dbusInterface, 'UpdateResolution', resW, resH);
            resSent = true;
            console.error('[CrossMacro] Resolution sent successfully');
        }
    } catch (e) {
        console.error('[CrossMacro] DBus Error (Res): ' + e);
    }
}

// Initial Resolution Attempt
sendResolution();

// Start cursor tracking. KWin scripting reliably exposes QTimer here; do not
// depend on cursor-position change signals that are not available everywhere.
var timer = new QTimer();
timer.interval = 1;  // 1ms interval for 1000Hz mouse support
var ticks = 0;

timer.timeout.connect(function() {
    try {
        ticks++;
        if (!resSent && (ticks % 100 === 0)) {
            sendResolution();
        }

        var pos = workspace.cursorPos;
        if (!pos) return;
        
        var x = Math.floor(pos.x);
        var y = Math.floor(pos.y);

        // Only send update if position changed
        if (x !== lastX || y !== lastY) {
            callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition', x, y);
            lastX = x;
            lastY = y;
            errorCount = 0;
        }
    } catch (e) {
        errorCount++;
        if (errorCount <= 3) {
            console.error('[CrossMacro] DBus Error (Pos #' + errorCount + '): ' + e);
        }
    }
});
timer.start();
console.error('[CrossMacro] Position tracking started');
