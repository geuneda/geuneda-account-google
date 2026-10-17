# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.2.0] - 2026-10-17

**호환성 파괴 변경**(com.geuneda.account 0.3.0 이 필요합니다. 이전 동작이 필요한 프로젝트는 `0.1.0` 태그와 account `0.2.0` 을 고정해 씁니다):
- `GoogleAuthProvider.SignInAsync(CancellationToken)` 이 `SignInAsync(string nonce, CancellationToken)` 으로 바뀜(account 0.3.0 의 `IAuthProvider`). `RequiresNonce` 는 true. nonce 가 비면 `ArgumentException`
- Android Java 진입점 `GeunedaGoogleSignIn.signIn(activity, serverClientId, nonce, callback)` 에 nonce 인자 추가

**New**:
- id 토큰 replay 방어: `AuthService` 가 넘긴 서버 nonce 원문을 Android 는 `GetSignInWithGoogleOption.Builder.setNonce`, iOS 는 인가 요청의 `nonce` 파라미터로 넣음. 구글은 원문을 id 토큰의 `nonce` 클레임에 그대로 넣고 서버 `GoogleIdTokenVerifier` 가 비교함

## [0.1.0] - 2026-10-14

**New**:
- `GoogleAuthProvider`/`GoogleAuthOptions`: 구글 id 토큰을 자격으로 돌려주는 제공자. 사용자 닫기는 `Cancelled`(네이티브 메시지 포함), 그 밖의 실패는 `PlatformSignInFailed`, 취소 토큰으로 중단
- Android: Credential Manager(`GetSignInWithGoogleOption`) Android 라이브러리 플러그인(Gradle 의존성 androidx.credentials 1.3.0, googleid 1.1.1, credentials-play-services-auth 가 빼는 androidx.core·androidx.loader, R8 consumer 규칙), 로그아웃은 로그인 상태 지우기(끝날 때까지 기다림)
- iOS: ASWebAuthenticationSession + OAuth 인가 코드 + PKCE(S256, 상태 검사), 토큰 엔드포인트 교환(외부 SDK 없음)
