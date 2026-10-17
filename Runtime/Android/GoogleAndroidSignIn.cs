#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>
	/// Android 로그인(<c>Plugins/Android/GeunedaGoogleSignIn.androidlib</c>)입니다. Java 콜백은 Android UI 스레드에서 오므로 Unity 메인 스레드로 넘겨 끝냅니다.
	/// </summary>
	internal sealed class GoogleAndroidSignIn
	{
		private const string PluginClass = "com.geuneda.account.google.GeunedaGoogleSignIn";
		private const string CallbackInterface = "com.geuneda.account.google.GeunedaGoogleSignInCallback";

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Account.Google");

		// Java 콜백(UI 스레드)을 Unity 메인 스레드로 넘긴다
		private sealed class Callback : AndroidJavaProxy
		{
			private readonly Action<int, string, string> _onResult;

			public Callback(Action<int, string, string> onResult) : base(CallbackInterface)
			{
				_onResult = onResult;
			}

			// Java 인터페이스의 메서드 이름과 같아야 한다
			// ReSharper disable once InconsistentNaming
			public void onResult(int status, string value, string message)
			{
				MainThreadDispatcher.Post(() => _onResult(status, value, message));
			}
		}

		/// <param name="nonce">서버가 발급한 nonce 원문. id 토큰의 <c>nonce</c> 클레임에 그대로 들어갑니다.</param>
		public async UniTask<Result<PlatformCredential>> SignInAsync(string serverClientId, string nonce, CancellationToken cancellationToken)
		{
			var completion = new UniTaskCompletionSource<Result<string>>();
			AndroidJavaObject signal;

			try
			{
				using (var plugin = new AndroidJavaClass(PluginClass))
				using (var activity = CurrentActivity())
				{
					signal = plugin.CallStatic<AndroidJavaObject>("signIn", activity, serverClientId, nonce,
						new Callback((status, value, message) => completion.TrySetResult(GoogleSignInResults.From(status, value, message))));
				}
			}
			catch (AndroidJavaException exception)
			{
				return Result<PlatformCredential>.Fail(AccountResultCodes.PlatformSignInFailed, exception.Message);
			}

			Result<string> token;

			using (signal)
			using (cancellationToken.Register(() => MainThreadDispatcher.Post(() =>
			       {
				       // 이미 끝났으면 신호는 해제됐을 수 있으니 건드리지 않는다(둘 다 메인 스레드)
				       if (completion.Task.Status.IsCompleted())
				       {
					       return;
				       }

				       signal.Call("cancel");
				       completion.TrySetResult(Result<string>.Fail(ResultCodes.Cancelled));
			       })))
			{
				token = await completion.Task;
			}

			return token.TryGetValue(out var idToken)
				? Result<PlatformCredential>.Ok(new PlatformCredential(AccountPlatforms.Google, idToken))
				: Result<PlatformCredential>.Fail(token.Code, token.Message);
		}

		/// <summary>로그인 상태를 다 지운 뒤에 끝납니다(바로 다시 로그인해도 겹치지 않게). 실패는 경고로 남기고, 취소하면 기다리기만 그만둡니다.</summary>
		public static async UniTask SignOutAsync(CancellationToken cancellationToken)
		{
			// null 이면 성공, 아니면 실패 메시지
			var completion = new UniTaskCompletionSource<string>();

			try
			{
				using (var plugin = new AndroidJavaClass(PluginClass))
				using (var activity = CurrentActivity())
				{
					plugin.CallStatic("signOut", activity, new Callback((status, value, message) =>
						completion.TrySetResult((GoogleSignInStatus) status == GoogleSignInStatus.Succeeded ? null : message ?? "unknown error")));
				}
			}
			catch (AndroidJavaException exception)
			{
				Logger.Warning("Google sign-out failed: " + exception.Message);
				return;
			}

			var (cancelled, failure) = await completion.Task.AttachExternalCancellation(cancellationToken).SuppressCancellationThrow();

			if (!cancelled && failure != null)
			{
				Logger.Warning("Google sign-out failed: " + failure);
			}
		}

		private static AndroidJavaObject CurrentActivity()
		{
			using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			{
				return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			}
		}
	}
}
#endif
