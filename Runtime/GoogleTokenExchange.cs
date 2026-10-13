using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using Geuneda.Http;
using Newtonsoft.Json.Linq;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>iOS 에서 받은 인가 코드를 구글 토큰 엔드포인트에서 id 토큰으로 바꿉니다(iOS 클라이언트는 비밀 값 없이 PKCE 검증자로).</summary>
	internal static class GoogleTokenExchange
	{
		public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

		public static async UniTask<Result<string>> ExchangeAsync(IHttpClient http, string iosClientId, string code, string verifier, CancellationToken cancellationToken)
		{
			var form = "grant_type=authorization_code" +
				"&code=" + Uri.EscapeDataString(code) +
				"&client_id=" + Uri.EscapeDataString(iosClientId) +
				"&redirect_uri=" + Uri.EscapeDataString(GooglePkce.RedirectUri(iosClientId)) +
				"&code_verifier=" + Uri.EscapeDataString(verifier);
			var request = HttpRequest.Post(TokenEndpoint).WithBody(Encoding.UTF8.GetBytes(form), "application/x-www-form-urlencoded");
			var sent = await http.SendAsync(request, cancellationToken);

			if (!sent.TryGetValue(out var response))
			{
				return Result<string>.Fail(sent.Code, sent.Message);
			}

			JObject body;

			try
			{
				body = JObject.Parse(response.ReadText());
			}
			catch (Newtonsoft.Json.JsonException)
			{
				return Result<string>.Fail(AccountResultCodes.PlatformSignInFailed, $"Google token endpoint returned HTTP {response.StatusCode} without JSON");
			}

			var idToken = StringField(body, "id_token");

			if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(idToken))
			{
				return Result<string>.Fail(AccountResultCodes.PlatformSignInFailed,
					$"Google token exchange failed (HTTP {response.StatusCode}): {StringField(body, "error")} {StringField(body, "error_description")}");
			}

			return Result<string>.Ok(idToken);
		}

		// 문자열이 아닌 필드(객체, 배열, 숫자)는 없는 것으로 본다(캐스팅 예외 대신 실패 결과가 되게)
		private static string StringField(JObject body, string name)
		{
			return body[name] is JValue { Type: JTokenType.String } value ? (string) value : null;
		}
	}
}
