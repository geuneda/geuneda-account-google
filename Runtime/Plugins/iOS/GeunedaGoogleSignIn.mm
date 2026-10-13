// Geuneda.Account.Google 의 iOS 구현(GoogleIosSignIn.cs). ASWebAuthenticationSession 으로 구글 인가 페이지를 열고 돌아온 주소를 넘긴다
// 상태 값은 GoogleSignInStatus 와 같다: 0 성공, 1 취소, 2 실패, 3 지원 안 함(iOS 13 미만). ARC 로 컴파일한다(meta 의 CompileFlags)
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

typedef void (*GeunedaGoogleSignInCallback)(intptr_t context, int status, const char* callbackUrl, long errorCode, const char* message);

API_AVAILABLE(ios(13.0))
@interface GeunedaGoogleSignInRequest : NSObject <ASWebAuthenticationPresentationContextProviding>
@property (nonatomic, assign) intptr_t context;
@property (nonatomic, assign) GeunedaGoogleSignInCallback callback;
@property (nonatomic, strong) ASWebAuthenticationSession* session;
@property (nonatomic, assign) BOOL finished;
@end

// 끝나지 않은 요청을 붙잡아 둔다(세션 완료 블록은 요청을 약하게 잡는다). 키는 C# 의 GCHandle 값이고 다음 요청이 같은 값을 다시 쓸 수 있다
static NSMutableDictionary<NSNumber*, GeunedaGoogleSignInRequest*>* GeunedaGoogleSignInPending()
{
    static NSMutableDictionary<NSNumber*, GeunedaGoogleSignInRequest*>* pending;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ pending = [[NSMutableDictionary alloc] init]; });
    return pending;
}

// 앱 창을 찾는다: 키 창, 없으면 활성 장면의 첫 창(앱 시작 직후에는 키 창이 아직 없을 수 있다)
static UIWindow* GeunedaPresentationWindow()
{
    UIWindow* fallback = nil;

    for (UIScene* scene in [UIApplication sharedApplication].connectedScenes)
    {
        if (![scene isKindOfClass:[UIWindowScene class]])
        {
            continue;
        }

        for (UIWindow* window in ((UIWindowScene*)scene).windows)
        {
            if (window.isKeyWindow)
            {
                return window;
            }

            if (fallback == nil && scene.activationState == UISceneActivationStateForegroundActive)
            {
                fallback = window;
            }
        }
    }

    return fallback;
}

@implementation GeunedaGoogleSignInRequest

// 콜백은 요청마다 정확히 한 번이고(완료 핸들러, start 실패, 취소 중 먼저 온 것), 그때 대기 목록에서 이 요청만 뺀다. 메인 스레드에서만 부른다
- (void)finishWithStatus:(int)status url:(NSString*)url errorCode:(long)errorCode message:(NSString*)message
{
    if (self.finished)
    {
        return;
    }

    self.finished = YES;
    intptr_t context = self.context;
    GeunedaGoogleSignInCallback callback = self.callback;

    if (GeunedaGoogleSignInPending()[@(context)] == self)
    {
        [GeunedaGoogleSignInPending() removeObjectForKey:@(context)];
    }

    // 목록에서 빠져 이 객체가 해제될 수 있으므로 꺼내 둔 값만 쓴다. C# 은 이 콜백에서 GCHandle 을 푼다
    callback(context, status, url.UTF8String, errorCode, message.UTF8String);
}

- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession*)session
{
    return GeunedaPresentationWindow();
}

@end

extern "C" void GeunedaGoogleSignIn_Start(intptr_t context, const char* authorizationUrl, const char* callbackScheme, int ephemeral,
    GeunedaGoogleSignInCallback callback)
{
    if (@available(iOS 13.0, *))
    {
        NSURL* url = [NSURL URLWithString:[NSString stringWithUTF8String:authorizationUrl]];
        NSString* scheme = [NSString stringWithUTF8String:callbackScheme];
        GeunedaGoogleSignInRequest* request = [[GeunedaGoogleSignInRequest alloc] init];
        request.context = context;
        request.callback = callback;
        __weak GeunedaGoogleSignInRequest* weakRequest = request;

        ASWebAuthenticationSession* session = [[ASWebAuthenticationSession alloc] initWithURL:url callbackURLScheme:scheme
            completionHandler:^(NSURL* callbackURL, NSError* error) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    // 이미 끝난(취소 등) 요청이면 nil 이거나 finished 라 아무것도 하지 않는다
                    GeunedaGoogleSignInRequest* strongRequest = weakRequest;

                    if (callbackURL != nil)
                    {
                        [strongRequest finishWithStatus:0 url:callbackURL.absoluteString errorCode:0 message:nil];
                        return;
                    }

                    int status = error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin ? 1 : 2;
                    [strongRequest finishWithStatus:status url:nil errorCode:(long)error.code message:error.localizedDescription ?: @""];
                });
            }];

        session.presentationContextProvider = request;
        session.prefersEphemeralWebBrowserSession = ephemeral != 0;
        request.session = session;
        GeunedaGoogleSignInPending()[@(context)] = request;

        if (![session start])
        {
            // 거부 이유(예: 앱 장면이 아직 전면 활성이 아님)는 완료 핸들러가 먼저 넘긴다. 그것이 없을 때만 일반 실패로 끝낸다
            dispatch_async(dispatch_get_main_queue(), ^{
                [weakRequest finishWithStatus:2 url:nil errorCode:0 message:@"ASWebAuthenticationSession could not start"];
            });
        }

        return;
    }

    callback(context, 3, NULL, 0, "Google sign-in needs iOS 13 or later");
}

// 진행 중인 요청을 취소로 끝내고(콜백으로 C# 핸들 해제) 화면을 닫는다. 프로그램 취소는 완료 핸들러가 온다는 보장이 없어 여기서 끝낸다
extern "C" void GeunedaGoogleSignIn_Cancel(intptr_t context)
{
    if (@available(iOS 13.0, *))
    {
        GeunedaGoogleSignInRequest* request = GeunedaGoogleSignInPending()[@(context)];
        ASWebAuthenticationSession* session = request.session;
        [request finishWithStatus:1 url:nil errorCode:ASWebAuthenticationSessionErrorCodeCanceledLogin message:@"Google sign-in was cancelled by the app"];
        [session cancel];
    }
}
