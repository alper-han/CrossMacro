// Local Apple Vision OCR. Input is an image file, never the desktop.
#import <Foundation/Foundation.h>
#import <Vision/Vision.h>
#import <ImageIO/ImageIO.h>
int main(int argc, const char *argv[]) {
  @autoreleasepool {
    if (argc < 2) { fprintf(stderr, "usage: vision-ocr image.png [language]\n"); return 2; }
    NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:argv[1]]];
    VNRecognizeTextRequest *request = [VNRecognizeTextRequest new];
    request.recognitionLevel = VNRequestTextRecognitionLevelAccurate;
    request.usesLanguageCorrection = NO;
    request.recognitionLanguages = @[(argc > 2 ? [NSString stringWithUTF8String:argv[2]] : @"en-US"), @"en-US"];
    VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithURL:url options:@{}];
    NSError *error = nil;
    if (![handler performRequests:@[request] error:&error]) {
      fprintf(stderr, "%s\n", error.localizedDescription.UTF8String); return 1;
    }
    NSMutableArray *lines = [NSMutableArray new];
    for (VNRecognizedTextObservation *observation in request.results) {
      VNRecognizedText *text = [[observation topCandidates:1] firstObject];
      if (!text) continue;
      CGRect b = observation.boundingBox;
      [lines addObject:@{@"text":text.string, @"confidence":@(text.confidence),
        @"bounds":@[@(b.origin.x), @(1-b.origin.y-b.size.height), @(b.size.width), @(b.size.height)]}];
    }
    CGImageSourceRef source = CGImageSourceCreateWithURL((__bridge CFURLRef)url, NULL);
    if (!source) return 1;
    NSDictionary *properties = CFBridgingRelease(CGImageSourceCopyPropertiesAtIndex(source, 0, NULL));
    CFRelease(source);
    NSNumber *width = properties[(NSString *)kCGImagePropertyPixelWidth];
    NSNumber *height = properties[(NSString *)kCGImagePropertyPixelHeight];
    if (!width || !height) return 1;
    NSData *data = [NSJSONSerialization dataWithJSONObject:@{@"lines":lines, @"width":width, @"height":height} options:0 error:&error];
    if (!data) return 1;
    puts([[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding].UTF8String);
    return 0;
  }
}
