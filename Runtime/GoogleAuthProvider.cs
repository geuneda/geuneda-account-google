using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using Geuneda.Http;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>
	/// Google 로그인 제공자입니다. 구글 id 토큰을 받아 서버의 <c>GoogleIdTokenVerifier</c> 가 검증합니다. <c>AuthServiceOptions.Providers</c> 에 등록합니다.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Android 는 Credential Manager 의 "Google 로 로그인" 화면(<c>GetSignInWithGoogleOption</c>)을 띄웁니다. Google Play 서비스가 있는 기기여야 합니다.
	/// iOS 는 ASWebAuthenticationSession 으로 구글 로그인 페이지를 열고 OAuth 인가 코드 + PKCE 로 id 토큰을 받습니다(외부 SDK 없음).
	/// </para>
	/// <para>
	/// 사용자가 닫으면 <c>ResultCodes.Cancelled</c>, 그 밖의 실패는 <see cref="AccountResultCodes.PlatformSignInFailed"/> 입니다(토큰 교환의 네트워크 실패는 그 코드).
	/// Android 의 <see cref="SignOutAsync"/> 는 Credential Manager 의 로그인 상태를 지워 다음에 계정을 다시 고르게 합니다. iOS 는 할 일이 없습니다.
	/// </para>
	/// </remarks>
	public sealed class GoogleAuthProvider : IAuthProvider
	{
		private static readonly TimeSpan DefaultTokenTimeout = TimeSpan.FromSeconds(15);

		private readonly GoogleAuthOptions _options;
		private readonly IHttpClient _tokenClient;

		/// <exception cref="ArgumentNullException"><paramref name="options"/> 가 null 인 경우</exception>
		/// <exception cref="ArgumentException">클라이언트 ID 가 하나도 없거나 형식이 틀린 경우</exception>
		public GoogleAuthProvider(GoogleAuthOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));

			var hasAndroid = !string.IsNullOrEmpty(options.AndroidServerClientId);
			var hasIos = !string.IsNullOrEmpty(options.IosClientId);

			if (!hasAndroid && !hasIos)
			{
				throw new ArgumentException("Set AndroidServerClientId and/or IosClientId", nameof(options));
			}

			if ((hasAndroid && !GoogleAuthOptions.IsClientId(options.AndroidServerClientId)) || (hasIos && !GoogleAuthOptions.IsClientId(options.IosClientId)))
			{
				throw new ArgumentException("Google OAuth client ids end with .apps.googleusercontent.com", nameof(options));
			}

			_tokenClient = options.TokenClient ?? new HttpClientBuilder().AddMiddleware(new TimeoutMiddleware(DefaultTokenTimeout)).Build();
		}

		public string Platform => AccountPlatforms.Google;

		public UniTask<Result<PlatformCredential>> SignInAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromResult(Result<PlatformCredential>.Fail(ResultCodes.Cancelled));
			}

#if UNITY_ANDROID && !UNITY_EDITOR
			return string.IsNullOrEmpty(_options.AndroidServerClientId)
				? Unavailable("AndroidServerClientId is not set")
				: new GoogleAndroidSignIn().SignInAsync(_options.AndroidServerClientId, cancellationToken);
#elif UNITY_IOS && !UNITY_EDITOR
			return string.IsNullOrEmpty(_options.IosClientId)
				? Unavailable("IosClientId is not set")
				: new GoogleIosSignIn(_options.IosClientId, _options.IosEphemeralSession, _tokenClient).SignInAsync(cancellationToken);
#else
			return Unavailable("Google sign-in is only available on Android and iOS");
#endif
		}

		public UniTask SignOutAsync(CancellationToken cancellationToken)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			return GoogleAndroidSignIn.SignOutAsync(cancellationToken);
#else
			return UniTask.CompletedTask;
#endif
		}

		private static UniTask<Result<PlatformCredential>> Unavailable(string message)
		{
			return UniTask.FromResult(Result<PlatformCredential>.Fail(AccountResultCodes.PlatformSignInFailed, message));
		}
	}
}
