# Geuneda.Account.Google - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.account.google`, 어셈블리 `Geuneda.Account.Google`
- **의존성**: `com.geuneda.account`, `com.geuneda.core`(Result, MainThreadDispatcher), `com.geuneda.http`(iOS 토큰 교환), UniTask, Newtonsoft
- **이유(geuneda 결정 D3)**: Android Gradle 의존성과 네이티브 코드를 가진 연동은 기능 패키지와 따로 둡니다. 설치한 게임에만 들어갑니다.
- iOS 는 Google Sign-In SDK(CocoaPods/EDM4U) 대신 ASWebAuthenticationSession + PKCE 를 씁니다(사용자 결정 2026-10-14).

## 2. 구조

| 파일 | 역할 |
|------|------|
| `Runtime/GoogleAuthProvider.cs`, `GoogleAuthOptions.cs` | 공개 제공자와 설정(클라이언트 ID 형식 검사) |
| `Runtime/GooglePkce.cs` | 검증자·도전값(S256)·상태, 인가 주소(서버 nonce 포함), 돌아온 주소 해석(상태 검사) |
| `Runtime/GoogleTokenExchange.cs` | `https://oauth2.googleapis.com/token` 인가 코드 교환(form, PKCE) |
| `Runtime/GoogleSignInResults.cs`, `GoogleSignInStatus.cs` | 네이티브 상태 → 결과 코드 |
| `Runtime/Android/GoogleAndroidSignIn.cs` | `#if UNITY_ANDROID && !UNITY_EDITOR`. Java 호출, `AndroidJavaProxy` 콜백(UI 스레드 → `MainThreadDispatcher.Post`), `CancellationSignal` 취소 |
| `Runtime/Ios/GoogleIosSignIn.cs` | `#if UNITY_IOS && !UNITY_EDITOR`. 네이티브 세션 → 상태 검사 → 토큰 교환 |
| `Runtime/Plugins/Android/GeunedaGoogleSignIn.androidlib/` | Android 라이브러리: `build.gradle`(의존성, compileSdk ≥ 34, consumer 규칙), `proguard-rules.pro`, Java `GeunedaGoogleSignIn`·`GeunedaGoogleSignInCallback`. 폴더 하나가 플러그인이라 안쪽에는 meta 가 없습니다 |
| `Runtime/Plugins/iOS/GeunedaGoogleSignIn.mm` | ASWebAuthenticationSession. meta: iOS 만, `-fobjc-arc`, AuthenticationServices |

## 3. 규칙

- **콜백은 정확히 한 번(iOS)**: `start` 가 실패하면 완료 핸들러도 오류로 불립니다. 네이티브 요청 객체(`GeunedaGoogleSignInRequest`)의 `finishWithStatus` 만 콜백하고 `finished` 플래그로 한 번만 부릅니다(완료 핸들러, `start` 실패, 앱 취소 중 먼저 온 것). 완료 핸들러 블록은 요청을 약하게 잡고, 대기 사전에서는 그 키의 값이 이 요청일 때만 뺍니다(키는 GCHandle 값이라 다음 요청이 같은 값을 쓸 수 있음). `start` 실패 시 일반 실패는 메인 큐에 늦게 넣어, 완료 핸들러의 구체적 오류(예: 코드 3 장면 비활성)가 먼저 가게 합니다. 이를 어기면 해제된 GCHandle 로 두 번째 콜백이 와 앱이 종료됩니다(2026-10-14 재현).
- **앱 취소(iOS)**: `[session cancel]` 은 완료 핸들러가 온다는 보장이 없으므로, 네이티브 취소가 요청을 취소로 끝내 콜백(GCHandle 해제)한 뒤 세션을 닫습니다. C# 은 네이티브 콜백이 오면 `_context` 를 0 으로 바꾸고 `Cancel` 은 0 이면 아무것도 하지 않습니다(이미 끝난 요청의 늦은 취소가 같은 GCHandle 값을 받은 다음 요청을 취소하지 않게).
- **네이티브 콜백 안에서 예외 금지**: `OnCompleted` 는 try/catch, 결과는 `MainThreadDispatcher.Post`.
- **Android 스레드**: Credential Manager 호출은 UI 스레드(`runOnUiThread`), 결과 콜백도 메인 루퍼 executor 에서 옵니다. `Runnable` 안의 예외는 C# 의 `AndroidJavaException` 으로 잡히지 않으므로 Java 에서 잡아 실패 콜백으로 넘깁니다(로그인·로그아웃 모두 콜백으로 끝남). C# 은 Unity 메인 스레드로 넘깁니다. 취소도 메인 스레드에서 끝났는지 확인한 뒤 `signal.cancel()` 합니다(해제된 신호를 건드리지 않게).
- **R8**: `proguard-rules.pro` 가 Java 진입점과 콜백 인터페이스, `androidx.credentials.playservices` 를 지킵니다(consumerProguardFiles). 클래스·메서드 이름을 바꾸면 규칙도 바꿉니다.
- **상태 값**(0 성공, 1 취소, 2 실패, 3 미지원)은 C# enum, Java, .mm 이 같아야 합니다.
- **nonce**: `RequiresNonce` 는 true 이고 서버 nonce 원문을 그대로 넘깁니다. Android 는 Java `signIn(activity, serverClientId, nonce, callback)` 이 `GetSignInWithGoogleOption.Builder.setNonce`, iOS 는 `GooglePkce.AuthorizationUrl(clientId, nonce)` 가 인가 요청의 `nonce` 파라미터로 넣습니다. 구글은 원문을 id 토큰 `nonce` 클레임에 그대로 넣고 서버 `GoogleIdTokenVerifier` 는 원문과 비교합니다(해시하지 않음). Java 메서드 시그니처를 바꾸면 C# `CallStatic` 인자와 R8 규칙을 함께 봅니다.
- 의존성 버전을 올리면 compileSdk 요구(1.3.0 은 34)를 확인합니다.
- **AndroidX 직접 의존**: `credentials-play-services-auth` 가 play-services-auth 에서 `androidx.core`·`androidx.loader` 를 빼고 받으므로 `build.gradle` 에 직접 넣습니다. Unity 앱에는 AndroidX 가 없어 빼면 `HiddenActivity` 가 `androidx.activity.ComponentActivity` 를 못 읽고 앱이 죽습니다(2026-10-14 BlueStacks 재현, R8 과 무관한 의존성 누락).
- **Android 취소**: 구글 화면이 떠 있는 동안 Unity 플레이어가 멈춰 `MainThreadDispatcher` 로 넘긴 취소는 앱으로 돌아온 뒤에 실행됩니다. Credential Manager 는 콘솔 설정 오류도 취소 예외로 주므로 `Cancelled` 결과에 네이티브 메시지(`getType()` + 메시지, 클래스 이름은 R8 이 바꿈)를 남깁니다.

## 4. 테스트

- `Tests/EditMode/`: PKCE(RFC 7636 예시 값), 인가 주소(nonce 포함), 돌아온 주소(상태 불일치·오류·코드 없음), 토큰 교환(가짜 전송: 성공·400·id_token 없음·HTML·네트워크), 설정 검사, nonce 누락 예외, 상태 → 코드.
- 기기(2026-10-14): iOS 시뮬레이터에서 세션이 열리고 취소 → `Cancelled`, 장면 비활성일 때 오류 3 → `PlatformSignInFailed`. Android 는 BlueStacks 에서 Credential Manager 호출 경로(가짜 클라이언트 ID). 실제 로그인은 Google Cloud 클라이언트 설정이 필요해 확인하지 않았습니다.
