// macOS 14.2+ process-only audio capture. No microphone or global mix fallback.
#import <AppKit/AppKit.h>
#import <CoreAudio/CoreAudio.h>
#import <CoreAudio/CATapDescription.h>
#import <CoreAudio/AudioHardwareTapping.h>
#import <AudioToolbox/AudioToolbox.h>
#include <signal.h>
#include <stdatomic.h>
#include <math.h>

static volatile sig_atomic_t running = 1;
static void stopCapture(int value) { running = 0; }
typedef struct {
    ExtAudioFileRef file;
    UInt32 bytesPerFrame;
    _Atomic unsigned long long frames;
    _Atomic int error;
    double firstBufferUnix;
} Capture;

static OSStatus receiveAudio(AudioObjectID device, const AudioTimeStamp *now,
    const AudioBufferList *input, const AudioTimeStamp *inputTime,
    AudioBufferList *output, const AudioTimeStamp *outputTime, void *opaque) {
    Capture *capture = opaque;
    if (!input || !input->mNumberBuffers || !input->mBuffers[0].mData || !capture->bytesPerFrame) return noErr;
    UInt32 frames = input->mBuffers[0].mDataByteSize / capture->bytesPerFrame;
    if (!atomic_load(&capture->frames)) capture->firstBufferUnix = CFAbsoluteTimeGetCurrent() + kCFAbsoluteTimeIntervalSince1970;
    OSStatus status = ExtAudioFileWriteAsync(capture->file, frames, input);
    if (status) atomic_store(&capture->error, status);
    atomic_fetch_add(&capture->frames, frames);
    return noErr;
}

static BOOL check(OSStatus status, const char *operation) {
    if (!status) return YES;
    fprintf(stderr, "%s failed: %d\n", operation, (int)status);
    return NO;
}

int main(int argc, const char *argv[]) {
    @autoreleasepool {
        if (argc != 4) {
            fprintf(stderr, "usage: audio-tap bundle-id output.caf seconds\n");
            return 2;
        }
        if (@available(macOS 14.2, *)) {} else { return 2; }
        signal(SIGINT,stopCapture); signal(SIGTERM,stopCapture);
        NSString *bundle = [NSString stringWithUTF8String:argv[1]];
        NSArray<NSRunningApplication *> *apps = [NSRunningApplication runningApplicationsWithBundleIdentifier:bundle];
        if (apps.count != 1) { fprintf(stderr,"Expected one running target process, found %lu\n",(unsigned long)apps.count); return 2; }
        pid_t pid = apps.firstObject.processIdentifier;
        char *end = NULL;
        double seconds = strtod(argv[3], &end);
        if (!end || end == argv[3] || *end || !isfinite(seconds) || seconds <= 0 || seconds > 600) return 2;
        AudioObjectID process = kAudioObjectUnknown, tap = kAudioObjectUnknown, aggregate = kAudioObjectUnknown;
        AudioDeviceIOProcID io = NULL;
        Capture capture = {0};
        AudioStreamBasicDescription format = {0};
        AudioObjectPropertyAddress property = {kAudioHardwarePropertyTranslatePIDToProcessObject,kAudioObjectPropertyScopeGlobal,kAudioObjectPropertyElementMain};
        UInt32 bytes = sizeof(process);
        if (!check(AudioObjectGetPropertyData(kAudioObjectSystemObject,&property,sizeof(pid),&pid,&bytes,&process),"Find audio process") || !process) return 2;
        CATapDescription *description = [[CATapDescription alloc] initStereoMixdownOfProcesses:@[@(process)]];
        description.name = @"CrossMacro target-app audio";
        description.privateTap = YES;
        description.muteBehavior = CATapUnmuted;
        NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:argv[2]]];
        int result = 1;
        BOOL started = NO;
        do {
            if (!check(AudioHardwareCreateProcessTap(description,&tap),"Create target tap")) break;
            property.mSelector = kAudioTapPropertyFormat; bytes = sizeof(format);
            if (!check(AudioObjectGetPropertyData(tap,&property,0,NULL,&bytes,&format),"Read tap format")) break;
            if (!format.mBytesPerFrame || !format.mSampleRate) break;
            NSDictionary *composition = @{
                @kAudioAggregateDeviceNameKey: @"CrossMacro private audio input",
                @kAudioAggregateDeviceUIDKey: NSUUID.UUID.UUIDString,
                @kAudioAggregateDeviceIsPrivateKey: @YES,
                @kAudioAggregateDeviceTapAutoStartKey: @YES,
                @kAudioAggregateDeviceTapListKey: @[@{
                    @kAudioSubTapUIDKey: description.UUID.UUIDString,
                    @kAudioSubTapDriftCompensationKey: @YES
                }]
            };
            if (!check(AudioHardwareCreateAggregateDevice((__bridge CFDictionaryRef)composition,&aggregate),"Create private aggregate")) break;
            if (!check(ExtAudioFileCreateWithURL((__bridge CFURLRef)url,kAudioFileCAFType,&format,NULL,kAudioFileFlags_EraseFile,&capture.file),"Create audio file")) break;
            capture.bytesPerFrame = format.mBytesPerFrame;
            if (!check(ExtAudioFileWriteAsync(capture.file,0,NULL),"Prepare audio writer")) break;
            if (!check(AudioDeviceCreateIOProcID(aggregate,receiveAudio,&capture,&io),"Create audio callback")) break;
            if (!check(AudioDeviceStart(aggregate,io),"Start audio")) break;
            started = YES;
            double begin = CFAbsoluteTimeGetCurrent();
            printf("{\"status\":\"audio_ready\",\"pid\":%d,\"sample_rate\":%.0f,\"channels\":%u,\"started_unix\":%.6f}\n",pid,format.mSampleRate,format.mChannelsPerFrame,begin+kCFAbsoluteTimeIntervalSince1970); fflush(stdout);
            while (running && CFAbsoluteTimeGetCurrent()-begin < seconds)
                CFRunLoopRunInMode(kCFRunLoopDefaultMode,.05,false);
            result = 0;
        } while (false);
        if (started) AudioDeviceStop(aggregate,io);
        if (io) AudioDeviceDestroyIOProcID(aggregate,io);
        if (capture.file) ExtAudioFileDispose(capture.file);
        if (aggregate) AudioHardwareDestroyAggregateDevice(aggregate);
        if (tap) AudioHardwareDestroyProcessTap(tap);
        unsigned long long frames = atomic_load(&capture.frames);
        printf("{\"status\":\"audio_stopped\",\"frames\":%llu,\"audio_seconds\":%.6f,\"first_buffer_unix\":%.6f,\"write_error\":%d}\n",frames,format.mSampleRate ? frames/format.mSampleRate : 0,capture.firstBufferUnix,atomic_load(&capture.error));
        if (result || !frames || atomic_load(&capture.error)) return 1;
        return 0;
    }
}
