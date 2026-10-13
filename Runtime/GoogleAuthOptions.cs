using System;
using Geuneda.Http;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary><see cref="GoogleAuthProvider"/> 설정입니다. Google Cloud 콘솔의 OAuth 클라이언트 ID 를 넣습니다.</summary>
	public sealed class GoogleAuthOptions
	{
		/// <summary>
		/// Android 에서 id 토큰의 대상이 될 <b>웹 애플리케이션</b> 클라이언트 ID(<c>....apps.googleusercontent.com</c>)입니다. Android 에서 필수입니다.
		/// 같은 프로젝트에 앱 패키지 이름과 서명 인증서 SHA-1 로 Android 클라이언트도 만들어 둬야 합니다(코드에는 넣지 않음).
		/// </summary>
		public string AndroidServerClientId { get; set; }

		/// <summary>iOS 클라이언트 ID(<c>....apps.googleusercontent.com</c>)입니다. iOS 에서 필수입니다. id 토큰의 대상이 됩니다.</summary>
		public string IosClientId { get; set; }

		/// <summary>iOS 에서 인가 코드를 id 토큰으로 바꿀 때 쓰는 http 클라이언트입니다. 없으면 15초 제한 시간의 기본 클라이언트를 만듭니다.</summary>
		public IHttpClient TokenClient { get; set; }

		/// <summary>iOS 의 ASWebAuthenticationSession 에서 Safari 의 구글 로그인 상태를 쓰지 않으려면 true 입니다. 기본 false(이미 로그인한 계정을 바로 고를 수 있음).</summary>
		public bool IosEphemeralSession { get; set; }

		internal static bool IsClientId(string clientId)
		{
			return !string.IsNullOrWhiteSpace(clientId) && clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal);
		}
	}
}
