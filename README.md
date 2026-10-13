# Geuneda Account Google

[com.geuneda.account](https://github.com/geuneda/geuneda-account) 의 Google 로그인 제공자입니다. 구글 id 토큰을 받아 서버가 검증합니다. 이 패키지를 설치한 게임에만 네이티브 코드와 Android 의존성이 들어갑니다.

- **Android**: Credential Manager(`androidx.credentials`)의 "Google 로 로그인" 화면. 패키지 안 Android 라이브러리(`GeunedaGoogleSignIn.androidlib`)가 Gradle 의존성(`androidx.credentials:credentials`·`credentials-play-services-auth` 1.3.0, `googleid` 1.1.1, `androidx.core`·`androidx.loader`)과 R8 keep 규칙을 가져옵니다. `credentials-play-services-auth` 는 앱에 `androidx.core`·`androidx.loader` 가 이미 있다고 보고 빼고 받는데, Unity 앱에는 없어 직접 넣습니다(없으면 로그인 화면을 열 때 `androidx.activity.ComponentActivity` 를 못 읽고 앱이 죽습니다).
- **iOS**: 외부 SDK 없이 ASWebAuthenticationSession 으로 구글 로그인 페이지를 열고 OAuth 인가 코드 + PKCE 로 id 토큰을 받습니다.

| 기능 | 타입 | 네임스페이스 |
|------|------|-------------|
| 제공자 | `GoogleAuthProvider`, `GoogleAuthOptions` | `Geuneda.Account.Google` |

## 설치

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.core": "https://github.com/geuneda/geuneda-core.git",
    "com.geuneda.http": "https://github.com/geuneda/geuneda-http.git",
    "com.geuneda.account": "https://github.com/geuneda/geuneda-account.git",
    "com.geuneda.account.google": "https://github.com/geuneda/geuneda-account-google.git"
  }
}
```

## 사용법

```csharp
using Geuneda.Account;
using Geuneda.Account.Google;

options.Providers.Add(new GoogleAuthProvider(new GoogleAuthOptions
{
    AndroidServerClientId = "1234-web.apps.googleusercontent.com",   // 웹 애플리케이션 클라이언트 ID
    IosClientId = "1234-ios.apps.googleusercontent.com"              // iOS 클라이언트 ID
}));

// 로그인 버튼에서(앱이 전면에 있을 때)
var result = await auth.SignInAsync(AccountPlatforms.Google, token);
```

서버에는 구글 검증기를 켭니다(com.geuneda.account 의 `Server~`). 대상은 토큰을 요청한 클라이언트 ID 입니다.

```csharp
builder.Services.AddGeunedaAccount(...).AddGoogle(options =>
{
    options.ClientIds.Add("1234-web.apps.googleusercontent.com");   // Android(Credential Manager)가 받은 토큰의 대상
    options.ClientIds.Add("1234-ios.apps.googleusercontent.com");   // iOS(PKCE)가 받은 토큰의 대상
});
```

## 설정 (Google Cloud 콘솔)

1. OAuth 동의 화면을 만듭니다.
2. **웹 애플리케이션** 클라이언트를 만들고 그 ID 를 `AndroidServerClientId` 와 서버 `ClientIds` 에 넣습니다.
3. **Android** 클라이언트를 만들고 앱 패키지 이름과 서명 인증서 SHA-1(업로드 키와 Play 앱 서명 키 모두)을 등록합니다. 이 ID 는 코드에 넣지 않습니다.
4. **iOS** 클라이언트를 번들 ID 로 만들고 그 ID 를 `IosClientId` 와 서버 `ClientIds` 에 넣습니다. 리디렉션 스킴(`com.googleusercontent.apps.<ID 앞부분>`)은 ASWebAuthenticationSession 이 처리하므로 Info.plist 에 넣지 않아도 됩니다.

## 동작

- 사용자가 닫으면 `ResultCodes.Cancelled`, 그 밖의 실패는 `AccountResultCodes.PlatformSignInFailed` 입니다(메시지에 원인). iOS 의 토큰 교환이 네트워크로 실패하면 그 코드(`NetworkError`, `Timeout`)입니다.
- 로그인은 앱이 전면 활성일 때 부릅니다(버튼). iOS 는 앱 장면이 활성이 아니면 세션을 열지 못해 실패합니다(ASWebAuthenticationSession 오류 3).
- `SignInAsync` 의 취소 토큰을 취소하면 결과는 `Cancelled` 입니다. iOS 는 바로 끝나고 화면도 닫습니다. Android 는 구글 화면이 떠 있는 동안 Unity 가 멈춰 있어 사용자가 화면을 닫고 앱으로 돌아온 뒤에 결과가 오며, 화면을 대신 닫지 않습니다.
- Android Credential Manager 는 콘솔 설정 오류(패키지 이름·SHA-1 미등록, 잘못된 웹 클라이언트 ID)도 취소로 돌려줍니다. 이때 결과 메시지도 사용자 취소와 같으므로(`...TYPE_USER_CANCELED: activity is cancelled by the user.`), 계정을 골랐는데 `Cancelled` 가 오면 logcat `Auth` 태그(`UNREGISTERED_ON_API_CONSOLE` 등)를 확인합니다.
- Android 의 `SignOutAsync` 는 Credential Manager 의 로그인 상태를 지워 다음에 계정을 다시 고르게 하고, 지우기가 끝난 뒤에 끝납니다(실패는 경고 로그, 취소하면 기다리기만 그만둠). iOS 는 할 일이 없습니다(브라우저 세션은 Safari 가 관리, 매번 새로 고르게 하려면 `IosEphemeralSession = true`).
- Android 는 Google Play 서비스가 있는 기기여야 합니다.

## 요구 사항

- Unity 2021.3 이상, Android API 23 이상(compileSdk 34 이상으로 빌드), iOS 13 이상
- `com.geuneda.account` 0.1.0, `com.geuneda.core` 0.2.0, `com.geuneda.http` 0.1.0, `com.cysharp.unitask` 2.5.10, `com.unity.nuget.newtonsoft-json` 3.2.1

변경 이력은 [CHANGELOG.md](CHANGELOG.md) 를 참고하세요.

## 라이선스

MIT. [LICENSE.md](LICENSE.md) 를 참고하세요.
