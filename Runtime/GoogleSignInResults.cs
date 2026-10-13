using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>네이티브 결과(상태, 값, 메시지)를 결과 규약으로 바꿉니다.</summary>
	internal static class GoogleSignInResults
	{
		public static Result<string> From(int status, string value, string message)
		{
			switch ((GoogleSignInStatus) status)
			{
				case GoogleSignInStatus.Succeeded when !string.IsNullOrEmpty(value):
					return Result<string>.Ok(value);
				case GoogleSignInStatus.Succeeded:
					return Result<string>.Fail(AccountResultCodes.PlatformSignInFailed, "Google sign-in returned no value");
				// Android Credential Manager 는 콘솔 설정 오류도 취소로 돌려주므로 원인을 알 수 있게 네이티브 메시지를 남긴다
				case GoogleSignInStatus.Cancelled when !string.IsNullOrEmpty(message):
					return Result<string>.Fail(ResultCodes.Cancelled, "Google sign-in was cancelled: " + message);
				case GoogleSignInStatus.Cancelled:
					return Result<string>.Fail(ResultCodes.Cancelled, "Google sign-in was cancelled");
				case GoogleSignInStatus.Unsupported:
					return Result<string>.Fail(AccountResultCodes.PlatformSignInFailed, "Google sign-in needs iOS 13 or later");
				default:
					return Result<string>.Fail(AccountResultCodes.PlatformSignInFailed, "Google sign-in failed: " + message);
			}
		}
	}
}
