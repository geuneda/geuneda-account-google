// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>네이티브 플러그인(Java, Objective-C)이 돌려주는 상태입니다. 값은 플러그인과 같아야 합니다.</summary>
	internal enum GoogleSignInStatus
	{
		Succeeded = 0,
		Cancelled = 1,
		Failed = 2,
		/// <summary>OS 버전이 낮아 쓸 수 없음(iOS 13 미만)</summary>
		Unsupported = 3
	}
}
