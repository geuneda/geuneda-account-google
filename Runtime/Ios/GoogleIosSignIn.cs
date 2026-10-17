#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using Geuneda.Http;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>
	/// iOS 로그인입니다. ASWebAuthenticationSession(<c>Plugins/iOS/GeunedaGoogleSignIn.mm</c>)으로 구글 인가 페이지를 열고, 돌아온 인가 코드를
	/// PKCE 검증자로 id 토큰과 바꿉니다. 네이티브에는 GCHandle 을 넘기고 네이티브 콜백에서만 풉니다(static 상태 없이 연결, 늦은 콜백 안전).
	/// 네이티브는 요청마다 콜백을 정확히 한 번 부르고(취소 포함), 콜백과 취소는 모두 메인 스레드에서 실행됩니다.
	/// </summary>
	internal sealed class GoogleIosSignIn
	{
		private const string AccessDenied = "access_denied";

		private delegate void CompletionCallback(IntPtr context, int status, string callbackUrl, long errorCode, string message);

		// 네이티브가 들고 있을 함수 포인터. 수거되지 않게 둔다(바뀌지 않는 값)
		private static readonly CompletionCallback Completed = OnCompleted;

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Account.Google");

		private readonly UniTaskCompletionSource<Result<string>> _completion = new UniTaskCompletionSource<Result<string>>();
		private readonly string _clientId;
		private readonly bool _ephemeral;
		private readonly IHttpClient _http;
		// 네이티브 요청의 GCHandle 값. 네이티브 콜백이 오면 0 으로 바꾼다(그 뒤 같은 값을 다음 요청이 받을 수 있다)
		private IntPtr _context;

		[DllImport("__Internal")]
		private static extern void GeunedaGoogleSignIn_Start(IntPtr context, string authorizationUrl, string callbackScheme, int ephemeral, CompletionCallback callback);

		[DllImport("__Internal")]
		private static extern void GeunedaGoogleSignIn_Cancel(IntPtr context);

		public GoogleIosSignIn(string clientId, bool ephemeral, IHttpClient http)
		{
			_clientId = clientId;
			_ephemeral = ephemeral;
			_http = http;
		}

		/// <param name="nonce">서버가 발급한 nonce 원문. 인가 요청에 넣으면 구글이 id 토큰의 <c>nonce</c> 클레임에 그대로 넣습니다.</param>
		public async UniTask<Result<PlatformCredential>> SignInAsync(string nonce, CancellationToken cancellationToken)
		{
			var pkce = GooglePkce.Create();
			_context = GCHandle.ToIntPtr(GCHandle.Alloc(this));
			GeunedaGoogleSignIn_Start(_context, pkce.AuthorizationUrl(_clientId, nonce), GooglePkce.CallbackScheme(_clientId), _ephemeral ? 1 : 0, Completed);

			Result<string> callback;

			// 취소는 아무 스레드에서나 올 수 있어 UIKit 을 부르는 취소는 메인 스레드로 넘긴다
			using (cancellationToken.Register(() => MainThreadDispatcher.Post(Cancel)))
			{
				callback = await _completion.Task;
			}

			if (!callback.TryGetValue(out var callbackUrl))
			{
				return Result<PlatformCredential>.Fail(callback.Code, callback.Message);
			}

			if (!pkce.TryReadCallback(callbackUrl, out var code, out var error))
			{
				return error == AccessDenied
					? Result<PlatformCredential>.Fail(ResultCodes.Cancelled, "Google sign-in was denied")
					: Result<PlatformCredential>.Fail(AccountResultCodes.PlatformSignInFailed, "Google sign-in failed: " + error);
			}

			var idToken = await GoogleTokenExchange.ExchangeAsync(_http, _clientId, code, pkce.Verifier, cancellationToken);
			return idToken.TryGetValue(out var token)
				? Result<PlatformCredential>.Ok(new PlatformCredential(AccountPlatforms.Google, token))
				: Result<PlatformCredential>.Fail(idToken.Code, idToken.Message);
		}

		private void Cancel()
		{
			// 이미 끝난 요청의 늦은 취소(완료 결과와 취소가 함께 큐에 들어간 경우)는 다음 요청을 건드리지 않게 아무것도 하지 않는다
			if (_context == IntPtr.Zero)
			{
				return;
			}

			// 네이티브가 취소로 콜백해 핸들을 풀고 화면을 닫는다. 결과는 바로 끝낸다
			GeunedaGoogleSignIn_Cancel(_context);
			_completion.TrySetResult(Result<string>.Fail(ResultCodes.Cancelled));
		}

		[MonoPInvokeCallback(typeof(CompletionCallback))]
		private static void OnCompleted(IntPtr context, int status, string callbackUrl, long errorCode, string message)
		{
			// 네이티브 콜백 안에서 예외가 나면 앱이 종료되므로 여기서 막고, 이어지는 코드는 다음 처리에서 실행한다
			try
			{
				var handle = GCHandle.FromIntPtr(context);
				var request = (GoogleIosSignIn) handle.Target;
				request._context = IntPtr.Zero;
				handle.Free();
				var result = GoogleSignInResults.From(status, callbackUrl, $"{message} ({errorCode})");
				MainThreadDispatcher.Post(() => request._completion.TrySetResult(result));
			}
			catch (Exception exception)
			{
				Logger.Error(exception, "Google sign-in callback failed");
			}
		}
	}
}
#endif
